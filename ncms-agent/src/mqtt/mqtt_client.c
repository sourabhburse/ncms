#include "mqtt_client.h"

#include "app_handler.h"
#include "ota_handler.h"

#include <json-c/json.h>
#include <mosquitto.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

static struct mosquitto *mosq = NULL;
static volatile int mqtt_running = 0;
static volatile int mqtt_connected = 0;
MqttConfig *g_config = NULL;

static void get_openwrt_version(char *out, size_t out_sz)
{
    FILE *fp = popen("grep '^DISTRIB_RELEASE' /etc/openwrt_release "
                     "| cut -d= -f2 | tr -d \"'\\\"\"",
                     "r");
    if (fp && fgets(out, (int)out_sz, fp))
        out[strcspn(out, "\r\n")] = '\0';
    else
        strncpy(out, "unknown", out_sz - 1);

    if (fp) pclose(fp);
}

static void mqtt_disconnect_callback(struct mosquitto *mosq, void *userdata, int rc);
static void mqtt_log_callback(struct mosquitto *mosq, void *userdata, int level, const char *str);

static void mqtt_message_callback(struct mosquitto *mosq, void *userdata,
                                  const struct mosquitto_message *message)
{
    if (!message->payloadlen) return;

    printf("\n[MQTT] Received message on topic: %s\n", message->topic);
    printf("[MQTT] Payload: %.*s\n", message->payloadlen, (char *)message->payload);

    if (!g_config || !message->topic) return;

    if (g_config->topics.config_subscribe &&
        strcmp(message->topic, g_config->topics.config_subscribe) == 0) {
        printf("[MQTT] Config update received\n");

        char *payload_str = strndup((char *)message->payload, message->payloadlen);
        if (payload_str) {
            // TODO->Handle Config request
            free(payload_str);
        }
        return;
    }

    if (strcmp(message->topic, g_config->topics.ota_subscribe) == 0) {
        printf("[MQTT] OTA request received\n");

        char *payload_str = strndup((char *)message->payload, message->payloadlen);
        if (payload_str) {
            ota_handle_request(payload_str);
            free(payload_str);
        }
        return;
    }

    if (g_config->topics.application_subscribe &&
        strcmp(message->topic, g_config->topics.application_subscribe) == 0) {
        printf("[MQTT] Application request received\n");

        char *payload_str = strndup((char *)message->payload, message->payloadlen);
        if (payload_str) {
            app_handle_request(payload_str);
            free(payload_str);
        }
        return;
    }

    if (strcmp(message->topic, g_config->topics.command_subscribe) == 0) {
        char *payload_str = strndup((char *)message->payload, message->payloadlen);
        if (!payload_str) return;

        struct json_object *cmd = json_tokener_parse(payload_str);
        free(payload_str);

        if (cmd) {
            struct json_object *cmd_id_obj, *cmd_name_obj;
            json_object_object_get_ex(cmd, "command_id", &cmd_id_obj);
            json_object_object_get_ex(cmd, "command", &cmd_name_obj);

            const char *cmd_id = cmd_id_obj ? json_object_get_string(cmd_id_obj) : "unknown";
            const char *cmd_name = cmd_name_obj ? json_object_get_string(cmd_name_obj) : "unknown";

            printf("[MQTT] Processing command: %s (ID: %s)\n", cmd_name, cmd_id);

            struct json_object *response = json_object_new_object();
            json_object_object_add(response, "command_id", json_object_new_string(cmd_id));
            json_object_object_add(response, "status", json_object_new_string("success"));
            json_object_object_add(response, "message", json_object_new_string("Command executed"));
            json_object_object_add(response, "timestamp",
                                   json_object_new_int64((int64_t)time(NULL) * 1000));

            const char *resp_payload = json_object_to_json_string(response);
            mqtt_publish(g_config->topics.command_response_publish, resp_payload, 1, 0);

            json_object_put(response);
            json_object_put(cmd);
        }
        return;
    }
}

static void mqtt_connect_callback(struct mosquitto *mosq, void *userdata, int rc)
{
    if (rc == 0) {
        mqtt_connected = 1;
        printf("[MQTT] Connected to broker successfully!\n");

        if (g_config) {

            if (g_config->topics.config_subscribe) {
                mosquitto_subscribe(mosq, NULL, g_config->topics.config_subscribe, 0);
                printf("[MQTT]   Subscribed to: %s (config)\n", g_config->topics.config_subscribe);
            }

            if (g_config->topics.command_subscribe) {
                mosquitto_subscribe(mosq, NULL, g_config->topics.command_subscribe, 0);
                printf("[MQTT]   Subscribed to: %s (commands)\n",
                       g_config->topics.command_subscribe);
            }

            if (g_config->topics.ota_subscribe) {
                mosquitto_subscribe(mosq, NULL, g_config->topics.ota_subscribe, 0);
                printf("[MQTT]   Subscribed to: %s (OTA)\n", g_config->topics.ota_subscribe);
            }

            if (g_config->topics.application_subscribe) {
                mosquitto_subscribe(mosq, NULL, g_config->topics.application_subscribe, 0);
                printf("[MQTT]   Subscribed to: %s (application)\n",
                       g_config->topics.application_subscribe);
            }

            if (ota_result_pending()) {
                OtaResult result;
                if (ota_get_pending_result(&result) == 0) {

                    const char *client_id = g_config ? g_config->mqtt.client_id : "unknown";
                    int was_success = (strcmp(result.status, "success") == 0);

                    if (was_success &&
                        (strlen(result.version) == 0 || strcmp(result.version, "unknown") == 0)) {
                        char version[64] = {0};
                        get_openwrt_version(version, sizeof(version));
                        if (strlen(version) > 0 && strcmp(version, "unknown") != 0) {
                            strncpy(result.version, version, sizeof(result.version) - 1);
                        }
                    }

                    ota_publish_result(client_id, &result);
                    ota_publish_status(client_id,
                                       was_success ? OTA_STATE_SUCCESS : OTA_STATE_FAILED);

                    ota_clear_result_flag();
                } else {

                    char version[64] = {0};
                    char topic[256] = {0};
                    char payload[512] = {0};

                    get_openwrt_version(version, sizeof(version));

                    if (strcmp(version, "unknown") == 0 || version[0] == '\0')
                        ota_get_pending_version(version, sizeof(version));

                    const char *client_id = g_config ? g_config->mqtt.client_id : "unknown";
                    snprintf(topic, sizeof(topic), "d/%s/ota/res", client_id);

                    snprintf(payload, sizeof(payload),
                             "{\"status\":\"success\","
                             "\"version\":\"%s\"}",
                             version);

                    int pub_ret = mqtt_publish(topic, payload, 1, 0);
                    if (pub_ret == MOSQ_ERR_SUCCESS)
                        printf("[OTA] Result published to %s: %s\n", topic, payload);
                    else
                        fprintf(stderr, "[OTA] Failed to publish result: %s\n",
                                mosquitto_strerror(pub_ret));

                    ota_publish_status(client_id, OTA_STATE_SUCCESS);
                    ota_clear_result_flag();
                }
            }
        }
    } else {
        fprintf(stderr, "[MQTT] Failed to connect: %s\n", mosquitto_connack_string(rc));
    }
}

static void mqtt_disconnect_callback(struct mosquitto *mosq, void *userdata, int rc)
{
    mqtt_connected = 0;
    printf("[MQTT] Disconnected from broker (rc: %d)\n", rc);
}

static void mqtt_log_callback(struct mosquitto *mosq, void *userdata, int level, const char *str)
{

    printf("[MQTT-LIB] %s\n", str);
}

int mqtt_init_from_config(const char *config_path)
{
    FILE *fp = fopen(config_path, "r");
    if (!fp) {
        fprintf(stderr, "Failed to open config file: %s\n", config_path);
        return -1;
    }

    fseek(fp, 0, SEEK_END);
    long len = ftell(fp);
    fseek(fp, 0, SEEK_SET);

    char *json_data = malloc(len + 1);
    if (!json_data) {
        fclose(fp);
        return -1;
    }

    fread(json_data, 1, len, fp);
    json_data[len] = '\0';
    fclose(fp);

    struct json_object *parsed = json_tokener_parse(json_data);
    if (!parsed) {
        fprintf(stderr, "Failed to parse JSON config\n");
        free(json_data);
        return -1;
    }

    g_config = calloc(1, sizeof(MqttConfig));
    if (!g_config) {
        fprintf(stderr, "Failed to allocate config\n");
        json_object_put(parsed);
        free(json_data);
        return -1;
    }

    struct json_object *device_id_obj, *status_obj;
    json_object_object_get_ex(parsed, "device_id", &device_id_obj);
    json_object_object_get_ex(parsed, "status", &status_obj);

    if (device_id_obj) g_config->device_id = strdup(json_object_get_string(device_id_obj));
    if (status_obj) g_config->status = strdup(json_object_get_string(status_obj));

    struct json_object *mqtt_obj;
    json_object_object_get_ex(parsed, "mqtt", &mqtt_obj);
    if (mqtt_obj) {
        struct json_object *broker_url, *broker_port, *client_id;
        json_object_object_get_ex(mqtt_obj, "broker_url", &broker_url);
        json_object_object_get_ex(mqtt_obj, "broker_port", &broker_port);
        json_object_object_get_ex(mqtt_obj, "client_id", &client_id);

        if (broker_url) g_config->mqtt.broker_url = strdup(json_object_get_string(broker_url));
        if (broker_port) g_config->mqtt.broker_port = json_object_get_int(broker_port);
        if (client_id) g_config->mqtt.client_id = strdup(json_object_get_string(client_id));
    }

    struct json_object *topics_obj;
    json_object_object_get_ex(parsed, "topics", &topics_obj);
    if (topics_obj) {
        struct json_object *telemetry, *heartbeat, *config_sub, *command_sub, *cmd_resp, *ota,
            *ota_resp, *application_sub, *application_resp;

        json_object_object_get_ex(topics_obj, "telemetry_publish", &telemetry);
        json_object_object_get_ex(topics_obj, "heartbeat_publish", &heartbeat);
        json_object_object_get_ex(topics_obj, "config_subscribe", &config_sub);
        json_object_object_get_ex(topics_obj, "command_subscribe", &command_sub);
        json_object_object_get_ex(topics_obj, "command_response_publish", &cmd_resp);
        json_object_object_get_ex(topics_obj, "ota_subscribe", &ota);
        json_object_object_get_ex(topics_obj, "ota_response_publish", &ota_resp);
        json_object_object_get_ex(topics_obj, "app_subscribe", &application_sub);
        json_object_object_get_ex(topics_obj, "app_response_publish", &application_resp);

        if (telemetry)
            g_config->topics.telemetry_publish = strdup(json_object_get_string(telemetry));
        if (heartbeat)
            g_config->topics.heartbeat_publish = strdup(json_object_get_string(heartbeat));
        if (config_sub)
            g_config->topics.config_subscribe = strdup(json_object_get_string(config_sub));
        if (command_sub)
            g_config->topics.command_subscribe = strdup(json_object_get_string(command_sub));
        if (cmd_resp)
            g_config->topics.command_response_publish = strdup(json_object_get_string(cmd_resp));
        if (ota) g_config->topics.ota_subscribe = strdup(json_object_get_string(ota));
        if (ota_resp)
            g_config->topics.ota_response_publish = strdup(json_object_get_string(ota_resp));
        if (application_sub)
            g_config->topics.application_subscribe =
                strdup(json_object_get_string(application_sub));
        if (application_resp)
            g_config->topics.application_response_publish =
                strdup(json_object_get_string(application_resp));
    }

    struct json_object *telemetry_interval_obj, *heartbeat_interval_obj;
    json_object_object_get_ex(parsed, "telemetry_interval_seconds", &telemetry_interval_obj);
    json_object_object_get_ex(parsed, "heartbeat_interval_seconds", &heartbeat_interval_obj);

    if (telemetry_interval_obj)
        g_config->telemetry_interval_seconds = json_object_get_int(telemetry_interval_obj);
    if (heartbeat_interval_obj)
        g_config->heartbeat_interval_seconds = json_object_get_int(heartbeat_interval_obj);

    json_object_put(parsed);
    free(json_data);

    return 0;
}

int mqtt_run(void)
{
    if (!g_config) {
        fprintf(stderr, "MQTT not initialized. "
                        "Call mqtt_init_from_config first.\n");
        return -1;
    }

    int ret;

    mosquitto_lib_init();

    mosq = mosquitto_new(g_config->mqtt.client_id, 1, NULL);
    if (!mosq) {
        fprintf(stderr, "Failed to create Mosquitto instance\n");
        return -1;
    }

    mosquitto_connect_callback_set(mosq, mqtt_connect_callback);
    mosquitto_disconnect_callback_set(mosq, mqtt_disconnect_callback);
    mosquitto_message_callback_set(mosq, mqtt_message_callback);
    mosquitto_log_callback_set(mosq, mqtt_log_callback);

    mosquitto_reconnect_delay_set(mosq, NCMS_MQTT_RETRY_SECONDS, NCMS_MQTT_RETRY_MAX_SECONDS, true);

    ret = mosquitto_tls_set(mosq, CA_CERT_PATH, NULL, CERT_PATH, KEY_PATH, NULL);
    if (ret != MOSQ_ERR_SUCCESS) {
        fprintf(stderr, "Failed to set TLS certificates: %s\n", mosquitto_strerror(ret));
        return -1;
    }

    ret = mosquitto_tls_opts_set(mosq, 1, "tlsv1.3", NULL);
    if (ret != MOSQ_ERR_SUCCESS) {
        fprintf(stderr, "Failed to set TLS opts: %s\n", mosquitto_strerror(ret));
        return -1;
    }

    mosquitto_tls_insecure_set(mosq, true);

    printf("\n[MQTT] Connecting to broker: %s:%d\n", g_config->mqtt.broker_url,
           g_config->mqtt.broker_port);
    printf("[MQTT] Client ID: %s\n", g_config->mqtt.client_id);

    mqtt_running = 1;

    while (mqtt_running) {
        ret = mosquitto_connect(mosq, g_config->mqtt.broker_url, g_config->mqtt.broker_port, 60);
        if (ret == MOSQ_ERR_SUCCESS) {
            break;
        }

        fprintf(stderr, "Failed to connect: %s. Retrying in %d seconds...\n",
                mosquitto_strerror(ret), NCMS_MQTT_RETRY_SECONDS);
        sleep(NCMS_MQTT_RETRY_SECONDS);
    }

    if (!mqtt_running) return 0;

    ret = mosquitto_loop_start(mosq);
    if (ret != MOSQ_ERR_SUCCESS) {
        fprintf(stderr, "Failed to start MQTT loop: %s\n", mosquitto_strerror(ret));
        return -1;
    }

    while (mqtt_running) {
        sleep(1);
    }

    return 0;
}

void mqtt_stop(void)
{
    mqtt_running = 0;
    if (mosq) {
        mosquitto_disconnect(mosq);
        mosquitto_loop_stop(mosq, 1);
        mosquitto_destroy(mosq);
        mosq = NULL;
    }
    mosquitto_lib_cleanup();
}

int mqtt_is_connected(void)
{
    return (mosq && mqtt_running && mqtt_connected);
}

int mqtt_publish(const char *topic, const char *payload, int qos, int retain)
{
    if (!mosq || !mqtt_running || !mqtt_connected || !topic || !payload) return -1;
    return mosquitto_publish(mosq, NULL, topic, strlen(payload), payload, qos, retain);
}

MqttConfig *mqtt_get_config(void)
{
    return g_config;
}

void mqtt_cleanup(void)
{
    mqtt_stop();

    if (g_config) {
        free(g_config->device_id);
        free(g_config->status);
        free(g_config->mqtt.broker_url);
        free(g_config->mqtt.client_id);
        free(g_config->topics.telemetry_publish);
        free(g_config->topics.heartbeat_publish);
        free(g_config->topics.config_subscribe);
        free(g_config->topics.command_subscribe);
        free(g_config->topics.command_response_publish);
        free(g_config->topics.ota_subscribe);
        free(g_config->topics.application_subscribe);
        free(g_config);
        g_config = NULL;
    }
}