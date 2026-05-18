# Implementation Plan: Device Management Platform

This document outlines the architecture and a phased implementation plan for building a centralized device management platform. The system is divided into a **.NET Cloud CMS** and an **OpenWrt Device Agent**.

---

## Architecture Overview

The system relies on a persistent, lightweight connection between the devices and the cloud, supplemented by standard HTTP for heavy data transfers.

*   **Communication Protocol:** **MQTT over TLS (mTLS)**. MQTT is ideal for IoT/routers because it's lightweight, handles unreliable networks well, and allows real-time bidirectional communication (pushing configs, pulling telemetry).
*   **Large File Transfer:** **HTTPS**. Used by the router to download large configuration files or firmware binaries (OTA) triggered by an MQTT command.
*   **Security:** Mutual TLS (mTLS). Both the server and the device authenticate each other using certificates.

---

## Part 1: The OpenWrt Application (Device Agent)

The device agent sits on the router, gathers data, and executes commands from the CMS. 

### What to Build
You need a lightweight daemon that runs as a background service on OpenWrt.
Given OpenWrt's resource constraints (often 16MB/32MB Flash and 64MB/128MB RAM), the best language choices are:
1.  **C/C++:** Using `libmosquitto` (for MQTT), `libuci` (for configuration), and `libubox`/`ubus` (for system events). This produces the smallest binary.
2.  **Lua:** OpenWrt natively uses Lua for its web interface (LuCI).
3.  **Go:** If your routers have enough storage/RAM, Go provides a fast development cycle with excellent MQTT libraries, but binaries are larger.

### Core Modules
1.  **Telemetry Engine:** Periodically reads system metrics (CPU from `/proc/stat`, RAM from `/proc/meminfo`, Interface stats via `ubus call network.interface status`) and publishes them to `device/{mac_address}/telemetry`.
2.  **Configuration Manager:** Subscribes to `device/{mac_address}/config`. When a new config payload arrives, it translates the JSON into OpenWrt `uci` commands (e.g., `uci set network.lan.ipaddr=192.168.2.1; uci commit network; /etc/init.d/network restart`).
3.  **OTA Manager:** Subscribes to `device/{mac_address}/ota`. Receives a URL, downloads the firmware to `/tmp` via `curl` or `wget`, verifies the hash, and executes `sysupgrade /tmp/firmware.bin`.
4.  **Command Executor:** Subscribes to `device/{mac_address}/command` to handle tasks like `reboot`, `factory_reset`, or retrieving logs.

### How to Build It
1.  Set up the **OpenWrt Buildroot (SDK)** for your specific router architecture (e.g., `mips_24kc`, `aarch64`).
2.  Write the application and create an OpenWrt Makefile (`Makefile`).
3.  Cross-compile your application into an `.ipk` package.
4.  Write a `procd` init script (`/etc/init.d/your_agent`) to ensure the daemon starts on boot and restarts if it crashes.

---

## Part 2: The .NET CMS System (Cloud Backend & Frontend)

The cloud system manages the fleet, stores data, and provides the UI.

### What to Build
A monolithic or microservice architecture using **ASP.NET Core 8.0+**.
1.  **Web Application (Frontend/Admin UI):** ASP.NET Core MVC, Blazor Server/WebAssembly, or a separate frontend framework (React/Vue) communicating with a .NET Web API.
2.  **Database:** PostgreSQL or SQL Server managed via Entity Framework Core (EF Core).
3.  **MQTT Broker / Worker:** A service to handle the thousands of concurrent device connections.

### Core Modules
1.  **MQTT integration (MQTTnet):** 
    *   Use the `MQTTnet` library. You can either embed an MQTT Broker directly inside your .NET application (`MQTTnet.Server`) or use an external enterprise broker (like EMQX or Mosquitto) and build a .NET Background Worker (Hosted Service) that connects to it to process messages.
2.  **Device Registry & State Management:** A database schema tracking Device MAC, Serial, Firmware Version, Last Seen timestamp, and Current Configuration state.
3.  **Rule & Alert Engine:** A background service evaluating incoming telemetry against thresholds (e.g., "If CPU > 90% for 5 mins, create an Alert").
4.  **Configuration Builder:** A UI tool that lets users define settings (WiFi SSID, APN, Port Forwarding) which the backend converts into a JSON payload tailored for the device.
5.  **OTA Vault:** A secure storage (e.g., Azure Blob Storage or local disk) for `.bin` files. An API endpoint generates expiring, signed URLs for devices to download the firmware.

---

## Phased Execution Plan

### Phase 1: Proof of Concept (The "Hello World" of IoT)
*   **Goal:** Establish secure, bidirectional communication.
*   **.NET:** Setup a basic ASP.NET Core Web API with an integrated MQTTnet Server. Create a simple DB table `Devices`.
*   **OpenWrt:** Write a basic shell script or C program that uses `mosquitto_pub` and `mosquitto_sub` to send a heartbeat (MAC address + uptime) every 60 seconds and listen for a "Reboot" command.
*   **Outcome:** You can see routers appearing online in your database and can click a button in a simple UI to reboot a specific router.

### Phase 2: Telemetry & Monitoring
*   **Goal:** Gather rich data.
*   **.NET:** Expand DB schema to include Timeseries data for telemetry. Create a dashboard UI using charting libraries (e.g., Chart.js) to display real-time RAM/CPU/Network usage. Use SignalR to push MQTT telemetry from the backend to the user's web browser in real-time.
*   **OpenWrt:** Enhance the agent to parse OpenWrt's `ubus` system for detailed interface statistics, connected Wi-Fi clients, and cellular modem signal strength (if applicable), sending JSON payloads to the broker.

### Phase 3: Configuration Management (The Hardest Part)
*   **Goal:** Remote management of router settings.
*   **.NET:** Build the UI for users to define "Templates" (e.g., a standard WiFi setup). Write the logic that diffs the "Desired State" vs "Current State" and pushes a JSON config payload via MQTT.
*   **OpenWrt:** Build the Config Manager module. It must take your JSON payload, map it to OpenWrt `uci` commands, apply them, test the connection, and **rollback** to the previous config if the router loses connection to the MQTT broker (crucial to prevent bricking remote routers).

### Phase 4: FOTA (Firmware Over-The-Air) & Advanced Features
*   **Goal:** Lifecycle management.
*   **.NET:** Build the Firmware Repository UI. Create the workflow: Select Devices -> Select Firmware -> Schedule Update. Publish the OTA command with the download URL.
*   **OpenWrt:** Agent downloads the binary, verifies the MD5/SHA256 checksum, stops non-essential services, and runs `sysupgrade`.
*   **Refinement:** Add Role-Based Access Control (RBAC) to the .NET CMS, implement email/SMS alerts for offline devices.
