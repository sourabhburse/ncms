#ifndef APP_DEFAULTS_H
#define APP_DEFAULTS_H

#define NCMS_DEFAULT_CONFIG_DIR       "/etc/config"
#define NCMS_DEFAULT_STORAGE_DIR      "/etc/ncms/"
#define NCMS_DEFAULT_SYSUPGRADE_CONF  "/etc/sysupgrade.conf"
#define OTA_STATE_PATH                NCMS_DEFAULT_STORAGE_DIR "ota_state.json"
#define OTA_STATE_TMP_PATH            OTA_STATE_PATH ".tmp"
#define FW_ENV_CONFIG_PATH            "/etc/fw_env.config"
#define PROC_MTD                      "/proc/mtd"
#define UBOOT_ENV_LABEL               "u-boot-env"
#define NCMS_FW_PATH                  "/tmp/fw.bin"
#define NCMS_APP_IPK_PATH             "/tmp/app.ipk"
#define NCMS_APP_BUNDLE_ARCHIVE       "/tmp/app_bundle.tar.gz"
#define NCMS_APP_BUNDLE_DIR           "/tmp/app_bundle"

#define NCMS_MQTT_RETRY_SECONDS       10
#define NCMS_MQTT_RETRY_MAX_SECONDS   300
#define NCMS_DOWNLOAD_TIMEOUT_SECONDS 300
#define NCMS_CONNECT_TIMEOUT_SECONDS  10

#endif //APP_DEFAULTS_H
