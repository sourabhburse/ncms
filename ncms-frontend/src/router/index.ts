import type { RouteRecordRaw } from "vue-router"
import { createRouter } from "vue-router"
import { routerConfig } from "@/router/config"
import { setRouteChange } from "@@/composables/useRouteListener"

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
        meta: { title: "Hardware Inventory", elIcon: "Cpu" }
      }
    ]
  },
  {
    path: "/devices",
    component: Layouts,
    children: [
      {
        path: "",
        component: () => import("@/pages/devices/index.vue"),
        name: "Devices",
        meta: { title: "Devices", elIcon: "Monitor" }
      }
    ]
  },
  {
    // Device details — reached from the Devices table View action; hidden from sidebar
    path: "/devices/:id",
    component: Layouts,
    meta: { hidden: true },
    children: [
      {
        path: "",
        component: () => import("@/pages/devices/detail.vue"),
        name: "DeviceDetails",
        meta: { title: "Device Details", hidden: true }
      }
    ]
  },
  {
    // Accessed from Hardware Inventory rows — hidden from sidebar
    path: "/telemetry",
    component: Layouts,
    meta: { hidden: true },
    children: [
      {
        path: ":serialNumber",
        component: () => import("@/pages/telemetry/index.vue"),
        name: "Telemetry",
        meta: { title: "Telemetry", elIcon: "DataLine", hidden: true }
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

export const router = createRouter({
  history: routerConfig.history,
  routes: constantRoutes
})

router.afterEach((to) => {
  setRouteChange(to)
})
