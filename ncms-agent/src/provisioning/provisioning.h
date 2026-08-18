#ifndef PROVISIONING_H
#define PROVISIONING_H

typedef struct {
    const char *serial_number;
    const char *firmware_version;
    const char *agent_version;
    const char *hardware_model;
    const char *base_mac;
    const char *server_url;
} ProvisioningDeviceInfo;

int provisioning_register_device(const ProvisioningDeviceInfo *info, const char *csr_string,
                                 char **response_body);
int provisioning_save_response(const char *response_body);

#endif
