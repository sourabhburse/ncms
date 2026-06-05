
export const DeviceEnum = {
  Mobile: 0,
  Desktop: 1
} as const

export type DeviceEnum = typeof DeviceEnum[keyof typeof DeviceEnum]

export const SIDEBAR_OPENED = "opened"

export const SIDEBAR_CLOSED = "closed"

export type SidebarOpened = typeof SIDEBAR_OPENED

export type SidebarClosed = typeof SIDEBAR_CLOSED
