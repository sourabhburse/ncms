<script lang="ts" setup>
import { ElMessage } from "element-plus"
import { Delete, Edit, Search, CirclePlus, Remove, Download } from "@element-plus/icons-vue"
import ConfirmDialog from "@@/components/ConfirmDialog/index.vue"
import { DEFAULT_TENANT_ID } from "@@/constants/tenant"

// ── Batch import dialog ────────────────────────────────────────────────────────
const batchImportVisible = ref(false)
const uploadFile         = ref<File | null>(null)
const importProductId    = ref("")
const importing          = ref(false)
const downloadingTpl     = ref(false)
const uploadRef          = useTemplateRef<UploadInstance>("uploadRef")

function handleUploadChange(uploadedFile: UploadFile) {
  const file = uploadedFile.raw
  if (!file) return
  const isCsv = file.type === "text/csv" || file.name.toLowerCase().endsWith(".csv")
  if (!isCsv) {
    ElMessage.error("Only .csv files are supported")
    return
  }
  uploadFile.value = file
}

function handleAddCommand(command: string) {
  if (command === "batch-import") {
    // Reset the dialog each time it's opened.
    importProductId.value = ""
    uploadFile.value = null
    uploadRef.value?.clearFiles()
    batchImportVisible.value = true
  }
}

/** Save a Blob to disk via a transient anchor. */
function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement("a")
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

/** Download the CSV template for the chosen product. */
async function handleDownloadTemplate() {
  if (!importProductId.value) {
    ElMessage.warning("Select a product to download its template")
    return
  }
  downloadingTpl.value = true
  try {
    const blob = await downloadHardwareInventoryTemplate(importProductId.value)
    saveBlob(blob, "hardware-inventory-template.csv")
  } finally {
    downloadingTpl.value = false
  }
}

/** Upload the chosen CSV file for the chosen product. */
async function handleImport() {
  if (!importProductId.value) {
    ElMessage.warning("Select a product first")
    return
  }
  if (!uploadFile.value) {
    ElMessage.warning("Choose a .csv file to import")
    return
  }
  importing.value = true
  try {
    await importHardwareInventoryCsv({
      file:      uploadFile.value,
      productId: importProductId.value,
      tenantId:  DEFAULT_TENANT_ID
    })
    ElMessage.success("Hardware inventory imported successfully")
    batchImportVisible.value = false
    uploadFile.value = null
    uploadRef.value?.clearFiles()
    loadInventory()
  } finally {
    importing.value = false
  }
}

function handleRemoveCommand(command: string) {
  if (command === "remove-all") {
    if (!inventoryList.value.length) {
      ElMessage.warning("There are no entries to remove")
      return
    }
    batchDeleteKind.value = "all"
    batchIds.value = inventoryList.value.map(r => r.id)
    batchDeleteVisible.value = true
  }
}
import type { FormInstance, FormRules, UploadFile, UploadInstance } from "element-plus"
import type { HardwareInventoryItem, ClaimEntry, Product } from "./api"
import {
  fetchProducts,
  fetchHardwareInventory,
  createHardwareInventory,
  updateHardwareInventory,
  deleteHardwareInventory,
  deleteHardwareInventoryBatch,
  downloadHardwareInventoryTemplate,
  importHardwareInventoryCsv,
  claimEntriesToRecord,
  recordToClaimEntries
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

// ── Telemetry navigation ───────────────────────────────────────────────────────

// ── Delete ─────────────────────────────────────────────────────────────────────

// Single-delete confirmation dialog state.
const deleteVisible = ref(false)
const deleting      = ref(false)
const rowToDelete   = ref<HardwareInventoryItem | null>(null)

/** Open the warning dialog for the chosen row (no deletion happens yet). */
function handleDeleteSingle(row: HardwareInventoryItem) {
  rowToDelete.value = row
  deleteVisible.value = true
}

/** Confirmed in the dialog → perform the delete. */
async function confirmDelete() {
  if (!rowToDelete.value) return
  deleting.value = true
  try {
    await deleteHardwareInventory(rowToDelete.value.id)
    ElMessage.success("Entry deleted")
    deleteVisible.value = false
    loadInventory()
  } finally {
    deleting.value = false
  }
}

// ── Batch delete (DELETE /hardware-inventory/batch) ──────────────────────────────

const batchDeleteVisible = ref(false)
const batchDeleting      = ref(false)
const batchIds           = ref<string[]>([])
/** Distinguishes the "selected rows" flow from the "remove all" flow. */
const batchDeleteKind    = ref<"selected" | "all">("selected")

/** Human-readable confirmation line for the dialog. */
const batchDeleteMessage = computed(() => {
  const n = batchIds.value.length
  const noun = n === 1 ? "entry" : "entries"
  return batchDeleteKind.value === "all"
    ? `Delete ALL ${n} hardware inventory ${noun}? This cannot be undone.`
    : `Delete ${n} selected ${noun}? This cannot be undone.`
})

/** REMOVE button → confirm deleting the currently selected rows. */
function handleDeleteSelected() {
  if (!selectedRows.value.length) {
    ElMessage.warning("Select at least one entry to remove")
    return
  }
  batchDeleteKind.value = "selected"
  batchIds.value = selectedRows.value.map(r => r.id)
  batchDeleteVisible.value = true
}

/** Confirmed in the dialog → perform the batch delete. */
async function confirmBatchDelete() {
  if (!batchIds.value.length) return
  batchDeleting.value = true
  try {
    await deleteHardwareInventoryBatch(batchIds.value)
    ElMessage.success(batchDeleteKind.value === "all" ? "All entries deleted" : "Entries deleted")
    batchDeleteVisible.value = false
    loadInventory()
  } finally {
    batchDeleting.value = false
  }
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
      tenantId:       DEFAULT_TENANT_ID,
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

// ── Edit dialog ──────────────────────────────────────────────────────────────────

const editVisible   = ref(false)
const editSaving     = ref(false)
const editFormRef    = useTemplateRef<FormInstance>("editFormRef")
const editingId      = ref("")
const editingSerial  = ref("")

interface EditFormModel {
  status: string
  identityPolicy: string
  claimEntries: ClaimEntry[]
}

const editForm = reactive<EditFormModel>({
  status:         "",
  identityPolicy: "",
  claimEntries:   [{ key: "", value: "" }]
})

const editFormRules: FormRules<EditFormModel> = {
  status:         [{ required: true, message: "Status is required",          trigger: "blur" }],
  identityPolicy: [{ required: true, message: "Identity policy is required", trigger: "blur" }]
}

function openEditDialog(row: HardwareInventoryItem) {
  editingId.value     = row.id
  editingSerial.value = row.serialNumber
  editForm.status         = row.status ?? ""
  editForm.identityPolicy = row.identityPolicy
  editForm.claimEntries   = recordToClaimEntries(row.identityClaims)
  editVisible.value = true
}

function closeEditDialog() {
  editVisible.value = false
  editFormRef.value?.resetFields()
}

function addEditClaimRow()             { editForm.claimEntries.push({ key: "", value: "" }) }
function removeEditClaimRow(i: number) { if (editForm.claimEntries.length > 1) editForm.claimEntries.splice(i, 1) }

async function handleEditSubmit() {
  const valid = await editFormRef.value?.validate().catch(() => false)
  if (!valid) return

  editSaving.value = true
  try {
    await updateHardwareInventory(editingId.value, {
      status:         editForm.status,
      identityPolicy: editForm.identityPolicy,
      identityClaims: claimEntriesToRecord(editForm.claimEntries)
    })
    ElMessage.success("Hardware entry updated successfully")
    closeEditDialog()
    loadInventory()
  } finally {
    editSaving.value = false
  }
}

// ── Claim display helper ───────────────────────────────────────────────────────

function formatClaims(claims: Record<string, string>): string {
  const entries = Object.entries(claims ?? {})
  if (!entries.length) return "—"
  return entries.map(([k, v]) => `${k}: ${v}`).join("  ·  ")
}

const popperOptions = {
  modifiers: [
    // Shift menu left by the label-button width so it aligns with the
    // full split-button's left edge (reference is the caret button).
    // Total button = 124px, caret ≈ 32px → label ≈ 92px → skid = -92.
    { name: "offset",          options: { offset: [-84, 2] } },
    { name: "preventOverflow", options: { padding: 0 } }
  ]
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
      <div class="page-toolbar__actions">

        <!-- ADD split button -->
        <el-dropdown
          split-button
          type="primary"
          size="small"
          :popper-options="popperOptions"
          :show-arrow="false"
          placement="bottom-start"
          popper-class="hw-inv-dropdown"
          class="toolbar-split-btn"
          @click="openAddDialog"
          @command="handleAddCommand"
        >
          ADD
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="batch-import">BATCH IMPORT</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>

        <!-- REMOVE split button -->
        <el-dropdown
          split-button
          type="danger"
          size="small"
          :popper-options="popperOptions"
          :show-arrow="false"
          placement="bottom-start"
          popper-class="hw-inv-dropdown"
          class="toolbar-split-btn"
          @click="handleDeleteSelected"
          @command="handleRemoveCommand"
        >
          REMOVE
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="remove-all" :disabled="!inventoryList.length">
                BATCH REMOVE
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>

      </div>

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
    <div class="app-table mt-5">
      <el-table
        v-loading="tableLoading"
        :data="filteredList"
        style="width: 100%"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="45" />
        <el-table-column type="index" label="Index" width="65" align="center" />

        <el-table-column label="Serial Number" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="app-mono">{{ row.serialNumber }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Product" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            {{ productLabelMap[row.productId] ?? row.productId ?? "—" }}
          </template>
        </el-table-column>

        <el-table-column label="Identity Policy" min-width="130" show-overflow-tooltip>
          <template #default="{ row }">
            <el-tag size="small" type="info">{{ row.identityPolicy }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column label="Identity Claims" min-width="240" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="claims-cell">{{ formatClaims(row.identityClaims) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="Created At" min-width="155" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.createdAt ?? "—" }}
          </template>
        </el-table-column>

        <el-table-column label="Action" width="220" fixed="right" align="center">
          <template #default="{ row }">
            <div class="action-cell">
              <el-button link type="primary" size="small" :icon="Edit" @click="openEditDialog(row)">
                Edit
              </el-button>
              <el-button link type="danger" size="small" :icon="Delete" @click="handleDeleteSingle(row)">
                Delete
              </el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- ── Batch Import Dialog ───────────────────────────────────────────────── -->
    <el-dialog
      v-model="batchImportVisible"
      title="Batch Import"
      width="min(600px, 90vw)"
      :close-on-click-modal="false"
      header-class="app-dialog__header"
      class="app-dialog"
    >
      <!-- 1) Pick the product the CSV rows belong to -->
      <div class="batch-import__field">
        <label class="batch-import__label">Product</label>
        <el-select
          v-model="importProductId"
          placeholder="Select a product"
          :loading="productsLoading"
          filterable
          style="width: 100%"
        >
          <el-option
            v-for="product in products"
            :key="product.id"
            :label="`${product.vendorName} – ${product.modelName}`"
            :value="product.id"
          />
        </el-select>
      </div>

      <!-- 2) Download the template for that product -->
      <el-button
        type="primary"
        plain
        :loading="downloadingTpl"
        :disabled="!importProductId"
        @click="handleDownloadTemplate"
      >
        <el-icon><Download /></el-icon>
        <span>Download Template</span>
      </el-button>

      <!-- 3) Upload the filled-in CSV -->
      <div class="upload-file">
        <el-upload
          ref="uploadRef"
          class="upload-demo"
          drag
          :auto-upload="false"
          :show-file-list="true"
          :limit="1"
          accept=".csv"
          @change="handleUploadChange"
        >
          <el-icon class="el-icon--upload"><upload-filled /></el-icon>
          <div class="el-upload__text">
            <div>Click or drag a file here to upload</div>
            <div class="text-xs text-gray-400">Supported extension：.csv</div>
          </div>
        </el-upload>
      </div>

      <template #footer>
        <div class="app-dialog__footer">
          <el-button size="small" @click="batchImportVisible = false">Close</el-button>
          <el-button
            type="primary"
            size="small"
            :loading="importing"
            :disabled="!importProductId || !uploadFile"
            @click="handleImport"
          >
            Import
          </el-button>
        </div>
      </template>
    </el-dialog>

    <!-- ── Add Hardware Entry Dialog ────────────────────────────────────────── -->
    <el-dialog
      v-model="dialogVisible"
      title="Add Hardware Inventory"
      width="min(680px, 90vw)"
      :close-on-click-modal="false"
      class="app-dialog"
      header-class="app-dialog__header"
      @close="closeDialog"
    >
      <el-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        label-width="auto"
        label-position="left"
        style="max-width: 600px"
        size="default"
        
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

    <!-- ── Edit Hardware Entry Dialog ───────────────────────────────────────── -->
    <el-dialog
      v-model="editVisible"
      title="Edit Hardware Inventory"
      width="min(680px, 90vw)"
      :close-on-click-modal="false"
      class="app-dialog"
      header-class="app-dialog__header"
      @close="closeEditDialog"
    >
      <el-form
        ref="editFormRef"
        :model="editForm"
        :rules="editFormRules"
        label-width="auto"
        label-position="left"
        style="max-width: 600px"
        size="default"
      >
        <el-form-item label="Serial Number">
          <el-input :model-value="editingSerial" disabled />
        </el-form-item>

        <el-form-item label="Status" prop="status">
          <el-input
            v-model="editForm.status"
            placeholder="e.g. ACTIVE, DECOMMISSIONED"
          />
        </el-form-item>

        <el-form-item label="Identity Policy" prop="identityPolicy">
          <el-input
            v-model="editForm.identityPolicy"
            placeholder="e.g. jwt, x509, shared-secret"
          />
        </el-form-item>

        <!-- Dynamic claim key/value rows -->
        <el-form-item label="Identity Claims">
          <div class="claims-editor">
            <div
              v-for="(entry, index) in editForm.claimEntries"
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
                :disabled="editForm.claimEntries.length === 1"
                @click="removeEditClaimRow(index)"
              />
            </div>
            <el-button text :icon="CirclePlus" class="claims-editor__add-btn" @click="addEditClaimRow">
              Add Claim
            </el-button>
          </div>
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="app-dialog__footer">
          <el-button size="small" @click="closeEditDialog">Cancel</el-button>
          <el-button type="primary" size="small" :loading="editSaving" @click="handleEditSubmit">
            Save
          </el-button>
        </div>
      </template>
    </el-dialog>

    <!-- ── Delete confirmation dialog ───────────────────────────────────────── -->
    <ConfirmDialog
      v-model="deleteVisible"
      title="Confirm Delete"
      confirm-text="Delete"
      :loading="deleting"
      @confirm="confirmDelete"
    >
      Delete hardware entry
      <strong>{{ rowToDelete?.serialNumber }}</strong>?
      This action cannot be undone.
    </ConfirmDialog>

    <!-- ── Batch delete confirmation dialog ──────────────────────────────────── -->
    <ConfirmDialog
      v-model="batchDeleteVisible"
      :title="batchDeleteKind === 'all' ? 'Confirm Remove All' : 'Confirm Batch Delete'"
      :message="batchDeleteMessage"
      :confirm-text="batchDeleteKind === 'all' ? 'Delete All' : 'Delete'"
      :loading="batchDeleting"
      @confirm="confirmBatchDelete"
    />

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



// ── Split-button toolbar ───────────────────────────────────────────────────────
.toolbar-split-btn {
  width: 124px; // fixed width makes ADD and REMOVE identical in size
  & + .toolbar-split-btn { margin-left: 8px; }

  // Stretch the inner wrapper so both buttons share the fixed width
  :deep(.el-button-group) {
    width: 100%;
    display: flex;
  }

  // Main label button — fills remaining space, text centred
  :deep(.el-button:first-child) {
    flex: 1;
    min-width: 0;
    text-align: center;
    justify-content: center;
    text-transform: uppercase;
    font-weight: 600;
    letter-spacing: 0.5px;
    height: 32px;
    padding: 0;
    font-size: 13px;
  }

  // Caret (▼) trigger — fixed width, no flex growth
  :deep(.el-dropdown__caret-button) {
    flex-shrink: 0;
    height: 32px;
    padding: 0 9px;

    &::before {
      background-color: rgba(255, 255, 255, 0.4);
    }
  }
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

// Inventory table styling now comes from the global `.app-table` primitive and
// mono cells from `.app-mono`. Delete/batch-delete confirmations use the shared
// <ConfirmDialog> component.

// ── Row actions — keep View / Edit / Delete on one line ──────────────────────────
.action-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: nowrap;
  white-space: nowrap;
  gap: 8px;

  // el-button (link) adds its own left margin between siblings; the flex gap
  // already spaces them, so drop the default margin.
  :deep(.el-button + .el-button) {
    margin-left: 0;
  }
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

<style>
.hw-inv-dropdown .el-dropdown-menu {
  padding: 0;
}

.hw-inv-dropdown .el-dropdown-menu__item {
  margin: 0;
  text-align: center;
  padding: 4px 20px;
  border-radius: 0;
}

.hw-inv-dropdown .el-dropdown-menu__item:not(.is-disabled):hover {
  border-radius: 0;
}
.app-dialog {
  padding: 0;
  overflow: hidden;
  border-radius: 8px;
}

.app-dialog .el-dialog__body {
  padding: 30px 20px;
}
.batch-import__field {
  margin-bottom: 20px;
}

.batch-import__label {
  display: block;
  margin-bottom: 6px;
  font-weight: 700;
  font-size: 14px;
  color: var(--el-text-color-primary);
}

.upload-file {
     width: 100%;
    margin-top: 20px;
}

.app-dialog .el-dialog__footer {
  border-top: 1px solid #e4e7ec;
  padding: 12px;
  text-align: center;
}

.app-dialog .el-dialog__footer .el-button {
  padding: 0 46px;
    height: 40px;
}

.app-dialog .app-dialog__header {
  font-weight: 700;
  padding: 17.5px;
  background: #eff6ff;
}

.el-form-item__label {
  font-weight: 700;
  color: var(--el-text-color-primary);
}
</style>

