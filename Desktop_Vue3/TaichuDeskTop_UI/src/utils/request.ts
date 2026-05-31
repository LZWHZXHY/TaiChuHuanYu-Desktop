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
  (response) => response.data, 
  (error) => {
    if (error.response?.status === 401) {
      // 1. 清除本地残存的身份印记
      localStorage.removeItem('token');
      localStorage.removeItem('username'); // 如果有存用户名，顺便一起清了

      // 2. 🌟 【核心新增】如果当前不在登录页，立刻强行驱逐回接入界面
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
        // 返回一个永远 pending 的 Promise，斩断后续组件的链式报错，让页面安静地跳转
        return new Promise(() => {}); 
      }
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