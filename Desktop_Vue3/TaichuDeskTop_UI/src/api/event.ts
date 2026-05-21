import request from '../utils/request';

// ==========================================
// 1. 类型定义 (Types & Interfaces)
// ==========================================

// 🌟 常量对象 (Plain Object) + as const，避免 enum 带来的编译报错
export const EventStatus = {
  Draft: 0,       // 草稿/未发布
  Published: 1,   // 已发布/未开始
  Ongoing: 2,     // 进行中
  Completed: 3,   // 已结束
  Cancelled: 4    // 已取消
} as const;

// 🌟 巧妙提取类型：EventStatusType 的值将被限定为 0 | 1 | 2 | 3 | 4
export type EventStatusType = typeof EventStatus[keyof typeof EventStatus];

/**
 * 后端统一返回格式
 */
export interface ApiResponse<T = any> {
  code: number;
  data: T;
  message: string;
}

/**
 * 活动数据传输对象
 */
export interface EventDto {
  id: string;
  title: string;
  description?: string;
  startDate: string;  
  endDate: string;    
  startTime?: string; 
  endTime?: string;   
  status: EventStatusType; 
}

/**
 * 按天分组的活动字典
 */
export type DailyEventsMap = Record<string, EventDto[]>;


// ==========================================
// 2. API 方法封装
// ==========================================

export const eventApi = {
  /**
   * 获取指定月份的活动列表（日历视图使用）
   * @param year 年份，例如 2026
   * @param month 月份，1 - 12
   */
  getMonthEvents(year: number, month: number): Promise<DailyEventsMap> {
    return request.get<ApiResponse<DailyEventsMap>>('/event/month', {
      params: { year, month }
    }).then(res => res.data || {});
  },

  /**
   * 获取活动详情
   */
  getEventDetail(id: string): Promise<EventDto> {
    return request.get<ApiResponse<EventDto>>(`/event/${id}`).then(res => res.data);
  },

  // ----------------------------------------
  // 下面是管理后台专用的 CRUD 接口
  // ----------------------------------------

  /**
   * 获取所有活动列表 (管理后台用)
   */
  getAllEvents(): Promise<EventDto[]> {
    return request.get<ApiResponse<EventDto[]>>('/event').then(res => res.data || []);
  },

  /**
   * 创建新活动
   */
  createEvent(data: Partial<EventDto>): Promise<string> {
    return request.post<ApiResponse<string>>('/event', data).then(res => res.data);
  },

  /**
   * 更新完整活动信息
   */
  updateEvent(id: string, data: Partial<EventDto>): Promise<string> {
    return request.put<ApiResponse<string>>(`/event/${id}`, data).then(res => res.data);
  },

  /**
   * 快捷更新活动状态 (局部更新)
   */
  updateEventStatus(id: string, status: number): Promise<string> {
    return request.patch<ApiResponse<string>>(`/event/${id}/status`, { status }).then(res => res.data);
  },

  /**
   * 删除活动
   */
  deleteEvent(id: string): Promise<string> {
    return request.delete<ApiResponse<string>>(`/event/${id}`).then(res => res.data);
  }
};