#ifndef NCMS_CONFIG_H
#define NCMS_CONFIG_H

#include <libubox/list.h>
#include <ucimap.h>

struct uci_ncms_device {
    struct ucimap_section_data map;
    struct list_head list;

    const char *serial_number;
    const char *firmware_version;
    const char *agent_version;
    const char *hardware_model;
    const char *base_mac;
    const char *server_url;
    int enabled;
};

/* Loads UCI-backed NCMS config; returns 0 on success, -1 on failure. */
int ncms_init_config(const char *config_path);

struct uci_ncms_device *ncms_get_config(void);

void ncms_cleanup_config(void);

/* Returns NULL when option_name is unknown. */
const char *ncms_get_value(const char *option_name);

#endif
