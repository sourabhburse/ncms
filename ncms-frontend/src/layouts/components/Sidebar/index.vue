<script lang="ts" setup>
import { useAppStore } from "@/pinia/stores/app"
import { constantRoutes } from "@/router"
import Item from "./Item.vue"

const route = useRoute()
const appStore = useAppStore()

const activeMenu = computed(() => route.meta.activeMenu || route.path)
const noHiddenRoutes = computed(() => constantRoutes.filter(item => !item.meta?.hidden))
const isCollapse = computed(() => !appStore.sidebar.opened)

const menuStyle = {
  "--el-menu-text-color": "var(--menuText)",
  "--el-menu-hover-text-color": "var(--menuHoverText)",
  "--el-menu-bg-color": "var(--menuBg)",
  "--el-menu-hover-bg-color": "var(--menuHover)",
  "--el-menu-active-color": "var(--menuActiveText)",
  "--el-menu-border-color": "transparent"
}

function toggleSidebar() {
  appStore.toggleSidebar(false)
}
</script>

<template>
  <div class="sidebar-content h-full">
    <el-scrollbar class="sidebar-scrollbar">
      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapse"
        :collapse-transition="false"
        :router="true"
        mode="vertical"
        :style="menuStyle"
      >
        <Item
          v-for="r in noHiddenRoutes"
          :key="r.path"
          :item="r"
          :base-path="r.path"
          :is-collapse="isCollapse"
        />
      </el-menu>
    </el-scrollbar>

    <!-- Hamburger toggle -->
    <div
      class="humburger-shadow px-[17px] cursor-pointer h-[60px] flex items-center justify-center"
      @click="toggleSidebar"
    >
      <SvgIcon name="toggle" class="hamburger" />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.sidebar-content {
  display: flex;
  flex-direction: column;
  height: 100%;
  box-shadow: 0 0 10px #0003;
}

.sidebar-scrollbar {
  flex: 1;
  overflow: hidden;

  :deep(.el-scrollbar__wrap) {
    overflow-x: hidden;
  }

  :deep(.el-scrollbar__bar.is-horizontal) {
    display: none;
  }
}

:deep(.el-menu) {
  border: none;
  width: 100%;
  user-select: none;
}

// Override item/sub-menu heights and hover colours
:deep(.el-menu-item),
:deep(.el-sub-menu__title),
:deep(.el-sub-menu .el-menu-item) {
  height: var(--v3-sidebar-menu-item-height);
  line-height: var(--v3-sidebar-menu-item-height);
  display: flex;
  align-items: center;
  padding: 0 16px;
  gap: 12px;
}

// Neutralise el-icon's default margin — .sidebar-icon on Item.vue owns all spacing/sizing
:deep(.el-menu-item .el-icon),
:deep(.el-sub-menu__title .el-icon) {
  margin-right: 0;
}

:deep(.el-sub-menu.is-active > .el-sub-menu__title) {
  color: var(--menuActiveText);
}

:deep(.el-menu-item:not(.is-active):hover) {
  background-color: var(--menuHover) !important;
  color: var(--menuHoverText) !important;
}

:deep(.el-menu-item.is-active:hover) {
  background-color: unset !important;
  cursor: default;
}

:deep(.el-menu--collapse) {
  // Hide the expand chevron — meaningless in collapsed mode
  .el-sub-menu__icon-arrow {
    display: none !important;
  }

  // Disable the flyout tooltip for sub-menu items.
  // pointer-events: none on the title stops mouseenter from reaching
  // the el-tooltip trigger, so the popup never fires.
  // The parent <li> still receives :hover so we can re-apply the background.
  .el-sub-menu > .el-sub-menu__title {
    pointer-events: none;
    cursor: default;
  }

  // Re-apply hover background via the parent li (which still gets pointer events)
  .el-sub-menu:hover > .el-sub-menu__title {
    background-color: var(--menuHover);
    color: var(--menuHoverText);
  }

  .el-sub-menu.is-active > .el-sub-menu__title {
    background-color: var(--menuHover);
    color: var(--menuHoverText);
  }
}

// Hamburger
.humburger-shadow {
  flex-shrink: 0;
  border-top: 1px solid var(--el-border-color-light);
  color: var(--menuText);
  transition: background-color 0.2s;
  .hamburger {
    font-size: 18px;
    width: 1em;
    height: 1em;
  }
}
</style>
