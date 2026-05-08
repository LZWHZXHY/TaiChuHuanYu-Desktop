import axios from 'axios';
import { bus } from './bus';

// 1. 创建原始实例
const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 8000,
});

// 2. 请求拦截器
request.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// 3. 响应拦截器
request.interceptors.response.use(
  (response) => response.data, // 🌟 这里已经在运行时剥离了 data
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
    }

    const backendMessage = error.response?.data?.message || '通信链接异常';
    const backendDetail = error.response?.data?.detail || error.message;
    
    bus.emit('api-error', { 
      msg: backendMessage, 
      detail: backendDetail 
    });

    error.friendlyMessage = `${backendMessage}: ${backendDetail}`;
    return Promise.reject(error);
  }
);

// 4. 🌟 类型包装：这是为了给 Admin.ts 提供上帝视角的类型提示
const service = {
  get: <T = any>(url: string, config?: any): Promise<T> => request.get(url, config),
  post: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.post(url, data, config),
  put: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.put(url, data, config),
  patch: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.patch(url, data, config),
  delete: <T = any>(url: string, config?: any): Promise<T> => request.delete(url, config),
};

// 只有这一行是皇帝，唯一的默认导出
export default service;