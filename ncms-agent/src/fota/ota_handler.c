#include "ota_handler.h"

#include "firmware_upgrade.h"
#include "hash.h"
#include "mqtt_client.h"
#include "ota_state.h"
#include "download.h"
#include "update_lock.h"

#include <json-c/json.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include "app_defaults.h"

static const char *get_client_id(void)
{
    MqttConfig *config = mqtt_get_config();
    if (config && config->mqtt.client_id && config->mqtt.client_id[0] != '\0') {
        return config->mqtt.client_id;
    }
    return "unknown";
}

int ota_publish_status(const char *client_id, OtaState state)
{
    if (!client_id) return -1;

    const char *state_str;
    switch (state) {
    case OTA_STATE_ACKNOWLEDGED:
        state_str = "acknowledged";
        break;
    case OTA_STATE_APPLYING:
        state_str = "applying";
        break;
    case OTA_STATE_REBOOTING:
        state_str = "rebooting";
        break;
    case OTA_STATE_SUCCESS:
        state_str = "success";
        break;
    case OTA_STATE_FAILED:
        state_str = "failed";
        break;
    default:
        state_str = "unknown";
        break;
    }

    struct json_object *payload = json_object_new_object();
    json_object_object_add(payload, "status", json_object_new_string(state_str));
    json_object_object_add(payload, "timestamp", json_object_new_int64((int64_t)time(NULL) * 1000));

    const char *payload_str = json_object_to_json_string(payload);
    printf("[OTA] Status: %s\n", state_str);

    printf("payload: %s\n", payload_str);
    int ret = mqtt_publish(g_config->topics.ota_response_publish, payload_str, 1, 0);
    json_object_put(payload);

    return ret;
}

int ota_publish_result(const char *client_id, const OtaResult *result)
{
    if (!client_id || !result) return -1;

    struct json_object *payload = json_object_new_object();

    json_object_object_add(payload, "status", json_object_new_string(result->status));

    if (strcmp(result->status, "failed") == 0) {
        json_object_object_add(payload, "error_code", json_object_new_string(result->error_code));
        json_object_object_add(payload, "error_message",
                               json_object_new_string(result->error_message));
    } else {

        json_object_object_add(payload, "version", json_object_new_string(result->version));
    }

    const char *payload_str = json_object_to_json_string(payload);
    printf("[OTA] Publishing result to %s: %s\n", g_config->topics.ota_response_publish,
           payload_str);

    int ret = mqtt_publish(g_config->topics.ota_response_publish, payload_str, 1, 0);
    json_object_put(payload);

    if (ret == 0) {
        printf("[OTA] Result published successfully\n");
        return 0;
    } else {
        printf("[OTA] Failed to publish result (ret=%d)\n", ret);
        return -1;
    }
}

typedef struct {
    char *package_url;
    long expected_size;
    char expected_md5[33];
    char expected_sha256[65];
    char client_id[128];
} OtaArgs;

static long get_file_size(const char *path)
{
    FILE *fp = fopen(path, "rb");
    if (!fp) return -1;

    fseek(fp, 0, SEEK_END);
    long size = ftell(fp);
    fclose(fp);
    return size;
}

static void *ota_thread(void *arg)
{
    OtaArgs *args = (OtaArgs *)arg;
    OtaResult result;
    int ota_success = 0;

    memset(&result, 0, sizeof(result));
    strncpy(result.status, "failed", sizeof(result.status) - 1);

    printf("\n========================================\n");
    printf("OTA UPDATE STARTED\n");
    printf("========================================\n");
    printf("Client ID: %s\n", args->client_id);
    printf("URL    : %s\n", args->package_url);
    printf("Size   : %ld bytes\n", args->expected_size);
    printf("MD5    : %s\n", args->expected_md5);
    printf("SHA256 : %s\n", args->expected_sha256);
    printf("----------------------------------------\n");

    ota_publish_status(args->client_id, OTA_STATE_ACKNOWLEDGED);

    printf("[OTA] Step 1/5 : Downloading firmware...\n");
    ota_publish_status(args->client_id, OTA_STATE_APPLYING);

    if (download_file(args->package_url, NCMS_FW_PATH) != 0) {
        const char *err_msg = "Firmware download failed";
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_DOWNLOAD_FAILED, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] Download complete.\n");

    printf("[OTA] Step 2/5 : Checking file size...\n");

    long actual_size = get_file_size(NCMS_FW_PATH);

    if (actual_size < 0) {
        const char *err_msg = "Cannot read downloaded file";
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_UNKNOWN_ERROR, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] Expected size : %ld bytes\n", args->expected_size);
    printf("[OTA] Actual size   : %ld bytes\n", actual_size);

    if (actual_size != args->expected_size) {
        char err_msg[256];
        snprintf(err_msg, sizeof(err_msg), "Size mismatch: expected %ld, got %ld",
                 args->expected_size, actual_size);
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_SIZE_MISMATCH, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] Size check PASSED\n");

    printf("[OTA] Step 3/5 : Verifying MD5...\n");

    char computed_md5[33];

    if (compute_md5_of_file(NCMS_FW_PATH, computed_md5) != 0) {
        const char *err_msg = "Could not compute MD5";
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_UNKNOWN_ERROR, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] Expected MD5 : %s\n", args->expected_md5);
    printf("[OTA] Computed MD5 : %s\n", computed_md5);

    if (strncmp(computed_md5, args->expected_md5, 32) != 0) {
        char err_msg[256];
        snprintf(err_msg, sizeof(err_msg), "MD5 mismatch: expected %s, got %s", args->expected_md5,
                 computed_md5);
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_MD5_MISMATCH, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] MD5 VERIFIED SUCCESSFULLY\n");

    printf("[OTA] Step 4/5 : Verifying SHA256...\n");

    if (verify_sha256(NCMS_FW_PATH, args->expected_sha256) != 0) {
        const char *err_msg = "SHA256 verification failed";
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_SHA256_MISMATCH, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] SHA256 VERIFIED SUCCESSFULLY\n");

    printf("[OTA] Step 5/5 : Validating firmware image...\n");

    if (validate_firmware(NCMS_FW_PATH) != 0) {
        const char *err_msg = "Firmware validation failed";
        printf("[OTA] FAILED: %s\n", err_msg);
        ota_set_error(OTA_STATUS_VALIDATION_FAILED, err_msg);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    }

    printf("[OTA] Validation PASSED. Setting OTA pending flag...\n");

    if (ota_state_setenv("ota_pending", "1") != 0) {
        printf("[OTA] WARNING: could not persist OTA state to %s.\n"
               "         Post-reboot result will NOT be published.\n"
               "         Check that /etc/ncms/ exists and is writable "
               "(disk full? read-only overlay?).\n",
               NCMS_DEFAULT_STORAGE_DIR "ota_state.json");

        ota_clear_error();

        strncpy(result.status, "failed", sizeof(result.status) - 1);
        strncpy(result.error_code, "99", sizeof(result.error_code) - 1);
        strncpy(result.error_message, "Failed to persist OTA state", sizeof(result.error_message) - 1);
        ota_publish_result(args->client_id, &result);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);
        goto ota_fail;
    } else {
        printf("[OTA] OTA state: pending=1\n");
        ota_clear_error();
        ota_state_setenv("ota_status", "success");
        ota_state_setenv("ota_error_code", "");
        ota_state_setenv("ota_error_message", "");
    }

    ota_state_setenv("ota_version", args->expected_sha256);

    ota_publish_status(args->client_id, OTA_STATE_REBOOTING);

    printf("[OTA] Starting flash in 5 seconds...\n");
    sleep(5);

    if (flash_firmware(NCMS_FW_PATH) != 0) {
        const char *err_msg = "Firmware flash failed";
        printf("[OTA] FAILED: %s\n", err_msg);

        strncpy(result.status, "failed", sizeof(result.status) - 1);
        strncpy(result.error_code, "6", sizeof(result.error_code) - 1);
        strncpy(result.error_message, err_msg, sizeof(result.error_message) - 1);
        ota_publish_result(args->client_id, &result);
        ota_publish_status(args->client_id, OTA_STATE_FAILED);

        ota_set_error(OTA_STATUS_FLASH_FAILED, err_msg);
        goto ota_fail;
    }

    printf("\n========================================\n");
    printf("OTA UPDATE COMPLETED (no reboot triggered)\n");
    printf("========================================\n");

    strncpy(result.status, "success", sizeof(result.status) - 1);
    strncpy(result.version, args->expected_sha256, sizeof(result.version) - 1);
    ota_publish_result(args->client_id, &result);
    ota_publish_status(args->client_id, OTA_STATE_SUCCESS);

    ota_success = 1;
    goto ota_done;

ota_fail:
    printf("\n========================================\n");
    printf("OTA UPDATE FAILED — device unchanged\n");
    printf("========================================\n");

    if (!ota_success && mqtt_is_connected()) {
        OtaResult pending_result;
        if (ota_get_pending_result(&pending_result) == 0) {
            ota_publish_result(args->client_id, &pending_result);
        }
    }

ota_done:
    free(args->package_url);
    free(args);

    update_lock_release();

    return NULL;
}

int ota_handle_request(const char *json_payload)
{

    if (update_lock_try_acquire("ota") != 0) {
        printf("[OTA] Request ignored: an update is already in progress\n");
        return -1;
    }

    struct json_object *parsed = json_tokener_parse(json_payload);

    if (!parsed) {
        printf("[OTA] FAILED: invalid JSON payload\n");
        goto parse_fail;
    }

    struct json_object *url_obj, *size_obj, *md5_obj, *sha256_obj;

    json_object_object_get_ex(parsed, "package_url", &url_obj);
    json_object_object_get_ex(parsed, "size", &size_obj);
    json_object_object_get_ex(parsed, "md5", &md5_obj);
    json_object_object_get_ex(parsed, "sha256", &sha256_obj);

    if (!url_obj || !size_obj || !md5_obj || !sha256_obj) {
        printf("[OTA] FAILED: missing required field(s) "
               "(package_url / size / md5 / sha256)\n");
        json_object_put(parsed);
        goto parse_fail;
    }

    OtaArgs *args = calloc(1, sizeof(OtaArgs));

    if (!args) {
        printf("[OTA] FAILED: out of memory\n");
        json_object_put(parsed);
        goto parse_fail;
    }

    args->package_url = strdup(json_object_get_string(url_obj));
    args->expected_size = (long)json_object_get_int64(size_obj);

    strncpy(args->expected_md5, json_object_get_string(md5_obj), sizeof(args->expected_md5) - 1);

    strncpy(args->expected_sha256, json_object_get_string(sha256_obj),
            sizeof(args->expected_sha256) - 1);

    const char *client_id = get_client_id();
    strncpy(args->client_id, client_id, sizeof(args->client_id) - 1);

    json_object_put(parsed);

    pthread_t tid;
    pthread_attr_t attr;

    pthread_attr_init(&attr);
    pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);

    if (pthread_create(&tid, &attr, ota_thread, args) != 0) {
        printf("[OTA] FAILED: could not spawn OTA thread\n");
        pthread_attr_destroy(&attr);
        free(args->package_url);
        free(args);
        goto parse_fail;
    }

    pthread_attr_destroy(&attr);

    printf("[OTA] OTA thread spawned successfully\n");
    return 0;

parse_fail:
    update_lock_release();
    return -1;
}