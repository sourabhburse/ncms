// Grey mode and color weakness are disabled (both default to false).
// [SETTINGS HOOK] Restore settingsStore-driven watchEffect to make these toggleable.

function initGreyAndColorWeakness() {
  document.documentElement.classList.remove("grey-mode", "color-weakness")
}

export function useGreyAndColorWeakness() {
  return { initGreyAndColorWeakness }
}
