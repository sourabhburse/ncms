import type * as ElementPlusIconsVue from "@element-plus/icons-vue"
import type { SvgName } from "~virtual/svg-component"
import "vue-router"

export {}

type ElementPlusIconsName = keyof typeof ElementPlusIconsVue

declare module "vue-router" {
  interface RouteMeta {

    title?: string

    svgIcon?: SvgName

    elIcon?: ElementPlusIconsName
  
    hidden?: boolean
   
    roles?: string[]
   
    breadcrumb?: boolean
   
    affix?: boolean
 
    alwaysShow?: boolean
  
    activeMenu?: string
   
    keepAlive?: boolean
  }
}
