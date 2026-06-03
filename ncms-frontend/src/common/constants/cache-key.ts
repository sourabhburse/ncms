const SYSTEM_NAME = "v3-admin-vite"

/** Keys used when caching data in localStorage */
export class CacheKey {
  // [AUTH HOOK] Restore when implementing auth:
  // static readonly TOKEN = `${SYSTEM_NAME}-token-key`

  // [SETTINGS HOOK] Restore when adding settings store:
  // static readonly CONFIG_LAYOUT = `${SYSTEM_NAME}-config-layout-key`

  static readonly SIDEBAR_STATUS = `${SYSTEM_NAME}-sidebar-status-key`
  static readonly ACTIVE_THEME_NAME = `${SYSTEM_NAME}-active-theme-name-key`
  static readonly VISITED_VIEWS = `${SYSTEM_NAME}-visited-views-key`
  static readonly CACHED_VIEWS = `${SYSTEM_NAME}-cached-views-key`
}
