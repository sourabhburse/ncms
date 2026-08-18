#ifndef HASH_H
#define HASH_H

/* Writes 32-byte lowercase hex plus NUL; returns 0 on success, -1 on error. */
int compute_md5_of_file(const char *filepath, char *out_hex);

/* Returns 0 when SHA256 matches, -1 on mismatch or command error. */
int verify_sha256(const char *filepath, const char *expected_sha256);

#endif
