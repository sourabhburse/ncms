<script lang="ts" setup>
// ─────────────────────────────────────────────────────────────────────────────
// ConfirmDialog — a reusable warning/confirmation modal.
//
// Replaces hand-rolled el-dialog confirmation blocks. Uses the global
// `.app-dialog` styling; the body slot defaults to the `message` prop.
// ─────────────────────────────────────────────────────────────────────────────
import { WarningFilled } from "@element-plus/icons-vue"

interface Props {
  /** v-model: dialog visibility. */
  modelValue: boolean
  title?: string
  message?: string
  confirmText?: string
  cancelText?: string
  confirmType?: "primary" | "danger" | "warning"
  loading?: boolean
  width?: string
}

const props = withDefaults(defineProps<Props>(), {
  title: "Please Confirm",
  message: "",
  confirmText: "Confirm",
  cancelText: "Cancel",
  confirmType: "danger",
  loading: false,
  width: "min(460px, 90vw)"
})

const emit = defineEmits<{
  "update:modelValue": [value: boolean]
  confirm: []
  cancel: []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: value => emit("update:modelValue", value)
})

function onCancel() {
  visible.value = false
  emit("cancel")
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="title"
    :width="width"
    :close-on-click-modal="false"
    class="app-dialog"
    header-class="app-dialog__header"
  >
    <div class="confirm-dialog__body">
      <el-icon class="confirm-dialog__icon"><WarningFilled /></el-icon>
      <div class="confirm-dialog__text">
        <slot>{{ message }}</slot>
      </div>
    </div>

    <template #footer>
      <div class="app-dialog__footer">
        <el-button size="small" @click="onCancel">{{ cancelText }}</el-button>
        <el-button :type="confirmType" size="small" :loading="loading" @click="emit('confirm')">
          {{ confirmText }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<style lang="scss" scoped>
.confirm-dialog__body {
  display: flex;
  align-items: flex-start;
  gap: var(--spacing-3);
}

.confirm-dialog__icon {
  flex-shrink: 0;
  font-size: 24px;
  color: var(--el-color-warning);
  margin-top: 2px;
}

.confirm-dialog__text {
  margin: 0;
  font-size: var(--text-base);
  line-height: 1.6;
  color: var(--el-text-color-regular);
}
</style>
