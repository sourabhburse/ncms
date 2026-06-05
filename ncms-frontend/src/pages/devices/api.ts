import { request } from "@/http/axios"
import type { DeviceTelemetry } from "@/pages/telemetry/api"

// Re-export the telemetry model so the details page depends on one devices
// surface rather than importing the telemetry feature directly.
export type { DeviceTelemetry }

// ─────────────────────────────────────────────────────────────────────────────
// Devices — data layer
// Model mirrors GET /api/v1/devices exactly.
// ─────────────────────────────────────────────────────────────────────────────

/** A managed network device. */
export interface Device {
  id: string
  hardwareInventoryId: string
  serialNumber: string
  tenantId: string
  name: string | null
  status: string
  lastSeenAt: string | null
  currentFirmwareVersion: string | null
  currentAgentVersion: string | null
  wanIpAddress: string | null
  /** Interface → MAC address map (may be empty). */
  macAddresses: Record<string, string>
  latitude: number | null
  longitude: number | null
  notes: string | null
  createdAt: string
  updatedAt: string
}

// ── API functions ──────────────────────────────────────────────────────────────

/** Fetch all devices. */
export function fetchDevices() {
  return request<Device[]>({ url: "/devices", method: "get" })
}

/** Fetch a single device by id (used by the details page). */
export function fetchDevice(id: string) {
  return request<Device>({ url: `/devices/${id}`, method: "get" })
}

// ── Device telemetry (History & Analysis tab) ───────────────────────────────────

/** Server-side pagination params for GET /api/v1/device-telemetry. */
export interface FetchTelemetryPageParams {
  /** Restrict to one device by its id (preferred) … */
  deviceId?: string
  /** … or by serial number. */
  serialNumber?: string
  page: number
  pageSize: number
  /** Legacy upper bound on rows; the API expects 100 for normal paging. */
  limit?: number
  /** Only records strictly older than this "YYYY-MM-DD HH:mm:ss" instant. */
  before?: string
}

/**
 * Fetch one page of telemetry for a device, newest-first.
 *
 * The endpoint returns a plain array (no total-count envelope), so callers
 * estimate the pager total from whether a full page came back.
 */
export function fetchDeviceTelemetry(params: FetchTelemetryPageParams) {
  return request<DeviceTelemetry[]>({
    url: "/device-telemetry",
    method: "get",
    params: { limit: 100, ...params }
  })
}

/**
 * History table columns — every telemetry field except `id`/`deviceId`,
 * keyed off `DeviceTelemetry` so it stays in sync with the model.
 */
export const TELEMETRY_TABLE_COLUMNS: ReadonlyArray<{ key: keyof DeviceTelemetry, label: string }> = [
  { key: "timestamp", label: "Timestamp" },
  { key: "cpuUsagePercent", label: "CPU Usage (%)" },
  { key: "ramUsageMb", label: "RAM Used (MB)" },
  { key: "ramTotalMb", label: "RAM Total (MB)" },
  { key: "storageUsedMb", label: "Storage Used (MB)" },
  { key: "storageTotalMb", label: "Storage Total (MB)" },
  { key: "uptimeSeconds", label: "Uptime (s)" },
  { key: "wanIp", label: "WAN IP" },
  { key: "temperatureCelsius", label: "Temperature (°C)" },
  { key: "signalStrengthRssi", label: "Signal Strength (dBm)" },
  { key: "signalQualityRsrp", label: "Signal Quality (dBm)" }
] as const
