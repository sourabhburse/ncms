<script lang="ts" setup>
import type { RouterLink } from "vue-router"
import Screenfull from "@@/components/Screenfull/index.vue"
import { useRouteListener } from "@@/composables/useRouteListener"
import { ArrowLeft, ArrowRight } from "@element-plus/icons-vue"

interface Props {
  tagRefs: InstanceType<typeof RouterLink>[] | null
}

const props = defineProps<Props>()

const route = useRoute()

const { listenerRouteChange } = useRouteListener()


const scrollbarRef = useTemplateRef("scrollbarRef")


const scrollbarContentRef = useTemplateRef("scrollbarContentRef")


let currentScrollLeft = 0


const translateDistance = 200


function scroll({ scrollLeft }: { scrollLeft: number }) {
  currentScrollLeft = scrollLeft
}

function wheelScroll({ deltaY }: WheelEvent) {
  if (deltaY.toString().startsWith("-")) {
    scrollTo("left")
  } else {
    scrollTo("right")
  }
}

function getWidth() {

  const scrollbarContentRefWidth = scrollbarContentRef.value!.clientWidth

  const scrollbarRefWidth = scrollbarRef.value!.wrapRef!.clientWidth

  const lastDistance = scrollbarContentRefWidth - scrollbarRefWidth - currentScrollLeft

  return { scrollbarContentRefWidth, scrollbarRefWidth, lastDistance }
}


function scrollTo(direction: "left" | "right", distance: number = translateDistance) {
  let scrollLeft = 0
  const { scrollbarContentRefWidth, scrollbarRefWidth, lastDistance } = getWidth()

  if (scrollbarRefWidth > scrollbarContentRefWidth) return
  if (direction === "left") {
    scrollLeft = Math.max(0, currentScrollLeft - distance)
  } else {
    scrollLeft = Math.min(currentScrollLeft + distance, currentScrollLeft + lastDistance)
  }
  scrollbarRef.value!.setScrollLeft(scrollLeft)
}


function moveTo() {
  const tagRefs = props.tagRefs!
  for (let i = 0; i < tagRefs.length; i++) {
    // @ts-expect-error ignore
    if (route.path === tagRefs[i].$props.to.path) {
      // @ts-expect-error ignore
      const el: HTMLElement = tagRefs[i].$el
      const offsetWidth = el.offsetWidth
      const offsetLeft = el.offsetLeft
      const { scrollbarRefWidth } = getWidth()
     
      if (offsetLeft < currentScrollLeft) {
        const distance = currentScrollLeft - offsetLeft
        scrollTo("left", distance)
        return
      }
     
      const width = scrollbarRefWidth + currentScrollLeft - offsetWidth
      if (offsetLeft > width) {
        const distance = offsetLeft - width
        scrollTo("right", distance)
        return
      }
    }
  }
}


listenerRouteChange(() => {
  nextTick(moveTo)
})
</script>

<template>
  <div class="scroll-container">
    <el-tooltip content="Scroll the tabs to the left (clickable if exceeding maximum width)">
      <el-icon class="arrow left" @click="scrollTo('left')">
        <ArrowLeft />
      </el-icon>
    </el-tooltip>
    <el-scrollbar ref="scrollbarRef" @wheel.passive="wheelScroll" @scroll="scroll">
      <div ref="scrollbarContentRef" class="scrollbar-content">
        <slot />
      </div>
    </el-scrollbar>
    <el-tooltip content="Scroll the tabs to the right (clickable if exceeding maximum width)">
      <el-icon class="arrow right" @click="scrollTo('right')">
        <ArrowRight />
      </el-icon>
    </el-tooltip>
    <Screenfull :content="true" class="screenfull" />
  </div>
</template>

<style lang="scss" scoped>
.scroll-container {
  height: 100%;
  user-select: none;
  display: flex;
  justify-content: space-between;
  .arrow {
    width: 40px;
    height: 100%;
    font-size: 18px;
    cursor: pointer;
    &.left {
      box-shadow: 5px 0 5px -6px var(--el-border-color-darker);
    }
    &.right {
      box-shadow: -5px 0 5px -6px var(--el-border-color-darker);
    }
  }
  .el-scrollbar {
    flex: 1;
    white-space: nowrap;
    .scrollbar-content {
      display: inline-block;
    }
  }
  .screenfull {
    width: 40px;
    display: flex;
    justify-content: center;
    align-items: center;
    cursor: pointer;
  }
}
</style>
