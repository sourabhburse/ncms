#include "update_lock.h"

#include <pthread.h>
#include <stdio.h>
#include <string.h>

static volatile int g_update_in_progress = 0;
static pthread_mutex_t g_update_mutex = PTHREAD_MUTEX_INITIALIZER;
static char g_update_type[16] = {0};

int update_lock_try_acquire(const char *type)
{
    pthread_mutex_lock(&g_update_mutex);

    if (g_update_in_progress) {
        printf("[UPDATE] Lock busy: '%s' already in progress, "
               "rejecting '%s'\n",
               g_update_type, type ? type : "unknown");
        pthread_mutex_unlock(&g_update_mutex);
        return -1;
    }

    g_update_in_progress = 1;
    strncpy(g_update_type, type ? type : "unknown", sizeof(g_update_type) - 1);
    g_update_type[sizeof(g_update_type) - 1] = '\0';

    pthread_mutex_unlock(&g_update_mutex);

    printf("[UPDATE] Lock acquired by '%s'\n", g_update_type);
    return 0;
}

void update_lock_release(void)
{
    pthread_mutex_lock(&g_update_mutex);

    if (g_update_in_progress) printf("[UPDATE] Lock released by '%s'\n", g_update_type);

    g_update_in_progress = 0;
    g_update_type[0] = '\0';

    pthread_mutex_unlock(&g_update_mutex);
}

int update_lock_is_busy(void)
{
    int busy;
    pthread_mutex_lock(&g_update_mutex);
    busy = g_update_in_progress;
    pthread_mutex_unlock(&g_update_mutex);
    return busy;
}
