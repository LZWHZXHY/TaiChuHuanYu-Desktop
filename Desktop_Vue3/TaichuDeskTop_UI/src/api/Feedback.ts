import request from '../utils/request';

// ==========================================
// 1. 类型定义 (Types & Interfaces)
// ==========================================
export const EventStatus = {
  Draft: 0,
  Published: 1,
  Ongoing: 2,
  Completed: 3,
  Cancelled: 4
} as const;

export type EventStatusType = typeof EventStatus[keyof typeof EventStatus];

export interface ApiResponse<T = any> {
  code: number;
  data: T;
  message: string;
}

export interface CreateFeedbackDto {
  content: string;
  contactInfo?: string | null; // 🌟 完美兼容后端的 null
  images: string[];
  isAnonymous: boolean;
}

// ==========================================
// 2. API 方法封装 (纯净安全版)
// ==========================================
export const feedbackApi = {
  /**
   * 提交反馈
   */
  submit(data: CreateFeedbackDto): Promise<string> {
    return request.post<ApiResponse<string>>('/feedback', data).then(res => res.data);
  },

  /**
   * 🌟 获取公示反馈 (前端用户列表专用，安全脱敏数据)
   */
  getPublicFeedbacks(): Promise<any[]> {
    return request.get<ApiResponse<any[]>>('/feedback/public').then(res => res.data || []);
  },

  /**
   * 🚨 获取所有反馈 (管理后台专用，包含真实隐私数据)
   */
  getAllFeedbacks(): Promise<any[]> {
    return request.get<ApiResponse<any[]>>('/feedback').then(res => res.data || []);
  },

  /**
   * 修改反馈处理状态 (管理后台使用)
   */
  updateFeedbackStatus(id: string, status: number): Promise<string> {
    return request.patch<ApiResponse<string>>(`/feedback/${id}/status`, { status }).then(res => res.data);
  },

  /**
   * 删除反馈 (管理后台使用)
   */
  deleteFeedback(id: string): Promise<string> {
    return request.delete<ApiResponse<string>>(`/feedback/${id}`).then(res => res.data);
  }
};