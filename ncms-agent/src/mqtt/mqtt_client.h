#ifndef MQTT_CLIENT_H
#define MQTT_CLIENT_H

#include <stdbool.h>
#include "app_defaults.h"

#define KEY_PATH     NCMS_DEFAULT_STORAGE_DIR "device.key"
#define CSR_PATH     NCMS_DEFAULT_STORAGE_DIR "device.csr"
#define CERT_PATH    NCMS_DEFAULT_STORAGE_DIR "device.crt"
#define CA_CERT_PATH NCMS_DEFAULT_STORAGE_DIR "ca.crt"
#define CONFIG_PATH  NCMS_DEFAULT_STORAGE_DIR "config.json"

typedef struct {
    char *device_id;
    char *status;
    struct {
        char *broker_url;
        int broker_port;
        char *client_id;
    } mqtt;
    struct {
        char *telemetry_publish;
        char *heartbeat_publish;
        char *config_subscribe;
        char *command_subscribe;
        char *command_response_publish;
        char *ota_subscribe;
        char *ota_response_publish;
        char *application_subscribe;
        char *application_response_publish;
    } topics;
    int telemetry_interval_seconds;
    int heartbeat_interval_seconds;
} MqttConfig;

extern MqttConfig *g_config;

/* Loads MQTT config; returns 0 on success, -1 on failure. */
int mqtt_init_from_config(const char *config_path);

/* Blocking loop; returns 0 on clean exit, -1 on failure. */
int mqtt_run(void);

void mqtt_stop(void);

int mqtt_is_connected(void);

/* Returns 0 when publish succeeds, -1 otherwise. */
int mqtt_publish(const char *topic, const char *payload, int qos, int retain);

MqttConfig *mqtt_get_config(void);

void mqtt_cleanup(void);

#endif
