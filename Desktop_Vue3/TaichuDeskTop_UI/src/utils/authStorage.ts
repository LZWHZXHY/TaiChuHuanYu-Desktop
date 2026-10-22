import type { UserInfo } from '@/api/auth'

export const authStorageKeys = ['token', 'userInfo', 'username', 'userId'] as const

export function getStoredToken(): string {
  return localStorage.getItem('token') || ''
}

// 仅用于界面身份回退；权限校验由后端执行。
export function getTokenUserId(token: string): string {
  try {
    const encoded = token.split('.')[1] || ''
    const base64 = encoded.replace(/-/g, '+').replace(/_/g, '/')
    const payload = JSON.parse(atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')))
    const id = payload.sub || payload.nameid || payload.userId || payload.userid || payload.nameId
      || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
      || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier']
    return typeof id === 'string' ? id : ''
  } catch {
    return ''
  }
}

export function readStoredUser(): UserInfo | null {
  if (!getStoredToken()) return null
  try {
    const user = JSON.parse(localStorage.getItem('userInfo') || 'null')
    if (!user || typeof user.username !== 'string' || typeof user.email !== 'string') return null
    return {
      ...user,
      id: typeof user.id === 'string' ? user.id : getTokenUserId(getStoredToken()),
      permissions: Array.isArray(user.permissions)
        ? user.permissions.filter((permission: unknown) => typeof permission === 'string')
        : [],
    }
  } catch {
    return null
  }
}

export function persistUser(user: UserInfo | null): void {
  if (user) {
    localStorage.setItem('userInfo', JSON.stringify(user))
    localStorage.setItem('username', user.username)
    localStorage.setItem('userId', user.id)
  } else {
    for (const key of authStorageKeys) {
      if (key !== 'token') localStorage.removeItem(key)
    }
  }
}

export function persistToken(token: string): void {
  if (token) localStorage.setItem('token', token)
  else localStorage.removeItem('token')
}
