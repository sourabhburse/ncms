#ifndef OTA_HANDLER_H
#define OTA_HANDLER_H
#include "stddef.h"

#include <stdbool.h>

typedef enum {
    OTA_STATUS_SUCCESS = 0,
    OTA_STATUS_DOWNLOAD_FAILED = 1,
    OTA_STATUS_SIZE_MISMATCH = 2,
    OTA_STATUS_MD5_MISMATCH = 3,
    OTA_STATUS_SHA256_MISMATCH = 4,
    OTA_STATUS_VALIDATION_FAILED = 5,
    OTA_STATUS_FLASH_FAILED = 6,
    OTA_STATUS_UNKNOWN_ERROR = 99
} OtaErrorCode;

typedef struct {
    char status[16];
    char error_code[16];
    char error_message[256];
    char version[64];
} OtaResult;

typedef enum {
    OTA_STATE_ACKNOWLEDGED = 0,
    OTA_STATE_APPLYING = 1,
    OTA_STATE_REBOOTING = 2,
    OTA_STATE_SUCCESS = 3,
    OTA_STATE_FAILED = 4
} OtaState;

/* Handles one OTA JSON request; returns 0 after dispatch, -1 on parse/lock errors. */
int ota_handle_request(const char *json_payload);

/* Returns 1 when a reboot-pending OTA result exists, otherwise 0. */
int ota_result_pending(void);

/* Returns 0 when result is loaded, -1 when no pending result exists. */
int ota_get_pending_result(OtaResult *result);

void ota_get_pending_version(char *out, size_t out_sz);

void ota_clear_result_flag(void);

/* MQTT publish helpers return 0 on success, -1 on failure. */
int ota_publish_result(const char *device_id, const OtaResult *result);

int ota_publish_status(const char *client_id, OtaState state);

#endif
