#ifndef HEARTBEAT_H
#define HEARTBEAT_H

#include <json-c/json.h>

int heartbeat_start(void);
void heartbeat_stop(void);
struct json_object *heartbeat_build_payload(const char *device_id);

#endif
