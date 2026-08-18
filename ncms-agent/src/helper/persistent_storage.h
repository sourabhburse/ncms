#ifndef PERSISTENT_STORAGE_H
#define PERSISTENT_STORAGE_H

/* Creates persistent certificate storage; returns 0 on success, -1 on error. */
int init_persistent_storage(void);

/* Returns 1 when all certificate files exist, otherwise 0. */
int certificates_exist(void);

#endif
