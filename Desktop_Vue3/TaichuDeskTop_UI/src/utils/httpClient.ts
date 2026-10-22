import axios, { type AxiosRequestConfig } from 'axios'
import { describeApiError, type ApiErrorNotice } from './apiError'

declare module 'axios' {
  interface AxiosRequestConfig {
    /** 登录、注册等公开接口不携带凭证，也不触发会话失效处理。 */
    skipAuth?: boolean
    /** 由调用方显示错误，例如登录表单。 */
    silentError?: boolean
  }
}

interface HttpClientOptions {
  baseURL?: string
  getToken: () => string
  onUnauthorized: (token: string) => void
  onError: (notice: ApiErrorNotice) => void
}

export function createHttpClient(options: HttpClientOptions) {
  const client = axios.create({ baseURL: options.baseURL, timeout: 60000 })

  client.interceptors.request.use((config) => {
    const token = options.getToken()
    if (!config.skipAuth && token) config.headers.set('Authorization', `Bearer ${token}`)
    return config
  })

  client.interceptors.response.use(undefined, (error: unknown) => {
    if (axios.isCancel(error)) return Promise.reject(error)

    const config = axios.isAxiosError(error) ? error.config : undefined
    const status = axios.isAxiosError(error) ? error.response?.status : undefined
    const authorization = config?.headers.get('Authorization')
    const requestToken = typeof authorization === 'string' && authorization.startsWith('Bearer ')
      ? authorization.slice(7) : ''
    const notice = describeApiError(error)

    if (status === 401 && !config?.skipAuth && requestToken) {
      // 忽略旧会话的迟到响应；当前会话清理后，其余并发 401 也会被忽略。
      if (requestToken !== options.getToken()) return Promise.reject(error)
      notice.msg = '身份凭证失效，请重新登录账户'
      options.onUnauthorized(requestToken)
    }

    if (error instanceof Error) Object.assign(error, { friendlyMessage: notice.msg })
    if (!config?.silentError) options.onError(notice)
    return Promise.reject(error)
  })

  // 仅解包 HTTP 响应，保留 Blob 和业务层的 { data } 结构。
  // 兼容旧调用方的默认 any；新增 API 应明确填写响应类型。
  return {
    get: <T = any>(url: string, config?: AxiosRequestConfig): Promise<T> =>
      client.get<T>(url, config).then(response => response.data),
    post: <T = any>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> =>
      client.post<T>(url, data, config).then(response => response.data),
    put: <T = any>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> =>
      client.put<T>(url, data, config).then(response => response.data),
    patch: <T = any>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> =>
      client.patch<T>(url, data, config).then(response => response.data),
    delete: <T = any>(url: string, config?: AxiosRequestConfig): Promise<T> =>
      client.delete<T>(url, config).then(response => response.data),
  }
}
