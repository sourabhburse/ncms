#include "app_handler.h"

#include "download.h"
#include "hash.h"
#include "mqtt_client.h"
#include "opkg_install.h"
#include "update_lock.h"

#include <json-c/json.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>


#define APP_BUNDLE_FIND_IPKS "$(find " NCMS_APP_BUNDLE_DIR " -type f -name '*.ipk')"

#define APP_PATH             NCMS_APP_IPK_PATH

static int url_is_bundle(const char *url)
{
    char path_only[512];
    char *q;
    size_t len;

    if (!url) return 0;

    strncpy(path_only, url, sizeof(path_only) - 1);
    path_only[sizeof(path_only) - 1] = '\0';

    q = strpbrk(path_only, "?#");
    if (q) *q = '\0';

    len = strlen(path_only);

    if (len >= 7 && strcmp(path_only + len - 7, ".tar.gz") == 0) return 1;
    if (len >= 4 && strcmp(path_only + len - 4, ".tgz") == 0) return 1;

    return 0;
}

static const char *get_client_id(void)
{
    MqttConfig *config = mqtt_get_config();
    if (config && config->mqtt.client_id && config->mqtt.client_id[0] != '\0') {
        return config->mqtt.client_id;
    }
    return "unknown";
}

static const char *action_name(AppAction action)
{
    switch (action) {
    case APP_ACTION_INSTALL:
        return "install";
    case APP_ACTION_UPGRADE:
        return "upgrade";
    case APP_ACTION_DOWNGRADE:
        return "downgrade";
    case APP_ACTION_REMOVE:
        return "remove";
    default:
        return "unknown";
    }
}

static const char *action_verb_ing(AppAction action)
{
    switch (action) {
    case APP_ACTION_INSTALL:
        return "installing";
    case APP_ACTION_UPGRADE:
        return "upgrading";
    case APP_ACTION_DOWNGRADE:
        return "downgrading";
    case APP_ACTION_REMOVE:
        return "removing";
    default:
        return "processing";
    }
}

static int parse_action(const char *str, AppAction *out)
{
    if (!str || str[0] == '\0' || strcmp(str, "install") == 0) {
        *out = APP_ACTION_INSTALL;
        return 0;
    }
    if (strcmp(str, "upgrade") == 0) {
        *out = APP_ACTION_UPGRADE;
        return 0;
    }
    if (strcmp(str, "downgrade") == 0) {
        *out = APP_ACTION_DOWNGRADE;
        return 0;
    }
    if (strcmp(str, "remove") == 0) {
        *out = APP_ACTION_REMOVE;
        return 0;
    }
    return -1;
}

int app_publish_status(const char *client_id, AppState state, AppAction action)
{
    if (!client_id) return -1;

    const char *state_str;
    switch (state) {
    case APP_STATE_ACKNOWLEDGED:
        state_str = "acknowledged";
        break;
    case APP_STATE_IN_PROGRESS:
        state_str = action_verb_ing(action);
        break;
    case APP_STATE_SUCCESS:
        state_str = "success";
        break;
    case APP_STATE_FAILED:
        state_str = "failed";
        break;
    default:
        state_str = "unknown";
        break;
    }

    struct json_object *payload = json_object_new_object();
    json_object_object_add(payload, "status", json_object_new_string(state_str));
    json_object_object_add(payload, "action", json_object_new_string(action_name(action)));
    json_object_object_add(payload, "timestamp", json_object_new_int64((int64_t)time(NULL) * 1000));

    const char *payload_str = json_object_to_json_string(payload);
    printf("[APP] Status: %s (%s)\n", state_str, action_name(action));

    int ret = mqtt_publish(g_config->topics.application_response_publish, payload_str, 1, 0);
    json_object_put(payload);

    return ret;
}

int app_publish_result(const char *client_id, const AppResult *result)
{
    if (!client_id || !result) return -1;

    struct json_object *payload = json_object_new_object();
    json_object_object_add(payload, "status", json_object_new_string(result->status));

    if (strcmp(result->status, "failed") == 0) {
        json_object_object_add(payload, "error_code", json_object_new_string(result->error_code));
        json_object_object_add(payload, "error_message",
                               json_object_new_string(result->error_message));
    } else {
        json_object_object_add(payload, "version", json_object_new_string(result->version));
    }

    const char *payload_str = json_object_to_json_string(payload);
    printf("[APP] Publishing result to %s: %s\n", g_config->topics.application_response_publish, payload_str);

    int ret = mqtt_publish(g_config->topics.application_response_publish, payload_str, 1, 0);
    json_object_put(payload);

    return (ret == 0) ? 0 : -1;
}

typedef struct {
    AppAction action;
    char *package_url;
    long expected_size;
    char expected_md5[33];
    char expected_sha256[65];
    char package_name[64];
    char declared_version[64];

    char client_id[128];
} AppArgs;

static long get_file_size(const char *path)
{
    FILE *fp = fopen(path, "rb");
    if (!fp) return -1;

    fseek(fp, 0, SEEK_END);
    long size = ftell(fp);
    fclose(fp);
    return size;
}

static void set_failure(AppResult *result, AppErrorCode code, const char *msg)
{
    snprintf(result->error_code, sizeof(result->error_code), "%d", code);
    snprintf(result->error_message, sizeof(result->error_message), "%s", msg);
}

static void cleanup_app_temp_files(void)
{
    system("rm -f " NCMS_APP_IPK_PATH " " NCMS_APP_BUNDLE_ARCHIVE " 2>/dev/null");
    system("rm -rf " NCMS_APP_BUNDLE_DIR " 2>/dev/null");
}

static void *app_thread(void *arg)
{
    AppArgs *args = (AppArgs *)arg;
    AppResult result;
    char opkg_diag[200];
    int is_bundle = args->package_url ? url_is_bundle(args->package_url) : 0;
    const char *download_path = is_bundle ? NCMS_APP_BUNDLE_ARCHIVE : NCMS_APP_IPK_PATH;

    memset(&result, 0, sizeof(result));
    strncpy(result.status, "failed", sizeof(result.status) - 1);

    printf("\n========================================\n");
    printf("APPLICATION %s STARTED\n", action_name(args->action));
    printf("========================================\n");
    printf("Client ID    : %s\n", args->client_id);
    printf("Action       : %s\n", action_name(args->action));
    if (args->package_name[0]) printf("Package name : %s\n", args->package_name);
    if (args->package_url) {
        printf("URL    : %s%s\n", args->package_url, is_bundle ? "  (dependency bundle)" : "");
        printf("Size   : %ld bytes\n", args->expected_size);
        printf("MD5    : %s\n", args->expected_md5);
        printf("SHA256 : %s\n", args->expected_sha256);
    }
    printf("----------------------------------------\n");

    app_publish_status(args->client_id, APP_STATE_ACKNOWLEDGED, args->action);

    if (args->action == APP_ACTION_REMOVE) {
        app_publish_status(args->client_id, APP_STATE_IN_PROGRESS, args->action);

        printf("[APP] Step 1/3 : Checking package is installed...\n");

        if (package_is_installed(args->package_name) != 1) {
            char msg[128];
            snprintf(msg, sizeof(msg), "Package '%s' is not installed", args->package_name);
            printf("[APP] FAILED: %s\n", msg);
            set_failure(&result, APP_STATUS_NOT_INSTALLED, msg);
            goto app_fail;
        }

        printf("[APP] Step 2/3 : Validating removal...\n");
        memset(opkg_diag, 0, sizeof(opkg_diag));

        if (validate_remove(args->package_name, opkg_diag, sizeof(opkg_diag)) != 0) {
            printf("[APP] FAILED: removal validation failed\n");
            set_failure(&result, APP_STATUS_VALIDATION_FAILED,
                        opkg_diag[0] ? opkg_diag : "Removal validation failed");
            goto app_fail;
        }

        printf("[APP] Step 3/3 : Removing package...\n");
        memset(opkg_diag, 0, sizeof(opkg_diag));

        if (remove_package(args->package_name, opkg_diag, sizeof(opkg_diag)) != 0) {
            printf("[APP] FAILED: package remove failed\n");
            set_failure(&result, APP_STATUS_REMOVE_FAILED,
                        opkg_diag[0] ? opkg_diag : "Package remove failed");
            goto app_fail;
        }

        printf("\n========================================\n");
        printf("APPLICATION REMOVE COMPLETED\n");
        printf("========================================\n");

        strncpy(result.status, "success", sizeof(result.status) - 1);
        strncpy(result.version, args->package_name, sizeof(result.version) - 1);
        app_publish_result(args->client_id, &result);
        app_publish_status(args->client_id, APP_STATE_SUCCESS, args->action);

        goto app_done;
    }

    if (args->action == APP_ACTION_UPGRADE || args->action == APP_ACTION_DOWNGRADE) {
        if (args->package_name[0] == '\0' || args->declared_version[0] == '\0') {
            printf("[APP] FAILED: package_name and version required for %s\n",
                   action_name(args->action));
            set_failure(&result, APP_STATUS_MISSING_FIELDS,
                        "package_name and version are required for upgrade/downgrade");
            goto app_fail;
        }

        printf("[APP] Step 0a : Checking package is currently installed...\n");

        if (package_is_installed(args->package_name) != 1) {
            char msg[192];
            snprintf(msg, sizeof(msg),
                     "Package '%s' is not installed - %s requires an "
                     "existing installation",
                     args->package_name, action_name(args->action));
            printf("[APP] FAILED: %s\n", msg);
            set_failure(&result, APP_STATUS_NOT_INSTALLED, msg);
            goto app_fail;
        }

        printf("[APP] Step 0b : Verifying version direction...\n");

        char installed_version[64] = {0};

        if (get_installed_version(args->package_name, installed_version,
                                  sizeof(installed_version)) != 0) {
            printf("[APP] FAILED: could not read installed version for '%s'\n", args->package_name);
            set_failure(&result, APP_STATUS_UNKNOWN_ERROR,
                        "Could not read installed package version");
            goto app_fail;
        }

        printf("[APP] Installed version : %s\n", installed_version);
        printf("[APP] Declared version  : %s\n", args->declared_version);

        const char *op = (args->action == APP_ACTION_UPGRADE) ? ">>" : "<<";
        int direction_ok = opkg_compare_versions(args->declared_version, op, installed_version);

        if (direction_ok != 1) {
            char msg[220];
            snprintf(msg, sizeof(msg),
                     "action=%s but declared version %s is not %s installed "
                     "version %s",
                     action_name(args->action), args->declared_version,
                     args->action == APP_ACTION_UPGRADE ? "newer than" : "older than",
                     installed_version);
            printf("[APP] FAILED: %s\n", msg);
            set_failure(&result, APP_STATUS_VERSION_MISMATCH, msg);
            goto app_fail;
        }

        printf("[APP] Version direction check PASSED\n");
    }

    printf("[APP] Step 1/5 : Downloading %s...\n", is_bundle ? "bundle" : "package");
    app_publish_status(args->client_id, APP_STATE_IN_PROGRESS, args->action);

    if (download_file(args->package_url, download_path) != 0) {
        printf("[APP] FAILED: download failed\n");
        set_failure(&result, APP_STATUS_DOWNLOAD_FAILED, "Download failed");
        goto app_fail;
    }

    printf("[APP] Download complete.\n");

    printf("[APP] Step 2/5 : Checking file size...\n");

    long actual_size = get_file_size(download_path);

    if (actual_size < 0) {
        printf("[APP] FAILED: cannot read downloaded file\n");
        set_failure(&result, APP_STATUS_UNKNOWN_ERROR, "Cannot read downloaded file");
        goto app_fail;
    }

    printf("[APP] Expected size : %ld bytes\n", args->expected_size);
    printf("[APP] Actual size   : %ld bytes\n", actual_size);

    if (actual_size != args->expected_size) {
        char msg[256];
        snprintf(msg, sizeof(msg), "Size mismatch: expected %ld, got %ld", args->expected_size,
                 actual_size);
        printf("[APP] FAILED: %s\n", msg);
        set_failure(&result, APP_STATUS_SIZE_MISMATCH, msg);
        goto app_fail;
    }

    printf("[APP] Size check PASSED\n");

    printf("[APP] Step 3/5 : Verifying MD5...\n");

    char computed_md5[33];

    if (compute_md5_of_file(download_path, computed_md5) != 0) {
        printf("[APP] FAILED: could not compute MD5\n");
        set_failure(&result, APP_STATUS_UNKNOWN_ERROR, "Could not compute MD5");
        goto app_fail;
    }

    printf("[APP] Expected MD5 : %s\n", args->expected_md5);
    printf("[APP] Computed MD5 : %s\n", computed_md5);

    if (strncmp(computed_md5, args->expected_md5, 32) != 0) {
        char msg[256];
        snprintf(msg, sizeof(msg), "MD5 mismatch: expected %s, got %s", args->expected_md5,
                 computed_md5);
        printf("[APP] FAILED: %s\n", msg);
        set_failure(&result, APP_STATUS_MD5_MISMATCH, msg);
        goto app_fail;
    }

    printf("[APP] MD5 VERIFIED SUCCESSFULLY\n");

    printf("[APP] Step 4/5 : Verifying SHA256...\n");

    if (verify_sha256(download_path, args->expected_sha256) != 0) {
        printf("[APP] FAILED: SHA256 verification failed\n");
        set_failure(&result, APP_STATUS_SHA256_MISMATCH, "SHA256 verification failed");
        goto app_fail;
    }

    printf("[APP] SHA256 VERIFIED SUCCESSFULLY\n");

    const char *op_target = NCMS_APP_IPK_PATH;

    if (is_bundle) {
        char cmd[300];

        printf("[APP] Extracting dependency bundle...\n");

        snprintf(cmd, sizeof(cmd), "rm -rf %s && mkdir -p %s", NCMS_APP_BUNDLE_DIR, NCMS_APP_BUNDLE_DIR);
        system(cmd);

        snprintf(cmd, sizeof(cmd), "tar -xzf %s -C %s 2>&1", download_path, NCMS_APP_BUNDLE_DIR);

        if (system(cmd) != 0) {
            printf("[APP] FAILED: bundle extraction failed\n");
            set_failure(&result, APP_STATUS_UNKNOWN_ERROR, "Bundle extraction (tar -xzf) failed");
            goto app_fail;
        }

        snprintf(cmd, sizeof(cmd), "find %s -type f -name '*.ipk' | grep -q .", NCMS_APP_BUNDLE_DIR);

        if (system(cmd) != 0) {
            printf("[APP] FAILED: bundle contained no .ipk files\n");
            set_failure(&result, APP_STATUS_UNKNOWN_ERROR,
                        "Bundle extracted but contained no .ipk files");
            goto app_fail;
        }

        printf("[APP] Bundle extracted successfully\n");
        op_target = APP_BUNDLE_FIND_IPKS;
    }

    printf("[APP] Step 5/5 : Validating %s...\n", is_bundle ? "bundle" : "package");

    memset(opkg_diag, 0, sizeof(opkg_diag));
    int validate_ret;

    if (args->action == APP_ACTION_DOWNGRADE)
        validate_ret = validate_downgrade(op_target, opkg_diag, sizeof(opkg_diag));
    else
        validate_ret = validate_package(op_target, opkg_diag, sizeof(opkg_diag));

    if (validate_ret != 0) {
        if (validate_ret == -2) {
            printf("[APP] FAILED: missing dependency\n");
            char msg[220];
            snprintf(msg, sizeof(msg), "Missing dependency: %s",
                     opkg_diag[0] ? opkg_diag : "unresolved dependency");
            set_failure(&result, APP_STATUS_DEPENDENCY_MISSING, msg);
        } else {
            printf("[APP] FAILED: package validation failed\n");
            set_failure(&result, APP_STATUS_VALIDATION_FAILED,
                        opkg_diag[0] ? opkg_diag : "Package validation failed");
        }
        goto app_fail;
    }

    printf("[APP] Validation PASSED. %s...\n", args->action == APP_ACTION_DOWNGRADE ? "Downgrading"
                                               : args->action == APP_ACTION_UPGRADE ? "Upgrading"
                                                                                    : "Installing");

    memset(opkg_diag, 0, sizeof(opkg_diag));
    const char *pkg_name_or_null = args->package_name[0] ? args->package_name : NULL;

    int commit_ret;
    if (args->action == APP_ACTION_DOWNGRADE)
        commit_ret = downgrade_package(op_target, pkg_name_or_null, opkg_diag, sizeof(opkg_diag));
    else
        commit_ret = install_package(op_target, pkg_name_or_null, opkg_diag, sizeof(opkg_diag));

    if (commit_ret != 0) {
        printf("[APP] FAILED: %s failed\n", action_name(args->action));
        set_failure(&result, APP_STATUS_INSTALL_FAILED,
                    opkg_diag[0] ? opkg_diag : "Package operation failed");
        goto app_fail;
    }

    printf("\n========================================\n");
    printf("APPLICATION %s COMPLETED\n", action_name(args->action));
    printf("========================================\n");

    strncpy(result.status, "success", sizeof(result.status) - 1);
    strncpy(result.version, args->package_name[0] ? args->package_name : args->expected_sha256,
            sizeof(result.version) - 1);
    app_publish_result(args->client_id, &result);
    app_publish_status(args->client_id, APP_STATE_SUCCESS, args->action);

    goto app_done;

app_fail:
    printf("\n========================================\n");
    printf("APPLICATION %s FAILED — device unchanged\n", action_name(args->action));
    printf("========================================\n");

    app_publish_result(args->client_id, &result);
    app_publish_status(args->client_id, APP_STATE_FAILED, args->action);

app_done:
    cleanup_app_temp_files();

    free(args->package_url);
    free(args);

    update_lock_release();

    return NULL;
}

static int app_handle_version_query(struct json_object *parsed)
{
    struct json_object *name_obj = NULL;
    json_object_object_get_ex(parsed, "package_name", &name_obj);

    if (!name_obj || json_object_get_string(name_obj)[0] == '\0') {
        printf("[APP] Version query FAILED: package_name required\n");
        json_object_put(parsed);
        return -1;
    }

    char package_name[64];
    strncpy(package_name, json_object_get_string(name_obj), sizeof(package_name) - 1);
    package_name[sizeof(package_name) - 1] = '\0';

    json_object_put(parsed);

    int installed = package_is_installed(package_name);
    char installed_version[64] = {0};
    int have_version = 0;

    if (installed == 1) {
        have_version = (get_installed_version(package_name, installed_version,
                                              sizeof(installed_version)) == 0);
    }

    const char *client_id = get_client_id();
    char topic[256];
    snprintf(topic, sizeof(topic), "d/%s/application/version", client_id);

    struct json_object *payload = json_object_new_object();
    json_object_object_add(payload, "package_name", json_object_new_string(package_name));
    json_object_object_add(payload, "installed", json_object_new_boolean(installed == 1));
    if (have_version)
        json_object_object_add(payload, "version", json_object_new_string(installed_version));

    const char *payload_str = json_object_to_json_string(payload);
    printf("[APP] Version query result for '%s': %s\n", package_name, payload_str);

    int ret = mqtt_publish(topic, payload_str, 1, 0);
    json_object_put(payload);

    return (ret == 0) ? 0 : -1;
}

int app_handle_request(const char *json_payload)
{
    struct json_object *parsed = json_tokener_parse(json_payload);

    if (!parsed) {
        printf("[APP] FAILED: invalid JSON payload\n");
        return -1;
    }

    struct json_object *action_peek = NULL;
    json_object_object_get_ex(parsed, "action", &action_peek);
    const char *action_str = action_peek ? json_object_get_string(action_peek) : NULL;

    if (action_str && strcmp(action_str, "query_version") == 0)
        return app_handle_version_query(parsed);

    if (update_lock_try_acquire("application") != 0) {
        printf("[APP] Request ignored: an update is already in progress\n");
        json_object_put(parsed);
        return -1;
    }

    AppAction action;
    if (parse_action(action_str, &action) != 0) {
        printf("[APP] FAILED: unknown action '%s'\n", action_str ? action_str : "(null)");
        json_object_put(parsed);
        goto parse_fail;
    }

    struct json_object *name_obj = NULL;
    json_object_object_get_ex(parsed, "package_name", &name_obj);

    if (action == APP_ACTION_REMOVE) {
        if (!name_obj || json_object_get_string(name_obj)[0] == '\0') {
            printf("[APP] FAILED: 'remove' requires package_name\n");
            json_object_put(parsed);
            goto parse_fail;
        }

        AppArgs *args = calloc(1, sizeof(AppArgs));
        if (!args) {
            printf("[APP] FAILED: out of memory\n");
            json_object_put(parsed);
            goto parse_fail;
        }

        args->action = APP_ACTION_REMOVE;
        strncpy(args->package_name, json_object_get_string(name_obj),
                sizeof(args->package_name) - 1);
        strncpy(args->client_id, get_client_id(), sizeof(args->client_id) - 1);

        json_object_put(parsed);

        pthread_t tid;
        pthread_attr_t attr;
        pthread_attr_init(&attr);
        pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);

        if (pthread_create(&tid, &attr, app_thread, args) != 0) {
            printf("[APP] FAILED: could not spawn thread\n");
            pthread_attr_destroy(&attr);
            free(args);
            goto parse_fail;
        }

        pthread_attr_destroy(&attr);
        printf("[APP] Remove thread spawned successfully\n");
        return 0;
    }

    struct json_object *url_obj, *size_obj, *md5_obj, *sha256_obj, *version_obj = NULL;

    json_object_object_get_ex(parsed, "package_url", &url_obj);
    json_object_object_get_ex(parsed, "size", &size_obj);
    json_object_object_get_ex(parsed, "md5", &md5_obj);
    json_object_object_get_ex(parsed, "sha256", &sha256_obj);
    json_object_object_get_ex(parsed, "version", &version_obj);

    if (!url_obj || !size_obj || !md5_obj || !sha256_obj) {
        printf("[APP] FAILED: missing required field(s) "
               "(package_url / size / md5 / sha256)\n");
        json_object_put(parsed);
        goto parse_fail;
    }

    AppArgs *args = calloc(1, sizeof(AppArgs));

    if (!args) {
        printf("[APP] FAILED: out of memory\n");
        json_object_put(parsed);
        goto parse_fail;
    }

    args->action = action;
    args->package_url = strdup(json_object_get_string(url_obj));
    args->expected_size = (long)json_object_get_int64(size_obj);

    strncpy(args->expected_md5, json_object_get_string(md5_obj), sizeof(args->expected_md5) - 1);

    strncpy(args->expected_sha256, json_object_get_string(sha256_obj),
            sizeof(args->expected_sha256) - 1);

    if (name_obj)
        strncpy(args->package_name, json_object_get_string(name_obj),
                sizeof(args->package_name) - 1);

    if (version_obj)
        strncpy(args->declared_version, json_object_get_string(version_obj),
                sizeof(args->declared_version) - 1);

    strncpy(args->client_id, get_client_id(), sizeof(args->client_id) - 1);

    json_object_put(parsed);

    pthread_t tid;
    pthread_attr_t attr;

    pthread_attr_init(&attr);
    pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);

    if (pthread_create(&tid, &attr, app_thread, args) != 0) {
        printf("[APP] FAILED: could not spawn thread\n");
        pthread_attr_destroy(&attr);
        free(args->package_url);
        free(args);
        goto parse_fail;
    }

    pthread_attr_destroy(&attr);

    printf("[APP] %s thread spawned successfully\n", action_name(action));
    return 0;

parse_fail:
    update_lock_release();
    return -1;
}
