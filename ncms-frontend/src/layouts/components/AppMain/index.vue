<script lang="ts" setup>
import { useTagsViewStore } from "@/pinia/stores/tags-view"
import { Footer } from "../index"

const tagsViewStore = useTagsViewStore()
</script>

<template>
  <section class="app-main">
    <div class="app-scrollbar">
      <router-view v-slot="{ Component, route }">
        <transition name="el-fade-in" mode="out-in">
          <keep-alive :include="tagsViewStore.cachedViews">
            <component :is="Component" :key="route.path" class="app-container-grow" />
          </keep-alive>
        </transition>
      </router-view>
      <Footer />
    </div>
    <el-backtop target=".app-scrollbar" />
  </section>
</template>

<style lang="scss" scoped>
.app-main {
  display: flex;
}

.app-scrollbar {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  display: flex;
  flex-direction: column;

  // Inline scrollbar styles — @extend %scrollbar cannot reliably style
  // pseudo-elements through Vue's scoped attribute injection
  &::-webkit-scrollbar {
    width: 6px;
    height: 6px;
  }

  &::-webkit-scrollbar-button {
    display: none;
    height: 0;
    width: 0;
  }

  &::-webkit-scrollbar-track {
    background: #d3dce6;
    margin: 0;
  }

  &::-webkit-scrollbar-thumb {
    border-radius: 20px;
    background: #99a9bf;
  }

  &::-webkit-scrollbar-thumb:hover {
    background: darken(#99a9bf, 10%);
  }

  &::-webkit-scrollbar-corner {
    background: transparent;
  }

  .app-container-grow {
    flex-grow: 1;
  }
}
</style>
