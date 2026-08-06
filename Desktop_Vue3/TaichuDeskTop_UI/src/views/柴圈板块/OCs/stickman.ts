import request from '@/utils/request'

// ========== 属性类型 ==========
export interface StickmanAttribute {
  id: string
  key: string
  value?: string
  sortOrder: number
  type: 'short' | 'long'   // ✅ 已有
}

// ========== 图片类型 ==========
export interface StickmanImage {
  id: string
  url: string
  alt?: string
  sortOrder: number
}

// ========== 角色类型 ==========
// ========== 角色类型 ==========
export interface StickmanCharacter {
  id: string
  title: string
  description?: string
  coverUrl?: string
  authorId: string
  authorName: string
  views: number
  status: 'draft' | 'published' | 'archived'
  isBattleEnabled?: boolean
  createdAt: string
  updatedAt?: string
  attributes: StickmanAttribute[]
  images: StickmanImage[]
  // ⭐ 新增：约战战绩
  battleWins: number
  battleLosses: number
  battleDraws: number
}

// ========== 创建 DTO ==========
export interface CreateStickmanDto {
  title: string
  description?: string
  coverUrl?: string
  status: 'draft' | 'published'
  attributes?: { 
    key: string
    value?: string
    sortOrder: number
    type: 'short' | 'long'   // ← 新增
  }[]
  images?: { url: string; alt?: string; sortOrder: number }[]
}

// ========== 更新 DTO ==========
export interface UpdateStickmanDto {
  title?: string
  description?: string
  coverUrl?: string
  status?: string
  attributes?: { 
    id?: string
    key: string
    value?: string
    sortOrder: number
    type: 'short' | 'long'   // ← 新增
  }[]
  images?: { id?: string; url: string; alt?: string; sortOrder: number }[]
}

// ========== 列表响应 ==========
export interface StickmanListResponse {
  items: StickmanCharacter[]
  total: number
  page: number
  pageSize: number
}

// ========== API 函数 ==========
export const stickmanApi = {
  getList: (params?: { page?: number; pageSize?: number; keyword?: string; tag?: string }) =>
    request.get<StickmanListResponse>('/StickMan', { params }),

  getDetail: (id: string) =>
    request.get<StickmanCharacter>(`/StickMan/${id}`),

  create: (data: CreateStickmanDto) =>
    request.post<StickmanCharacter>('/StickMan', data),

  update: (id: string, data: UpdateStickmanDto) =>
    request.put<StickmanCharacter>(`/StickMan/${id}`, data),

  delete: (id: string) =>
    request.delete(`/StickMan/${id}`),

  getMyCharacters: (params?: { status?: string }) =>
    request.get<StickmanCharacter[]>('/StickMan/my', { params }),
}