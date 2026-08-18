cmake_minimum_required(VERSION 3.10)

project(NCMS C)

set(PACK_VERSION 1.0)
set(PACK_RELEASE 1)

set(CMAKE_C_STANDARD 99)


# Find OpenSSL
find_package(OpenSSL REQUIRED)
include_directories(. mqtt fota application helper provisioning telemetry)
add_executable(NCMS
        main.c
        ncms-config.c
        mqtt/mqtt_client.c
        provisioning/key_csr.c
        provisioning/provisioning.c
        telemetry/telemetry.c
        telemetry/heartbeat.c
        helper/download.c
        helper/hash.c
        helper/update_lock.c
        fota/ota_handler.c
        fota/firmware_upgrade.c
        fota/ota_state.c
        helper/persistent_storage.c
        application/app_handler.c
        application/opkg_install.c
)

# Link libraries correctly
target_link_libraries(NCMS
        ${OPENSSL_LIBRARIES}    # This adds ssl and crypto
        curl
        uci
        json-c
        pthread
        mosquitto
        libucimap.a
        argparse
)

# Include OpenSSL headers
target_include_directories(NCMS PRIVATE ${OPENSSL_INCLUDE_DIR})

install(TARGETS NCMS RUNTIME DESTINATION /usr/bin/)

if(BUILD_TESTING)
    add_executable(ncms_telemetry_cellular_test
            tests/telemetry_cellular_test.c
            telemetry/telemetry.c
    )
    target_link_libraries(ncms_telemetry_cellular_test json-c pthread)
    add_test(NAME ncms_telemetry_cellular_test COMMAND ncms_telemetry_cellular_test)
endif()
