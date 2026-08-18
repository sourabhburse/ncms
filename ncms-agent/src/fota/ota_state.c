#include "ota_state.h"

#include <errno.h>
#include <fcntl.h>
#include <json-c/json.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#include "app_defaults.h"

typedef struct {
    int  pending;
    char status[16];
    char error_code[16];
    char error_message[256];
    char version[64];
} OtaStateFile;

/* Reads OTA_STATE_PATH into *st. Returns 0 on success, -1 if the file
 * doesn't exist / can't be parsed — *st is zeroed either way, so callers
 * always get a well-defined (empty/not-pending) state on failure. */
static int load_state(OtaStateFile *st)
{
    memset(st, 0, sizeof(*st));

    FILE *fp = fopen(OTA_STATE_PATH, "r");
    if (!fp) return -1;

    char buf[1024];
    size_t n = fread(buf, 1, sizeof(buf) - 1, fp);
    fclose(fp);
    buf[n] = '\0';

    struct json_object *root = json_tokener_parse(buf);
    if (!root) return -1;

    struct json_object *field;

    if (json_object_object_get_ex(root, "pending", &field))
        st->pending = json_object_get_boolean(field) ? 1 : 0;

    if (json_object_object_get_ex(root, "status", &field))
        strncpy(st->status, json_object_get_string(field), sizeof(st->status) - 1);

    if (json_object_object_get_ex(root, "error_code", &field))
        strncpy(st->error_code, json_object_get_string(field), sizeof(st->error_code) - 1);

    if (json_object_object_get_ex(root, "error_message", &field))
        strncpy(st->error_message, json_object_get_string(field), sizeof(st->error_message) - 1);

    if (json_object_object_get_ex(root, "version", &field))
        strncpy(st->version, json_object_get_string(field), sizeof(st->version) - 1);

    json_object_put(root);
    return 0;
}

/* Writes *st to OTA_STATE_PATH atomically: write to a temp file, fsync
 * it, rename() over the real path (POSIX rename is atomic on the same
 * filesystem), then fsync the containing directory so the rename
 * itself survives a power loss, not just the file's contents. */
static int save_state_atomic(const OtaStateFile *st)
{
    struct json_object *root = json_object_new_object();
    json_object_object_add(root, "pending", json_object_new_boolean(st->pending));
    json_object_object_add(root, "status", json_object_new_string(st->status));
    json_object_object_add(root, "error_code", json_object_new_string(st->error_code));
    json_object_object_add(root, "error_message", json_object_new_string(st->error_message));
    json_object_object_add(root, "version", json_object_new_string(st->version));

    const char *json_str = json_object_to_json_string(root);

    FILE *fp = fopen(OTA_STATE_TMP_PATH, "w");
    if (!fp) {
        fprintf(stderr, "[OTA] Failed to open %s: %s\n", OTA_STATE_TMP_PATH, strerror(errno));
        json_object_put(root);
        return -1;
    }

    int write_ok = (fputs(json_str, fp) != EOF);
    fflush(fp);
    fsync(fileno(fp));
    fclose(fp);
    json_object_put(root);

    if (!write_ok) {
        fprintf(stderr, "[OTA] Failed to write %s\n", OTA_STATE_TMP_PATH);
        return -1;
    }

    if (rename(OTA_STATE_TMP_PATH, OTA_STATE_PATH) != 0) {
        fprintf(stderr, "[OTA] Failed to rename %s -> %s: %s\n",
                OTA_STATE_TMP_PATH, OTA_STATE_PATH, strerror(errno));
        return -1;
    }

    int dfd = open(NCMS_DEFAULT_STORAGE_DIR, O_RDONLY);
    if (dfd >= 0) {
        fsync(dfd);
        close(dfd);
    }

    return 0;
}

int ota_state_setenv(const char *key, const char *value)
{
    OtaStateFile st;
    load_state(&st); /* ignore failure — first-ever write starts from a zeroed state */

    if (strcmp(key, "ota_pending") == 0) {
        st.pending = (strcmp(value, "1") == 0) ? 1 : 0;
    } else if (strcmp(key, "ota_status") == 0) {
        strncpy(st.status, value, sizeof(st.status) - 1);
        st.status[sizeof(st.status) - 1] = '\0';
    } else if (strcmp(key, "ota_error_code") == 0) {
        strncpy(st.error_code, value, sizeof(st.error_code) - 1);
        st.error_code[sizeof(st.error_code) - 1] = '\0';
    } else if (strcmp(key, "ota_error_message") == 0) {
        strncpy(st.error_message, value, sizeof(st.error_message) - 1);
        st.error_message[sizeof(st.error_message) - 1] = '\0';
    } else if (strcmp(key, "ota_version") == 0) {
        strncpy(st.version, value, sizeof(st.version) - 1);
        st.version[sizeof(st.version) - 1] = '\0';
    } else {
        fprintf(stderr, "[OTA] ota_state_setenv: unknown key '%s'\n", key);
        return -1;
    }

    if (save_state_atomic(&st) != 0) {
        printf("[OTA] Warning: failed to persist OTA state (%s)\n", OTA_STATE_PATH);
        return -1;
    }

    return 0;
}

void ota_set_error(OtaErrorCode code, const char *message)
{
    char error_code_str[16];
    char escaped_message[256];

    snprintf(error_code_str, sizeof(error_code_str), "%d", code);

    strncpy(escaped_message, message, sizeof(escaped_message) - 1);
    escaped_message[sizeof(escaped_message) - 1] = '\0';

    ota_state_setenv("ota_status", "failed");
    ota_state_setenv("ota_error_code", error_code_str);
    ota_state_setenv("ota_error_message", escaped_message);
}

void ota_clear_error(void)
{
    ota_state_setenv("ota_status", "success");
    ota_state_setenv("ota_error_code", "");
    ota_state_setenv("ota_error_message", "");
}

int ota_result_pending(void)
{
    OtaStateFile st;
    if (load_state(&st) != 0) return 0;
    return st.pending ? 1 : 0;
}

int ota_get_pending_result(OtaResult *result)
{
    if (!result) return -1;

    memset(result, 0, sizeof(OtaResult));

    OtaStateFile st;
    if (load_state(&st) != 0 || !st.pending) return -1;

    strncpy(result->status, st.status[0] ? st.status : "success", sizeof(result->status) - 1);

    if (strcmp(result->status, "failed") == 0) {
        strncpy(result->error_code, st.error_code[0] ? st.error_code : "99",
                sizeof(result->error_code) - 1);
        strncpy(result->error_message, st.error_message[0] ? st.error_message : "Unknown error",
                sizeof(result->error_message) - 1);
    }

    strncpy(result->version, st.version[0] ? st.version : "unknown", sizeof(result->version) - 1);

    return 0;
}

void ota_get_pending_version(char *out, size_t out_sz)
{
    OtaStateFile st;

    if (load_state(&st) == 0 && st.version[0]) {
        strncpy(out, st.version, out_sz - 1);
    } else {
        strncpy(out, "unknown", out_sz - 1);
    }
    out[out_sz - 1] = '\0';
}

void ota_clear_result_flag(void)
{
    OtaStateFile st;
    memset(&st, 0, sizeof(st)); /* pending=0, all fields empty */

    save_state_atomic(&st);
    printf("[OTA] OTA state file cleared.\n");
}