# Provisioning Architecture — Detailed Design (PKI mTLS)

This document defines the complete provisioning architecture for NCMS. It covers the .NET CMS Server and the OpenWrt Device Agent, with a strong focus on **Public Key Infrastructure (PKI) and mTLS** for secure communication.

---

## 1. Identity Strategy

### Chosen Strategy: Serial Allow-List + Extensible Identity Policy + mTLS PKI
- **Strict Constraint:** The firmware must be **100% identical** across all devices and clients. No client-specific tokens or secrets can be baked into the firmware.
- **Primary Identity:** A server-generated **UUID** assigned during registration.
- **Registration Credential (Current):** The device presents its auto-detected `serial_number`. The serial must match a pre-loaded inventory record in `PENDING_ACTIVATION` status.
- **Identity Policy (Future-Ready):** The bootstrap identity model is intentionally extensible. Product or vendor policies can later require extra claims such as `imei`, `base_mac`, `device_fingerprint`, or `vendor_device_id` without changing the base provisioning flow.
- **Authentication (Operational):** After successful registration, the .NET CMS acts as a Certificate Authority (CA) and generates a unique **X.509 Client Certificate** tied to the UUID. All subsequent MQTT communication uses mTLS (Mutual TLS).

---

## 2. Pre-Registration (CMS Side)

Before a device can be provisioned, its hardware identity must exist in the CMS database.

### Workflow
1.  **Admin/Vendor Action:** A user with the "DeviceManager" role logs into the CMS Dashboard.
2.  **Add Device Inventory:** They explicitly add devices to a specific Tenant using `serial_number` and, when available, optional identity claims such as IMEI, base MAC, or vendor device ID.
3.  **Bulk Import (Optional):** Upload a CSV with `serial_number, model, tenant_id` plus optional identity claim columns.
4.  **System Generates:** The CMS creates a record in `HardwareInventory` with `Status = PENDING_ACTIVATION`.
5.  **Activation Window (Optional):** Admin can set a time window (e.g., "Activate within 7 days"). If the device doesn't register within this window, the record expires and must be manually re-enabled. This prevents stale inventory records from being exploited later.

---

## 3. Device-Side Provisioning Flow (OpenWrt Agent)

When the device powers on, the agent follows a strict state machine.

### State Machine Diagram

```mermaid
stateDiagram-v2
    [*] --> CHECK_CONFIG: Agent Starts

    CHECK_CONFIG --> CHECK_CERT_EXPIRY: PKI Certs Found in Flash
    CHECK_CONFIG --> HTTP_REGISTER: No Certs Found

    CHECK_CERT_EXPIRY --> MQTT_CONNECT: Cert Valid
    CHECK_CERT_EXPIRY --> HTTP_REGISTER: Cert Expired or Expiring Soon

    HTTP_REGISTER --> SAVE_CERTS: 200 OK (Certs Received)
    HTTP_REGISTER --> RETRY_WAIT: 404/409 (Rejected)
    HTTP_REGISTER --> RETRY_WAIT: Network Error / Timeout

    RETRY_WAIT --> HTTP_REGISTER: After Backoff (30s, 60s... max 15min)

    SAVE_CERTS --> MQTT_CONNECT: Certs Written to Flash

    MQTT_CONNECT --> OPERATIONAL: mTLS Handshake Success
    MQTT_CONNECT --> HTTP_REGISTER: TLS Error / Cert Revoked
    MQTT_CONNECT --> MQTT_RECONNECT: Network Error

    MQTT_RECONNECT --> MQTT_CONNECT: After Backoff

    OPERATIONAL --> MQTT_RECONNECT: Connection Lost
    OPERATIONAL --> HTTP_REGISTER: Received "renew-cert" Command via MQTT
```

### Step-by-Step Logic (Pseudocode)

```
PROVISION_URL = "https://provision.youNCMS.com/api/v1/provision/register"
CERT_RENEW_DAYS = 30  # Re-register if cert expires within 30 days

function main():
    if file_exists("/etc/cms_agent/client.crt") AND file_exists("/etc/cms_agent/client.key"):
        
        # Check if cert is about to expire
        expiry = get_cert_expiry("/etc/cms_agent/client.crt")
        if (expiry - now()) < CERT_RENEW_DAYS:
            log("Certificate expiring soon, re-registering...")
            goto REGISTER
        
        result = mqtt_connect_mtls(
            host = read_config("mqtt_host"),
            ca   = "/etc/cms_agent/ca.crt",
            cert = "/etc/cms_agent/client.crt",
            key  = "/etc/cms_agent/client.key"
        )
        
        if result == SUCCESS:
            enter_operational_loop()  # Blocks here until disconnect
        
        elif result == TLS_HANDSHAKE_FAILED:
            # Server revoked our certificate, clear and re-register
            delete_files("/etc/cms_agent/client.*")
            goto REGISTER
    
    REGISTER:
    backoff = 30  # seconds
    
    while true:
        response = https_post(PROVISION_URL, {
            "serial_number": get_serial(), # Auto-detected from board or manufacturing data
            "firmware_version": get_firmware_version(),
            "agent_version": AGENT_VERSION,
            "hardware_model": get_model(),
            "mac_addresses": get_all_macs(),
            "identity_claims": get_optional_identity_claims()
        })
        
        if response.status == 200:
            write_file("/etc/cms_agent/ca.crt", response.pki.ca_certificate)
            write_file("/etc/cms_agent/client.crt", response.pki.client_certificate)
            write_file("/etc/cms_agent/client.key", response.pki.private_key)
            write_config("mqtt_host", response.mqtt.broker_url)
            write_config("device_id", response.device_id)
            
            mqtt_connect_mtls(response.mqtt.broker_url, ...)
            enter_operational_loop()
            break
        
        elif response.status == 404:
            log("Serial number not found in CMS inventory.")
        elif response.status == 409:
            log("Device already active. Admin must reset before re-registration.")
        elif response.status == 422:
            log("Identity policy validation failed.")
        
        sleep(backoff)
        backoff = min(backoff * 2, 900)  # Exponential backoff, max 15 minutes
```

### How the Device Gets Its Identity Automatically

| Data Point | How to Get It on OpenWrt |
|---|---|
| **Serial Number** | Read from `/sys/class/dmi/id/product_serial` or custom EEPROM partition |
| **MAC Addresses** | `cat /sys/class/net/eth0/address` |
| **IMEI** (Optional cellular modem claim) | `AT+CGSN` via serial port, or `ubus call gsm.modem0 info` |
| **Firmware Version** | `cat /etc/openwrt_release` or custom file `/etc/cms_version` |
| **Hardware Model** | `cat /tmp/sysinfo/model` or `ubus call system board` |

---

## 4. Server-Side Provisioning Flow (.NET CMS)

### The .NET PKI Engine
The .NET backend includes a Certificate Authority service (using `BouncyCastle` or `System.Security.Cryptography`). When a device registers:
1.  Generates an **ECC P-256** key pair (smaller, faster than RSA on constrained devices).
2.  Creates an X.509 certificate where `CN = {device_uuid}`.
3.  Sets `Not After` to a configurable validity period (e.g., 1 year).
4.  Signs the certificate using the CMS **Intermediate CA** (not the Root CA directly).
5.  Stores the certificate thumbprint and expiry in the `DeviceCertificates` table.

### API Endpoint: `POST /api/v1/provision/register`

#### Request (from Device)
```http
POST /api/v1/provision/register HTTP/1.1
Content-Type: application/json

{
    "serial_number": "SN-001",
    "firmware_version": "1.0.2",
    "agent_version": "0.1.0",
    "hardware_model": "MT7621",
    "board_name": "vendor,model",
    "mac_addresses": {
        "eth0": "AA:BB:CC:DD:EE:01",
        "wlan0": "AA:BB:CC:DD:EE:02"
    },
    "identity_claims": {
        "imei": null,
        "base_mac": "AA:BB:CC:DD:EE:01",
        "device_fingerprint": null,
        "vendor_device_id": null
    }
}
```

#### Server Processing Logic

```mermaid
flowchart TD
    A[Receive POST /register] --> RL{Rate Limit Check}
    RL -- Exceeded --> RL_R[Return 429: Too Many Requests]
    RL -- OK --> B{Lookup serial_number in HardwareInventory}
    B -- Not Found --> C[Return 404: Serial Not Recognized]
    B -- Found --> IP{IdentityPolicy Extra Claims Required?}
    IP -- Invalid/Missing --> SV_R[Return 422: Identity Policy Failed]
    IP -- Valid/Not Required --> F{Check HardwareInventory.Status}
    F -- DECOMMISSIONED --> G[Return 403: Device Decommissioned]
    F -- ACTIVE --> CONFLICT[Return 409: Already Active - Admin Must Reset]
    F -- PENDING_ACTIVATION --> AW{Activation Window Valid?}
    AW -- Expired --> AW_R[Return 403: Activation Window Expired]
    AW -- Valid --> H[Create New Device Record]
    H --> K[Generate UUID & X.509 Certificate via PKI Engine]
    K --> L[Store Cert in DeviceCertificates Table]
    L --> M[Update HardwareInventory Status → ACTIVE]
    M --> LOG[Write AuditLog Entry]
    LOG --> N[Return 200 with PKI Bundle]
```

#### Admin Reset Endpoint: `POST /api/v1/provision/reset/{inventoryId}`
For hardware swap scenarios, an admin can call this endpoint to:
1.  Revoke the existing device certificate.
2.  Reset `HardwareInventory.Status` back to `PENDING_ACTIVATION`.
3.  Optionally update the serial number or identity claims if the replacement hardware has different identifiers.

#### Response — Success (200 OK)
```json
{
    "device_id": "d4e5f6a7-b8c9-4d0e-a1b2-c3d4e5f6a7b8",
    "mqtt": {
        "broker_url": "mqtt.youNCMS.com",
        "broker_port": 8883,
        "client_id": "d4e5f6a7-b8c9-4d0e-a1b2-c3d4e5f6a7b8",
        "pki": {
            "ca_certificate": "-----BEGIN CERTIFICATE-----\nMIIDdz...\n-----END CERTIFICATE-----",
            "client_certificate": "-----BEGIN CERTIFICATE-----\nMIICaD...\n-----END CERTIFICATE-----",
            "private_key": "-----BEGIN EC PRIVATE KEY-----\nMHQCA...\n-----END EC PRIVATE KEY-----"
        }
    },
    "topics": {
        "telemetry_publish": "d/{device_id}/telemetry",
        "config_subscribe": "d/{device_id}/config",
        "command_subscribe": "d/{device_id}/cmd",
        "command_response": "d/{device_id}/cmd/res",
        "ota_subscribe": "d/{device_id}/ota"
    },
    "telemetry_interval_seconds": 60,
    "heartbeat_interval_seconds": 30
}
```

> **Note:** MQTT topics use `d/{device_id}/...` — **not** `t/{tenant_id}/...`. Tenant scoping is enforced at the MQTT broker ACL level, not exposed to the device. This prevents a compromised device from discovering other devices in the same tenant.

#### Response — Errors

| HTTP Code | Meaning | Device Action |
|---|---|---|
| `200 OK` | Registered successfully | Save certs, connect to MQTT |
| `400 Bad Request` | Malformed payload | Log error, do not retry |
| `403 Forbidden` | Decommissioned or activation window expired | Log error, stop retrying |
| `404 Not Found` | Serial number not in inventory | Retry with backoff |
| `409 Conflict` | Device already active (admin must reset) | Log error, stop retrying |
| `422 Unprocessable` | Required identity policy claim is missing or does not match | Log error, stop retrying |
| `429 Too Many Requests` | Rate limited | Respect `Retry-After` header |
| `500+ Server Error` | Server issue | Retry with backoff |

---

## 5. Security Considerations

### Allow-List Security
- Security currently relies on the CMS **Allow-List** with `serial_number` as the required bootstrap identifier.
- When a product needs stronger bootstrap assurance, its `IdentityPolicy` can require additional claims such as IMEI, base MAC, vendor ID, or a device fingerprint.
- An attacker would need to know the exact serial number, plus any required policy claims, for a device that is currently in `PENDING_ACTIVATION` state.
- Once a device registers, its status becomes `ACTIVE`. The `/register` endpoint returns `409 Conflict` for any subsequent attempts. Only an admin can reset a device for re-registration.
- **Activation Windows** further reduce the attack surface by limiting the time period during which a device can register.

### PKI mTLS Security
- **No passwords.** The MQTT broker is configured to *only* accept connections with client certificates signed by the CMS Intermediate CA.
- The broker extracts the `CN` (Device UUID) from the client certificate during the TLS handshake and uses it as the authorized Client ID.
- **Certificate Renewal:** The CMS monitors `DeviceCertificates.ExpiresAt`. When a certificate is within 30 days of expiry, the CMS publishes a `renew-cert` command to the device's MQTT topic. The device then re-runs the HTTP registration flow to receive a fresh certificate.
- **Revocation:** The CMS maintains a CRL (Certificate Revocation List) or uses OCSP stapling. Revoked certs are immediately rejected by the broker.

### Transport Security
- **Registration API:** HTTPS with TLS 1.2+. Server-side certificate only (standard HTTPS).
- **MQTT Broker:** mTLS on port 8883. Both server and client certificates are validated.
- **OTA Downloads:** HTTPS with signed URLs that expire after a configurable window (e.g., 30 minutes).

### Rate Limiting & Abuse Prevention
- `/register` is rate-limited per IP (e.g., 10 requests/minute) and per `serial_number` (e.g., 5 requests/hour).
- Failed validation attempts are logged in `AuditLogs` and trigger alerts after a threshold.
- All registration events (success and failure) are recorded for forensic analysis.

---

## 6. Device Lifecycle — Complete State Diagram

```mermaid
stateDiagram-v2
    [*] --> PENDING_ACTIVATION: Admin adds serial_number to CMS

    PENDING_ACTIVATION --> ACTIVE: Device calls /register (serial_number validated, PKI issued)
    PENDING_ACTIVATION --> EXPIRED: Activation window elapsed (optional)
    EXPIRED --> PENDING_ACTIVATION: Admin re-enables

    ACTIVE --> OFFLINE: Heartbeat timeout (3x heartbeat interval)
    OFFLINE --> ACTIVE: Device reconnects via mTLS

    ACTIVE --> CERT_EXPIRING: Certificate within 30 days of expiry
    CERT_EXPIRING --> ACTIVE: Device re-registers and gets new cert

    ACTIVE --> DECOMMISSIONED: Admin decommissions (cert revoked)
    OFFLINE --> DECOMMISSIONED: Admin decommissions

    DECOMMISSIONED --> PENDING_ACTIVATION: Admin resets for hardware swap

    ACTIVE --> FIRMWARE_UPDATING: OTA update in progress
    FIRMWARE_UPDATING --> ACTIVE: Device reboots, reconnects with existing cert
```

---

## 7. Scalability Design

### Horizontal Scaling of the Provisioning API
- The `/register` endpoint is **stateless**. Multiple instances behind a load balancer.
- Use a **Redis cache** for serial number lookups and rate limiting counters.

### MQTT Broker Scaling
- The provisioning response includes a specific `broker_url`. The CMS distributes devices across broker instances.
- Use a broker cluster (EMQX supports native clustering) with shared subscription for backend workers.
- **Broker ACL:** The broker is configured with ACL rules that map each `device_id` (from the client certificate CN) to its permitted topics. ACL data is pulled from the CMS database or Redis.

### Telemetry Ingestion
- MQTT messages are consumed by a .NET **Background Worker Service**.
- For high volume: Worker → Message Queue (RabbitMQ / Azure Service Bus) → Batch Writer → TimescaleDB.
