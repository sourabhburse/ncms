#ifndef UPDATE_LOCK_H
#define UPDATE_LOCK_H

/* Returns 0 when acquired, -1 if another update is running. */
int update_lock_try_acquire(const char *type);

void update_lock_release(void);

/* Returns 1 when busy, otherwise 0. */
int update_lock_is_busy(void);

#endif
