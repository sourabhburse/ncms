<script lang="ts" setup>
import {Search, View } from "@element-plus/icons-vue"
import { formatDateTime } from "@@/utils/datetime"
import { useApiRequest } from "@@/composables/useApiRequest"
import type { Device } from "./api"
import { fetchDevices } from "./api"
import { formatMacAddresses, statusTagType } from "./helpers"

const router = useRouter()

// ── Table state ──────────────────────────────────────────────────────────────

const devices     = ref<Device[]>([])
const searchQuery = ref("")

const filteredList = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return devices.value
  return devices.value.filter(d =>
    d.serialNumber.toLowerCase().includes(q) ||
    (d.name ?? "").toLowerCase().includes(q) ||
    d.status.toLowerCase().includes(q) ||
    (d.wanIpAddress ?? "").toLowerCase().includes(q)
  )
})

// The interceptor toasts errors; we keep `error` for the inline empty state.
const { loading, error, execute: runFetchDevices } = useApiRequest(fetchDevices, {
  fallbackError: "Failed to load devices."
})

async function loadDevices() {
  devices.value = (await runFetchDevices()) ?? []
}

// ── View action — navigate to the dedicated details page ──────────────────────

function handleView(row: Device) {
  router.push({ name: "DeviceDetails", params: { id: row.id } })
}

onMounted(loadDevices)
</script>

<template>
  <div class="app-container">

    <!-- ── Page header ──────────────────────────────────────────────────────── -->
    <div class="page-head">
      <div class="page-head__content">
        <h1 class="page-head__title">Devices</h1>
        <p class="page-head__subtitle">View registered devices and their current status.</p>
      </div>
    </div>

    <!-- ── Toolbar ──────────────────────────────────────────────────────────── -->
    <div class="page-toolbar">
      <div class="page-toolbar__actions" />
      <el-input
        v-model="searchQuery"
        placeholder="Search serial, name, status, IP…"
        clearable
        class="page-toolbar__search"
      >
        <template #append>
          <el-button :icon="Search" />
        </template>
      </el-input>
    </div>
    <!-- ── Devices table (all fields except id & hardwareInventoryId) ───────── -->
    <div class="app-table mt-5">
      <el-table
        v-loading="loading"
        :data="filteredList"
        style="width: 100%"
      >
        <el-table-column type="index" label="Index" width="65" align="center" fixed />

        <el-table-column label="Serial Number" min-width="140" show-overflow-tooltip fixed>
          <template #default="{ row }">
            <span class="app-mono">{{ row.serialNumber }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Name" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ row.name || "—" }}</template>
        </el-table-column>

        <el-table-column label="Status" width="120" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column label="Tenant ID" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="app-mono">{{ row.tenantId }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Firmware" min-width="100" show-overflow-tooltip>
          <template #default="{ row }">{{ row.currentFirmwareVersion || "—" }}</template>
        </el-table-column>

        <el-table-column label="Agent" min-width="100" show-overflow-tooltip>
          <template #default="{ row }">{{ row.currentAgentVersion || "—" }}</template>
        </el-table-column>

        <el-table-column label="WAN IP" min-width="140" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="app-mono">{{ row.wanIpAddress || "—" }}</span>
          </template>
        </el-table-column>

        <el-table-column label="MAC Addresses" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">{{ formatMacAddresses(row.macAddresses) }}</template>
        </el-table-column>

        <el-table-column label="Latitude" min-width="110" show-overflow-tooltip>
          <template #default="{ row }">{{ row.latitude ?? "—" }}</template>
        </el-table-column>

        <el-table-column label="Longitude" min-width="110" show-overflow-tooltip>
          <template #default="{ row }">{{ row.longitude ?? "—" }}</template>
        </el-table-column>

        <el-table-column label="Notes" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ row.notes || "—" }}</template>
        </el-table-column>

        <el-table-column label="Last Seen" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.lastSeenAt ? formatDateTime(row.lastSeenAt) : "—" }}
          </template>
        </el-table-column>

        <el-table-column label="Created At" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>

        <el-table-column label="Updated At" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>

        <el-table-column label="Action" width="110" fixed="right" align="center">
          <template #default="{ row }">
            <el-button link type="primary" size="small" :icon="View" @click="handleView(row)">
              View
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty
        v-if="!loading && !error && filteredList.length === 0"
        description="No devices found"
        :image-size="80"
        class="devices-table-empty"
      />
    </div>

  </div>
</template>

<style lang="scss" scoped>
// ── page-head spacing inside app-container ────────────────────────────────────
// Table styling now comes from the global `.app-table` primitive; mono cells
// from `.app-mono`. Only page-specific bits remain here.
.page-head {
  margin: -20px -20px 16px; // bleed to app-container edges
}

.devices-table-empty {
  padding: var(--spacing-10) 0;
  background-color: var(--color-surface-sunken);
}
</style>
