import type { RouterHistory } from "vue-router"
import { createWebHashHistory, createWebHistory } from "vue-router"

interface RouterConfig {
  history: RouterHistory
}

export const routerConfig: RouterConfig = {
  history:
    import.meta.env.VITE_ROUTER_HISTORY === "hash"
      ? createWebHashHistory(import.meta.env.VITE_PUBLIC_PATH)
      : createWebHistory(import.meta.env.VITE_PUBLIC_PATH)
}

// [AUTH HOOK] Restore when implementing auth:
// dynamic: boolean              — enable role-based dynamic routes
// defaultRoles: string[]        — fallback roles when dynamic is disabled
// thirdLevelRouteCache: boolean — flatten 3+ level routes to 2 levels
