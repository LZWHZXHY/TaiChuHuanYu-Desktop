import axios from 'axios';
import { bus } from './bus';

// 1. 创建原始实例
const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 60000,
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

// 3. 响应拦截器 🌟 [在此处进行了全方位增强]
request.interceptors.response.use(
  (response) => response.data, 
  (error) => {
    const status = error.response?.status;
    const responseData = error.response?.data;

    // --- A. 核心：优先解析提取后端传回的精准错误文本 ---
    let backendMessage = '未知灵气波动';
    let errorTitle = '天道报错 (Debug)';

    if (responseData) {
      if (typeof responseData === 'string') {
        backendMessage = responseData;
      } else if (responseData.message) {
        backendMessage = responseData.message; // 对应后端 StatusCode(403, new { message = "..." })
      } else if (responseData.title) {
        backendMessage = responseData.title;   // 适配 .NET 默认的 ProblemDetails 结构
      }
    } else {
      backendMessage = error.message || '通信链接异常';
    }

    // --- B. 核心：根据不同状态码，定制化业务逻辑和通知标题 ---
    if (status === 401) {
      errorTitle = '身份验证失败';
      backendMessage = '身份凭证失效，请重新登陆账户';

      // 清除本地残存的身份印记
      localStorage.removeItem('token');
      localStorage.removeItem('username');

      // 如果当前不在登录页，立刻强行驱逐回接入界面
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
        // 返回一个永远 pending 的 Promise，斩断后续组件的链式报错
        return new Promise(() => {}); 
      }
    } 
    else if (status === 403) {
      errorTitle = '权限受阻';
      // 如果后端没给具体话术，给个保底话术
      if (backendMessage === '未知灵气波动') {
        backendMessage = '您当前权限不足，无权操作此项';
      }
    } 
    else if (status === 500) {
      errorTitle = '灵脉紊乱 (500)';
    }

    // --- C. 核心：提取更详细的 Debug 信息供用户展开查看 ---
    const backendDetail = responseData && typeof responseData === 'object'
      ? JSON.stringify(responseData, null, 2)
      : String(responseData || error.stack || '无进一步深层细节');

    // 🌟 发送总线事件，完美契合你的 GlobalNotify.vue 接收的参数格式
    bus.emit('api-error', { 
      title: errorTitle,      // 对应你的 data.title
      msg: backendMessage,    // 对应你的 data.msg
      detail: backendDetail,  // 对应你的 data.detail
      type: 'error'           // 类型固定为错误
    });

    error.friendlyMessage = `${backendMessage}: ${backendDetail}`;
    return Promise.reject(error);
  }
);

// 4. 类型包装：这是为了给 Admin.ts 提供上帝视角的类型提示
const service = {
  get: <T = any>(url: string, config?: any): Promise<T> => request.get(url, config),
  post: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.post(url, data, config),
  put: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.put(url, data, config),
  patch: <T = any>(url: string, data?: any, config?: any): Promise<T> => request.patch(url, data, config),
  delete: <T = any>(url: string, config?: any): Promise<T> => request.delete(url, config),
};

// 只有这一行是皇帝，唯一的默认导出
export default service;