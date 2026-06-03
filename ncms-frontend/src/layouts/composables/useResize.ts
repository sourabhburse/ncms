import { useRouteListener } from "@@/composables/useRouteListener"
import { DeviceEnum } from "@@/constants/app-key"
import { useAppStore } from "@/pinia/stores/app"

const MAX_MOBILE_WIDTH = 992


export function useResize() {
  const appStore = useAppStore()

  const { listenerRouteChange } = useRouteListener()

  const isMobile = () => {
    const rect = document.body.getBoundingClientRect()
    return rect.width - 1 < MAX_MOBILE_WIDTH
  }

  const resizeHandler = () => {
    if (!document.hidden) {
      const _isMobile = isMobile()
      appStore.toggleDevice(_isMobile ? DeviceEnum.Mobile : DeviceEnum.Desktop)
      _isMobile && appStore.closeSidebar(true)
    }
  }


  listenerRouteChange(() => {
    if (appStore.device === DeviceEnum.Mobile && appStore.sidebar.opened) {
      appStore.closeSidebar(false)
    }
  })


  onBeforeMount(() => {
    window.addEventListener("resize", resizeHandler)
  })


  onMounted(() => {
    if (isMobile()) {
      appStore.toggleDevice(DeviceEnum.Mobile)
      appStore.closeSidebar(true)
    }
  })


  onBeforeUnmount(() => {
    window.removeEventListener("resize", resizeHandler)
  })
}
