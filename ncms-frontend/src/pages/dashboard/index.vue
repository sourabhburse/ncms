<script lang="ts" setup>
// Dashboard — placeholder until the feature is built.
</script>

<template>
  <div class="app-container">
    <!-- Page header -->
    <div class="page-head">
      <div class="page-head__content">
        <h1 class="page-head__title">Dashboard</h1>
        <p class="page-head__subtitle">Overview of your network and devices.</p>
      </div>
    </div>

    <!-- Upcoming feature placeholder -->
    <div class="app-card dashboard-empty mt-4">
      <el-empty description="Dashboard is an upcoming feature — stay tuned." :image-size="120" />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.page-head {
  margin: -20px -20px 0;
}

.dashboard-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - 220px);
  padding: var(--spacing-10) var(--spacing-5);
}
</style>
