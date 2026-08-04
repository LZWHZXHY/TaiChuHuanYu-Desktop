import request from '@/utils/request'

// ========== 类型定义 ==========
export interface StickmanCharacter {
  id: string
  name: string
  nickname?: string
  gender: '男' | '女' | '未知' | '其他'
  age?: number
  height?: string
  weight?: string
  appearance: string
  outfit?: string
  personality: string
  background: string
  abilities?: string
  tags: string[]
  avatar?: string
  gallery: string[]
  authorId: string
  authorName: string
  views: number
  likes: number
  favorites: number
  createdAt: string
  updatedAt?: string
  status: 'draft' | 'published' | 'archived'
}

export interface CreateStickmanDto {
  name: string
  nickname?: string
  gender: string
  age?: number
  height?: string
  weight?: string
  appearance: string
  outfit?: string
  personality: string
  background: string
  abilities?: string
  tags: string[]
  avatar?: string
  gallery: string[]
  status: 'draft' | 'published'
}

export interface UpdateStickmanDto extends Partial<CreateStickmanDto> {}

// ========== API 函数 ==========
export const stickmanApi = {
  // 获取角色列表（公开）
  getList: (params?: { page?: number; pageSize?: number; keyword?: string; tag?: string }) =>
    request.get<{ items: StickmanCharacter[]; total: number }>('/stickman', { params }),

  // 获取角色详情
  getDetail: (id: string) =>
    request.get<StickmanCharacter>(`/stickman/${id}`),

  // 创建角色
  create: (data: CreateStickmanDto) =>
    request.post<StickmanCharacter>('/stickman', data),

  // 更新角色
  update: (id: string, data: UpdateStickmanDto) =>
    request.put<StickmanCharacter>(`/stickman/${id}`, data),

  // 删除角色
  delete: (id: string) =>
    request.delete(`/stickman/${id}`),

  // 获取我的角色列表
  getMyCharacters: (params?: { status?: string }) =>
    request.get<StickmanCharacter[]>('/stickman/my', { params }),

  // 点赞/取消点赞
  toggleLike: (id: string) =>
    request.post(`/stickman/${id}/like`),

  // 收藏/取消收藏
  toggleFavorite: (id: string) =>
    request.post(`/stickman/${id}/favorite`),
}