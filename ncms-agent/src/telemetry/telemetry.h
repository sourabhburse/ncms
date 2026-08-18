#ifndef TELEMETRY_H
#define TELEMETRY_H

#include <json-c/json.h>

int telemetry_start(void);
void telemetry_stop(void);
unsigned long telemetry_get_uptime_seconds(void);
struct json_object *telemetry_build_payload(void);

#endif
