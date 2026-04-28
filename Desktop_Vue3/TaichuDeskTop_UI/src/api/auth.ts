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
  // 建议登录接口直接返回部分用户信息
  user?: UserInfo; 
}

export const authApi = {
  login: (data: any) => 
    request.post<any, LoginResponse>('/Auth/login', data),

  register: (data: any) => 
    request.post('/Auth/register', data),

  sendCode: (email: string) => 
    request.post('/Auth/send-code', { email }),

  getPlugins: () => 
    request.get<any, Plugin[]>('/Plugins'),


  getUserInfo: () =>
    request.get<any, UserInfo>('/User/me') 
};