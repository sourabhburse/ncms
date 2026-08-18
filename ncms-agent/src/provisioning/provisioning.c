#include "provisioning.h"

#include "mqtt_client.h"

#include <curl/curl.h>
#include <json-c/json.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

struct MemoryStruct {
    char *memory;
    size_t size;
};

static size_t write_memory_callback(void *contents, size_t size, size_t nmemb, void *userp)
{
    size_t realsize = size * nmemb;
    struct MemoryStruct *mem = (struct MemoryStruct *)userp;

    char *ptr = realloc(mem->memory, mem->size + realsize + 1);
    if (!ptr) return 0;

    mem->memory = ptr;
    memcpy(&(mem->memory[mem->size]), contents, realsize);
    mem->size += realsize;
    mem->memory[mem->size] = 0;

    return realsize;
}

int provisioning_register_device(const ProvisioningDeviceInfo *info, const char *csr_string,
                                 char **response_body)
{
    CURL *curl;
    CURLcode res;
    struct MemoryStruct chunk;

    if (!info || !csr_string || !response_body) return -1;

    *response_body = NULL;

    chunk.memory = malloc(1);
    chunk.size = 0;

    if (!chunk.memory) return -1;

    struct json_object *payload = json_object_new_object();
    json_object_object_add(payload, "serial_number", json_object_new_string(info->serial_number));
    json_object_object_add(payload, "firmware_version",
                           json_object_new_string(info->firmware_version));
    json_object_object_add(payload, "agent_version", json_object_new_string(info->agent_version));
    json_object_object_add(payload, "hardware_model", json_object_new_string(info->hardware_model));

    struct json_object *claims = json_object_new_object();
    json_object_object_add(claims, "base_mac", json_object_new_string(info->base_mac));
    json_object_object_add(payload, "identity_claims", claims);
    json_object_object_add(payload, "csr", json_object_new_string(csr_string));

    const char *json_string = json_object_to_json_string(payload);
    printf("Sending request to %s\n", info->server_url);

    curl_global_init(CURL_GLOBAL_ALL);
    curl = curl_easy_init();
    res = CURLE_FAILED_INIT;

    if (curl) {
        struct curl_slist *headers = NULL;
        headers = curl_slist_append(headers, "Content-Type: application/json");
        headers = curl_slist_append(headers, "Accept: application/json");

        curl_easy_setopt(curl, CURLOPT_URL, info->server_url);
        curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
        curl_easy_setopt(curl, CURLOPT_POSTFIELDS, json_string);
        curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE, strlen(json_string));
        curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_memory_callback);
        curl_easy_setopt(curl, CURLOPT_WRITEDATA, (void *)&chunk);
        curl_easy_setopt(curl, CURLOPT_TIMEOUT, 30L);
        curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT, 10L);
        curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
        curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);

        res = curl_easy_perform(curl);

        if (res == CURLE_OK) {
            long http_code = 0;
            curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &http_code);

            if (http_code == 200 || http_code == 201) {
                printf("Registration successful!\n");
                printf("Response: %s\n", chunk.memory);
                *response_body = strdup(chunk.memory);
            } else {
                fprintf(stderr, "HTTP error: %ld\n", http_code);
                fprintf(stderr, "Response: %s\n", chunk.memory);
                res = CURLE_HTTP_RETURNED_ERROR;
            }
        } else {
            fprintf(stderr, "curl error: %s\n", curl_easy_strerror(res));
        }

        curl_slist_free_all(headers);
        curl_easy_cleanup(curl);
    }

    free(chunk.memory);
    json_object_put(payload);
    curl_global_cleanup();

    return (res == CURLE_OK && *response_body != NULL) ? 0 : -1;
}

int provisioning_save_response(const char *response_body)
{
    struct json_object *parsed = json_tokener_parse(response_body);
    if (!parsed) {
        fprintf(stderr, "Failed to parse response\n");
        return -1;
    }

    struct json_object *pki_obj;
    json_object_object_get_ex(parsed, "pki", &pki_obj);

    if (pki_obj) {
        struct json_object *ca_cert, *client_cert;
        json_object_object_get_ex(pki_obj, "ca_certificate", &ca_cert);
        json_object_object_get_ex(pki_obj, "client_certificate", &client_cert);

        if (ca_cert) {
            FILE *fp = fopen(CA_CERT_PATH, "w");
            if (fp) {
                fprintf(fp, "%s\n", json_object_get_string(ca_cert));
                fclose(fp);
                printf("CA certificate saved to %s\n", CA_CERT_PATH);
            }
        }

        if (client_cert) {
            FILE *fp = fopen(CERT_PATH, "w");
            if (fp) {
                fprintf(fp, "%s\n", json_object_get_string(client_cert));
                fclose(fp);
                printf("Client certificate saved to %s\n", CERT_PATH);
            }
        }
    }

    FILE *fp = fopen(CONFIG_PATH, "w");
    if (fp) {
        fprintf(fp, "%s\n", json_object_to_json_string_ext(parsed, JSON_C_TO_STRING_PRETTY));
        fclose(fp);
        printf("Configuration saved to %s\n", CONFIG_PATH);
    }

    json_object_put(parsed);
    return 0;
}
