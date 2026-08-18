#include "heartbeat.h"
#include "key_csr.h"
#include "mqtt_client.h"
#include "ncms-config.h"
#include "persistent_storage.h"
#include "provisioning.h"
#include "telemetry.h"

#include <argparse.h>
#include <json-c/json.h>
#include <openssl/err.h>
#include <openssl/evp.h>
#include <openssl/x509.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static volatile int running = 1;

static char *g_serial_number = NULL;
static char *g_firmware_version = NULL;
static char *g_agent_version = NULL;
static char *g_hardware_model = NULL;
static char *g_base_mac = NULL;
static char *g_server_url = NULL;

const char *config_path = NULL;

#define XNET_BOARD_INFO_PATH "/var/xnet_board_info.json"

static char *load_serial_number_from_board_info(void)
{
    struct json_object *root = json_object_from_file(XNET_BOARD_INFO_PATH);
    struct json_object *serial_number_obj = NULL;
    const char *serial_number;

    if (!root) {
        fprintf(stderr, "Could not read %s for serial number\n", XNET_BOARD_INFO_PATH);
        return NULL;
    }

    if (!json_object_is_type(root, json_type_object) ||
        !json_object_object_get_ex(root, "serial_number", &serial_number_obj) ||
        !json_object_is_type(serial_number_obj, json_type_string)) {
        fprintf(stderr, "No serial_number found in %s\n", XNET_BOARD_INFO_PATH);
        json_object_put(root);
        return NULL;
    }

    serial_number = json_object_get_string(serial_number_obj);
    if (!serial_number || serial_number[0] == '\0') {
        fprintf(stderr, "Empty serial_number in %s\n", XNET_BOARD_INFO_PATH);
        json_object_put(root);
        return NULL;
    }

    char *result = strdup(serial_number);
    json_object_put(root);
    return result;
}

void signal_handler(int signum)
{
    printf("\nReceived signal %d, shutting down...\n", signum);
    running = 0;
    mqtt_stop();
}

static int initialize_config(const char *config_path)
{
    printf("\n=== CONFIG INITIALIZATION ===\n");

    printf("config_path: %s\n", config_path);
    const char *uci_config_path = config_path ? config_path : NCMS_DEFAULT_CONFIG_DIR;

    if (ncms_init_config(uci_config_path) != 0) {
        fprintf(stderr, "Failed to initialize NCMS configuration\n");
        return -1;
    }

    struct uci_ncms_device *config = ncms_get_config();
    if (!config) {
        fprintf(stderr, "No NCMS device configuration found\n");
        return -1;
    }

    if (config->serial_number && config->serial_number[0] != '\0') {
        g_serial_number = strdup(config->serial_number);
    } else {
        g_serial_number = load_serial_number_from_board_info();
        if (g_serial_number)
            printf("Using serial number from %s\n", XNET_BOARD_INFO_PATH);
        else
            g_serial_number = strdup("SN-200");
    }
    g_firmware_version = strdup(config->firmware_version ? config->firmware_version : "1.0.0");
    g_agent_version = strdup(config->agent_version ? config->agent_version : "1.0.0");
    g_hardware_model = strdup(config->hardware_model ? config->hardware_model : "XE-33");
    g_base_mac = strdup(config->base_mac ? config->base_mac : "AA:BB:CC:DD:EE:01");
    g_server_url = strdup(config->server_url ? config->server_url
                                             : "http://82.180.146.203:5080/api/v1/provision/");

    printf("Configuration loaded successfully:\n");
    printf("  Serial Number: %s\n", g_serial_number);
    printf("  Hardware Model: %s\n", g_hardware_model);
    printf("  Firmware Version: %s\n", g_firmware_version);
    printf("  Agent Version: %s\n", g_agent_version);
    printf("  Base MAC: %s\n", g_base_mac);
    printf("  Server URL: %s\n", g_server_url);

    printf("Config handler initialized successfully\n");
    return 0;
}

static void cleanup_config(void)
{
    free(g_serial_number);
    free(g_firmware_version);
    free(g_agent_version);
    free(g_hardware_model);
    free(g_base_mac);
    free(g_server_url);

    g_serial_number = NULL;
    g_firmware_version = NULL;
    g_agent_version = NULL;
    g_hardware_model = NULL;
    g_base_mac = NULL;
    g_server_url = NULL;

    ncms_cleanup_config();
}

int initialize_cli_args(int argc, const char *argv[])
{
    struct argparse_option options[] = {
        OPT_HELP(), OPT_STRING('p', "path", &config_path, "Path to configuration file"), OPT_END()};
    struct argparse argparse;
    static const char *const usage[] = {
        "NCMS [options]",
        NULL,
    };
    argparse_describe(&argparse, "\nxNet MODBUS MQTT Client.", "");
    argparse_init(&argparse, options, usage, 0);
    argparse_parse(&argparse, argc, argv);
    if (config_path != NULL) printf("path: %s\n", config_path);
}

int main(int argc, char *argv[])
{
    EVP_PKEY *pkey = NULL;
    X509_REQ *req = NULL;
    char *response_body = NULL;

    int opt;
    initialize_cli_args(argc, argv);

    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);

    OpenSSL_add_all_algorithms();
    ERR_load_crypto_strings();

    printf("========================================\n");
    printf("NCMS Provisioning & MQTT Client\n");
    printf("========================================\n");

    if (initialize_config(config_path) != 0) {
        fprintf(stderr, "Failed to initialize configuration\n");
        return 1;
    }

    printf("Serial Number: %s\n", g_serial_number);
    printf("Hardware Model: %s\n", g_hardware_model);
    printf("Firmware: %s\n\n", g_firmware_version);

    if (init_persistent_storage() != 0) {
        fprintf(stderr, "Warning: Persistent storage initialization failed\n");
    }

    if (access(CERT_PATH, F_OK) == 0 && access(KEY_PATH, F_OK) == 0 &&
        access(CONFIG_PATH, F_OK) == 0) {

        printf("Device already provisioned.\n");
        printf("Loading existing configuration and starting MQTT...\n");

        if (mqtt_init_from_config(CONFIG_PATH) == 0) {
            printf("\nStarting MQTT client...\n");
            if (heartbeat_start() != 0) fprintf(stderr, "Failed to start heartbeat service\n");
            if (telemetry_start() != 0) fprintf(stderr, "Failed to start telemetry service\n");
            mqtt_run();
            telemetry_stop();
            heartbeat_stop();
        } else {
            fprintf(stderr, "Failed to load MQTT configuration\n");
        }

        mqtt_cleanup();
        cleanup_config();
        return 0;
    }

    printf("=== PROVISIONING PHASE ===\n");

    printf("1. Generating ECC private key (prime256v1)...\n");
    pkey = generate_ecc_private_key();
    if (!pkey) {
        fprintf(stderr, "Failed to generate private key\n");
        cleanup_config();
        return 1;
    }
    printf("ECC private key generated\n");

    if (save_private_key(pkey, KEY_PATH) != 0) {
        fprintf(stderr, "Failed to save private key\n");
        EVP_PKEY_free(pkey);
        cleanup_config();
        return 1;
    }
    printf("Private key saved to %s\n", KEY_PATH);

    printf("2. Generating CSR...\n");
    req = generate_csr(pkey, g_serial_number);
    if (!req) {
        fprintf(stderr, "Failed to generate CSR\n");
        EVP_PKEY_free(pkey);
        cleanup_config();
        return 1;
    }
    printf("CSR generated\n");

    if (save_csr(req, CSR_PATH) != 0) {
        fprintf(stderr, "Failed to save CSR\n");
        X509_REQ_free(req);
        EVP_PKEY_free(pkey);
        cleanup_config();
        return 1;
    }
    printf("CSR saved to %s\n", CSR_PATH);

    char *csr_string = get_csr_string(CSR_PATH);
    if (!csr_string) {
        fprintf(stderr, "Failed to read CSR\n");
        X509_REQ_free(req);
        EVP_PKEY_free(pkey);
        cleanup_config();
        return 1;
    }

    printf("\n3. Registering with provisioning server...\n");
    ProvisioningDeviceInfo provisioning_info = {
        .serial_number = g_serial_number,
        .firmware_version = g_firmware_version,
        .agent_version = g_agent_version,
        .hardware_model = g_hardware_model,
        .base_mac = g_base_mac,
        .server_url = g_server_url,
    };
    int result = provisioning_register_device(&provisioning_info, csr_string, &response_body);
    free(csr_string);

    if (result != 0 || !response_body) {
        fprintf(stderr, "Registration failed\n");
        X509_REQ_free(req);
        EVP_PKEY_free(pkey);
        cleanup_config();
        return 1;
    }

    printf("\n4. Saving certificates and configuration...\n");
    provisioning_save_response(response_body);
    free(response_body);

    printf("\nProvisioning completed successfully!\n");

    X509_REQ_free(req);
    EVP_PKEY_free(pkey);

    printf("\n=== MQTT PHASE ===\n");
    printf("Starting MQTT client with provisioned credentials...\n");

    if (mqtt_init_from_config(CONFIG_PATH) != 0) {
        fprintf(stderr, "Failed to initialize MQTT client\n");
        cleanup_config();
        return 1;
    }

    MqttConfig *config = mqtt_get_config();
    if (config) {
        printf("\nMQTT Configuration:\n");
        printf("  Broker: %s:%d\n", config->mqtt.broker_url, config->mqtt.broker_port);
        printf("  Client ID: %s\n", config->mqtt.client_id);
        printf("  Telemetry interval: %d seconds\n", config->telemetry_interval_seconds);
        printf("  Heartbeat interval: %d seconds\n", config->heartbeat_interval_seconds);
        if (config->topics.config_subscribe) {
            printf("  Config topic: %s\n", config->topics.config_subscribe);
        }
        if (config->topics.command_subscribe) {
            printf("  Command topic: %s\n", config->topics.command_subscribe);
        }
        if (config->topics.ota_subscribe) {
            printf("  OTA topic: %s\n", config->topics.ota_subscribe);
        }
        if (config->topics.application_subscribe) {
            printf("  Application topic: %s\n", config->topics.application_subscribe);
        }
        if (config->topics.application_response_publish) {
            printf("  Application Response topic: %s\n", config->topics.application_subscribe);
        }
        if (config->topics.ota_response_publish) {
            printf("  OTA Response topic: %s\n", config->topics.ota_response_publish);
        }
        if (config->topics.command_response_publish) {
            printf("  Response topic: %s\n", config->topics.command_response_publish);
        }
    }

    if (heartbeat_start() != 0) fprintf(stderr, "Failed to start heartbeat service\n");
    if (telemetry_start() != 0) fprintf(stderr, "Failed to start telemetry service\n");

    mqtt_run();

    telemetry_stop();
    heartbeat_stop();

    mqtt_cleanup();
    cleanup_config();

    printf("\nProgram terminated.\n");
    return 0;
}
