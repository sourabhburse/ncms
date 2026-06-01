<script lang="ts" setup>
import type { RouteLocationNormalizedGeneric, RouteRecordRaw, RouterLink } from "vue-router"
import type { TagView } from "@/pinia/stores/tags-view"
import { useRouteListener } from "@@/composables/useRouteListener"
import { ArrowDown, Close, DArrowLeft, DArrowRight, RefreshRight } from "@element-plus/icons-vue"
import path from "path-browserify"
import { useTagsViewStore } from "@/pinia/stores/tags-view"
import { useAppStore } from "@/pinia/stores/app"
import { constantRoutes } from "@/router"

const router = useRouter()
const route = useRoute()
const tagsViewStore = useTagsViewStore()
const appStore = useAppStore()
const { listenerRouteChange } = useRouteListener()

const sidebarOffset = computed(() =>
  appStore.sidebar.opened ? "var(--sideBarWidth)" : "var(--miniSideBarWidth)"
)

const containerStyle = computed(() => ({
  marginLeft: sidebarOffset.value,
  width: `calc(100% - ${sidebarOffset.value})`,
  transition: "margin-left 0.28s, width 0.28s"
}))

const scrollbarRef = useTemplateRef<InstanceType<typeof import("element-plus")["ElScrollbar"]>>("scrollbarRef")
const scrollContentRef = useTemplateRef<HTMLElement>("scrollContentRef")
const tagRefs = useTemplateRef<InstanceType<typeof RouterLink>[]>("tagRefs")

let currentScrollLeft = 0
const isScrolling = ref(false)
const isRefreshing = ref(false)
const showLeftButton = ref(false)
const showRightButton = ref(false)
let scrollTimer: ReturnType<typeof setTimeout> | null = null

// ── Tag helpers ───────────────────────────────────────────────────────────────

function isActive(tag: TagView) {
  return tag.path === route.path
}

function isAffix(tag: TagView) {
  return tag.meta?.affix
}

const currentTag = computed<TagView>(
  () => tagsViewStore.visitedViews.find(t => t.path === route.path) ?? {}
)

// ── Affix tag init ────────────────────────────────────────────────────────────

let affixTags: TagView[] = []

function filterAffixTags(routes: RouteRecordRaw[], basePath = "/") {
  const tags: TagView[] = []
  routes.forEach((r) => {
    if (r.meta?.affix) {
      const tagPath = path.resolve(basePath, r.path)
      tags.push({ fullPath: tagPath, path: tagPath, name: r.name, meta: { ...r.meta } })
    }
    if (r.children) tags.push(...filterAffixTags(r.children, r.path))
  })
  return tags
}

function initTags() {
  affixTags = filterAffixTags(constantRoutes)
  for (const tag of affixTags) {
    tag.name && tagsViewStore.addVisitedView(tag)
  }
}

// ── Tag actions ───────────────────────────────────────────────────────────────

function toLastView(visitedViews: TagView[], view: TagView) {
  const latest = visitedViews.slice(-1)[0]
  if (latest?.fullPath) {
    router.push(latest.fullPath)
  } else {
    view.name === "Dashboard"
      ? router.push({ path: `/redirect${view.path}`, query: view.query })
      : router.push("/")
  }
}

function refreshSelectedTag(view: TagView) {
  if (isRefreshing.value) return
  isRefreshing.value = true
  tagsViewStore.delCachedView(view)
  router.replace({ path: `/redirect${view.path}`, query: view.query })
  setTimeout(() => { isRefreshing.value = false }, 1000)
}

function closeSelectedTag(view: TagView) {
  tagsViewStore.delVisitedView(view)
  tagsViewStore.delCachedView(view)
  isActive(view) && toLastView(tagsViewStore.visitedViews, view)
}

function closeOthersTags(view?: TagView) {
  const tag = view ?? currentTag.value
  if (tag.fullPath && tag.fullPath !== route.path) router.push(tag.fullPath)
  tagsViewStore.delOthersVisitedViews(tag)
  tagsViewStore.delOthersCachedViews(tag)
}

function closeLeftTags(view: TagView) {
  const idx = tagsViewStore.visitedViews.findIndex(v => v.path === view.path)
  const toRemove = tagsViewStore.visitedViews.filter((v, i) => i < idx && !v.meta?.affix)
  toRemove.forEach(v => {
    tagsViewStore.delVisitedView(v)
    tagsViewStore.delCachedView(v)
  })
  if (toRemove.some(v => v.path === route.path)) {
    router.push(view.fullPath ?? view.path ?? "/")
  }
}

function closeRightTags(view: TagView) {
  const idx = tagsViewStore.visitedViews.findIndex(v => v.path === view.path)
  const toRemove = tagsViewStore.visitedViews.filter((v, i) => i > idx && !v.meta?.affix)
  toRemove.forEach(v => {
    tagsViewStore.delVisitedView(v)
    tagsViewStore.delCachedView(v)
  })
  if (toRemove.some(v => v.path === route.path)) {
    router.push(view.fullPath ?? view.path ?? "/")
  }
}

function closeAllTags(view: TagView) {
  tagsViewStore.delAllVisitedViews()
  tagsViewStore.delAllCachedViews()
  if (affixTags.some(t => t.path === route.path)) return
  toLastView(tagsViewStore.visitedViews, view)
}

// ── Dropdown (Tag Operation) ───────────────────────────────────────────────────

function handleDropdownAction(action: string) {
  switch (action) {
    case "refresh": refreshSelectedTag(currentTag.value); break
    case "close": if (!isAffix(currentTag.value)) closeSelectedTag(currentTag.value); break
    case "closeOthers": closeOthersTags(); break
    case "closeLeft": closeLeftTags(currentTag.value); break
    case "closeRight": closeRightTags(currentTag.value); break
    case "closeAll": closeAllTags(currentTag.value); break
  }
}

// ── Scroll ────────────────────────────────────────────────────────────────────

function syncScrollButtons(scrollLeft: number) {
  const wrapEl = scrollbarRef.value?.wrapRef
  const wrapWidth = wrapEl?.clientWidth ?? 0
  const scrollWidth = wrapEl?.scrollWidth ?? 0
  showLeftButton.value = scrollLeft > 0
  showRightButton.value = scrollWidth > wrapWidth && scrollLeft + wrapWidth < scrollWidth - 1
}

function handleScroll({ scrollLeft }: { scrollLeft: number }) {
  currentScrollLeft = scrollLeft
  syncScrollButtons(scrollLeft)
}

function handleWheel(e: WheelEvent) {
  scrollTo(e.deltaY < 0 ? "left" : "right")
}

function scrollTo(direction: "left" | "right", distance = 200) {
  const wrapEl = scrollbarRef.value?.wrapRef
  const wrapWidth = wrapEl?.clientWidth ?? 0
  const scrollWidth = wrapEl?.scrollWidth ?? 0
  if (wrapWidth >= scrollWidth) return
  const target = direction === "left"
    ? Math.max(0, currentScrollLeft - distance)
    : Math.min(currentScrollLeft + distance, scrollWidth - wrapWidth)
  isScrolling.value = true
  scrollbarRef.value?.setScrollLeft(target)
  if (scrollTimer) clearTimeout(scrollTimer)
  scrollTimer = setTimeout(() => { isScrolling.value = false }, 300)
}

async function updateScrollButtons() {
  await nextTick()
  syncScrollButtons(currentScrollLeft)
}

function scrollToActiveTag() {
  const refs = tagRefs.value
  if (!refs) return
  for (const ref of refs) {
    // @ts-expect-error RouterLink.$props
    if (route.path !== ref.$props?.to?.path) continue
    // @ts-expect-error RouterLink.$el
    const el: HTMLElement = ref.$el
    const wrapWidth = scrollbarRef.value?.wrapRef?.clientWidth ?? 0
    if (el.offsetLeft < currentScrollLeft) {
      scrollbarRef.value?.setScrollLeft(el.offsetLeft)
    } else if (el.offsetLeft + el.offsetWidth > currentScrollLeft + wrapWidth) {
      scrollbarRef.value?.setScrollLeft(el.offsetLeft + el.offsetWidth - wrapWidth)
    }
    break
  }
  updateScrollButtons()
}

// ── Init ──────────────────────────────────────────────────────────────────────

initTags()

listenerRouteChange((r: RouteLocationNormalizedGeneric) => {
  if (r.name) {
    tagsViewStore.addVisitedView(r)
    tagsViewStore.addCachedView(r)
  }
  nextTick(scrollToActiveTag)
}, true)

watch(() => tagsViewStore.visitedViews.length, () => nextTick(updateScrollButtons))
watch(() => appStore.sidebar.opened, () => nextTick(updateScrollButtons))

onMounted(() => {
  nextTick(updateScrollButtons)
  window.addEventListener("resize", updateScrollButtons)
})

onUnmounted(() => {
  window.removeEventListener("resize", updateScrollButtons)
})
</script>

<template>
  <div class="tags-container" :style="containerStyle">
    <!-- Left scroll button — visible only when scrolled right -->
    <div
      v-show="showLeftButton"
      class="scroll-button scroll-button-left"
      :class="{ 'is-scrolling': isScrolling }"
      @click="scrollTo('left')"
    >
      <el-icon><DArrowLeft /></el-icon>
    </div>

    <!-- Horizontally scrollable tags -->
    <div class="scroll-container">
      <el-scrollbar
        ref="scrollbarRef"
        :vertical="false"
        @scroll="handleScroll"
        @wheel.passive="handleWheel"
      >
        <div ref="scrollContentRef" class="scroll-inner">
          <router-link
            v-for="tag in tagsViewStore.visitedViews"
            ref="tagRefs"
            :key="tag.path"
            :class="{ active: isActive(tag) }"
            class="tags-item"
            :to="{ path: tag.path, query: tag.query }"
            @click.middle="!isAffix(tag) && closeSelectedTag(tag)"
          >
            <span class="text-[14px]">{{ tag.meta?.title }}</span>
            <span
              v-if="!isAffix(tag)"
              class="tags-item-close ml-2"
              @click.prevent.stop="closeSelectedTag(tag)"
            >
              <el-icon :size="10"><Close /></el-icon>
            </span>
          </router-link>
        </div>
      </el-scrollbar>
    </div>

    <!-- Right buttons -->
    <div class="right-buttons">
      <!-- Right scroll button — visible only when more content exists to the right -->
      <div
        v-show="showRightButton"
        class="scroll-button scroll-button-right"
        :class="{ 'is-scrolling': isScrolling }"
        @click="scrollTo('right')"
      >
        <el-icon><DArrowRight /></el-icon>
      </div>

      <!-- Tag Operation dropdown -->
      <el-dropdown class="h-full operate-button" trigger="click" @command="handleDropdownAction">
        <div class="h-full flex items-center">
          Tag Operation
          <el-icon class="ml-1"><ArrowDown /></el-icon>
        </div>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="refresh">
              <el-icon><RefreshRight /></el-icon>Refresh
            </el-dropdown-item>
            <el-dropdown-item command="close" :disabled="!!isAffix(currentTag)">
              <el-icon><Close /></el-icon>Close
            </el-dropdown-item>
            <el-dropdown-item command="closeOthers">Close Others</el-dropdown-item>
            <el-dropdown-item command="closeLeft">Close Left</el-dropdown-item>
            <el-dropdown-item command="closeRight">Close Right</el-dropdown-item>
            <el-dropdown-item command="closeAll">Close All</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>

      <!-- Refresh button -->
      <div
        class="operate-button"
        :class="{ refreshing: isRefreshing }"
        @click="refreshSelectedTag(currentTag)"
      >
        <el-icon><RefreshRight /></el-icon>
        <span class="text-[14px]">Refresh</span>
      </div>
    </div>

  </div>
</template>

<style lang="scss" scoped>
// ── Container ──────────────────────────────────────────────────────────────────
.tags-container {
  height: var(--v3-tagsview-height);
  background-color: var(--el-fill-color-light);
  border-bottom: 1px solid var(--el-border-color-lighter);
  display: flex;
  align-items: stretch;
  box-sizing: border-box;
  user-select: none;
  overflow: hidden;
}

// ── Scrollable tab area ────────────────────────────────────────────────────────
.scroll-container {
  flex: 1;
  min-width: 0;
  height: 100%;
  overflow: hidden;

  :deep(.el-scrollbar),
  :deep(.el-scrollbar__wrap) {
    height: 100%;
  }

  :deep(.el-scrollbar__view) {
    height: 100%;
    display: inline-flex;
    align-items: stretch;
    min-width: 100%;
  }

  :deep(.el-scrollbar__thumb) {
    display: none !important;
  }

  .scroll-inner {
    display: flex;
    align-items: stretch;
    height: 100%;
  }
}

// ── Browser-style tab ─────────────────────────────────────────────────────────
// Each tab fills the full height of the bar. A 2px top border acts as the
// active indicator. A right border separates tabs from each other.
.tags-item {
  display: inline-flex;
  align-items: center;
  height: 100%;
  padding: 0 14px;
  border: none;
  border-top: 2px solid transparent;          // placeholder — filled when active
  border-right: 1px solid var(--el-border-color-lighter);
  background-color: transparent;
  color: var(--el-text-color-regular);
  font-size: 13px;
  cursor: pointer;
  text-decoration: none;
  white-space: nowrap;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: color 0.15s, background-color 0.15s, border-color 0.15s;

  &:first-child {
    border-left: 1px solid var(--el-border-color-lighter);
  }

  // ── Inactive hover ───────────────────────────────────────────────────────
  &:hover:not(.active) {
    color: var(--el-color-primary);
    background-color: var(--el-bg-color);
  }

  // ── Active tab ───────────────────────────────────────────────────────────
  &.active {
    background-color: var(--el-bg-color);
    color: var(--el-color-primary);
    font-weight: 500;
  }
}

// ── Close ( × ) button ────────────────────────────────────────────────────────
.tags-item-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  margin-left: 6px;
  border-radius: 50%;
  flex-shrink: 0;
  transition: background-color 0.15s, color 0.15s;
  color: var(--el-text-color-placeholder);

  &:hover {
    background-color: var(--el-fill-color-dark);
    color: var(--el-text-color-primary);
  }

  .active & {
    color: var(--el-color-primary-light-3);
    &:hover {
      background-color: var(--el-color-primary-light-8);
      color: var(--el-color-primary);
    }
  }
}

// ── Scroll arrow buttons ───────────────────────────────────────────────────────
.scroll-button {
  width: 30px;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  cursor: pointer;
  color: var(--el-text-color-secondary);
  background-color: var(--el-fill-color-light);
  border-right: 1px solid var(--el-border-color-lighter);
  transition: color 0.15s, background-color 0.15s;

  &.scroll-button-right {
    border-right: none;
    border-left: 1px solid var(--el-border-color-lighter);
  }

  &:hover {
    color: var(--el-color-primary);
    background-color: var(--el-bg-color);
  }

  &.is-scrolling {
    cursor: not-allowed;
    opacity: 0.4;
    pointer-events: none;
  }
}

// ── Right-side action buttons (Tag Operation / Refresh) ────────────────────────
.right-buttons {
  display: flex;
  align-items: stretch;
  height: 100%;
  flex-shrink: 0;
  border-left: 1px solid var(--el-border-color-lighter);
}

.operate-button {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  cursor: pointer;
  color: var(--el-text-color-regular);
  background-color: transparent;
  padding: 0 14px;
  font-size: 13px;
  white-space: nowrap;
  border-left: 1px solid var(--el-border-color-lighter);
  transition: color 0.15s, background-color 0.15s;

  &:first-child { border-left: none; }

  &:hover {
    color: var(--el-color-primary);
    background-color: var(--el-bg-color);
  }

  &.refreshing {
    color: var(--el-color-primary);
    cursor: not-allowed;
    opacity: 0.6;

    :deep(.el-icon) {
      animation: spin 0.8s linear infinite;
    }
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}
</style>
