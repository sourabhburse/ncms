<script lang="ts" setup>
import { ElMessage, ElMessageBox } from "element-plus"
import { Plus, Delete, Search, CirclePlus, Remove } from "@element-plus/icons-vue"
import type { FormInstance, FormRules } from "element-plus"
import type { HardwareInventoryItem, ClaimEntry, Product } from "./api"
import {
  fetchProducts,
  fetchHardwareInventory,
  createHardwareInventory,
  deleteHardwareInventory,
  deleteHardwareInventoryBatch,
  claimEntriesToRecord
} from "./api"

// ── Table state ────────────────────────────────────────────────────────────────

const inventoryList = ref<HardwareInventoryItem[]>([])
const searchQuery   = ref("")
const tableLoading  = ref(false)
const selectedRows  = ref<HardwareInventoryItem[]>([])

const filteredList = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return inventoryList.value
  return inventoryList.value.filter(
    item =>
      item.serialNumber.toLowerCase().includes(q) ||
      item.identityPolicy.toLowerCase().includes(q) ||
      (productLabelMap.value[item.productId] ?? "").toLowerCase().includes(q)
  )
})

async function loadInventory() {
  tableLoading.value = true
  try {
    inventoryList.value = await fetchHardwareInventory()
  } catch {
    // error surfaced by axios interceptor
  } finally {
    tableLoading.value = false
  }
}

function handleSelectionChange(rows: HardwareInventoryItem[]) {
  selectedRows.value = rows
}

// ── Delete ─────────────────────────────────────────────────────────────────────

async function handleDeleteSingle(row: HardwareInventoryItem) {
  await ElMessageBox.confirm(
    `Delete hardware entry <strong>${row.serialNumber}</strong>? This cannot be undone.`,
    "Confirm Delete",
    { confirmButtonText: "Delete", cancelButtonText: "Cancel", type: "warning", dangerouslyUseHTMLString: true }
  )
  await deleteHardwareInventory(row.id)
  ElMessage.success("Entry deleted")
  loadInventory()
}

async function handleDeleteSelected() {
  if (!selectedRows.value.length) {
    ElMessage.warning("Select at least one entry to remove")
    return
  }
  await ElMessageBox.confirm(
    `Delete ${selectedRows.value.length} selected entr${selectedRows.value.length > 1 ? "ies" : "y"}?`,
    "Confirm Batch Delete",
    { confirmButtonText: "Delete", cancelButtonText: "Cancel", type: "warning" }
  )
  await deleteHardwareInventoryBatch(selectedRows.value.map(r => r.id))
  ElMessage.success("Entries deleted")
  loadInventory()
}

// ── Products ───────────────────────────────────────────────────────────────────

const products        = ref<Product[]>([])
const productsLoading = ref(false)

const productLabelMap = computed<Record<string, string>>(() =>
  Object.fromEntries(
    products.value.map(p => [p.id, `${p.vendorName} – ${p.modelName}`])
  )
)

async function loadProducts() {
  productsLoading.value = true
  try {
    products.value = await fetchProducts()
  } catch {
    // silently handled
  } finally {
    productsLoading.value = false
  }
}

// ── Add dialog ─────────────────────────────────────────────────────────────────

const dialogVisible = ref(false)
const dialogSaving  = ref(false)
const formRef       = useTemplateRef<FormInstance>("formRef")

interface FormModel {
  productId: string
  serialNumber: string
  identityPolicy: string
  claimEntries: ClaimEntry[]
}

function emptyForm(): FormModel {
  return {
    productId:      "",
    serialNumber:   "",
    identityPolicy: "",
    claimEntries:   [{ key: "", value: "" }]
  }
}

const form = reactive<FormModel>(emptyForm())

const formRules: FormRules<FormModel> = {
  productId:      [{ required: true, message: "Product is required",        trigger: "change" }],
  serialNumber:   [{ required: true, message: "Serial number is required",  trigger: "blur"   }],
  identityPolicy: [{ required: true, message: "Identity policy is required", trigger: "blur"  }]
}

function openAddDialog() {
  Object.assign(form, emptyForm())
  dialogVisible.value = true
}

function closeDialog() {
  dialogVisible.value = false
  formRef.value?.resetFields()
}

function addClaimRow()                { form.claimEntries.push({ key: "", value: "" }) }
function removeClaimRow(i: number)    { if (form.claimEntries.length > 1) form.claimEntries.splice(i, 1) }

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  dialogSaving.value = true
  try {
    await createHardwareInventory({
      productId:      form.productId,
      serialNumber:   form.serialNumber,
      identityPolicy: form.identityPolicy,
      identityClaims: claimEntriesToRecord(form.claimEntries)
    })
    ElMessage.success("Hardware entry registered successfully")
    closeDialog()
    loadInventory()
  } finally {
    dialogSaving.value = false
  }
}

// ── Claim display helper ───────────────────────────────────────────────────────

function formatClaims(claims: Record<string, string>): string {
  const entries = Object.entries(claims ?? {})
  if (!entries.length) return "—"
  return entries.map(([k, v]) => `${k}: ${v}`).join("  ·  ")
}

// ── Init ───────────────────────────────────────────────────────────────────────

onMounted(() => {
  loadInventory()
  loadProducts()
})
</script>

<template>
  <div class="app-container">

    <!-- ── Page header ──────────────────────────────────────────────────────── -->
    <div class="page-head">
      <div class="page-head__content">
        <h1 class="page-head__title">Hardware Inventory</h1>
        <p class="page-head__subtitle">Register and manage device hardware entries.</p>
      </div>
    </div>

    <!-- ── Toolbar ──────────────────────────────────────────────────────────── -->
    <div class="page-toolbar">
      <el-button type="primary" :icon="Plus" @click="openAddDialog">Add</el-button>

      <el-button
        type="danger"
        :icon="Delete"
        :disabled="!selectedRows.length"
        @click="handleDeleteSelected"
      >
        Remove
      </el-button>

      <el-input
        v-model="searchQuery"
        placeholder="Search serial number, policy…"
        clearable
        class="page-toolbar__search"
      >
        <template #append>
          <el-button :icon="Search" />
        </template>
      </el-input>
    </div>

    <!-- ── Inventory table ──────────────────────────────────────────────────── -->
    <div class="inventory-table-wrap mt-5">
      <el-table
        v-loading="tableLoading"
        :data="filteredList"
        style="width: 100%"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="42" />
        <el-table-column type="index" label="#" width="52" align="center" />

        <el-table-column label="Serial Number" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="sn-cell">{{ row.serialNumber }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Product" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">
            {{ productLabelMap[row.productId] ?? row.productId ?? "—" }}
          </template>
        </el-table-column>

        <el-table-column label="Identity Policy" min-width="150" show-overflow-tooltip>
          <template #default="{ row }">
            <el-tag size="small" type="info">{{ row.identityPolicy }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column label="Identity Claims" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="claims-cell">{{ formatClaims(row.identityClaims) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Created At" width="170" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.createdAt ?? "—" }}
          </template>
        </el-table-column>

        <el-table-column label="Action" width="100" fixed="right" align="center">
          <template #default="{ row }">
            <el-button link type="danger" size="small" :icon="Delete" @click="handleDeleteSingle(row)">
              Delete
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty
        v-if="!tableLoading && filteredList.length === 0"
        description="No hardware entries found"
        :image-size="80"
        class="inventory-table-empty"
      />
    </div>

    <!-- ── Add Hardware Entry Dialog ────────────────────────────────────────── -->
    <el-dialog
      v-model="dialogVisible"
      title="Add Hardware Inventory"
      width="680px"
      :close-on-click-modal="false"
      class="app-dialog"
      @close="closeDialog"
    >
      <el-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        label-width="140px"
        label-position="left"
        size="small"
      >
        <el-form-item label="Serial Number" prop="serialNumber">
          <el-input
            v-model="form.serialNumber"
            placeholder="Enter device serial number"
            maxlength="100"
            show-word-limit
          />
        </el-form-item>

        <el-form-item label="Product" prop="productId">
          <el-select
            v-model="form.productId"
            placeholder="Select a product"
            :loading="productsLoading"
            style="width: 100%"
            filterable
          >
            <el-option
              v-for="product in products"
              :key="product.id"
              :label="`${product.vendorName} – ${product.modelName}`"
              :value="product.id"
            />
          </el-select>
        </el-form-item>

        <el-form-item label="Identity Policy" prop="identityPolicy">
          <el-input
            v-model="form.identityPolicy"
            placeholder="e.g. jwt, x509, shared-secret"
          />
        </el-form-item>

        <!-- Dynamic claim key/value rows -->
        <el-form-item label="Identity Claims">
          <div class="claims-editor">
            <div
              v-for="(entry, index) in form.claimEntries"
              :key="index"
              class="claims-editor__row"
            >
              <el-input v-model="entry.key"   placeholder="Claim key" class="claims-editor__key" />
              <el-input v-model="entry.value" placeholder="Value"     class="claims-editor__value" />
              <el-button
                :icon="Remove"
                circle
                size="small"
                plain
                type="danger"
                :disabled="form.claimEntries.length === 1"
                @click="removeClaimRow(index)"
              />
            </div>
            <el-button text :icon="CirclePlus" class="claims-editor__add-btn" @click="addClaimRow">
              Add Claim
            </el-button>
          </div>
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="app-dialog__footer">
          <el-button size="small" @click="closeDialog">Cancel</el-button>
          <el-button type="primary" size="small" :loading="dialogSaving" @click="handleSubmit">
            Save
          </el-button>
        </div>
      </template>
    </el-dialog>

  </div>
</template>

<style lang="scss" scoped>
// page-head, page-toolbar, app-dialog, app-dialog__footer
// are all provided globally by _page.scss and _dialog.scss.
// Only page-specific overrides live here.

// ── page-head spacing inside app-container ────────────────────────────────────
.page-head {
  margin: -20px -20px 16px; // bleed to app-container edges
}

// ── Toolbar height alignment ───────────────────────────────────────────────────
// Stretch makes every flex child fill the same row height, then we pin the
// search input's inner wrappers to that same 32 px so the append button
// and the action buttons sit flush.
.page-toolbar {
  align-items: center;

  &__search {
    margin-left: 10px;

    :deep(.el-input__wrapper) {
      height: 32px;
      box-sizing: border-box;
      padding-top: 0;
      padding-bottom: 0;
    }

    :deep(.el-input-group__append) {
      height: 32px;
      box-sizing: border-box;
      padding-top: 0;
      padding-bottom: 0;
    }
  }
}

// ── Inventory table ────────────────────────────────────────────────────────────
.inventory-table-wrap {
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);

  :deep(.el-table__row td) {
    background-color: #f7f7f7;
    border-bottom: 2px solid #fff;
  }

  :deep(.el-table__header th) {
    background-color: #eef0f6;
    border-bottom: none;
    font-size: 13px;
    font-weight: 600;
    color: #333;
  }

  :deep(.el-table--enable-row-hover .el-table__body tr:hover > td) {
    background-color: #eaf8ff !important;
  }
}

.inventory-table-empty {
  padding: 40px 0;
  background-color: #f7f7f7;
}

.sn-cell {
  font-family: ui-monospace, "SFMono-Regular", Consolas, monospace;
  font-size: 13px;
}

.claims-cell {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

// ── Claims dynamic editor ──────────────────────────────────────────────────────
.claims-editor {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 8px;

  &__row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__key   { width: 140px; flex-shrink: 0; }
  &__value { flex: 1; min-width: 0; }

  &__add-btn {
    align-self: flex-start;
    margin-top: 4px;
    color: var(--el-color-primary);
    font-size: 13px;
  }
}
</style>
