import axios from 'axios'

export interface ApiErrorNotice {
  title: string
  msg: string
  detail: string
  type: 'error'
}

export function getErrorMessage(error: unknown, fallback = '请求失败，请稍后重试'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (typeof data === 'string' && data.trim()) return data
    if (data && typeof data === 'object') {
      if (typeof data.message === 'string' && data.message) return data.message
      if (data.errors && typeof data.errors === 'object') {
        const messages = Object.values(data.errors).flat().filter((value) => typeof value === 'string')
        if (messages.length) return messages.join('\n')
      }
      if (typeof data.title === 'string' && data.title) return data.title
    }
    if (!error.response) return error.message || fallback
  }
  return fallback
}

export function describeApiError(error: unknown): ApiErrorNotice {
  const status = axios.isAxiosError(error) ? error.response?.status : undefined
  const data = axios.isAxiosError(error) ? error.response?.data : undefined
  const titles: Record<number, string> = {
    401: '身份验证失败',
    403: '权限受阻',
    500: '服务暂时不可用',
  }
  return {
    title: titles[status ?? 0] || '请求失败',
    msg: getErrorMessage(error, status === 403 ? '您当前权限不足，无权操作此项' : undefined),
    detail: data && typeof data === 'object' ? JSON.stringify(data, null, 2) : String(data || ''),
    type: 'error',
  }
}
