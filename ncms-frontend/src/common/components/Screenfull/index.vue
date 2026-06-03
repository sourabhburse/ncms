<script lang="ts" setup>
import screenfull from "screenfull"

interface Props {

  element?: string

  openTips?: string

  exitTips?: string

  content?: boolean
}

const { element = "html", openTips = "Full screen", exitTips = "Exit full screen", content = false } = defineProps<Props>()

const CONTENT_LARGE = "content-large"

const CONTENT_FULL = "content-full"

const classList = document.body.classList


const isEnabled = screenfull.isEnabled

const isFullscreen = ref<boolean>(false)

const fullscreenTips = computed(() => (isFullscreen.value ? exitTips : openTips))

const fullscreenSvgName = computed(() => (isFullscreen.value ? "fullscreen-exit" : "fullscreen"))

function handleFullscreenClick() {
  const dom = document.querySelector(element) || undefined
  isEnabled ? screenfull.toggle(dom) : ElMessage.warning("您的浏览器无法工作")
}

function handleFullscreenChange() {
  isFullscreen.value = screenfull.isFullscreen

  isFullscreen.value || classList.remove(CONTENT_LARGE, CONTENT_FULL)
}

watchEffect(() => {
  if (isEnabled) {

    screenfull.on("change", handleFullscreenChange)

    onWatcherCleanup(() => {
      screenfull.off("change", handleFullscreenChange)
    })
  }
})
// #endregion

const isContentLarge = ref<boolean>(false)

const contentLargeTips = computed(() => (isContentLarge.value ? "内容区复原" : "内容区放大"))

const contentLargeSvgName = computed(() => (isContentLarge.value ? "fullscreen-exit" : "fullscreen"))

function handleContentLargeClick() {
  isContentLarge.value = !isContentLarge.value

  classList.toggle(CONTENT_LARGE, isContentLarge.value)
}

function handleContentFullClick() {

  isContentLarge.value && handleContentLargeClick()

  classList.add(CONTENT_FULL)

  handleFullscreenClick()
}
// #endregion
</script>

<template>
  <div>

    <el-tooltip v-if="!content" effect="dark" :content="fullscreenTips" placement="bottom">
      <SvgIcon :name="fullscreenSvgName" @click="handleFullscreenClick" class="svg-icon" />
    </el-tooltip>
    <el-dropdown v-else :disabled="isFullscreen">
      <SvgIcon :name="contentLargeSvgName" class="svg-icon" />
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item @click="handleContentLargeClick">
            {{ contentLargeTips }}
          </el-dropdown-item>
          <el-dropdown-item @click="handleContentFullClick">
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </div>
</template>

<style lang="scss" scoped>
.svg-icon {
  font-size: 20px;
  &:focus {
    outline: none;
  }
}
</style>
