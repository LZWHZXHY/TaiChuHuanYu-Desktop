import axios from 'axios';
import { bus } from './bus';



const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 8000,
});

// 【新增】请求拦截器：每次出门带上“令牌”
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

request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
    }

    // 1. 提取后端 GlobalExceptionFilter 返回的标准结构
    const backendMessage = error.response?.data?.message || '通信链接异常';
    const backendDetail = error.response?.data?.detail || error.message;
    
    // 2. 发射信号给全局组件
    bus.emit('api-error', { 
      msg: backendMessage, 
      detail: backendDetail 
    });

    // 保持原有的逻辑，方便局部组件继续 catch
    error.friendlyMessage = `${backendMessage}: ${backendDetail}`;
    return Promise.reject(error);
  }
);

export default request;