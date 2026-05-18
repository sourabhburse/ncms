# NCMS Agent

Lightweight C-based daemon for OpenWrt routers.

## Features
- HTTPS Registration (Provisioning)
- MQTT over TLS (mTLS) for telemetry and commands
- OTA Firmware updates
- UCI configuration management

## Project Structure
- `src/`: C source code.
- `include/`: Header files.
- `files/`: OpenWrt-specific configuration and init scripts.
- `Makefile`: OpenWrt package definition.

## Compilation
This project is intended to be cross-compiled using the OpenWrt SDK.
