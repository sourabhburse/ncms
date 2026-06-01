import { request } from "@/http/axios"

// ── Domain types ───────────────────────────────────────────────────────────────

export interface Product {
  id: string
  vendorId: string
  vendorName: string
  modelName: string
  architecture: string
  configFormat: string
  configSchemaVersion: string
  createdAt: string
}

/** A single identity claim expressed as a key/value pair for form binding. */
export interface ClaimEntry {
  key: string
  value: string
}

/** Shape returned by GET /hardware-inventory */
export interface HardwareInventoryItem {
  id: string
  productId: string
  productName?: string
  serialNumber: string
  identityPolicy: string
  identityClaims: Record<string, string>
  createdAt?: string
  createdBy?: string
}

/** POST /hardware-inventory request body (tenantId excluded — managed by backend) */
export interface CreateHardwareInventoryPayload {
  productId: string
  serialNumber: string
  identityPolicy: string
  identityClaims: Record<string, string>
}

// ── API functions ──────────────────────────────────────────────────────────────

/** Fetch all available products for the product dropdown. */
export function fetchProducts() {
  return request<Product[]>({ url: "/products", method: "get" })
}

/** Fetch the full hardware inventory list. */
export function fetchHardwareInventory() {
  return request<HardwareInventoryItem[]>({ url: "/hardware-inventory", method: "get" })
}

/** Register a new hardware inventory entry. */
export function createHardwareInventory(payload: CreateHardwareInventoryPayload) {
  return request<HardwareInventoryItem>({ url: "/hardware-inventory", method: "post", data: payload })
}

/** Delete a single hardware inventory entry by id. */
export function deleteHardwareInventory(id: string) {
  return request<void>({ url: `/hardware-inventory/${id}`, method: "delete" })
}

/** Delete multiple hardware inventory entries in one call. */
export function deleteHardwareInventoryBatch(ids: string[]) {
  return request<void>({ url: "/hardware-inventory", method: "delete", data: { ids } })
}

// ── Helpers ────────────────────────────────────────────────────────────────────

/** Convert an array of ClaimEntry rows into the Record<string, string> the API expects. */
export function claimEntriesToRecord(entries: ClaimEntry[]): Record<string, string> {
  return Object.fromEntries(
    entries
      .filter(e => e.key.trim() !== "")
      .map(e => [e.key.trim(), e.value])
  )
}

/** Convert a Record<string, string> back to ClaimEntry rows for form editing. */
export function recordToClaimEntries(record: Record<string, string>): ClaimEntry[] {
  const entries = Object.entries(record).map(([key, value]) => ({ key, value }))
  return entries.length > 0 ? entries : [{ key: "", value: "" }]
}
