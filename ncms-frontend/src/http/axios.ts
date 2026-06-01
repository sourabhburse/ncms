import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from "axios"
import axios, { AxiosError } from "axios"
import { merge } from "lodash-es"

function createInstance() {
  const instance = axios.create()

  // ── Request interceptor ──────────────────────────────────────────────────
  instance.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
      // [AUTH HOOK] Inject bearer token when implementing auth:
      // import { getToken } from "@@/utils/local-storage"
      // const token = getToken()
      // if (token) config.headers.Authorization = `Bearer ${token}`
      return config
    },
    (error: AxiosError) => Promise.reject(error)
  )

  // ── Response interceptor ─────────────────────────────────────────────────
  instance.interceptors.response.use(
    (response: AxiosResponse) => {
      const apiData = response.data
      const responseType = response.config.responseType
      if (responseType === "blob" || responseType === "arraybuffer") return apiData

      const code = apiData.code
      // No envelope (raw response from backend) — pass data through as-is
      if (code === undefined) return apiData

      if (code === 0) return apiData

      // [AUTH HOOK] Handle token expiry when implementing auth:
      // if (code === 401) { useUserStore().logout(); router.push("/login"); return }

      ElMessage.error(apiData.message || "Request failed")
      return Promise.reject(new Error(apiData.message || "Error"))
    },
    (error: AxiosError<{ message?: string }>) => {
      const status = error.response?.status
      const serverMessage = error.response?.data?.message

      // [AUTH HOOK] Handle 401 when implementing auth:
      // if (status === 401) { useUserStore().logout(); router.push("/login"); return }

      const statusMessages: Record<number, string> = {
        400: "Bad Request (400)",
        403: "Access Denied (403)",
        404: "Resource Not Found (404)",
        408: "Request Timeout (408)",
        500: "Internal Server Error (500)",
        501: "Not Implemented (501)",
        502: "Bad Gateway (502)",
        503: "Service Unavailable (503)",
        504: "Gateway Timeout (504)",
        505: "HTTP Version Not Supported (505)"
      }

      error.message = serverMessage || statusMessages[status] || `Connection Error (${status ?? "unknown"})`
      ElMessage.error(error.message)
      return Promise.reject(error)
    }
  )

  return instance
}

function createRequest(instance: AxiosInstance) {
  return <T>(config: AxiosRequestConfig): Promise<T> => {
    const defaultConfig: AxiosRequestConfig = {
      baseURL: import.meta.env.VITE_BASE_URL,
      headers: {
        // [AUTH HOOK] Add Authorization header here when implementing auth:
        // "Authorization": token ? `Bearer ${token}` : undefined,
        "Content-Type": "application/json"
      },
      data: {},
      timeout: 5000,
      withCredentials: false
    }
    return instance(merge(defaultConfig, config))
  }
}

const instance = createInstance()

export const request = createRequest(instance)
