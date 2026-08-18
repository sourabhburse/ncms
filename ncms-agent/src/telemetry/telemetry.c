#include "telemetry.h"

#include "mqtt_client.h"

#include <ctype.h>
#include <pthread.h>
#include <sys/statvfs.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

static pthread_t telemetry_thread;
static volatile int telemetry_running = 0;

unsigned long telemetry_get_uptime_seconds(void)
{
    FILE *fp = fopen("/proc/uptime", "r");
    if (!fp) {
        fprintf(stderr, "Failed to open /proc/uptime\n");
        return 0;
    }

    char buffer[64];
    unsigned long uptime_seconds = 0;

    if (fgets(buffer, sizeof(buffer), fp)) {
        char *space = strchr(buffer, ' ');
        if (space) {
            *space = '\0';
        }
        uptime_seconds = strtoul(buffer, NULL, 10);
        printf("[TELEMETRY] uptime_seconds=%lu\n", uptime_seconds);
    }

    fclose(fp);
    return uptime_seconds;
}

static double telemetry_get_cpu_usage_percent(void)
{
    static unsigned long long prev_idle = 0;
    static unsigned long long prev_total = 0;
    unsigned long long user, nice, system, idle, iowait, irq, softirq, steal;

    FILE *fp = fopen("/proc/stat", "r");
    if (!fp) {
        printf("[TELEMETRY] cpu: failed to open /proc/stat\n");
        return 0.0;
    }

    int matched = fscanf(fp, "cpu %llu %llu %llu %llu %llu %llu %llu %llu",
                         &user, &nice, &system, &idle, &iowait, &irq, &softirq, &steal);
    fclose(fp);

    if (matched < 8) {
        printf("[TELEMETRY] cpu: failed to parse /proc/stat\n");
        return 0.0;
    }

    unsigned long long idle_all = idle + iowait;
    unsigned long long total = user + nice + system + idle + iowait + irq + softirq + steal;

    if (prev_total == 0) {
        prev_idle = idle_all;
        prev_total = total;
        printf("[TELEMETRY] cpu_usage_percent=0.00 first sample\n");
        return 0.0;
    }

    unsigned long long total_delta = total - prev_total;
    unsigned long long idle_delta = idle_all - prev_idle;

    prev_idle = idle_all;
    prev_total = total;

    if (total_delta == 0) {
        printf("[TELEMETRY] cpu_usage_percent=0.00 no delta\n");
        return 0.0;
    }

    double cpu_usage = ((double)(total_delta - idle_delta) * 100.0) / (double)total_delta;
    printf("[TELEMETRY] cpu_usage_percent=%.2f\n", cpu_usage);
    return cpu_usage;
}

static int telemetry_get_memory_mb(double *used_mb, double *total_mb)
{
    char key[64];
    char unit[16];
    unsigned long value;
    unsigned long mem_total = 0;
    unsigned long mem_available = 0;

    FILE *fp = fopen("/proc/meminfo", "r");
    if (!fp) {
        printf("[TELEMETRY] memory: failed to open /proc/meminfo\n");
        return -1;
    }

    while (fscanf(fp, "%63s %lu %15s\n", key, &value, unit) == 3) {
        if (strcmp(key, "MemTotal:") == 0) {
            mem_total = value;
        } else if (strcmp(key, "MemAvailable:") == 0) {
            mem_available = value;
        }
    }

    fclose(fp);

    if (mem_total == 0 || !used_mb || !total_mb) {
        printf("[TELEMETRY] memory: failed to parse MemTotal/MemAvailable\n");
        return -1;
    }

    *total_mb = (double)mem_total / 1024.0;
    *used_mb = (double)(mem_total - mem_available) / 1024.0;

    printf("[TELEMETRY] ram_usage_mb=%.2f ram_total_mb=%.2f\n", *used_mb, *total_mb);
    return 0;
}

static int telemetry_get_storage_mb(const char *path, double *used_mb, double *total_mb)
{
    struct statvfs st;

    if (!path || !used_mb || !total_mb) {
        printf("[TELEMETRY] storage: invalid input\n");
        return -1;
    }
    if (statvfs(path, &st) != 0) {
        printf("[TELEMETRY] storage: statvfs failed for %s\n", path);
        return -1;
    }

    unsigned long long total = (unsigned long long)st.f_blocks * st.f_frsize;
    unsigned long long free_bytes = (unsigned long long)st.f_bfree * st.f_frsize;
    unsigned long long used = total - free_bytes;

    *total_mb = (double)total / (1024.0 * 1024.0);
    *used_mb = (double)used / (1024.0 * 1024.0);

    printf("[TELEMETRY] storage_used_mb=%.2f storage_total_mb=%.2f path=%s\n", *used_mb,
           *total_mb, path);
    return 0;
}

static int telemetry_get_interface_ip(const char *ifname, char *out, size_t out_sz)
{
    char command[128];
    char buffer[4096];
    size_t len;

    snprintf(command, sizeof(command), "ubus call network.interface.%s status", ifname);

    FILE *fp = popen(command, "r");
    if (!fp) {
        printf("[TELEMETRY] wan_ip: failed to run ubus for interface=%s\n", ifname);
        return -1;
    }

    len = fread(buffer, 1, sizeof(buffer) - 1, fp);
    pclose(fp);

    buffer[len] = '\0';

    struct json_object *root = json_tokener_parse(buffer);
    if (!root) {
        printf("[TELEMETRY] wan_ip: failed to parse ubus JSON interface=%s\n", ifname);
        return -1;
    }

    struct json_object *up_obj = NULL;
    if (!json_object_object_get_ex(root, "up", &up_obj) ||
        !json_object_get_boolean(up_obj)) {
        json_object_put(root);
        printf("[TELEMETRY] wan_ip: interface=%s is down\n", ifname);
        return -1;
    }

    struct json_object *addr_array = NULL;
    if (!json_object_object_get_ex(root, "ipv4-address", &addr_array) ||
        !json_object_is_type(addr_array, json_type_array) ||
        json_object_array_length(addr_array) == 0) {
        json_object_put(root);
        printf("[TELEMETRY] wan_ip: interface=%s has no ipv4-address\n", ifname);
        return -1;
    }

    struct json_object *addr_obj = json_object_array_get_idx(addr_array, 0);
    struct json_object *address = NULL;

    if (!addr_obj || !json_object_object_get_ex(addr_obj, "address", &address)) {
        json_object_put(root);
        printf("[TELEMETRY] wan_ip: address missing interface=%s\n", ifname);
        return -1;
    }

    snprintf(out, out_sz, "%s", json_object_get_string(address));
    json_object_put(root);

    printf("[TELEMETRY] wan_ip=%s interface=%s\n", out, ifname);
    return 0;
}

static int telemetry_get_wan_ip(char *out, size_t out_sz)
{
    static const char *interfaces[] = {
        "wan",
        "wan6",
        "wwan"
    };

    if (!out || out_sz == 0) {
        printf("[TELEMETRY] wan_ip: invalid output buffer\n");
        return -1;
    }

    for (size_t i = 0; i < sizeof(interfaces) / sizeof(interfaces[0]); i++) {
        if (telemetry_get_interface_ip(interfaces[i], out, out_sz) == 0)
            return 0;
    }

    printf("[TELEMETRY] wan_ip: no active IPv4 interface found\n");
    return -1;
}

static void telemetry_clean_cellular_string(char *out, size_t out_sz, const char *value)
{
    size_t len = 0;

    if (!out || out_sz == 0) return;
    out[0] = '\0';
    if (!value) return;

    while (*value && (isspace((unsigned char)*value) || *value == '"'))
        value++;

    while (value[len] && value[len] != '\r' && value[len] != '\n' && len < out_sz - 1) {
        out[len] = value[len];
        len++;
    }

    while (len > 0 && (isspace((unsigned char)out[len - 1]) || out[len - 1] == '"'))
        len--;

    out[len] = '\0';
}

static int telemetry_add_cellular_string(struct json_object *cellular,
                                         struct json_object *status,
                                         const char *output_name, const char *input_name)
{
    struct json_object *value = NULL;
    char cleaned[128];

    if (!json_object_object_get_ex(status, input_name, &value) ||
        !json_object_is_type(value, json_type_string))
        return 0;

    telemetry_clean_cellular_string(cleaned, sizeof(cleaned), json_object_get_string(value));
    if (cleaned[0] == '\0') return 0;

    json_object_object_add(cellular, output_name, json_object_new_string(cleaned));
    return 1;
}

static int telemetry_add_cellular_int(struct json_object *cellular, struct json_object *status,
                                      const char *output_name, const char *input_name)
{
    struct json_object *value = NULL;

    if (!json_object_object_get_ex(status, input_name, &value) ||
        !json_object_is_type(value, json_type_int))
        return 0;

    json_object_object_add(cellular, output_name, json_object_new_int(json_object_get_int(value)));
    return 1;
}

static int telemetry_add_cellular_temperature(struct json_object *cellular,
                                              struct json_object *status)
{
    struct json_object *value = NULL;
    char cleaned[32];
    char *end = NULL;
    long temperature;

    if (!json_object_object_get_ex(status, "temperature", &value)) return 0;

    if (json_object_is_type(value, json_type_int)) {
        json_object_object_add(cellular, "temperature_c",
                               json_object_new_int(json_object_get_int(value)));
        return 1;
    }
    if (!json_object_is_type(value, json_type_string)) return 0;

    telemetry_clean_cellular_string(cleaned, sizeof(cleaned), json_object_get_string(value));
    temperature = strtol(cleaned, &end, 10);
    if (cleaned[0] == '\0' || end == cleaned) return 0;

    json_object_object_add(cellular, "temperature_c", json_object_new_int((int)temperature));
    return 1;
}

static int telemetry_add_cellular_roaming(struct json_object *cellular, struct json_object *status)
{
    struct json_object *value = NULL;
    char cleaned[16];

    if (!json_object_object_get_ex(status, "roaming", &value)) return 0;

    if (json_object_is_type(value, json_type_boolean)) {
        json_object_object_add(cellular, "roaming",
                               json_object_new_boolean(json_object_get_boolean(value)));
        return 1;
    }
    if (!json_object_is_type(value, json_type_string)) return 0;

    telemetry_clean_cellular_string(cleaned, sizeof(cleaned), json_object_get_string(value));
    if (strcmp(cleaned, "true") != 0 && strcmp(cleaned, "false") != 0) return 0;

    json_object_object_add(cellular, "roaming", json_object_new_boolean(strcmp(cleaned, "true") == 0));
    return 1;
}

static int telemetry_get_clean_cellular_string(struct json_object *status, const char *key,
                                               char *out, size_t out_sz)
{
    struct json_object *value = NULL;

    if (!json_object_object_get_ex(status, key, &value) ||
        !json_object_is_type(value, json_type_string))
        return 0;

    telemetry_clean_cellular_string(out, out_sz, json_object_get_string(value));
    return out[0] != '\0';
}

static int telemetry_is_safe_interface_name(const char *ifname)
{
    size_t len;

    if (!ifname || ifname[0] == '\0') return 0;

    for (len = 0; ifname[len] != '\0'; len++) {
        unsigned char c = (unsigned char)ifname[len];
        if (!(isalnum(c) || c == '.' || c == '_' || c == '-')) return 0;
        if (len >= 63) return 0;
    }

    return 1;
}

static int telemetry_add_interface_address(struct json_object *cellular,
                                           struct json_object *status,
                                           const char *source_name, const char *output_name,
                                           char *wan_ip, size_t wan_ip_sz)
{
    struct json_object *addresses = NULL;
    struct json_object *entry;
    struct json_object *address = NULL;
    const char *address_text;

    if (!json_object_object_get_ex(status, source_name, &addresses) ||
        !json_object_is_type(addresses, json_type_array) ||
        json_object_array_length(addresses) == 0)
        return 0;

    entry = json_object_array_get_idx(addresses, 0);
    if (!entry || !json_object_object_get_ex(entry, "address", &address) ||
        !json_object_is_type(address, json_type_string))
        return 0;

    address_text = json_object_get_string(address);
    if (!address_text || address_text[0] == '\0') return 0;

    json_object_object_add(cellular, output_name, json_object_new_string(address_text));
    if (wan_ip && strcmp(wan_ip, "unknown") == 0)
        snprintf(wan_ip, wan_ip_sz, "%s", address_text);

    return 1;
}

static void telemetry_add_cellular_interface_status(struct json_object *cellular,
                                                    const char *ifname, char *wan_ip,
                                                    size_t wan_ip_sz)
{
    char command[128];
    char buffer[4096];
    size_t len;
    struct json_object *response;
    struct json_object *up = NULL;

    if (!telemetry_is_safe_interface_name(ifname)) {
        printf("[TELEMETRY] cellular: invalid interface name\n");
        return;
    }

    snprintf(command, sizeof(command), "ubus call network.interface.%s status", ifname);
    FILE *fp = popen(command, "r");
    if (!fp) {
        printf("[TELEMETRY] cellular: failed to run ubus for interface=%s\n", ifname);
        return;
    }

    len = fread(buffer, 1, sizeof(buffer) - 1, fp);
    pclose(fp);
    buffer[len] = '\0';

    response = json_tokener_parse(buffer);
    if (!response || !json_object_is_type(response, json_type_object)) {
        printf("[TELEMETRY] cellular: invalid interface response=%s\n", ifname);
        if (response) json_object_put(response);
        return;
    }

    json_object_object_add(cellular, "interface_name", json_object_new_string(ifname));
    if (json_object_object_get_ex(response, "up", &up) &&
        json_object_is_type(up, json_type_boolean))
        json_object_object_add(cellular, "interface_up",
                               json_object_new_boolean(json_object_get_boolean(up)));

    telemetry_add_interface_address(cellular, response, "ipv4-address", "ipv4_address", wan_ip,
                                    wan_ip_sz);
    telemetry_add_interface_address(cellular, response, "ipv6-address", "ipv6_address", NULL, 0);

    json_object_put(response);
}

static void telemetry_add_cellular_status(struct json_object *telemetry, char *wan_ip,
                                          size_t wan_ip_sz)
{
    char buffer[4096];
    size_t len;
    int fields_added = 0;
    FILE *fp = popen("ubus call cellular status", "r");

    if (!fp) {
        printf("[TELEMETRY] cellular: failed to run ubus\n");
        return;
    }

    len = fread(buffer, 1, sizeof(buffer) - 1, fp);
    pclose(fp);
    buffer[len] = '\0';

    struct json_object *response = json_tokener_parse(buffer);
    struct json_object *message = NULL;
    if (!response || !json_object_object_get_ex(response, "message", &message) ||
        !json_object_is_type(message, json_type_string)) {
        printf("[TELEMETRY] cellular: invalid ubus response\n");
        if (response) json_object_put(response);
        return;
    }

    struct json_object *devices = json_tokener_parse(json_object_get_string(message));
    if (!devices || !json_object_is_type(devices, json_type_array) ||
        json_object_array_length(devices) == 0) {
        printf("[TELEMETRY] cellular: no modem status available\n");
        if (devices) json_object_put(devices);
        json_object_put(response);
        return;
    }

    struct json_object *status = json_object_array_get_idx(devices, 0);
    if (!status || !json_object_is_type(status, json_type_object)) {
        printf("[TELEMETRY] cellular: invalid modem status\n");
        json_object_put(devices);
        json_object_put(response);
        return;
    }

    char interface_name[64];
    fields_added += telemetry_add_cellular_string(telemetry, status, "imei", "imei");
    fields_added += telemetry_add_cellular_int(telemetry, status, "rssi_dbm", "rssi");
    fields_added += telemetry_add_cellular_string(telemetry, status, "network_type", "net_type");
    fields_added += telemetry_add_cellular_string(telemetry, status, "registration", "registration");
    fields_added += telemetry_add_cellular_int(telemetry, status, "registration_code",
                                                "registration_code");
    fields_added += telemetry_add_cellular_string(telemetry, status, "data_connectivity",
                                                  "data_connectivity");
    fields_added += telemetry_add_cellular_roaming(telemetry, status);
    fields_added += telemetry_add_cellular_string(telemetry, status, "operator_name",
                                                  "plmn_description");
    fields_added += telemetry_add_cellular_string(telemetry, status, "plmn", "plmn_code");
    fields_added += telemetry_add_cellular_string(telemetry, status, "device", "device");
    fields_added += telemetry_add_cellular_string(telemetry, status, "band", "band");
    fields_added += telemetry_add_cellular_string(telemetry, status, "sim_status", "sim_status");
    fields_added += telemetry_add_cellular_temperature(telemetry, status);

    if (telemetry_get_clean_cellular_string(status, "device", interface_name,
                                            sizeof(interface_name)))
        telemetry_add_cellular_interface_status(telemetry, interface_name, wan_ip, wan_ip_sz);

    if (fields_added > 0)
        printf("[TELEMETRY] cellular status fields=%d\n", fields_added);

    json_object_put(devices);
    json_object_put(response);
}

struct json_object *telemetry_build_payload(void)
{
    struct json_object *telemetry = json_object_new_object();

    double ram_used_mb = 0.0;
    double ram_total_mb = 0.0;
    double storage_used_mb = 0.0;
    double storage_total_mb = 0.0;
    char wan_ip[64] = "unknown";

    if (telemetry_get_memory_mb(&ram_used_mb, &ram_total_mb) != 0)
        printf("[TELEMETRY] memory values fallback to 0\n");
    if (telemetry_get_storage_mb("/", &storage_used_mb, &storage_total_mb) != 0)
        printf("[TELEMETRY] storage values fallback to 0\n");
    if (telemetry_get_wan_ip(wan_ip, sizeof(wan_ip)) != 0)
        printf("[TELEMETRY] wan_ip fallback to unknown\n");

    double cpu_usage_percent = telemetry_get_cpu_usage_percent();
    unsigned long uptime_seconds = telemetry_get_uptime_seconds();

    json_object_object_add(telemetry, "cpu_usage_percent",
                           json_object_new_double(cpu_usage_percent));
    json_object_object_add(telemetry, "ram_usage_mb", json_object_new_double(ram_used_mb));
    json_object_object_add(telemetry, "ram_total_mb", json_object_new_double(ram_total_mb));
    json_object_object_add(telemetry, "storage_used_mb", json_object_new_double(storage_used_mb));
    json_object_object_add(telemetry, "storage_total_mb", json_object_new_double(storage_total_mb));
    json_object_object_add(telemetry, "uptime_seconds", json_object_new_int64(uptime_seconds));
    telemetry_add_cellular_status(telemetry, wan_ip, sizeof(wan_ip));
    json_object_object_add(telemetry, "wan_ip", json_object_new_string(wan_ip));

    printf("[TELEMETRY] payload cpu=%.2f ram=%.2f/%.2f storage=%.2f/%.2f uptime=%lu wan_ip=%s\n",
           cpu_usage_percent, ram_used_mb, ram_total_mb, storage_used_mb, storage_total_mb,
           uptime_seconds, wan_ip);

    return telemetry;
}

static void *telemetry_worker(void *arg)
{
    while (telemetry_running) {
        MqttConfig *config = mqtt_get_config();

        if (config && mqtt_is_connected() && config->topics.telemetry_publish) {
            struct json_object *payload = telemetry_build_payload();
            const char *payload_str = json_object_to_json_string(payload);

            int ret = mqtt_publish(config->topics.telemetry_publish, payload_str, 1, 0);
            if (ret == 0)
                printf("[MQTT] Telemetry published to %s\n", config->topics.telemetry_publish);

            json_object_put(payload);
        }

        int interval = config && config->telemetry_interval_seconds > 0
                           ? config->telemetry_interval_seconds
                           : 60;
        sleep(interval);
    }

    return NULL;
}

int telemetry_start(void)
{
    if (telemetry_running) return 0;

    telemetry_running = 1;

    if (pthread_create(&telemetry_thread, NULL, telemetry_worker, NULL) != 0) {
        telemetry_running = 0;
        return -1;
    }

    return 0;
}

void telemetry_stop(void)
{
    if (!telemetry_running) return;

    telemetry_running = 0;
    pthread_join(telemetry_thread, NULL);
}
