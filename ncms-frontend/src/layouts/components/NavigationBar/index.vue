<script lang="ts" setup>
import Screenfull from "@@/components/Screenfull/index.vue"
import SearchMenu from "@@/components/SearchMenu/index.vue"
import ThemeSwitch from "@@/components/ThemeSwitch/index.vue"
import {UserFilled } from "@element-plus/icons-vue"
// import logoSrc from "@@/assets/images/layouts/logo.png?url"

// const notificationCount = ref(0)
</script>

<template>
  <div class="navbar">
    <!-- Left: logo + platform name -->
    <div class="logo-area">
      <router-link to="/" class="logo-link">
        <!-- <img :src="logoSrc" class="logo-icon" /> -->
        <span class="platform-name">NCMS Platform</span>
      </router-link>
    </div>

    <!-- Right: utility controls -->
    <div class="right-controls">
      <SearchMenu class="ctrl-item" />
      <Screenfull class="ctrl-item" />
      <ThemeSwitch class="ctrl-item" />

      <!-- <el-badge :value="notificationCount" :hidden="notificationCount === 0" class="ctrl-item bell">
        <el-icon :size="20"><Bell /></el-icon>
      </el-badge> -->

      <el-dropdown class="ctrl-item" trigger="click">
        <div class="user-info">
          <el-avatar :icon="UserFilled" :size="32" />
          <span class="username">Admin</span>
        </div>
        <template #dropdown>
          <el-dropdown-menu>
            <!-- [AUTH HOOK] Add logout item here when implementing auth -->
          </el-dropdown-menu>
        </template>
      </el-dropdown>
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

      .logo-icon {
        height: 30px;
        width: auto;
        object-fit: contain;
      }

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

      &.bell {
        padding: 0 12px;
      }

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
