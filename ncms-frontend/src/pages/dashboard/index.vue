<script lang="ts" setup>
const stats = [
  { label: "Total Users", value: "1,240", trend: "+8%", icon: "👤" },
  { label: "Revenue", value: "$48,200", trend: "+12%", icon: "💰" },
  { label: "Orders", value: "382", trend: "+3%", icon: "📦" },
  { label: "Growth", value: "24%", trend: "+5%", icon: "📈" }
]
</script>

<template>
  <div class="app-container">
    <!-- Page header -->
    <div class="mb-6">
      <h1 class="text-2xl font-semibold">Dashboard</h1>
      <p class="text-sm op-60 mt-1">
        Welcome back. Here's what's happening today.
      </p>
    </div>

    <!-- Stats grid -->
    <el-row :gutter="16" class="mb-6">
      <el-col v-for="stat in stats" :key="stat.label" :xs="24" :sm="12" :xl="6" class="mb-4">
        <el-card shadow="never">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm op-60 mb-1">{{ stat.label }}</p>
              <p class="text-2xl font-bold">{{ stat.value }}</p>
              <p class="text-xs text-green-500 mt-1">{{ stat.trend }} this month</p>
            </div>
            <span class="text-4xl op-80">{{ stat.icon }}</span>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- Main content area -->
    <el-row :gutter="16">
      <el-col :xs="24" :lg="16" class="mb-4">
        <el-card shadow="never">
          <template #header>
            <span class="font-medium">Recent Activity</span>
          </template>
          <el-empty description="Connect your API to display activity data" :image-size="80" />
        </el-card>
      </el-col>
      <el-col :xs="24" :lg="8" class="mb-4">
        <el-card shadow="never">
          <template #header>
            <span class="font-medium">Quick Actions</span>
          </template>
          <div class="flex flex-col gap-2">
            <el-button type="primary" plain>Create New Record</el-button>
            <el-button plain>Export Data</el-button>
            <el-button plain>View Reports</el-button>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>
