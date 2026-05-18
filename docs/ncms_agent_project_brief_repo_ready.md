# NCMS Project Brief for Implementation Agent

## Project Overview

NCMS is a centralized remote cloud management system for OpenWrt-based routers and IoT gateways. It is conceptually similar to Robustel Cloud Manager Service and OpenWISP, but this project is intended as a custom multi-tenant platform made of two independent systems: a cloud CMS/backend and a lightweight OpenWrt device agent. (sources: implementation_plan.md, features.md)

The platform must support remote provisioning, telemetry, configuration management, firmware over-the-air updates, alerting, security, and tenant isolation from day one. The architecture is planning-first: documentation and architecture decisions live in this repository, while the actual backend and device agent implementations should live in separate code repositories. (sources: features.md, provisioning_architecture.md, multi_tenant_strategy.md)

## Product Goals

The system should provide a single pane of glass for managing fleets of routers and gateways across multiple clients. Core capabilities include zero-touch provisioning, health monitoring, telemetry history, configuration templates, OTA firmware updates, diagnostics, audit logs, alerts, and secure remote operations. (source: features.md)

The system is multi-tenant by design. Every device, API action, database record, MQTT permission, and user permission must be scoped to a tenant. Tenant isolation is not optional and should be treated as a top-level architectural constraint in every subsystem. (sources: multi_tenant_strategy.md, database_schema.md)

## Core Architecture

The solution has two major components:

1. **CMS Backend and Dashboard**: a cloud-hosted ASP.NET Core-based management platform with REST APIs, background jobs, user access control, device lifecycle management, configuration workflows, PKI services, telemetry ingestion, and a web dashboard. (sources: implementation_plan.md, provisioning_architecture.md)
2. **Device Agent**: a lightweight daemon running on OpenWrt devices, ideally written in C for small footprint, packaged as an `.ipk`, and responsible for registration, MQTT connectivity, telemetry reporting, config application, command execution, and OTA handling. (sources: implementation_plan.md, openwrt_package_guide.md)

Communication should follow this model:

- HTTPS for first-time registration and certificate renewal. (source: provisioning_architecture.md)
- MQTT over TLS with mutual TLS for operational communication such as telemetry, commands, config pushes, and OTA notifications. (sources: provisioning_architecture.md, implementation_plan.md)
- HTTPS signed URLs for downloading firmware binaries and large payloads. (sources: provisioning_architecture.md, implementation_plan.md)

## Non-Negotiable Constraints

The firmware must remain 100 percent identical across all clients and all devices of the same product line. No tenant-specific token, secret, or client-specific customization may be baked into the firmware image. (sources: provisioning_architecture.md, multi_tenant_strategy.md)

Devices may not self-discover or claim arbitrary tenants. A device can only register if its serial number already exists in the CMS allow-list under a specific tenant. Optional product identity policies can later require extra claims such as IMEI, base MAC, vendor device ID, or device fingerprint. This pre-assignment model is the foundation of secure multi-tenant provisioning. (sources: multi_tenant_strategy.md, provisioning_architecture.md)

Operational device authentication must be certificate-based. After registration, the CMS issues a unique X.509 client certificate to the device, and all MQTT communication thereafter uses mTLS rather than passwords. (sources: provisioning_architecture.md, multi_tenant_strategy.md)

## Provisioning Model

Provisioning begins on the CMS side when an administrator imports or creates hardware inventory records containing serial number, product model, tenant assignment, and optional identity policy claims. These records begin in `PENDING_ACTIVATION` style state and may optionally be limited by an activation window. (sources: provisioning_architecture.md, database_schema.md)

When a device boots, the agent first checks whether valid certificates already exist on disk. If certificates are present and not close to expiry, the agent connects directly to MQTT over mTLS. If no certificate exists, the certificate is expiring, or the mTLS handshake fails, the agent falls back to the HTTPS registration flow. (sources: provisioning_architecture.md, multi_tenant_strategy.md)

The registration endpoint validates the serial number against the allow-list, applies any configured identity policy, creates a device record, generates a UUID identity, issues an ECC P-256 client certificate, stores certificate metadata, and returns the PKI bundle plus MQTT connection details to the device. (source: provisioning_architecture.md)

## Device Agent Responsibilities

The OpenWrt agent should be designed as a resilient background daemon with these modules: (source: implementation_plan.md)

- **Provisioning module**: collect hardware identity and register over HTTPS when needed. (source: provisioning_architecture.md)
- **MQTT session manager**: maintain mTLS MQTT connection, reconnect with backoff, and subscribe/publish to device-scoped topics. (source: provisioning_architecture.md)
- **Telemetry engine**: publish CPU, RAM, storage, uptime, interface statistics, WAN state, and optional cellular or GPS metrics on a schedule. (sources: implementation_plan.md, database_schema.md)
- **Configuration manager**: receive config payloads, apply changes safely, verify continued connectivity, and rollback if the device becomes unreachable after changes. (sources: implementation_plan.md, config_abstraction_layer.md)
- **Command executor**: handle commands like reboot, log collection, diagnostics, and future remote operations. (sources: implementation_plan.md, features.md)
- **OTA manager**: download firmware via HTTPS, verify checksum, perform upgrade, and report status transitions. (sources: implementation_plan.md, database_schema.md)

The agent must be offline-resilient. If the cloud is unreachable, it should continue local operation, retry connections gracefully, and preserve enough local state to recover safely when connectivity returns. (sources: provisioning_architecture.md, implementation_plan.md)

## CMS Backend Responsibilities

The backend is the control plane for the fleet and should expose APIs and background workflows for:

- tenant, user, role, and permission management. (source: database_schema.md)
- inventory allow-list management using serial number with optional extensible identity claims. (sources: provisioning_architecture.md, database_schema.md)
- secure provisioning and device registration. (source: provisioning_architecture.md)
- PKI lifecycle management, including issuance, expiry tracking, renewal, and revocation. (sources: provisioning_architecture.md, database_schema.md)
- device registry, grouping, tagging, and status tracking. (source: database_schema.md)
- telemetry ingestion, historical storage, and alert evaluation. (sources: implementation_plan.md, database_schema.md)
- configuration templates, desired vs reported state tracking, and push workflows. (sources: database_schema.md, config_abstraction_layer.md)
- firmware repository, OTA campaigns, and per-device rollout status. (sources: implementation_plan.md, database_schema.md)
- audit logging for compliance and forensics. (sources: provisioning_architecture.md, database_schema.md, features.md)

The backend should also include worker processes for consuming MQTT messages, evaluating alert rules, writing telemetry, managing certificate renewals, and executing campaign workflows. (sources: provisioning_architecture.md, implementation_plan.md)

## Multi-Tenant Model

Every device belongs to a tenant from the moment it is entered into `HardwareInventory`. There are no unassigned production devices in the system. On successful registration, the device inherits its tenant from the inventory record. (sources: multi_tenant_strategy.md, database_schema.md)

Tenant boundaries must be enforced in all layers:

- database row ownership and queries, (source: database_schema.md)
- API authorization and RBAC, (sources: features.md, database_schema.md)
- MQTT ACL rules mapped to device certificate identity, (source: provisioning_architecture.md)
- OTA access and configuration scope, (sources: implementation_plan.md, database_schema.md)
- dashboard visibility and reporting. (sources: features.md, database_schema.md)

Do not expose tenant topology to the device unless operationally required. The design prefers device-scoped topics and broker-side ACL enforcement so a compromised device cannot enumerate sibling devices in the same tenant. (source: provisioning_architecture.md)

## Configuration Strategy

The CMS should not store only vendor-native config formats. Instead, it should store a canonical, vendor-neutral JSON model for device intent, then translate it into vendor-specific formats such as OpenWrt UCI commands or other vendor adapters as needed. (source: config_abstraction_layer.md)

The recommended direction for this project is CMS-side translation. The backend converts canonical config into the native payload for the target product model, caches the translated payload, and pushes it to the device. The device agent remains small and focused on execution and safety rather than deep translation logic. (source: config_abstraction_layer.md)

Configuration changes that could affect connectivity must use a dead-man-switch rollback pattern: apply temporarily, verify that MQTT connectivity still works within a timeout window, and revert automatically if the new config breaks cloud reachability. (sources: config_abstraction_layer.md, implementation_plan.md)

## Data Model Summary

The database should support the following major domains: (source: database_schema.md)

- **Organization and access**: Tenants, Users, Roles, Permissions. (source: database_schema.md)
- **Hardware catalog**: Vendors, Products. (source: database_schema.md)
- **Provisioning**: HardwareInventory, Devices, DeviceCertificates. (sources: database_schema.md, provisioning_architecture.md)
- **Operations**: DeviceGroups, DeviceGroupMembers, DeviceTags. (source: database_schema.md)
- **Telemetry and alerts**: DeviceTelemetry, AlertRules, Alerts. (source: database_schema.md)
- **Configuration**: ConfigurationTemplates, DeviceConfigurations. (sources: database_schema.md, config_abstraction_layer.md)
- **Firmware**: FirmwareImages, FotaCampaigns, FotaCampaignDevices. (source: database_schema.md)
- **Audit**: AuditLogs. (source: database_schema.md)

Telemetry should be treated as time-series data and stored in a TimescaleDB-style hypertable model or equivalent if PostgreSQL is used. (sources: database_schema.md, implementation_plan.md)

## Recommended Technology Stack

A suitable implementation stack for this project is: (sources: implementation_plan.md, features.md, provisioning_architecture.md)

- **Backend API and workers**: ASP.NET Core Web API plus background worker services. (sources: implementation_plan.md, provisioning_architecture.md)
- **Frontend dashboard**: React with TypeScript consuming the .NET APIs. (sources: implementation_plan.md, features.md)
- **Primary database**: PostgreSQL. (sources: implementation_plan.md, database_schema.md)
- **Time-series telemetry**: TimescaleDB extension on PostgreSQL or equivalent. (sources: database_schema.md, features.md)
- **MQTT broker**: EMQX or Mosquitto, with EMQX preferred for cluster-scale deployment. (sources: implementation_plan.md, provisioning_architecture.md)
- **Cache and rate limiting**: Redis. (source: provisioning_architecture.md)
- **OTA binary storage**: S3-compatible object storage or Azure Blob Storage with signed URLs. (sources: implementation_plan.md, provisioning_architecture.md)
- **Device agent**: C with libmosquitto, libuci, and libubox/ubus, packaged as OpenWrt `.ipk`. (sources: implementation_plan.md, openwrt_package_guide.md)

## Suggested Repository Split

This planning repository should remain architecture-only. Actual implementation should be split into separate repos such as:

- `NCMS-backend`: ASP.NET Core APIs, workers, PKI, EF Core, provisioning, alerting, config and OTA logic.
- `NCMS-frontend`: React + TypeScript dashboard, authentication UI, telemetry views, config builders, campaign screens.
- `NCMS-agent`: OpenWrt C daemon, packaging files, procd init scripts, cross-compilation assets.
- `NCMS-infra`: deployment manifests, container orchestration, broker configuration, secrets integration, observability setup.

This keeps the concerns separate and aligns with the current documentation-first approach. (sources: implementation_plan.md, openwrt_package_guide.md)

## Delivery Phases

A practical implementation order is: (source: implementation_plan.md)

### Phase 1: Connectivity Proof of Concept

Build basic device registration, MQTT connectivity, a small device table, heartbeat ingestion, and a simple reboot command path. The goal is proving secure bidirectional cloud-to-device communication end to end. (source: implementation_plan.md)

### Phase 2: Telemetry and Monitoring

Expand telemetry ingestion, store time-series metrics, and build live dashboard views with charts and online/offline state. Add browser real-time updates, such as SignalR from backend to dashboard. (source: implementation_plan.md)

### Phase 3: Configuration Management

Implement canonical configuration models, translation to native payloads, desired vs reported config state, safe application, and rollback logic. This is one of the highest-risk and highest-value parts of the platform. (sources: implementation_plan.md, config_abstraction_layer.md)

### Phase 4: FOTA and Operational Workflows

Implement firmware repository, OTA campaigns, per-device rollout status, RBAC hardening, alerts, reporting, and lifecycle workflows like certificate renewal and device reset/decommission. (sources: implementation_plan.md, provisioning_architecture.md, database_schema.md)

## Guidance for the Implementation Agent

When implementing this project, the agent should follow these rules:

- Treat multi-tenancy and security as first-class requirements, not features to add later. (sources: multi_tenant_strategy.md, provisioning_architecture.md)
- Keep the OpenWrt agent minimal, resilient, and safe to run on constrained hardware. (source: implementation_plan.md)
- Prefer stateless, horizontally scalable backend APIs where possible. (source: provisioning_architecture.md)
- Use certificate-based identity for devices and avoid password-based operational auth. (source: provisioning_architecture.md)
- Model all important workflows explicitly: registration, certificate renewal, telemetry ingestion, config push, rollback, OTA campaign, decommission, and audit logging. (sources: provisioning_architecture.md, database_schema.md)
- Build UI and APIs around operational tasks that fleet managers actually need: inventory import, device lookup, bulk selection, policy/template assignment, telemetry drill-down, alert response, and campaign status visibility. (sources: features.md, implementation_plan.md)
- Separate canonical configuration intent from vendor-specific execution details. (source: config_abstraction_layer.md)
- Design for offline tolerance on the device and eventual consistency between device state and CMS state. (sources: implementation_plan.md, config_abstraction_layer.md)

## Immediate Next Steps

A good starting execution plan is:

1. Create the backend solution structure and database schema foundation for tenants, users, inventory, devices, and certificates. (sources: database_schema.md, implementation_plan.md)
2. Implement the HTTPS provisioning endpoint and PKI issuance flow. (source: provisioning_architecture.md)
3. Stand up the MQTT broker with mTLS and device ACL strategy. (source: provisioning_architecture.md)
4. Build a minimal OpenWrt agent that can register, save certs, connect to MQTT, send heartbeat telemetry, and receive one command. (sources: implementation_plan.md, provisioning_architecture.md)
5. Build the first dashboard screens for login, devices list, device detail, and online/offline status. (sources: implementation_plan.md, features.md)
6. Add telemetry storage and charting. (sources: implementation_plan.md, database_schema.md)
7. Add config management and rollback safety before attempting large-scale OTA rollout. (sources: config_abstraction_layer.md, implementation_plan.md)

## Definition of Success

The project is successful when a newly shipped router can be pre-assigned to a tenant, powered on in the field, securely register with the CMS using serial-number verification with optional product-specific identity claims, receive a unique certificate, connect over MQTT via mTLS, report telemetry, accept configuration and command updates safely, receive OTA firmware updates, and remain fully isolated within its tenant throughout its lifecycle. (sources: provisioning_architecture.md, multi_tenant_strategy.md, implementation_plan.md, features.md)
