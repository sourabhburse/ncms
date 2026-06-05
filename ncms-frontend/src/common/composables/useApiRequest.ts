import { ref } from "vue"

// ─────────────────────────────────────────────────────────────────────────────
// useApiRequest — standardizes the repeated loading / error / try-catch wrapper
// around an async API call.
//
// The axios response interceptor already surfaces failures via ElMessage, so by
// default this only records the message for inline empty/error states. Pass
// `onError` when a page needs extra handling.
// ─────────────────────────────────────────────────────────────────────────────

interface UseApiRequestOptions {
  /** Message stored in `error` when the call throws (before the thrown message). */
  fallbackError?: string
  /** Extra handling on failure (the interceptor already shows a toast). */
  onError?: (err: unknown) => void
}

export function useApiRequest<TArgs extends unknown[], TResult>(
  fn: (...args: TArgs) => Promise<TResult>,
  options: UseApiRequestOptions = {}
) {
  const loading = ref(false)
  const error = ref<string | null>(null)

  /** Run the request. Resolves to the result, or `undefined` if it threw. */
  async function execute(...args: TArgs): Promise<TResult | undefined> {
    loading.value = true
    error.value = null
    try {
      return await fn(...args)
    } catch (err: unknown) {
      error.value = err instanceof Error
        ? err.message
        : (options.fallbackError ?? "Request failed.")
      options.onError?.(err)
      return undefined
    } finally {
      loading.value = false
    }
  }

  return { loading, error, execute }
}
