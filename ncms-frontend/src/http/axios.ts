import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from "axios"
import axios, { AxiosError } from "axios"
import { merge } from "lodash-es"

function createInstance() {
  const instance = axios.create()

  // ── Request interceptor ──────────────────────────────────────────────────
  instance.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
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

      ElMessage.error(apiData.message || "Request failed")
      return Promise.reject(new Error(apiData.message || "Error"))
    },
    (error: AxiosError<{ message?: string }>) => {
      const status = error.response?.status
      const serverMessage = error.response?.data?.message

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

      const statusMessage = status === undefined ? undefined : statusMessages[status]
      error.message = serverMessage || statusMessage || `Connection Error (${status ?? "unknown"})`
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
        "Content-Type": "application/json"
      },
      data: {},
      timeout: 5000,
      withCredentials: false
    }
    const finalConfig = merge(defaultConfig, config)
    // For multipart uploads, drop the JSON content-type so the browser can set
    // `multipart/form-data` together with the required boundary.
    if (finalConfig.data instanceof FormData) {
      delete (finalConfig.headers as Record<string, unknown>)["Content-Type"]
    }
    return instance(finalConfig)
  }
}

const instance = createInstance()

export const request = createRequest(instance)
