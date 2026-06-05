<script lang="ts" setup>
// Utility component used by TagsView to force component remount (clears keep-alive cache).
// Navigation: /redirect/<original-path> → immediately redirects back to <original-path>.
const route = useRoute()
const router = useRouter()

onBeforeMount(() => {
  const { params, query } = route
  const { path } = params
  const resolvedPath = Array.isArray(path) ? path.join("/") : path
  router.replace({ path: `/${resolvedPath}`, query })
})
</script>

<template>
  <div />
</template>
