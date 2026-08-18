#include "hash.h"

#include <openssl/md5.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define READ_CHUNK_SIZE 4096

int compute_md5_of_file(const char *filepath, char *out_hex)
{
    FILE *fp;
    MD5_CTX ctx;
    unsigned char digest[MD5_DIGEST_LENGTH];
    unsigned char buf[READ_CHUNK_SIZE];
    size_t bytes_read;
    int i;

    fp = fopen(filepath, "rb");
    if (!fp) {
        printf("MD5: failed to open file: %s\n", filepath);
        return -1;
    }

    MD5_Init(&ctx);

    while ((bytes_read = fread(buf, 1, sizeof(buf), fp)) > 0) {
        MD5_Update(&ctx, buf, bytes_read);
    }

    if (ferror(fp)) {
        printf("MD5: file read error\n");
        fclose(fp);
        return -1;
    }

    fclose(fp);

    MD5_Final(digest, &ctx);

    for (i = 0; i < MD5_DIGEST_LENGTH; i++) {

        sprintf(out_hex + (i * 2), "%02x", (unsigned int)digest[i]);
    }
    out_hex[32] = '\0';

    return 0;
}

int verify_sha256(const char *filepath, const char *expected_sha256)
{
    char command[512];
    char buffer[512];
    FILE *fp;

    if (!filepath || !expected_sha256 || strlen(expected_sha256) != 64) {
        printf("SHA256: invalid input\n");
        return -1;
    }

    snprintf(command, sizeof(command), "sha256sum %s", filepath);

    fp = popen(command, "r");

    if (!fp) {
        printf("SHA256: failed to run sha256sum\n");
        return -1;
    }

    if (fgets(buffer, sizeof(buffer), fp) == NULL) {
        printf("SHA256: failed to read checksum output\n");
        pclose(fp);
        return -1;
    }

    pclose(fp);

    printf("SHA256 computed : %.64s\n", buffer);
    printf("SHA256 expected : %s\n", expected_sha256);

    if (strncmp(buffer, expected_sha256, 64) == 0) {
        printf("SHA256 VERIFIED SUCCESSFULLY\n");
        return 0;
    }

    printf("SHA256 VERIFICATION FAILED\n");
    return -1;
}
