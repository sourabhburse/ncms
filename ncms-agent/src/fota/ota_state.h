#ifndef OTA_STATE_H
#define OTA_STATE_H

#include "ota_handler.h"

void ota_set_error(OtaErrorCode code, const char *message);
void ota_clear_error(void);
int ota_result_pending(void);
int ota_get_pending_result(OtaResult *result);
void ota_get_pending_version(char *out, size_t out_sz);
void ota_clear_result_flag(void);
int ota_state_setenv(const char *key, const char *value);

#endif
