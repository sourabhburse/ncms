<script lang="ts" setup>
import { useAppStore } from "@/pinia/stores/app"
import { AppMain, NavigationBar, Sidebar, TagsView } from "../components"

const appStore = useAppStore()

const layoutClasses = computed(() => ({
  openSidebar:       appStore.sidebar.opened,
  hideSidebar:       !appStore.sidebar.opened,
  withoutAnimation:  appStore.sidebar.withoutAnimation
}))
</script>

<template>
  <div :class="layoutClasses" class="app-wrapper">

    <!-- Fixed full-width header: navbar stacked above tagsview -->
    <div class="fixed-header">
      <NavigationBar />
      <TagsView />
    </div>

    <!-- Fixed sidebar: spans full height, padded below the header -->
    <div class="sidebar-container">
      <Sidebar />
    </div>

    <!--
      Main content — NOT position:fixed.
      Uses height:100vh + overflow:hidden + padding to clear fixed regions.
      AppMain's inner el-scrollbar owns the scroll behaviour.
      Keeping this element in normal flow (not fixed) ensures that
      el-dialog overlays teleported to <body> stack above the header
      and sidebar at their correct z-index (2000 > 1002 > 1001).
    -->
    <AppMain class="app-main" />

  </div>
</template>

<style lang="scss" scoped>
@import "@@/assets/styles/mixins.scss";

$sidebar-transition: width 0.28s ease;
$content-transition: padding-left 0.28s ease;

// ── Root container ────────────────────────────────────────────────────────────
.app-wrapper {
  @extend %clearfix;
  width: 100%;
}

// ── Fixed top header ───────────────────────────────────────────────────────────
// Full viewport width; sits above sidebar and content (z-index 1002).
// pointer-events: none lets clicks pass through the transparent sidebar-area
// gap (x=0–sidebar-width, y=navbar-bottom–header-bottom). Direct children
// (NavigationBar, TagsView) restore pointer-events: auto for their content.
.fixed-header {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 1002;
  pointer-events: none;

  > * {
    pointer-events: auto;
  }
}

// ── Fixed left sidebar ─────────────────────────────────────────────────────────
// Spans full viewport height. padding-top keeps menu items below the header.
.sidebar-container {
  position: fixed;
  top: calc(var(--v3-navigationbar-height) + var(--v3-header-border-bottom-width));
  left: 0;
  bottom: 0;
  z-index: 1001;
  width: var(--v3-sidebar-width);
  background-color: var(--el-menu-bg-color);
  border-right: var(--v3-sidebar-border-right);
  overflow: hidden;
  transition: $sidebar-transition;
}

// ── Main content area ──────────────────────────────────────────────────────────
// NOT position:fixed — lives in normal document flow.
// padding clears the two fixed regions; height:100vh + overflow:hidden
// give AppMain's inner scroll container a constrained height to scroll within.
.app-main {
  height: 100vh;
  overflow: hidden;
  box-sizing: border-box;
  padding-top: var(--v3-header-height);
  padding-left: var(--v3-sidebar-width);
  transition: $content-transition;
}

// ── Collapsed sidebar state ────────────────────────────────────────────────────
.hideSidebar {
  .sidebar-container {
    width: var(--v3-sidebar-hide-width);
  }

  .app-main {
    padding-left: var(--v3-sidebar-hide-width);
  }
}

// ── Suppress transitions (initial load / mobile resize) ───────────────────────
.withoutAnimation {
  .sidebar-container,
  .app-main {
    transition: none;
  }
}
</style>
