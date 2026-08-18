#ifndef KEY_CSR_H
#define KEY_CSR_H

#include <openssl/evp.h>
#include <openssl/x509.h>

EVP_PKEY *generate_ecc_private_key(void);
int save_private_key(EVP_PKEY *pkey, const char *filename);
X509_REQ *generate_csr(EVP_PKEY *pkey, const char *serial_number);
int save_csr(X509_REQ *req, const char *filename);
char *get_csr_string(const char *csr_file);

#endif
