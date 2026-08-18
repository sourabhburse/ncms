#include "download.h"
#include "app_defaults.h"
#include <curl/curl.h>
#include <stdio.h>

static size_t write_data(void *ptr, size_t size, size_t nmemb, void *stream)
{
    return fwrite(ptr, size, nmemb, (FILE *)stream);
}

int download_file(const char *url, const char *output_path)
{
    CURL *curl;
    FILE *fp;
    CURLcode res;

    printf("Downloading firmware...\n");
    printf("URL: %s\n", url);

    curl = curl_easy_init();

    if (!curl) {
        printf("curl init failed\n");
        return -1;
    }

    fp = fopen(output_path, "wb");

    if (!fp) {
        printf("file open failed\n");

        curl_easy_cleanup(curl);
        return -1;
    }

    curl_easy_setopt(curl, CURLOPT_URL, url);

    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_data);

    curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);

    curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);

    curl_easy_setopt(curl, CURLOPT_TIMEOUT, NCMS_DOWNLOAD_TIMEOUT_SECONDS);

    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);

    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);

    res = curl_easy_perform(curl);

    fclose(fp);

    curl_easy_cleanup(curl);

    if (res != CURLE_OK) {
        printf("Download failed: %s\n", curl_easy_strerror(res));

        return -1;
    }

    printf("Download completed\n");

    return 0;
}
