#ifndef OPKG_INSTALL_H
#define OPKG_INSTALL_H
#include <stddef.h>

/* Returns 1 if installed, 0 if not installed. */
int package_is_installed(const char *package_name);

/* Returns 0 on success, -1 on validation failure, -2 for missing dependency. */
int validate_package(const char *filepath, char *error_buf, size_t error_buf_sz);

/* Returns 0 on success, -1 on install failure. */
int install_package(const char *filepath, const char *package_name, char *error_buf,
                    size_t error_buf_sz);

/* Returns 0 on success, -1 if not installed or unreadable. */
int get_installed_version(const char *package_name, char *version_buf, size_t version_buf_sz);

/* Returns 1 if comparison is true, 0 if false, -1 on opkg error. */
int opkg_compare_versions(const char *v1, const char *op, const char *v2);

/* Downgrade uses --force-downgrade; return codes match install validation. */
int validate_downgrade(const char *filepath, char *error_buf, size_t error_buf_sz);
int downgrade_package(const char *filepath, const char *package_name, char *error_buf,
                      size_t error_buf_sz);

/* Returns 0 on success, -1 on validation/remove failure. */
int validate_remove(const char *package_name, char *error_buf, size_t error_buf_sz);
int remove_package(const char *package_name, char *error_buf, size_t error_buf_sz);

#endif
