<script lang="ts" setup>
// ─────────────────────────────────────────────────────────────────────────────
// Device Details — dedicated page (no dialog). Reached via the View action on
// the Devices table: /devices/:id. Layout follows the provided screenshot:
// a Back bar, a tab strip (Overview active), and a Device Details panel made of
// highlighted tiles + plain rows, with a picture placeholder on the right.
// ─────────────────────────────────────────────────────────────────────────────
import {
  ArrowLeft, Clock, Collection, Connection, Cpu, Document, RefreshRight, Search, Setting, User
} from "@element-plus/icons-vue"
import { formatDateTime } from "@@/utils/datetime"
import { usePagination } from "@@/composables/usePagination"
import { useApiRequest } from "@@/composables/useApiRequest"
import type { Device, DeviceTelemetry } from "./api"
import { TELEMETRY_TABLE_COLUMNS, fetchDevice, fetchDeviceTelemetry } from "./api"
import { formatCoordinates, formatMacAddresses, statusTagType } from "./helpers"

const route  = useRoute()
const router = useRouter()

const deviceId = computed(() => route.params.id as string)

const device    = ref<Device | null>(null)
const activeTab = ref("overview")

/** The device is "connected" only while its status is active/online. */
const isConnected = computed(() => (device.value?.status ?? "").toUpperCase() === "ACTIVE")

// The interceptor toasts errors; we keep `error` for the inline alert/empty state.
const { loading, error, execute: runFetchDevice } = useApiRequest(fetchDevice, {
  fallbackError: "Failed to load device."
})

async function loadDevice() {
  device.value = (await runFetchDevice(deviceId.value)) ?? null
}

function goBack() {
  router.back()
}

// ── History & Analysis tab — server-paginated telemetry ────────────────────────
const { paginationData, handleCurrentChange, handleSizeChange } = usePagination({
  pageSize: 50,
  pageSizes: [20, 50, 100],
  layout: "sizes, prev, pager, next"
})

const telemetryRows    = ref<DeviceTelemetry[]>([])
const telemetryLoading = ref(false)
/** Whether the History tab has been opened at least once (lazy-load guard). */
const historyLoaded    = ref(false)

// ── Filters — mapped 1:1 to the device-telemetry query params ──────────────────
/** Free-text serial-number filter (API `serialNumber`). */
const serialFilter = ref("")
/** Upper-bound instant (API `before`), ISO "YYYY-MM-DDTHH:mm:ss" or "". */
const beforeFilter = ref("")

/** Fetch the current page of telemetry honouring the active filters. */
async function loadTelemetry() {
  telemetryLoading.value = true
  try {
    const rows = await fetchDeviceTelemetry({
      serialNumber: serialFilter.value.trim() || undefined,
      before: beforeFilter.value || undefined,
      page: paginationData.currentPage,
      pageSize: paginationData.pageSize
    })
    telemetryRows.value = rows
    // The endpoint returns no total; estimate it so prev/next stay usable —
    // a full page implies "there may be more", a short page is the last.
    const consumed = (paginationData.currentPage - 1) * paginationData.pageSize
    paginationData.total = consumed + rows.length
      + (rows.length === paginationData.pageSize ? paginationData.pageSize : 0)
  } catch {
    // The axios interceptor already surfaces the error toast.
    telemetryRows.value = []
  } finally {
    telemetryLoading.value = false
  }
}

function onPageChange(page: number) {
  handleCurrentChange(page)
  loadTelemetry()
}

function onSizeChange(size: number) {
  handleSizeChange(size)
  paginationData.currentPage = 1
  loadTelemetry()
}

/** A filter change always returns to the first page, then refetches. */
function onFilterChange() {
  paginationData.currentPage = 1
  loadTelemetry()
}

// Lazy-load: fetch telemetry only the first time the History tab is opened.
watch(activeTab, (tab) => {
  if (tab === "history-analysis" && !historyLoaded.value) {
    historyLoaded.value = true
    loadTelemetry()
  }
})

onMounted(loadDevice)
</script>

<template>
  <div class="app-container device-detail">

    <!-- ── Back bar ──────────────────────────────────────────────────────────── -->
    <div class="detail-back" @click="goBack">
      <el-icon><ArrowLeft /></el-icon>
      <span>Back</span>
    </div>

    <!-- ── Tabbed card ───────────────────────────────────────────────────────── -->
    <div class="detail-card">
      <el-tabs v-model="activeTab" class="app-tabs">

        <!-- ── Overview ────────────────────────────────────────────────────── -->
        <el-tab-pane label="Overview" name="overview">
          <div
            v-loading="loading"
            element-loading-background="rgba(255,255,255,0.6)"
            class="overview"
          >
            <!-- Load error -->
            <el-alert
              v-if="error && !loading"
              :title="error"
              type="error"
              show-icon
              :closable="false"
              class="overview__alert"
            />

            <template v-if="device && !error">
              <!-- Connection status banner (shown when not connected) -->
              <el-alert
                v-if="!isConnected"
                type="error"
                show-icon
                :closable="false"
                class="overview__alert"
              >
                <div class="conn-alert">
                  <span>The device connection has been disconnected, please try again later</span>
                  <el-icon class="conn-alert__refresh" @click="loadDevice"><RefreshRight /></el-icon>
                </div>
              </el-alert>

              <!-- Section header -->
              <div class="section-head">
                <span class="section-head__bar" />
                <h3 class="section-head__title">Device Details</h3>
              </div>

              <div class="overview-body mt-4">
                <div class="overview-main">
                  <!-- Highlighted tiles — el-row/el-col handle the responsive columns -->
                  <el-row :gutter="16" class="tile-row">
                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><Collection /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value">{{ device.name || device.serialNumber }}</div>
                          <div class="tile__label">Device Name</div>
                        </div>
                      </div>
                    </el-col>

                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><Document /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value mono">{{ device.serialNumber }}</div>
                          <div class="tile__label">Device Code</div>
                        </div>
                      </div>
                    </el-col>

                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><Connection /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value">
                            <el-tag :type="statusTagType(device.status)" size="small">
                              {{ device.status }}
                            </el-tag>
                          </div>
                          <div class="tile__label">Device Status</div>
                        </div>
                      </div>
                    </el-col>

                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><Cpu /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value">{{ device.currentFirmwareVersion || "—" }}</div>
                          <div class="tile__label">Firmware Version</div>
                        </div>
                      </div>
                    </el-col>

                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><Setting /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value">{{ device.currentAgentVersion || "—" }}</div>
                          <div class="tile__label">Agent Version</div>
                        </div>
                      </div>
                    </el-col>

                    <el-col :xs="24" :sm="12" :md="8" class="tile-col">
                      <div class="tile">
                        <div class="tile__icon"><el-icon><User /></el-icon></div>
                        <div class="tile__text">
                          <div class="tile__value mono">{{ device.tenantId }}</div>
                          <div class="tile__label">Tenant</div>
                        </div>
                      </div>
                    </el-col>
                  </el-row>

                  <!-- Plain rows -->
                  <div class="info-rows">
                    <el-row :gutter="32">
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">WAN IP</span>
                        <span class="info-row__value mono">{{ device.wanIpAddress || "—" }}</span>
                      </div>
                    </el-col>
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">MAC</span>
                        <span class="info-row__value">{{ formatMacAddresses(device.macAddresses) }}</span>
                      </div>
                    </el-col>
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">Last Seen</span>
                        <span class="info-row__value">
                          {{ device.lastSeenAt ? formatDateTime(device.lastSeenAt) : "—" }}
                        </span>
                      </div>
                    </el-col>
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">Location</span>
                        <span class="info-row__value">
                          {{ formatCoordinates(device.latitude, device.longitude) }}
                        </span>
                      </div>
                    </el-col>
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">Created At</span>
                        <span class="info-row__value">{{ formatDateTime(device.createdAt) }}</span>
                      </div>
                    </el-col>
                    <el-col :xs="24" :sm="12" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">Updated At</span>
                        <span class="info-row__value">{{ formatDateTime(device.updatedAt) }}</span>
                      </div>
                    </el-col>
                    <el-col :span="24" class="info-col">
                      <div class="info-row">
                        <span class="info-row__label">Remarks</span>
                        <span class="info-row__value">{{ device.notes || "—" }}</span>
                      </div>
                    </el-col>
                    </el-row>
                  </div>
                </div>

                <!-- Picture placeholder -->
                <div class="overview-pic">No device picture is available</div>
              </div>
            </template>

            <!-- Not found -->
            <el-empty
              v-if="!loading && !error && !device"
              description="Device not found"
              :image-size="80"
            />
          </div>
        </el-tab-pane>
        <!-- ── History & Analysis ──────────────────────────────────────────── -->
        <el-tab-pane label="History & Analysis" name="history-analysis">
          <div class="history">
            <!-- Filters (top of the tab) — mapped to the API query params -->
            <div class="history__filters">
              <el-input
                v-model="serialFilter"
                placeholder="Search by serial number…"
                clearable
                class="history__search"
                @keyup.enter="onFilterChange"
                @clear="onFilterChange"
              >
                <template #append>
                  <el-button :icon="Search" @click="onFilterChange" />
                </template>
              </el-input>
              <el-date-picker
                v-model="beforeFilter"
                type="datetime"
                :prefix-icon="Clock"
                placeholder="Before (records older than)"
                value-format="YYYY-MM-DDTHH:mm:ss"
                clearable
                class="history__before"
                @change="onFilterChange"
              />
              <el-button
                :icon="RefreshRight"
                :loading="telemetryLoading"
                @click="loadTelemetry"
              >
                Refresh
              </el-button>
            </div>

            <!-- Table only (no chart) -->
            <div class="app-table">
              <el-table
                v-loading="telemetryLoading"
                :data="telemetryRows"
                height="460"
                style="width: 100%"
              >
                <el-table-column type="index" label="Index" width="60" align="center" fixed />
                <el-table-column
                  v-for="col in TELEMETRY_TABLE_COLUMNS"
                  :key="col.key"
                  :label="col.label"
                  :min-width="col.key === 'timestamp' ? 190 : 130"
                  show-overflow-tooltip
                >
                  <template #default="{ row }">
                    <span v-if="col.key === 'timestamp'" class="app-mono">
                      {{ formatDateTime(row.timestamp) }}
                    </span>
                    <span v-else>{{ row[col.key] }}</span>
                  </template>
                </el-table-column>

                <template #empty>
                  <el-empty description="No telemetry data" :image-size="80" />
                </template>
              </el-table>
            </div>

            <!-- Server-side pagination -->
            <div class="history__pager">
              <el-pagination
                background
                :layout="paginationData.layout"
                :total="paginationData.total"
                :page-sizes="paginationData.pageSizes"
                :current-page="paginationData.currentPage"
                :page-size="paginationData.pageSize"
                :disabled="telemetryLoading || telemetryRows.length === 0"
                @current-change="onPageChange"
                @size-change="onSizeChange"
              />
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.device-detail {
  padding: 0;
}

.el-alert {
  margin: 20px 0 0;
}
.el-alert:first-child {
  margin: 0;
  margin-bottom: 20px;
}

// ── Back bar ─────────────────────────────────────────────────────────────────
.detail-back {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 14px 20px;
  font-size: 15px;
  color: var(--el-text-color-primary);
  cursor: pointer;

  &:hover { color: var(--el-color-primary); }
}

// ── Card ───────────────────────────────────────────────────────────────────────
.detail-card {
  background-color: var(--el-bg-color);
  border-radius: var(--radius-card);
  margin: 0 var(--spacing-5) var(--spacing-5);
  box-shadow: var(--shadow-card);
  overflow: hidden;
}

// Tab bar styling now comes from the global `.app-tabs` primitive.

.overview {
  min-height: 360px;
  padding: 24px;
  background-color: var(--el-bg-color);
  border-radius: 8px;

  &__alert { margin-bottom: 20px; }

  @media (max-width: 767px) { padding: 16px; }
}

// Section header styling now comes from the global `.section-head` primitive.

// ── Body: details (left) + picture (right) ──────────────────────────────────────
.overview-body {
  display: flex;
  gap: 24px;

  @media (max-width: 991px) {
    flex-direction: column;
    gap: 20px;
  }
}

.overview-main {
  flex: 1;
  min-width: 0;
}

.overview-pic {
  flex: 0 0 320px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-secondary);
  font-size: 14px;
  background: linear-gradient(135deg, #fafcff 0%, #f3f7fd 100%);
  border-radius: 6px;
  min-height: 260px;

  @media (max-width: 991px) { flex: 1 1 auto; min-height: 160px; }
}

// ── Highlighted tiles ────────────────────────────────────────────────────────────
// el-row/el-col own the responsive columns + horizontal gutter; we only add the
// vertical gap between wrapped rows and the section spacing below.
.tile-row {
  margin-bottom: 8px;
}

.tile-col {
  margin-bottom: 20px;
}

.tile {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;

  &__icon {
    flex-shrink: 0;
    width: 48px;
    height: 48px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: var(--color-brand-soft);
    color: var(--el-color-primary);
    font-size: 22px;
  }

  &__text { min-width: 0; }

  &__value {
    font-size: 15px;
    font-weight: 700;
    color: var(--el-text-color-primary);
    line-height: 1.35;
    word-break: break-word;
  }

  &__label {
    font-size: 13px;
    color: var(--el-text-color-secondary);
    margin-top: 2px;
  }
}

// ── Plain rows ────────────────────────────────────────────────────────────────────
// el-row/el-col handle the two-column split (single column on xs); we keep the
// per-row vertical rhythm here, plus a divider separating this section from the tiles.
.info-rows {
  padding-top: 24px;
  
}

.info-col {
  margin-bottom: 14px;
}

.info-row {
  display: flex;
  align-items: baseline;
  gap: 10px;
  font-size: 14px;
  line-height: 1.5;

  &__label {
    flex-shrink: 0;
    min-width: 130px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  &__value {
    color: var(--el-text-color-regular);
    word-break: break-word;
  }
}

// ── History & Analysis tab ──────────────────────────────────────────────────────
.history {
  padding: 20px;
  background-color: var(--el-bg-color);
  border-radius: 8px;

  &__filters {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
    margin-bottom: 16px;
  }

  &__search {
    flex: 1 1 280px;
    max-width: 360px;
    min-width: 220px;
  }

  &__before {
    flex: 0 1 260px;
    min-width: 200px;
  }

  // Table styling comes from the global `.app-table` primitive; timestamp cells
  // use `.app-mono`.

  &__pager {
    display: flex;
    justify-content: center;
    margin-top: var(--spacing-4);
  }
}
</style>
