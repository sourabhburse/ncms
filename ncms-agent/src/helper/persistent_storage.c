#include "persistent_storage.h"

#include "mqtt_client.h"

#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>
#include "app_defaults.h"


static int create_directory(const char *path)
{
    struct stat st = {0};

    if (stat(path, &st) == -1) {
        if (mkdir(path, 0755) != 0) {
            fprintf(stderr, "[Storage] Failed to create %s: %s\n", path, strerror(errno));
            return -1;
        }
        printf("[Storage] Created directory: %s\n", path);
    }
    return 0;
}

static int ensure_sysupgrade_preserve(void)
{

    FILE *fp = fopen(NCMS_DEFAULT_SYSUPGRADE_CONF, "r");
    if (fp) {
        char line[256];
        while (fgets(line, sizeof(line), fp)) {

            line[strcspn(line, "\r\n")] = '\0';
            if (strcmp(line, NCMS_DEFAULT_STORAGE_DIR) == 0) {
                fclose(fp);
                printf("[Storage] %s already preserved in sysupgrade.conf\n",NCMS_DEFAULT_STORAGE_DIR);
                return 0;
            }
        }
        fclose(fp);
    }

    fp = fopen(NCMS_DEFAULT_SYSUPGRADE_CONF, "a");
    if (!fp) {
        fprintf(stderr, "[Storage] Failed to open %s\n", NCMS_DEFAULT_SYSUPGRADE_CONF);
        return -1;
    }

    fprintf(fp, "\n# ncms certificates (preserved across sysupgrade)\n");
    fprintf(fp, "%s\n",NCMS_DEFAULT_STORAGE_DIR);
    fclose(fp);

    printf("[Storage] Added %s to %s\n", NCMS_DEFAULT_STORAGE_DIR ,NCMS_DEFAULT_SYSUPGRADE_CONF);
    return 0;
}

int init_persistent_storage(void)
{

    if (create_directory(NCMS_DEFAULT_STORAGE_DIR) != 0) {
        return -1;
    }

    if (ensure_sysupgrade_preserve() != 0) {
        fprintf(stderr, "[Storage] Warning: Could not update sysupgrade.conf\n");
    }

    printf("[Storage] Persistent storage ready at %s\n", NCMS_DEFAULT_STORAGE_DIR);
    return 0;
}

