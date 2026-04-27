import axios from 'axios';
import request from '../utils/request';



// 创建 axios 实例
const service = axios.create({
  baseURL: 'https://api.bianyuzhou.com/api',
  timeout: 5000
});


interface LoginResponse {
  token: string;
  message?: string;
  user?: any;
}

export const authApi = {
  login: (data: any) => 
    request.post<any, LoginResponse>('/Notes/login', data),
  register: (data: any) => 
    request.post('/Notes/register', data),
  forgotPassword: (email: string) => 
    request.post('/Notes/forgot-password', { email })
};