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
  // --- 基础字段 ---
  username: string;
  email: string;
  createdAt: string;
  avatar?: string;
  gender: string; // 确保这里是 string
  age?: number;
  // --- 档案字段 (新补齐) ---
  address?: string;
  birthday?:string;
  bio?: string;
  mood?: string;
  socialLinks?: string; // 存储 JSON 字符串
  
// --- 补上这两行，解决报错 ---
  phoneNumber?: string;   // 联系电话
  extraConfig?: string;   // 额外配置信息

  // --- 核心修复：补上这两行 ---
  zodiac?: string;         // 星座
  chineseZodiac?: string;  // 生肖

  // --- 数值字段 ---
  level: number;
  experience: number;
  points: number;
  maxSignStreak: number;
  title?: string;
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