<script lang="ts" setup>
import { useGreyAndColorWeakness } from "@@/composables/useGreyAndColorWeakness"
import { useTheme } from "@@/composables/useTheme"
import en from "element-plus/es/locale/lang/en"

const { initTheme } = useTheme()

const { initGreyAndColorWeakness } = useGreyAndColorWeakness()

initTheme()
initGreyAndColorWeakness()
</script>

<template>
  <el-config-provider :locale="en">
    <router-view />
  </el-config-provider>
</template>

