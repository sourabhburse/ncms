import type { Directive } from "vue"

export {}

declare module "vue" {
  export interface ComponentCustomProperties {
    vPermission: Directive<Element, string[]>
  }
}
