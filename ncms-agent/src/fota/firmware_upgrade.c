#include "firmware_upgrade.h"

#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

int validate_firmware(const char *filepath)
{
    char command[256];
    int ret;

    snprintf(command, sizeof(command), "sysupgrade -T %s", filepath);

    printf("Validating firmware image...\n");

    ret = system(command);

    if (ret != 0) {
        printf("Firmware validation FAILED\n");
        return -1;
    }

    printf("Firmware validation SUCCESS\n");
    return 0;
}

int flash_firmware(const char *filepath)
{
    char command[256];
    int ret;

    printf("Syncing filesystems before flash...\n");
    sync();

    snprintf(command, sizeof(command), "sysupgrade -c %s", filepath);

    printf("Starting firmware upgrade...\n");

    system(command);

    return 0;
}
