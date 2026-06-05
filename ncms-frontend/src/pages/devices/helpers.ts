// ─────────────────────────────────────────────────────────────────────────────
// Devices — presentation helpers shared by the table and details dialog.
// ─────────────────────────────────────────────────────────────────────────────

import type { Device } from "./api"

/** Map a device status to an Element Plus tag type. */
export function statusTagType(status: string): "success" | "info" | "warning" | "danger" {
  switch (status.toUpperCase()) {
    case "ONLINE":
      return "success"
    case "PROVISIONING":
    case "PENDING":
      return "warning"
    case "DECOMMISSIONED":
    case "BLOCKED":
    case "OFFLINE":
      return "danger"
    default:
      return "info"
  }
}

/** Render the interface→MAC map as a readable string, or a dash when empty. */
export function formatMacAddresses(macs: Device["macAddresses"]): string {
  const entries = Object.entries(macs ?? {})
  if (!entries.length) return "—"
  return entries.map(([iface, mac]) => `${iface}: ${mac}`).join("  ·  ")
}

/** Render latitude/longitude as a coordinate pair, or a dash when unset. */
export function formatCoordinates(lat: number | null, lng: number | null): string {
  if (lat === null || lng === null) return "—"
  return `${lat}, ${lng}`
}
