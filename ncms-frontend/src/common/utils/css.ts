/** 获取指定元素（默认全局）上的 CSS 变量的值 */
export function getCssVar(varName: string, element: HTMLElement = document.documentElement) {
  if (!varName?.startsWith("--")) {
    console.error("CSS variable names should start with '--'")
    return ""
  }

  return getComputedStyle(element).getPropertyValue(varName)
}

export function setCssVar(varName: string, value: string, element: HTMLElement = document.documentElement) {
  if (!varName?.startsWith("--")) {
    console.error("CSS variable names should start with '--'")
    return
  }
  element.style.setProperty(varName, value)
}
