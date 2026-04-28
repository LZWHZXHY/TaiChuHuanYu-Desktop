import request from '../utils/request';

// 签到数据类型：日期字符串 -> 状态码
export type SignData = Record<string, number>;

// 定义签到成功后的返回结构
export interface DoSignResponse {
  message: string;
  experienceAdded: number; // 👈 必须改为这个，对应后端的变量名
  currentStreak: number;   // 👈 新增：当前连签天数
  maxStreak: number;       // 👈 新增：历史最高连签
}

export const signApi = {
  // 获取月度数据
  getMonthData: (year: number, month: number) => 
    request.get<any, SignData>('/Sign/month-data', { 
      params: { year, month } 
    }),

  // 执行签到
  doSign: () => 
    request.post<any, DoSignResponse>('/Sign/do-sign')
};