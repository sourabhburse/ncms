import { LayoutModeEnum } from "@@/constants/app-key"

/** Layout configuration type */
export interface LayoutsConfig {
  showSettings: boolean
  layoutMode: LayoutModeEnum
  showTagsView: boolean
  showLogo: boolean
  fixedHeader: boolean
  showFooter: boolean
  showNotify: boolean
  showThemeSwitch: boolean
  showScreenfull: boolean
  showSearchMenu: boolean
  cacheTagsView: boolean
  showWatermark: boolean
  showGreyMode: boolean
  showColorWeakness: boolean
}

// [SETTINGS HOOK] Restore when adding settings store:
// import { getLayoutsConfig } from "@@/utils/local-storage"
// export const layoutsConfig: LayoutsConfig = { ...DEFAULT_CONFIG, ...getLayoutsConfig() }
