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
  status?: string
  identityPolicy: string
  identityClaims: Record<string, string>
  createdAt?: string
  createdBy?: string
}

/** POST /hardware-inventory request body (tenantId excluded — managed by backend) */
export interface CreateHardwareInventoryPayload {
  tenantId: string
  productId: string
  serialNumber: string
  identityPolicy: string
  identityClaims: Record<string, string>
}

/** PUT /hardware-inventory/{id} request body. */
export interface UpdateHardwareInventoryPayload {
  status: string
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

/** Update a hardware inventory entry's status / identity policy / claims. */
export function updateHardwareInventory(id: string, payload: UpdateHardwareInventoryPayload) {
  return request<HardwareInventoryItem>({ url: `/hardware-inventory/${id}`, method: "put", data: payload })
}

/** Delete a single hardware inventory entry by id. */
export function deleteHardwareInventory(id: string) {
  return request<void>({ url: `/hardware-inventory/${id}`, method: "delete" })
}

/** Delete multiple hardware inventory entries in one call. */
export function deleteHardwareInventoryBatch(ids: string[]) {
  return request<void>({ url: "/hardware-inventory/batch", method: "delete", data: { ids } })
}

/** Download the CSV import template for a product (returns the file as a Blob). */
export function downloadHardwareInventoryTemplate(productId: string) {
  return request<Blob>({
    url: "/hardware-inventory/template",
    method: "get",
    params: { productId },
    responseType: "blob"
  })
}

/** Bulk-import hardware inventory entries from a CSV file (multipart/form-data). */
export function importHardwareInventoryCsv(payload: { file: File, productId: string, tenantId: string }) {
  const formData = new FormData()
  formData.append("File", payload.file)
  formData.append("ProductId", payload.productId)
  formData.append("TenantId", payload.tenantId)
  return request<unknown>({
    url: "/hardware-inventory/import-csv",
    method: "post",
    data: formData
  })
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
