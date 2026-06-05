export function setCssVar(varName: string, value: string, element: HTMLElement = document.documentElement) {
  if (!varName?.startsWith("--")) {
    console.error("CSS variable names should start with '--'")
    return
  }
  element.style.setProperty(varName, value)
}
