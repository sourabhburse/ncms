import { request } from "@/http/axios"

// ── Domain types ───────────────────────────────────────────────────────────────

export interface DeviceTelemetry {
  id: string
  deviceId: string
  timestamp: string
  cpuUsagePercent: number
  ramUsageMb: number
  ramTotalMb: number
  storageUsedMb: number
  storageTotalMb: number
  uptimeSeconds: number
  wanIp: string
  temperatureCelsius: number
  signalStrengthRssi: number
  signalQualityRsrp: number
}

export interface FetchTelemetryParams {
  deviceId?: string
  serialNumber?: string
  limit?: number
}

// ── API functions ──────────────────────────────────────────────────────────────

/** Fetch telemetry records. Returns array sorted newest-first (first item = latest). */
export function fetchTelemetry(params?: FetchTelemetryParams) {
  return request<DeviceTelemetry[]>({
    url: "/device-telemetry",
    method: "get",
    params: { limit: 100, ...params }
  })
}
