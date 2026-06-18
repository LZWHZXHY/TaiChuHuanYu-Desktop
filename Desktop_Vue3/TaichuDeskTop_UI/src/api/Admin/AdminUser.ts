// services/Admin/AdminUser.ts
import request from '@/utils/request';

// ==========================================
// 1. 数据传输对象 (DTO) 类型定义
// 严格对齐 C# 后端实体与用户组件.vue 的需求
// ==========================================

export interface UserProfileDto {
  avatar: string | null;
  gender: string | null;
  bio: string | null;
  mood: string | null;
  birthday: string | null;
  phoneNumber: string | null;
  age: number;            // 后端 [NotMapped] 计算属性
  zodiac: string;         // 后端 [NotMapped] 计算属性
  chineseZodiac: string;  // 后端 [NotMapped] 计算属性
}

export interface UserStatsDto {
  level: number;          // 后端 [NotMapped] 计算属性
  experience: number;
  reputation: number;
  title: string | null;
  currentSignStreak: number;
  maxSignStreak: number;
  usedNotes: number;
  usedSpaces: number;
  maxNotes: number;
  maxSpaces: number;
  maxProjectCount: number;
}

export interface UserDto {
  id: string;
  username: string;
  email: string | null;
  createdAt: string;
  profile: UserProfileDto | null;
  stats: UserStatsDto | null;
  permissions: string[];  // 对应 AdminPermission 枚举的字符串数组
}

// 治理操作入参类型
export interface UpdateStatsPayload {
  reputation: number;
  experience: number;
  maxSpaces: number;
  maxNotes: number;
  maxProjectCount: number;
}

// 🌟 新增：分页响应包装类型 (对齐 C# 后端返回的匿名对象)
export interface PaginatedResult<T> {
  totalCount: number;
  page: number;
  pageSize: number;
  items: T[];
}

// ==========================================
// 2. API 路由层 (Admin User API)
// ==========================================

export const adminUserApi = {
  /**
   * 分页获取用户列表 (带有服务端检索过滤)
   * 对应后端 Controller: [HttpGet]
   */
  getUsers(params: {
    page: number;
    pageSize: number;
    search?: string;
    permission?: string;
    reputation?: string;
  }): Promise<PaginatedResult<UserDto>> {
    return request.get<PaginatedResult<UserDto>>('/admin/users', { params });
  },

  /**
   * 深度更新用户核心资产与配额 (Stats)
   * 对应后端 Controller: [HttpPut("{userId}/stats")]
   */
  updateStats(userId: string, data: UpdateStatsPayload): Promise<void> {
    return request.put(`/admin/users/${userId}/stats`, data);
  },

  /**
   * 指派用户系统级管理权限 (UserPermission)
   * 对应后端 Controller: [HttpPut("{userId}/permissions")]
   * @param permissions - 权限枚举字符串数组，如 ['SuperAdmin', 'Wiki_Editor']
   */
  updatePermissions(userId: string, permissions: string[]): Promise<void> {
    return request.put(`/admin/users/${userId}/permissions`, permissions);
  },

  /**
   * 快捷审计干预：违规扣除信誉分
   * 对应后端 Controller: [HttpPost("{userId}/punish")]
   * @param deduction - 扣除的具体分数 (例如 15)
   */
  punish(userId: string, deduction: number): Promise<void> {
    // 采用 POST 语义更符合此类动作型请求，通过 query 或 body 传参均可
    return request.post(`/admin/users/${userId}/punish`, { deduction });
  }
};