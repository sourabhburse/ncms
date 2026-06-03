// Unified localStorage access layer

import type { ThemeName } from "@@/composables/useTheme"
import type { SidebarClosed, SidebarOpened } from "@@/constants/app-key"
import type { TagView } from "@/pinia/stores/tags-view"
import { CacheKey } from "@@/constants/cache-key"

// [AUTH HOOK] Restore when implementing auth:
// export function getToken() { return localStorage.getItem(CacheKey.TOKEN) }
// export function setToken(token: string) { localStorage.setItem(CacheKey.TOKEN, token) }
// export function removeToken() { localStorage.removeItem(CacheKey.TOKEN) }

// [SETTINGS HOOK] Restore when adding settings store:
// export function getLayoutsConfig() { ... }
// export function setLayoutsConfig(settings: LayoutsConfig) { ... }
// export function removeLayoutsConfig() { localStorage.removeItem(CacheKey.CONFIG_LAYOUT) }

// #region Sidebar status
export function getSidebarStatus() {
  return localStorage.getItem(CacheKey.SIDEBAR_STATUS)
}

export function setSidebarStatus(sidebarStatus: SidebarOpened | SidebarClosed) {
  localStorage.setItem(CacheKey.SIDEBAR_STATUS, sidebarStatus)
}
// #endregion

// #region Active theme
export function getActiveThemeName() {
  return localStorage.getItem(CacheKey.ACTIVE_THEME_NAME) as ThemeName | null
}

export function setActiveThemeName(themeName: ThemeName) {
  localStorage.setItem(CacheKey.ACTIVE_THEME_NAME, themeName)
}
// #endregion

// #region Tags view
export function getVisitedViews() {
  const json = localStorage.getItem(CacheKey.VISITED_VIEWS)
  return JSON.parse(json ?? "[]") as TagView[]
}

export function setVisitedViews(views: TagView[]) {
  views.forEach((view) => {
    delete view.matched
    delete view.redirectedFrom
  })
  localStorage.setItem(CacheKey.VISITED_VIEWS, JSON.stringify(views))
}

export function getCachedViews() {
  const json = localStorage.getItem(CacheKey.CACHED_VIEWS)
  return JSON.parse(json ?? "[]") as string[]
}

export function setCachedViews(views: string[]) {
  localStorage.setItem(CacheKey.CACHED_VIEWS, JSON.stringify(views))
}
// #endregion
