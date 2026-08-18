#include "mqtt_client.h"
#include "telemetry.h"

#include <json-c/json.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

MqttConfig *g_config = NULL;

int mqtt_is_connected(void)
{
    return 0;
}

int mqtt_publish(const char *topic, const char *payload, int qos, int retain)
{
    (void)topic;
    (void)payload;
    (void)qos;
    (void)retain;
    return -1;
}

MqttConfig *mqtt_get_config(void)
{
    return NULL;
}

static int failures = 0;
static char fixture_dir[128];

static void expect(int condition, const char *message)
{
    if (!condition) {
        fprintf(stderr, "FAIL: %s\n", message);
        failures++;
    }
}

static struct json_object *get_object(struct json_object *object, const char *key)
{
    struct json_object *value = NULL;
    return json_object_object_get_ex(object, key, &value) ? value : NULL;
}

static void expect_string(struct json_object *object, const char *key, const char *expected)
{
    struct json_object *value = get_object(object, key);
    expect(value && json_object_is_type(value, json_type_string) &&
               strcmp(json_object_get_string(value), expected) == 0,
           key);
}

static void expect_boolean(struct json_object *object, const char *key, int expected)
{
    struct json_object *value = get_object(object, key);
    expect(value && json_object_is_type(value, json_type_boolean) &&
               json_object_get_boolean(value) == expected,
           key);
}

static struct json_object *build_payload_for(const char *scenario)
{
    setenv("NCMS_TEST_SCENARIO", scenario, 1);
    return telemetry_build_payload();
}

static void test_link_up(void)
{
    struct json_object *payload = build_payload_for("up");

    expect_string(payload, "interface_name", "LTE2");
    expect_boolean(payload, "interface_up", 1);
    expect_string(payload, "ipv4_address", "10.10.20.30");
    expect_string(payload, "ipv6_address", "2001:db8::1");
    expect_string(payload, "wan_ip", "10.10.20.30");
    json_object_put(payload);
}

static void test_link_down(void)
{
    struct json_object *payload = build_payload_for("down");

    expect_string(payload, "interface_name", "LTE2");
    expect_boolean(payload, "interface_up", 0);
    expect(!get_object(payload, "ipv4_address"), "no IPv4 when down");
    expect(!get_object(payload, "ipv6_address"), "no IPv6 when down");
    expect_string(payload, "wan_ip", "unknown");
    json_object_put(payload);
}

static void test_malformed_responses(void)
{
    struct json_object *payload = build_payload_for("malformed_cellular");
    expect(!get_object(payload, "imei"), "malformed cellular omitted");
    expect_string(payload, "wan_ip", "unknown");
    json_object_put(payload);

    payload = build_payload_for("malformed_interface");
    expect_string(payload, "imei", "868896069606673");
    expect(!get_object(payload, "interface_name"), "invalid interface response omitted");
    expect(!get_object(payload, "interface_up"), "invalid interface state omitted");
    json_object_put(payload);
}

static void test_invalid_interface_name(void)
{
    struct json_object *payload = build_payload_for("invalid_name");

    expect_string(payload, "device", "LTE2;bad");
    expect(!get_object(payload, "interface_name"), "unsafe interface name omitted");
    json_object_put(payload);
}

static int create_ubus_fixture(void)
{
    char path[192];
    const char *existing_path = getenv("PATH");
    char new_path[4096];
    const char script[] =
        "#!/bin/sh\n"
        "case \"$2\" in\n"
        "cellular)\n"
        "  case \"$NCMS_TEST_SCENARIO\" in\n"
        "    malformed_cellular) printf '{bad-json\\n' ;;\n"
        "    invalid_name) printf '%s\\n' '{\"message\":\"[{\\\"device\\\":\\\"LTE2;bad\\\"}]\"}' ;;\n"
        "    *) printf '%s\\n' '{\"message\":\"[{\\\"device\\\":\\\"LTE2\\\",\\\"imei\\\":\\\"868896069606673\\\"}]\"}' ;;\n"
        "  esac ;;\n"
        "network.interface.LTE2)\n"
        "  case \"$NCMS_TEST_SCENARIO\" in\n"
        "    up) printf '%s\\n' '{\"up\":true,\"ipv4-address\":[{\"address\":\"10.10.20.30\"}],\"ipv6-address\":[{\"address\":\"2001:db8::1\"}]}' ;;\n"
        "    down) printf '%s\\n' '{\"up\":false}' ;;\n"
        "    malformed_interface) printf '{bad-json\\n' ;;\n"
        "  esac ;;\n"
        "*) printf '%s\\n' '{}' ;;\n"
        "esac\n";

    snprintf(fixture_dir, sizeof(fixture_dir), "/tmp/ncms-telemetry-test-%ld", (long)getpid());
    if (mkdir(fixture_dir, 0700) != 0) return -1;

    snprintf(path, sizeof(path), "%s/ubus", fixture_dir);
    FILE *fp = fopen(path, "w");
    if (!fp) return -1;
    fputs(script, fp);
    fclose(fp);
    if (chmod(path, 0700) != 0) return -1;

    snprintf(new_path, sizeof(new_path), "%s:%s", fixture_dir,
             existing_path ? existing_path : "");
    return setenv("PATH", new_path, 1);
}

int main(void)
{
    if (create_ubus_fixture() != 0) {
        fprintf(stderr, "Could not create ubus fixture\n");
        return 1;
    }

    test_link_up();
    test_link_down();
    test_malformed_responses();
    test_invalid_interface_name();

    return failures == 0 ? 0 : 1;
}
