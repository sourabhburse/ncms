# NCMS - Niseva Cloud Management System

NCMS is a centralized management platform for OpenWrt-based routers and IoT gateways. It provides remote provisioning, telemetry monitoring, configuration management, and firmware over-the-air (FOTA) updates.

## Project Structure

This repository is organized into four main sub-projects:

- **[ncms-backend](./ncms-backend)**: ASP.NET Core 7.0 API and background workers. Handles multi-tenancy, PKI, device registration, and data ingestion.
- **[ncms-frontend](./ncms-frontend)**: Vue 3 + TypeScript dashboard for fleet management and visualization.
- **[ncms-agent](./ncms-agent)**: Lightweight C-based daemon for OpenWrt devices.
- **[ncms-infra](./ncms-infra)**: Infrastructure-as-Code, including Docker Compose, MQTT broker configuration (EMQX), and database setup (PostgreSQL/TimescaleDB).

## Core Architecture

1.  **Device Provisioning**: Secure registration via HTTPS with IMEI/Serial verification.
2.  **Communication**: MQTT over TLS (mTLS) for real-time operations.
3.  **Multi-Tenancy**: Strict isolation of data and control plane by tenant.
4.  **Configuration**: Canonical JSON models translated to native UCI commands.

## Getting Started & Build Instructions

### 1. ncms-backend (.NET 7)
Navigate to the backend folder and use the .NET CLI:
```bash
cd ncms-backend
dotnet restore
dotnet build
dotnet run --project src/NCMS.Backend.API
```

### 2. ncms-frontend (Vue 3 + Vite)
Navigate to the frontend folder and use NPM:
```bash
cd ncms-frontend
npm install
npm run dev     # for local development
npm run build   # for production build
```

### 3. ncms-infra (Docker)
Start the database, MQTT broker, and other infra:
```bash
cd ncms-infra
docker-compose up -d
```

### 4. ncms-agent (OpenWrt C Daemon)
The agent requires an OpenWrt buildroot to cross-compile. See `ncms-agent/README.md` for details.
