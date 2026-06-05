<script lang="ts" setup>
import type { RouteRecordRaw } from "vue-router"
import { isExternal } from "@@/utils/validate"

interface Props {
  item: RouteRecordRaw
  basePath?: string
  isCollapse?: boolean
}

const { item, basePath = "", isCollapse = false } = defineProps<Props>()

const alwaysShowRootMenu = computed(() => item.meta?.alwaysShow)

const showingChildren = computed(() =>
  item.children?.filter(child => !child.meta?.hidden) ?? []
)

const showingChildNumber = computed(() => showingChildren.value.length)

const theOnlyOneChild = computed<RouteRecordRaw | null>(() => {
  const n = showingChildNumber.value
  if (n > 1) return null
  if (n === 1) return showingChildren.value[0]
  return { ...item, path: "" }
})

function resolvePath(routePath: string) {
  if (isExternal(routePath)) return routePath
  if (isExternal(basePath)) return basePath
  if (routePath.startsWith("/")) return routePath
  return `/${[basePath, routePath].filter(Boolean).join("/")}`.replace(/\/+/g, "/")
}

const itemId = computed(() => String(item.name ?? basePath ?? item.path))
</script>

<template>
  <!-- Single leaf item -->
  <template v-if="!alwaysShowRootMenu && theOnlyOneChild && !theOnlyOneChild.children">
    <div
      v-if="theOnlyOneChild.meta"
      :id="itemId"
      :is-collapse="String(isCollapse)"
    >
      <div class="side-link">
        <el-menu-item :index="resolvePath(theOnlyOneChild.path)">
          <SvgIcon
            v-if="theOnlyOneChild.meta.svgIcon"
            :name="theOnlyOneChild.meta.svgIcon"
            class="sidebar-icon"
          />
          <el-icon v-else-if="theOnlyOneChild.meta.elIcon" class="sidebar-icon">
            <component :is="theOnlyOneChild.meta.elIcon" />
          </el-icon>
          <template #title>
            <span class="title">{{ theOnlyOneChild.meta.title }}</span>
          </template>
        </el-menu-item>
      </div>
    </div>
  </template>

  <!-- Sub-menu with multiple children -->
  <div
    v-else
    :id="itemId"
    :is-collapse="String(isCollapse)"
  >
    <el-sub-menu :index="resolvePath(item.path)" teleported>
      <template #title>
        <SvgIcon
          v-if="item.meta?.svgIcon"
          :name="item.meta.svgIcon"
          class="sidebar-icon"
        />
        <el-icon v-else-if="item.meta?.elIcon" class="sidebar-icon">
          <component :is="item.meta.elIcon" />
        </el-icon>
        <span v-if="item.meta?.title" class="title">{{ item.meta.title }}</span>
      </template>
      <Item
        v-for="child in showingChildren"
        :key="child.path"
        :item="child"
        :base-path="resolvePath(child.path)"
        :is-collapse="isCollapse"
      />
    </el-sub-menu>
  </div>
</template>

<style lang="scss" scoped>
@import "@@/assets/styles/mixins.scss";

.side-link {
  display: contents;
}

.sidebar-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 18px;
  height: 18px;
  min-width: 18px;
  font-size: 18px;
}

.title {
  @extend %ellipsis;
}
</style>
