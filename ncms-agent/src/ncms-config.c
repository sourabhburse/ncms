#include "ncms-config.h"

#include <libubox/list.h>
#include <stdlib.h>
#include <string.h>
#include <uci.h>
#include <ucimap.h>

static struct list_head lh_ncms_device;
static struct uci_ncms_device *ncms_device = NULL;

static int ncms_device_init(struct uci_map *map, void *section, struct uci_section *s)
{
    struct uci_ncms_device *dev = section;
    INIT_LIST_HEAD(&dev->list);
    return 0;
}

static int ncms_device_add(struct uci_map *map, void *section)
{
    struct uci_ncms_device *dev = section;
    list_add_tail(&dev->list, &lh_ncms_device);
    ncms_device = dev;
    return 0;
}

static struct ucimap_section_data *
ncms_device_allocate(struct uci_map *map, struct uci_sectionmap *sm, struct uci_section *s)
{
    struct uci_ncms_device *p = malloc(sizeof(struct uci_ncms_device));
    memset(p, 0, sizeof(struct uci_ncms_device));
    return &p->map;
}

static struct uci_optmap ncms_device_options[] = {
    {UCIMAP_OPTION(struct uci_ncms_device, serial_number), .type = UCIMAP_STRING,
     .name = "serial_number"},
    {UCIMAP_OPTION(struct uci_ncms_device, firmware_version), .type = UCIMAP_STRING,
     .name = "firmware_version"},
    {UCIMAP_OPTION(struct uci_ncms_device, agent_version), .type = UCIMAP_STRING,
     .name = "agent_version"},
    {UCIMAP_OPTION(struct uci_ncms_device, hardware_model), .type = UCIMAP_STRING,
     .name = "hardware_model"},
    {UCIMAP_OPTION(struct uci_ncms_device, base_mac), .type = UCIMAP_STRING, .name = "base_mac"},
    {UCIMAP_OPTION(struct uci_ncms_device, server_url), .type = UCIMAP_STRING,
     .name = "server_url"},
    {UCIMAP_OPTION(struct uci_ncms_device, enabled), .type = UCIMAP_INT, .name = "enabled"}};

static struct uci_sectionmap ncms_device_section = {UCIMAP_SECTION(struct uci_ncms_device, map),
                                                    .type = "ncms_device",
                                                    .alloc = ncms_device_allocate,
                                                    .init = ncms_device_init,
                                                    .add = ncms_device_add,
                                                    .options = &ncms_device_options[0],
                                                    .n_options = ARRAY_SIZE(ncms_device_options),
                                                    .options_size = sizeof(struct uci_optmap)};

struct uci_sectionmap *ncms_smap[] = {&ncms_device_section};

struct uci_map ncms_map = {
    .sections = ncms_smap,
    .n_sections = ARRAY_SIZE(ncms_smap),
};

int ncms_init_config(const char *config_path)
{
    struct uci_context *ctx;
    struct uci_package *pkg;

    INIT_LIST_HEAD(&lh_ncms_device);

    ctx = uci_alloc_context();
    if (config_path != NULL) {
        uci_set_confdir(ctx, config_path);
    }
    ucimap_init(&ncms_map);

    if (uci_load(ctx, "ncms-config", &pkg)) {
        printf("Error loading ncms-config configuration file\n");
        return 1;
    }
    ucimap_parse(&ncms_map, pkg);

    if (ncms_device == NULL) {
        printf("Warning: No ncms_device section found in config\n");
        return 1;
    }

    return 0;
}

struct uci_ncms_device *ncms_get_config(void)
{
    return ncms_device;
}

void ncms_cleanup_config(void)
{

    ncms_device = NULL;
}

const char *ncms_get_value(const char *option_name)
{
    if (!ncms_device) return NULL;

    if (strcmp(option_name, "serial_number") == 0) return ncms_device->serial_number;
    if (strcmp(option_name, "firmware_version") == 0) return ncms_device->firmware_version;
    if (strcmp(option_name, "agent_version") == 0) return ncms_device->agent_version;
    if (strcmp(option_name, "hardware_model") == 0) return ncms_device->hardware_model;
    if (strcmp(option_name, "base_mac") == 0) return ncms_device->base_mac;
    if (strcmp(option_name, "server_url") == 0) return ncms_device->server_url;

    return NULL;
}
