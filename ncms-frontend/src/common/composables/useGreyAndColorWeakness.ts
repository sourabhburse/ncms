function initGreyAndColorWeakness() {
  document.documentElement.classList.remove("grey-mode", "color-weakness")
}

export function useGreyAndColorWeakness() {
  return { initGreyAndColorWeakness }
}
