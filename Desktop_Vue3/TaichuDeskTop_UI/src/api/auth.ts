import request from '../utils/request'

export interface Plugin {
  Name: string
  Url: string
  Icon: string
  RequiresAuth: boolean
}

// 登录返回基础身份；/User/me 补充可选的资料字段。
export interface UserInfo {
  id: string
  username: string
  email: string
  level: number
  permissions?: string[]
  createdAt?: string
  avatar?: string
  gender?: string
  age?: number
  address?: string
  birthday?: string
  bio?: string
  mood?: string
  socialLinks?: string
  phoneNumber?: string
  extraConfig?: string
  zodiac?: string
  chineseZodiac?: string
  experience?: number
  points?: number
  maxSignStreak?: number
  title?: string
}

export interface LoginCredentials {
  identifier: string
  password: string
}

interface RegisterPayload {
  username: string
  password: string
  email: string
  verificationCode: string
}

interface ResetPasswordPayload {
  email: string
  verificationCode: string
  newPassword: string
}

export interface LoginResponse {
  token: string
  message?: string
  user?: UserInfo
}

interface MessageResponse {
  message: string
}

const publicAuthConfig = { skipAuth: true, silentError: true }

export const authApi = {
  login: (data: LoginCredentials) =>
    request.post<LoginResponse>('/Auth/login', data, publicAuthConfig),
  register: (data: RegisterPayload) =>
    request.post<MessageResponse>('/Auth/register', data, publicAuthConfig),
  /** purpose：register 注册（邮箱已注册会直接提示）/ reset 找回密码（邮箱没注册会直接提示） */
  sendCode: (email: string, purpose?: 'register' | 'reset') =>
    request.post<MessageResponse>('/Auth/send-code', { email: email.trim(), purpose }, publicAuthConfig),
  resetPassword: (data: ResetPasswordPayload) =>
    request.post<MessageResponse>('/Auth/reset-password', data, publicAuthConfig),
  getPlugins: () => request.get<Plugin[]>('/Plugins'),
  // 当前后端没有返回登录接口中的 ID 和权限，Store 刷新时保留这两个字段。
  getUserInfo: () => request.get<Omit<UserInfo, 'id' | 'permissions'>>('/User/me'),
}
