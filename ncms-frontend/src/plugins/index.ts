import type { App } from "vue"
import { installElementPlusIcons } from "./element-plus-icons"
import { installSvgIcon } from "./svg-icon"

// [AUTH HOOK] Restore when implementing RBAC:
// import { installPermissionDirective } from "./permission-directive"

export function installPlugins(app: App) {
  installElementPlusIcons(app)
  installSvgIcon(app)
  // [AUTH HOOK] installPermissionDirective(app)
}
