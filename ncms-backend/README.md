# NCMS Backend

Centralized Management System (CMS) backend for OpenWrt devices.

## Technology Stack
- **Framework:** ASP.NET Core 7.0
- **Database:** PostgreSQL with TimescaleDB
- **Messaging:** MQTT (via EMQX)
- **Cache:** Redis

## Project Structure
- `src/NCMS.Backend.API`: REST APIs and SignalR hubs.
- `src/NCMS.Backend.Core`: Domain logic, entities, and interfaces.
- `src/NCMS.Backend.Infrastructure`: Database context, PKI services, and external integrations.
- `src/NCMS.Backend.Worker`: Background tasks (telemetry ingestion, alerts).

## Getting Started
1. Install .NET 7.0 SDK.
2. Run `dotnet restore`.
3. Configure connection strings in `appsettings.Development.json`.
4. Run `dotnet run --project src/NCMS.Backend.API`.
