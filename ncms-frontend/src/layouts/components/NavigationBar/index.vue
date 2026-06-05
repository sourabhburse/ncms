<script lang="ts" setup>
import Screenfull from "@@/components/Screenfull/index.vue"
import SearchMenu from "@@/components/SearchMenu/index.vue"
import ThemeSwitch from "@@/components/ThemeSwitch/index.vue"
import { UserFilled } from "@element-plus/icons-vue"
</script>

<template>
  <div class="navbar">
    <!-- Left: logo + platform name -->
    <div class="logo-area">
      <router-link to="/" class="logo-link">
        <span class="platform-name">NCMS Platform</span>
      </router-link>
    </div>

    <!-- Right: utility controls -->
    <div class="right-controls">
      <SearchMenu class="ctrl-item" />
      <Screenfull class="ctrl-item" />
      <ThemeSwitch class="ctrl-item" />

      <div class="ctrl-item user-info">
        <el-avatar :icon="UserFilled" :size="32" />
        <span class="username">Admin</span>
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.navbar {
  height: var(--v3-navigationbar-height);
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 0 1px #0003;
  background: linear-gradient(135deg, #3f5bfa 0%, #097efc 100%);
  padding: 0 10px;
  color: #fff;

  // Force all icons from child components to be white
  :deep(svg),
  :deep(.el-icon),
  :deep(i) {
    color: #fff !important;
  }

  .logo-area {
    display: flex;
    align-items: center;
    padding-left: 20px;

    .logo-link {
      display: flex;
      align-items: center;
      gap: 10px;
      text-decoration: none;
      color: #fff;

      .platform-name {
        font-size: 16px;
        font-weight: 600;
        white-space: nowrap;
        color: #fff;
      }
    }
  }

  .right-controls {
    display: flex;
    align-items: center;
    height: 100%;
    gap: 4px;

    .ctrl-item {
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      padding: 0 10px;
      height: 100%;
      font-size: 18px;
      color: #fff;

      &:hover {
        background: rgba(255, 255, 255, 0.12);
      }
    }

    .user-info {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      padding: 0 4px;

      .username {
        font-size: 14px;
        color: #fff;
      }

      &:hover {
        background: rgba(255, 255, 255, 0.12);
        border-radius: 4px;
      }
    }
  }
}
</style>
