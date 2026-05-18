# Multi-Tenant Device Registration & Lifecycle Strategy

Building for multiple clients requires a "Multi-Tenant" architecture where devices are securely mapped to specific owners and isolation is strictly enforced.

---

## 1. The Pre-Assignment (Allow-List) Workflow
To handle the problem of multiple clients while maintaining a constraint of **zero firmware customization**, the system uses `serial_number` as the current pre-authorized key. The model remains extensible so product-specific identity policies can later require extra claims such as IMEI, base MAC, vendor device ID, or a device fingerprint.

### The Process:
1.  **Whitelisting:** When you ship 50 routers to "Client A", you (or the client) upload those 50 serial numbers into the CMS under "Client A's" organization.
2.  **Isolation:** In your database, these serial numbers are now linked to `TenantID: 123`.
3.  **The First Boot:** When the router powers on, it hits your HTTP `/register` API, presenting its automatically detected `serial_number` and optional identity claims.
4.  **The Lookup:** The CMS looks up the serial number. It finds it belongs to `TenantID: 123`.
5.  **Provisioning:** The CMS then issues MQTT credentials that are scoped specifically to that tenant's topics (e.g., `tenant123/device/mac/telemetry`).

**Why this is great:** It prevents "Autodiscovery" from becoming a security nightmare, and it allows the device firmware to be 100% identical. A device can only register if its hardware ID is already expected by the system.

---

## 2. Does the device need to register every time?
**No. Once is usually enough.**

For a perfect architecture, follow this "Persistence Rule":
1.  **Check Local Storage:** Upon boot, the agent checks `/etc/cms_agent/` for existing PKI certificates (`client.crt`, `client.key`, `ca.crt`).
2.  **Try MQTT:** If certificates exist and are not expired, it connects to the MQTT broker via mTLS directly.
3.  **Fallback to HTTP:** 
    *   If no certificates exist...
    *   OR if the mTLS handshake fails (meaning the CMS revoked the device's certificate)...
    *   OR if the certificate is expiring within 30 days...
    *   ...THEN it triggers the HTTP `/register` request again to obtain fresh PKI credentials.

**Benefit:** This drastically reduces the load on your .NET API. 99% of the time, devices will just go straight to MQTT.

---

## 3. No Autodiscovery (Strict Constraints)
Based on the architecture constraint that **firmware must be 100% identical** across all devices, "autodiscovery" or "claim codes" are not utilized.

*   **No Organization Tokens:** We cannot bake a tenant-specific token into the firmware.
*   **No Global Pools:** Devices are never allowed to register without first being explicitly added to a specific tenant's inventory by an administrator.

The system relies entirely on the **Pre-Assignment Workflow** detailed in Section 1.

---

## Scalable Database Schema (Simplified)

To support this in .NET, your database needs to handle the hierarchy:

*   **Tenants (Clients):** `Id`, `Name`, `Slug`, `MaxDevices`.
*   **Hardware_Inventory:** `SerialNumber`, `ProductId`, `TenantId` (Required, FK to Tenants), `Status`, `IdentityPolicy`, optional `IdentityClaims`.
*   **Devices (Active):** `Id`, `HardwareInventoryId`, `TenantId`, `Status`, `LastSeenAt`.
*   **DeviceCertificates:** `Id`, `DeviceId`, `Thumbprint`, `ExpiresAt`, `IsActive`.

### The Logic:
*   Every device in `Hardware_Inventory` **must** belong to a `TenantId`. There are no "unassigned" devices.
*   When a device hits the `/register` API, its `serial_number` is validated against the inventory record. If the inventory/product has an `IdentityPolicy`, the CMS also validates the required optional claims. The device automatically inherits the `TenantId` from that record.
*   Authentication after registration is handled entirely via **mTLS PKI certificates**, not passwords. See [provisioning_architecture.md](provisioning_architecture.md) for full details.
