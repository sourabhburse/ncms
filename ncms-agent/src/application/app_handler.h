#ifndef APP_HANDLER_H
#define APP_HANDLER_H

#include <stdbool.h>
#include <stddef.h>

typedef enum {
    APP_STATUS_SUCCESS = 0,
    APP_STATUS_DOWNLOAD_FAILED = 1,
    APP_STATUS_SIZE_MISMATCH = 2,
    APP_STATUS_MD5_MISMATCH = 3,
    APP_STATUS_SHA256_MISMATCH = 4,
    APP_STATUS_VALIDATION_FAILED = 5,
    APP_STATUS_INSTALL_FAILED = 6,
    APP_STATUS_DEPENDENCY_MISSING = 7,
    APP_STATUS_NOT_INSTALLED = 8,
    APP_STATUS_REMOVE_FAILED = 9,
    APP_STATUS_MISSING_FIELDS = 10,
    APP_STATUS_VERSION_MISMATCH = 11,
    APP_STATUS_UNKNOWN_ERROR = 99
} AppErrorCode;

typedef enum {
    APP_ACTION_INSTALL = 0,
    APP_ACTION_UPGRADE = 1,
    APP_ACTION_DOWNGRADE = 2,
    APP_ACTION_REMOVE = 3
} AppAction;

typedef struct {
    char status[16];
    char error_code[16];
    char error_message[256];
    char version[64];
} AppResult;

typedef enum {
    APP_STATE_ACKNOWLEDGED = 0,
    APP_STATE_IN_PROGRESS = 1,
    APP_STATE_SUCCESS = 2,
    APP_STATE_FAILED = 3
} AppState;

/* Handles one application JSON request; returns 0 after dispatch, -1 on parse/lock errors. */
int app_handle_request(const char *json_payload);

/* MQTT publish helpers return 0 on success, -1 on failure. */
int app_publish_status(const char *client_id, AppState state, AppAction action);

int app_publish_result(const char *client_id, const AppResult *result);

#endif
