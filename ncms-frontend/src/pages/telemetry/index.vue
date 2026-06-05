<script lang="ts" setup>
import { ElMessage } from "element-plus"
import { RefreshRight } from "@element-plus/icons-vue"
import { useTagsViewStore } from "@/pinia/stores/tags-view"
import type { DeviceTelemetry } from "./api"
import { fetchTelemetry } from "./api"

// ── State ──────────────────────────────────────────────────────────────────────

const route         = useRoute()
const tagsViewStore = useTagsViewStore()

const loading   = ref(false)
const error     = ref<string | null>(null)
const telemetry = ref<DeviceTelemetry | null>(null)
const activeTab = ref("information")

/** Serial number passed via route param (/telemetry/:serialNumber). */
const serialNumber = computed(() => route.params.serialNumber as string | undefined)

// ── Computed ───────────────────────────────────────────────────────────────────

const cpuPercent = computed(() =>
  telemetry.value ? Math.min(100, Math.round(telemetry.value.cpuUsagePercent)) : 0
)

const ramPercent = computed(() => {
  const t = telemetry.value
  if (!t || !t.ramTotalMb) return 0
  return Math.min(100, Math.round((t.ramUsageMb / t.ramTotalMb) * 100))
})

const storagePercent = computed(() => {
  const t = telemetry.value
  if (!t || !t.storageTotalMb) return 0
  return Math.min(100, Math.round((t.storageUsedMb / t.storageTotalMb) * 100))
})

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatUptime(seconds: number): string {
  if (!seconds && seconds !== 0) return "—"
  const d = Math.floor(seconds / 86400)
  const h = Math.floor((seconds % 86400) / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const parts: string[] = []
  if (d) parts.push(`${d} day${d > 1 ? "s" : ""}`)
  if (h) parts.push(`${h} hr${h > 1 ? "s" : ""}`)
  if (m) parts.push(`${m} min`)
  return parts.length ? parts.join(", ") : "< 1 min"
}

function formatTimestamp(ts: string): string {
  if (!ts) return "—"
  try { return new Date(ts).toLocaleString() } catch { return ts }
}

/** Returns el-progress status colour tier based on utilisation percentage. */
function progressStatus(pct: number): "" | "success" | "warning" | "exception" {
  if (pct >= 90) return "exception"
  if (pct >= 70) return "warning"
  return ""
}

// ── Data loading ───────────────────────────────────────────────────────────────

async function loadTelemetry() {
  loading.value = true
  error.value   = null
  try {
    const data = await fetchTelemetry({
      serialNumber: serialNumber.value,
      limit: 100
    })
    telemetry.value = Array.isArray(data) && data.length > 0 ? data[0] : null
    if (!telemetry.value) error.value = "No telemetry data available."
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : "Failed to load telemetry data."
    ElMessage.error(error.value ?? "Error")
  } finally {
    loading.value = false
    // Update the TagsView tab title to show the device serial number
    if (serialNumber.value) {
      const idx = tagsViewStore.visitedViews.findIndex(v => v.path === route.path)
      if (idx !== -1) {
        tagsViewStore.visitedViews[idx] = {
          ...tagsViewStore.visitedViews[idx],
          meta: { ...tagsViewStore.visitedViews[idx].meta, title: serialNumber.value }
        }
      }
    }
  }
}

onMounted(loadTelemetry)
</script>

<template>
  <div class="app-container">

    <!-- ── Page header ──────────────────────────────────────────────────────── -->
    <div class="page-head">
      <div class="page-head__content">
        <h1 class="page-head__title">Telemetry</h1>
        <p class="page-head__subtitle">Real-time device telemetry and system status monitoring.</p>
      </div>
      <div class="page-head__extra">
        <el-button :icon="RefreshRight" :loading="loading" @click="loadTelemetry">
          Refresh
        </el-button>
      </div>
    </div>

    <!-- ── Tab box ─────────────────────────────────────────────────────────── -->
    <div class="tabBox mt-4">
      <el-tabs v-model="activeTab" class="app-tabs">

        <!-- ── Information tab ─────────────────────────────────────────────── -->
        <el-tab-pane label="Information" name="information">
          <div v-loading="loading" element-loading-background="rgba(255,255,255,0.5)" class="deviceDetails-info">

            <!-- Error alert -->
            <el-alert
              v-if="error && !loading"
              :title="error"
              type="error"
              show-icon
              :closable="false"
              class="mb-4"
            />

            <!-- ── Info + utilization card ─────────────────────────────────── -->
            <el-card v-if="telemetry && !error" class="cardMap" shadow="never">
              <div class="info-body">

                <!-- Left: device fields -->
                <div class="info-form">
                  <ul class="info-form__ul1">
                    <el-row>
                      <el-col :span="12">
                        <li>
                          <span class="field-label">Observed At:&nbsp;</span>
                          <span class="field-value">{{ formatTimestamp(telemetry.timestamp) }}</span>
                        </li>
                      </el-col>
                      <el-col :span="12">
                        <li>
                          <span class="field-label">WAN IP:&nbsp;</span>
                          <span class="field-value mono">{{ telemetry.wanIp || "—" }}</span>
                        </li>
                      </el-col>
                      <el-col :span="12">
                        <li>
                          <span class="field-label">Temperature:&nbsp;</span>
                          <span class="field-value">
                            <span :class="telemetry.temperatureCelsius >= 80 ? 'text-danger' : telemetry.temperatureCelsius >= 60 ? 'text-warning' : 'text-normal'">
                              {{ telemetry.temperatureCelsius }}°C
                            </span>
                          </span>
                        </li>
                      </el-col>
                      <el-col :span="12">
                        <li>
                          <span class="field-label">Signal Strength (RSSI):&nbsp;</span>
                          <span class="field-value">{{ telemetry.signalStrengthRssi }} dBm</span>
                        </li>
                      </el-col>
                      <el-col :span="12">
                        <li>
                          <span class="field-label">Signal Quality (RSRP):&nbsp;</span>
                          <span class="field-value">{{ telemetry.signalQualityRsrp }} dBm</span>
                        </li>
                      </el-col>
                      <el-col :span="24">
                        <li>
                          <span class="field-label">Uptime:&nbsp;</span>
                          <span class="field-value">{{ formatUptime(telemetry.uptimeSeconds) }}</span>
                        </li>
                      </el-col>
                    </el-row>
                  </ul>
                </div>

                <!-- Right: utilization progress bars -->
                <div class="info-utilization">
                  <h4 class="util-section-title">Resource Utilization</h4>

                  <!-- CPU -->
                  <div class="util-item">
                    <div class="util-label-row">
                      <span class="util-name">CPU Usage</span>
                      <span class="util-value">{{ cpuPercent }}%</span>
                    </div>
                    <el-progress
                      :percentage="cpuPercent"
                      :status="progressStatus(cpuPercent)"
                      :stroke-width="10"
                    />
                  </div>

                  <!-- RAM -->
                  <div class="util-item">
                    <div class="util-label-row">
                      <span class="util-name">RAM Usage</span>
                      <span class="util-value">
                        {{ telemetry.ramUsageMb }} MB / {{ telemetry.ramTotalMb }} MB
                        ({{ ramPercent }}%)
                      </span>
                    </div>
                    <el-progress
                      :percentage="ramPercent"
                      :status="progressStatus(ramPercent)"
                      :stroke-width="10"
                    />
                  </div>

                  <!-- Storage -->
                  <div class="util-item">
                    <div class="util-label-row">
                      <span class="util-name">Storage Usage</span>
                      <span class="util-value">
                        {{ telemetry.storageUsedMb }} MB / {{ telemetry.storageTotalMb }} MB
                        ({{ storagePercent }}%)
                      </span>
                    </div>
                    <el-progress
                      :percentage="storagePercent"
                      :status="progressStatus(storagePercent)"
                      :stroke-width="10"
                    />
                  </div>
                </div>

              </div>
            </el-card>

            <!-- Empty state -->
            <el-empty
              v-if="!loading && !error && !telemetry"
              description="No telemetry data available"
              :image-size="80"
            />
          </div>
        </el-tab-pane>

        <!-- ── Placeholder tabs — uncomment when implementing ────────────────
        <el-tab-pane label="Reports"        name="reports">
          <el-empty description="Reports — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Firmware"       name="firmware">
          <el-empty description="Firmware — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Apps"           name="apps">
          <el-empty description="Apps — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Configuration"  name="setting">
          <el-empty description="Configuration — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Zero Touch"     name="deviceTemplate">
          <el-empty description="Zero Touch — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Remote Actions" name="commands">
          <el-empty description="Remote Actions — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Remote Access"  name="remoteAccess">
          <el-empty description="Remote Access — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="CLI Access"     name="diagnose">
          <el-empty description="CLI Access — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Syslog"         name="syslog">
          <el-empty description="Syslog — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Alert Logs"     name="log">
          <el-empty description="Alert Logs — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Captive Portal" name="captivePortal">
          <el-empty description="Captive Portal — coming soon" :image-size="64" />
        </el-tab-pane>
        <el-tab-pane label="Site Survey"    name="siteSurvey">
          <el-empty description="Site Survey — coming soon" :image-size="64" />
        </el-tab-pane>
        ──────────────────────────────────────────────────────────────────── -->

      </el-tabs>
    </div>

  </div>
</template>

<style lang="scss" scoped>
// ── page-head margin bleed ─────────────────────────────────────────────────────
.page-head {
  margin: -20px -20px 0;
}

// Tab bar styling now comes from the global `.app-tabs` primitive.

// ── Information tab content area ───────────────────────────────────────────────
.deviceDetails-info {
  min-height: 200px;

  .mb-4 { margin-bottom: 16px; }
}

// ── Main info card (.cardMap from reference) ───────────────────────────────────
.cardMap {
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
  border: 1px solid var(--el-border-color-lighter);

  :deep(.el-card__body) {
    padding: 0;
  }
}

// ── Card inner layout: fields left | utilization right ────────────────────────
.info-body {
  display: flex;
  gap: 0;

  @media (max-width: 767px) { flex-direction: column; }
}

// ── Device field list (.info-form / .info-form__ul1 from reference) ────────────
.info-form {
  flex: 1;
  min-width: 0;
  padding: 32px;
  border-right: 1px solid var(--el-border-color-lighter);

  @media (max-width: 767px) { border-right: none; border-bottom: 1px solid var(--el-border-color-lighter); }

  &__ul1 {
    list-style: none;
    margin: 0;
    padding-left: 20px;
    height: 100%;

    // Each li is a row inside el-col
    li {
      display: flex;
      align-items: baseline;
      flex-wrap: wrap;
      padding: 9px 0;
      border-bottom: 1px solid var(--el-fill-color-light);
      font-size: 13px;
      line-height: 1.5;

      &:last-child { border-bottom: none; }
    }
  }
}

.field-label {
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
  min-width: 160px;
}

.field-value {
  color: var(--el-text-color-primary);
  font-weight: 500;
  word-break: break-all;

  &.mono {
    font-family: ui-monospace, "SFMono-Regular", Consolas, monospace;
    font-size: 12px;
  }
}

// Temperature colour tiers
.text-danger  { color: #f56c6c; font-weight: 600; }
.text-warning { color: #e6a23c; font-weight: 600; }
.text-normal  { color: #26d9aa; font-weight: 600; }

// ── Utilization section (right panel) ─────────────────────────────────────────
.info-utilization {
  width: 340px;
  flex-shrink: 0;
  padding: 16px 20px;
  display: flex;
  flex-direction: column;
  gap: 20px;

  @media (max-width: 1023px) { width: 280px; }
  @media (max-width: 767px)  { width: 100%; }
}

.util-section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 4px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.util-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.util-label-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;

  .util-name {
    font-size: 13px;
    color: var(--el-text-color-regular);
    font-weight: 500;
  }

  .util-value {
    font-size: 12px;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
  }
}
</style>
