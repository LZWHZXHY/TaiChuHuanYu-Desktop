import { bus } from './bus'
import { getStoredToken } from './authStorage'
import { createHttpClient } from './httpClient'

const request = createHttpClient({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  getToken: getStoredToken,
  onUnauthorized: token => bus.emit('auth-expired', token),
  onError: notice => bus.emit('api-error', notice),
})

export default request
