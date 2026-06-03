<script lang="ts" setup>
import { useResize } from "./composables/useResize"
import LeftTopMode from "./modes/LeftTopMode.vue"

useResize()
</script>

<template>
  <div>
    <LeftTopMode />
  </div>
</template>
