#include "key_csr.h"

#include <openssl/ec.h>
#include <openssl/pem.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/stat.h>

EVP_PKEY *generate_ecc_private_key(void)
{
    EVP_PKEY *pkey = NULL;
    EVP_PKEY_CTX *ctx = EVP_PKEY_CTX_new_id(EVP_PKEY_EC, NULL);

    if (!ctx) return NULL;

    if (EVP_PKEY_keygen_init(ctx) <= 0) {
        EVP_PKEY_CTX_free(ctx);
        return NULL;
    }

    if (EVP_PKEY_CTX_set_ec_paramgen_curve_nid(ctx, NID_X9_62_prime256v1) <= 0) {
        EVP_PKEY_CTX_free(ctx);
        return NULL;
    }

    if (EVP_PKEY_keygen(ctx, &pkey) <= 0) {
        EVP_PKEY_CTX_free(ctx);
        return NULL;
    }

    EVP_PKEY_CTX_free(ctx);
    return pkey;
}

int save_private_key(EVP_PKEY *pkey, const char *filename)
{
    FILE *fp = fopen(filename, "w");
    if (!fp) return -1;

    int ret = PEM_write_PrivateKey(fp, pkey, NULL, NULL, 0, NULL, NULL);
    fclose(fp);

    if (ret) chmod(filename, 0600);
    return ret ? 0 : -1;
}

X509_REQ *generate_csr(EVP_PKEY *pkey, const char *serial_number)
{
    X509_REQ *req = X509_REQ_new();
    X509_NAME *name = NULL;

    if (!req) return NULL;

    if (X509_REQ_set_version(req, 0) != 1) goto error;

    name = X509_NAME_new();
    if (!name) goto error;

    X509_NAME_add_entry_by_txt(name, "CN", MBSTRING_ASC, (unsigned char *)"temp-device", -1, -1, 0);
    X509_NAME_add_entry_by_txt(name, "serialNumber", MBSTRING_ASC, (unsigned char *)serial_number,
                               -1, -1, 0);

    if (X509_REQ_set_subject_name(req, name) != 1) goto error;
    X509_NAME_free(name);
    name = NULL;

    if (X509_REQ_set_pubkey(req, pkey) != 1) goto error;
    if (X509_REQ_sign(req, pkey, EVP_sha256()) <= 0) goto error;

    return req;

error:
    X509_NAME_free(name);
    X509_REQ_free(req);
    return NULL;
}

int save_csr(X509_REQ *req, const char *filename)
{
    FILE *fp = fopen(filename, "w");
    if (!fp) return -1;

    int ret = PEM_write_X509_REQ(fp, req);
    fclose(fp);
    return ret ? 0 : -1;
}

char *get_csr_string(const char *csr_file)
{
    FILE *fp = fopen(csr_file, "r");
    if (!fp) return NULL;

    fseek(fp, 0, SEEK_END);
    long len = ftell(fp);
    fseek(fp, 0, SEEK_SET);

    char *csr_data = malloc(len + 1);
    if (!csr_data) {
        fclose(fp);
        return NULL;
    }

    size_t read_len = fread(csr_data, 1, len, fp);
    csr_data[read_len] = '\0';
    fclose(fp);

    char *end = csr_data + read_len - 1;
    while (end > csr_data && (*end == '\n' || *end == '\r')) {
        *end = '\0';
        end--;
    }

    return csr_data;
}
