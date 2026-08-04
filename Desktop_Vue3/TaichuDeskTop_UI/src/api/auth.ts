// auth.ts 

import request from '../utils/request';

// 插件接口结构定义
export interface Plugin {
  Name: string;
  Url: string;
  Icon: string;
  RequiresAuth: boolean;
}


export interface UserInfo {
  id: string;
  // --- 基础字段 ---
  username: string;
  email: string;
  createdAt: string;
  avatar?: string;
  gender: string;
  age?: number;
  // --- 档案字段 ---
  address?: string;
  birthday?: string;
  bio?: string;
  mood?: string;
  socialLinks?: string;
  
  // --- 补上这两行 ---
  phoneNumber?: string;
  extraConfig?: string;

  // --- 核心修复：补上这两行 ---
  zodiac?: string;
  chineseZodiac?: string;

  // --- 数值字段 ---
  level: number;
  experience: number;
  points: number;
  maxSignStreak: number;
  title?: string;

  // ✅ 新增：权限列表
  permissions?: string[];  // 如 ["Survey_Manage", "Wiki_Editor"]
}

interface LoginResponse {
  token: string;
  message?: string;
  user?: UserInfo; 
}

export const authApi = {
  // 修复：移除 any，只保留 LoginResponse
  login: (data: any) => 
    request.post<LoginResponse>('/Auth/login', data),

  register: (data: any) => 
    request.post('/Auth/register', data),

  sendCode: (email: string) => 
    request.post('/Auth/send-code', { email }),

  resetPassword: (data: any) => 
    request.post('/Auth/reset-password', data),

  // 修复：移除 any，只保留 Plugin[]
  getPlugins: () => 
    request.get<Plugin[]>('/Plugins'),

  // 修复：移除 any，只保留 UserInfo
  getUserInfo: () =>
    request.get<UserInfo>('/User/me') 
};