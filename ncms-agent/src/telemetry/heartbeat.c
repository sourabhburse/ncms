#include "heartbeat.h"

#include "mqtt_client.h"
#include "telemetry.h"

#include <pthread.h>
#include <stdio.h>
#include <time.h>
#include <unistd.h>

static pthread_t heartbeat_thread;
static volatile int heartbeat_running = 0;

struct json_object *heartbeat_build_payload(const char *device_id)
{
    struct json_object *heartbeat = json_object_new_object();

    json_object_object_add(heartbeat, "timestamp",
                           json_object_new_int64((int64_t)time(NULL) * 1000));
    json_object_object_add(heartbeat, "device_id", json_object_new_string(device_id));
    json_object_object_add(heartbeat, "status", json_object_new_string("online"));
    json_object_object_add(heartbeat, "uptime",
                           json_object_new_int64(telemetry_get_uptime_seconds()));

    return heartbeat;
}

static void *heartbeat_worker(void *arg)
{
    while (heartbeat_running) {
        MqttConfig *config = mqtt_get_config();

        if (config && mqtt_is_connected() && config->topics.heartbeat_publish) {
            struct json_object *payload = heartbeat_build_payload(config->mqtt.client_id);
            const char *payload_str = json_object_to_json_string(payload);

            int ret = mqtt_publish(config->topics.heartbeat_publish, payload_str, 1, 0);
            if (ret == 0)
                printf("[MQTT] Heartbeat published to %s\n", config->topics.heartbeat_publish);

            json_object_put(payload);
        }

        int interval = config && config->heartbeat_interval_seconds > 0
                           ? config->heartbeat_interval_seconds
                           : 30;
        sleep(interval);
    }

    return NULL;
}

int heartbeat_start(void)
{
    if (heartbeat_running) return 0;

    heartbeat_running = 1;

    if (pthread_create(&heartbeat_thread, NULL, heartbeat_worker, NULL) != 0) {
        heartbeat_running = 0;
        return -1;
    }

    return 0;
}

void heartbeat_stop(void)
{
    if (!heartbeat_running) return;
    heartbeat_running = 0;
    pthread_join(heartbeat_thread, NULL);
}
