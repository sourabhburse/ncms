import type { RouteRecordRaw } from "vue-router"
import { createRouter } from "vue-router"
import { routerConfig } from "@/router/config"
import { setRouteChange } from "@@/composables/useRouteListener"
import {Cpu } from "@element-plus/icons-vue"

const Layouts = () => import("@/layouts/index.vue")

export const constantRoutes: RouteRecordRaw[] = [
  {
    // Utility route used by TagsView to force component remount (keep-alive cache clear)
    path: "/redirect",
    component: Layouts,
    meta: { hidden: true },
    children: [
      {
        path: ":path(.*)",
        component: () => import("@/pages/redirect/index.vue")
      }
    ]
  },
  {
    path: "/",
    component: Layouts,
    redirect: "/dashboard",
    children: [
      {
        path: "dashboard",
        component: () => import("@/pages/dashboard/index.vue"),
        name: "Dashboard",
        meta: {
          title: "Dashboard",
          svgIcon: "dashboard",
          affix: true
        }
      }
    ]
  },
  {
    path: "/hardware-inventory",
    component: Layouts,
    children: [
      {
        path: "",
        component: () => import("@/pages/hardware-inventory/index.vue"),
        name: "HardwareInventory",
        meta: { title: "Hardware Inventory", elIcon: Cpu }
      }
    ]
  },
  {
    path: "/404",
    component: () => import("@/pages/error/404.vue"),
    meta: { hidden: true },
    alias: "/:pathMatch(.*)*"
  }
]

// [AUTH HOOK] Restore when implementing RBAC:
// export const dynamicRoutes: RouteRecordRaw[] = [...]
// export function resetRouter() { router.getRoutes().forEach(...) }

export const router = createRouter({
  history: routerConfig.history,
  routes: constantRoutes
})

router.afterEach((to) => {
  setRouteChange(to)
})
