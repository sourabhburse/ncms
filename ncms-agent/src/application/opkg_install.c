#include "opkg_install.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/wait.h>

static int run_opkg_capture(const char *command, char *error_buf, size_t error_buf_sz,
                            int *out_dependency_issue)
{
    char line[512];
    char dependency_line[512] = {0};
    char generic_line[512] = {0};
    int dependency_issue = 0;
    FILE *fp;
    int status;
    int exit_code;

    if (error_buf && error_buf_sz > 0) error_buf[0] = '\0';

    if (out_dependency_issue) *out_dependency_issue = 0;

    fp = popen(command, "r");

    if (!fp) {
        printf("[APP][opkg] Failed to run: %s\n", command);
        if (error_buf) snprintf(error_buf, error_buf_sz, "Could not run opkg");
        return -1;
    }

    while (fgets(line, sizeof(line), fp) != NULL) {

        line[strcspn(line, "\r\n")] = '\0';

        if (line[0] == '\0') continue;

        printf("[APP][opkg] %s\n", line);

        if (strstr(line, "cannot find dependency") != NULL) {
            dependency_issue = 1;
            strncpy(dependency_line, line, sizeof(dependency_line) - 1);
        } else if (strstr(line, "Unknown package") != NULL ||
                   strstr(line, "incompatible") != NULL ||
                   strstr(line, "Cannot install package") != NULL ||
                   (line[0] == ' ' && line[1] == '*')) {
            strncpy(generic_line, line, sizeof(generic_line) - 1);
        }
    }

    status = pclose(fp);
    exit_code = WIFEXITED(status) ? WEXITSTATUS(status) : -1;

    if (exit_code != 0) {
        const char *chosen;

        if (dependency_issue && dependency_line[0] != '\0')
            chosen = dependency_line;
        else if (generic_line[0] != '\0')
            chosen = generic_line;
        else
            chosen = "opkg command failed (see device log)";

        while (*chosen == ' ' || *chosen == '*')
            chosen++;

        if (error_buf) snprintf(error_buf, error_buf_sz, "%s", chosen);

        if (out_dependency_issue) *out_dependency_issue = dependency_issue;
    }

    return exit_code;
}

static int query_opkg_status(const char *package_name, char *version_buf, size_t version_buf_sz,
                             int *out_installed)
{
    char command[300];
    char line[256];
    FILE *fp;
    int installed = 0;

    if (version_buf && version_buf_sz > 0) version_buf[0] = '\0';
    if (out_installed) *out_installed = 0;

    if (!package_name || package_name[0] == '\0') return -1;

    snprintf(command, sizeof(command), "opkg status %s 2>/dev/null", package_name);

    fp = popen(command, "r");
    if (!fp) return -1;

    while (fgets(line, sizeof(line), fp) != NULL) {
        line[strcspn(line, "\r\n")] = '\0';

        if (strncmp(line, "Version:", 8) == 0) {
            const char *v = line + 8;
            while (*v == ' ')
                v++;
            if (version_buf) snprintf(version_buf, version_buf_sz, "%s", v);
        } else if (strncmp(line, "Status:", 7) == 0) {

            if (strstr(line, " installed") != NULL && strstr(line, "not-installed") == NULL) {
                installed = 1;
            }
        }
    }

    pclose(fp);

    if (out_installed) *out_installed = installed;

    return installed ? 0 : -1;
}

int package_is_installed(const char *package_name)
{
    int installed = 0;
    query_opkg_status(package_name, NULL, 0, &installed);
    return installed ? 1 : 0;
}

int get_installed_version(const char *package_name, char *version_buf, size_t version_buf_sz)
{
    int installed = 0;
    char local_version[64] = {0};

    if (query_opkg_status(package_name, local_version, sizeof(local_version), &installed) != 0 ||
        !installed || local_version[0] == '\0') {
        return -1;
    }

    if (version_buf) snprintf(version_buf, version_buf_sz, "%s", local_version);

    return 0;
}

int validate_package(const char *filepath, char *error_buf, size_t error_buf_sz)
{
    char command[300];
    int dependency_issue = 0;
    int exit_code;

    snprintf(command, sizeof(command), "opkg install --noaction %s 2>&1", filepath);

    printf("[APP] Validating package...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, &dependency_issue);

    if (exit_code != 0) {
        printf("[APP] Package validation FAILED%s\n",
               dependency_issue ? " (missing dependency)" : "");
        return dependency_issue ? -2 : -1;
    }

    printf("[APP] Package validation SUCCESS\n");
    return 0;
}

int install_package(const char *filepath, const char *package_name, char *error_buf,
                    size_t error_buf_sz)
{
    char command[300];
    int exit_code;

    snprintf(command, sizeof(command), "opkg install %s 2>&1", filepath);

    printf("[APP] Installing package...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, NULL);

    if (exit_code != 0) {
        printf("[APP] Package install FAILED (opkg exit code %d)\n", exit_code);
        return -1;
    }

    if (package_name && package_name[0] != '\0') {
        if (package_is_installed(package_name) != 1) {
            printf("[APP] Package '%s' not confirmed installed via "
                   "opkg status after install\n",
                   package_name);

            if (error_buf)
                snprintf(error_buf, error_buf_sz,
                         "Install reported success but 'opkg status %s' "
                         "doesn't show it installed",
                         package_name);

            return -1;
        }
    }

    printf("[APP] Package install SUCCESS\n");
    return 0;
}

int opkg_compare_versions(const char *v1, const char *op, const char *v2)
{
    char command[300];
    int ret;

    if (!v1 || !op || !v2 || v1[0] == '\0' || v2[0] == '\0') return -1;

    snprintf(command, sizeof(command), "opkg compare-versions '%s' '%s' '%s'", v1, op, v2);

    ret = system(command);

    if (ret == -1) return -1;

    return (WIFEXITED(ret) && WEXITSTATUS(ret) == 0) ? 1 : 0;
}

int validate_downgrade(const char *filepath, char *error_buf, size_t error_buf_sz)
{
    char command[300];
    int dependency_issue = 0;
    int exit_code;

    snprintf(command, sizeof(command), "opkg install --force-downgrade --noaction %s 2>&1",
             filepath);

    printf("[APP] Validating downgrade...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, &dependency_issue);

    if (exit_code != 0) {
        printf("[APP] Downgrade validation FAILED%s\n",
               dependency_issue ? " (missing dependency)" : "");
        return dependency_issue ? -2 : -1;
    }

    printf("[APP] Downgrade validation SUCCESS\n");
    return 0;
}

int downgrade_package(const char *filepath, const char *package_name, char *error_buf,
                      size_t error_buf_sz)
{
    char command[300];
    int exit_code;

    snprintf(command, sizeof(command), "opkg install --force-downgrade %s 2>&1", filepath);

    printf("[APP] Downgrading package...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, NULL);

    if (exit_code != 0) {
        printf("[APP] Package downgrade FAILED (opkg exit code %d)\n", exit_code);
        return -1;
    }

    if (package_name && package_name[0] != '\0') {
        if (package_is_installed(package_name) != 1) {
            printf("[APP] Package '%s' not confirmed installed via "
                   "opkg status after downgrade\n",
                   package_name);

            if (error_buf)
                snprintf(error_buf, error_buf_sz,
                         "Downgrade reported success but 'opkg status %s' "
                         "doesn't show it installed",
                         package_name);

            return -1;
        }
    }

    printf("[APP] Package downgrade SUCCESS\n");
    return 0;
}

int validate_remove(const char *package_name, char *error_buf, size_t error_buf_sz)
{
    char command[300];
    int exit_code;

    if (!package_name || package_name[0] == '\0') {
        if (error_buf) snprintf(error_buf, error_buf_sz, "No package_name provided");
        return -1;
    }

    snprintf(command, sizeof(command), "opkg remove --noaction %s 2>&1", package_name);

    printf("[APP] Validating removal...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, NULL);

    if (exit_code != 0) {
        printf("[APP] Removal validation FAILED\n");
        return -1;
    }

    printf("[APP] Removal validation SUCCESS\n");
    return 0;
}

int remove_package(const char *package_name, char *error_buf, size_t error_buf_sz)
{
    char command[300];
    int exit_code;

    if (!package_name || package_name[0] == '\0') {
        if (error_buf) snprintf(error_buf, error_buf_sz, "No package_name provided");
        return -1;
    }

    snprintf(command, sizeof(command), "opkg remove %s 2>&1", package_name);

    printf("[APP] Removing package...\n");

    exit_code = run_opkg_capture(command, error_buf, error_buf_sz, NULL);

    if (exit_code != 0) {
        printf("[APP] Package remove FAILED (opkg exit code %d)\n", exit_code);
        return -1;
    }

    if (package_is_installed(package_name) == 1) {
        printf("[APP] Package '%s' still shows installed via opkg status "
               "after remove\n",
               package_name);

        if (error_buf)
            snprintf(error_buf, error_buf_sz,
                     "Remove reported success but 'opkg status %s' still "
                     "shows it installed",
                     package_name);

        return -1;
    }

    printf("[APP] Package remove SUCCESS\n");
    return 0;
}
