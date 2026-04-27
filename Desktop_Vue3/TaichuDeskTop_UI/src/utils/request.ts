import axios from 'axios';

const request = axios.create({
  baseURL: 'https://api.bianyuzhou.com/api', // 你的全栈后端地址
  timeout: 8000,
});

// 响应拦截器：统一处理错误提示
request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const message = error.response?.data?.message || '通信链接异常，请检查网络';
    console.error('API Error:', message);
    return Promise.reject(error);
  }
);

export default request;