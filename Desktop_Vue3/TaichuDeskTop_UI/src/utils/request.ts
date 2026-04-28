import axios from 'axios';

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

// 响应拦截器：保持你现在的代码，处理报错
// request.ts 响应拦截器
request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
    }

    // --- 核心修改：提取后端最深处的错误信息 ---
    const backendMessage = error.response?.data?.message;
    const backendDetail = error.response?.data?.detail;
    
    // 把提取到的信息挂载到 error 对象上，方便组件直接读取
    error.friendlyMessage = backendMessage || '通信链接异常';
    if (backendDetail) error.friendlyMessage += `: ${backendDetail}`;

    console.error('API Error:', error.friendlyMessage);
    return Promise.reject(error);
  }
);

export default request;