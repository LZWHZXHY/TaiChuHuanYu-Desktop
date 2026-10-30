// services/Admin.ts
import request from '../utils/request';

// 🌟 定义物品接口，与后端 C# 模型一一对应
export interface IStoreItem {
  id: number;
  name: string;
  category: 'Quota' | 'Asset' | 'Utility' | 'Social';
  delivery: 'None' | 'Link' | 'SecretKey';
  baseCost: number;
  priceMultiplier: number;
  globalStock: number | null;
  baseWeight: number;
  benefit: string;
  description: string;
  staticPayload?: string | null;
  isActive: boolean;
  sortOrder?: number;
  createdAt?: string;
  /** 土豆价；null = 不能用土豆买（发卡密 / 链接的商品只能用经验） */
  potatoCost: number | null;
  /** 效果：title 头衔 / frame 头像框 / bg 网站背景 / pbg 主页横幅或整页背景 / avatar 官方头像（值是素材 Id）/ rename 改名卡 */
  effect: 'title' | 'frame' | 'bg' | 'pbg' | 'avatar' | 'rename' | null;
  effectValue: string | null;
  /** 以下只读：卡密总数 / 未发出、累计售出 */
  secretsTotal?: number;
  secretsUnused?: number;
  sold?: number;
}

/** 后台上传的图片头像框 */
export interface IFrameDef {
  id: number;
  key: string;
  name: string;
  url: string;
  scale: number;
  sortOrder: number;
  enabled: boolean;
  authorId: string | null;
  authorName: string | null;
}
export interface IFrameDefSave { name: string; url: string; scale: number; authorId: string | null; sortOrder: number; enabled: boolean }

/** 官方头像 */
export interface IAvatarDef { id: number; name: string; url: string; animated: boolean; sortOrder: number; enabled: boolean; authorId: string | null; authorName: string | null }
export interface IAvatarDefSave { name: string; url: string; animated: boolean; authorId: string | null; sortOrder: number; enabled: boolean }

export interface IStoreSecret {
  id: number;
  code: string;
  isUsed: boolean;
  assignedAt: string | null;
  assignedTo: string | null;
}

// 🌟 定义状态切换的返回结构
interface IToggleResponse {
  id: number;
  isActive: boolean;
}

export const TradeApi = {
  /** 获取所有资源列表 - 返回 IStoreItem 数组 */
  getAllItems: () => 
    request.get<IStoreItem[]>('/admin/trade/items'),

  /** 上架新资源 - 返回创建成功的对象 */
  createItem: (data: Partial<IStoreItem>) => 
    request.post<IStoreItem>('/admin/trade/items', data, { silentError: true }),

  /** 更新资源详情 */
  updateItem: (id: number, data: Partial<IStoreItem>) => 
    request.put<IStoreItem>(`/admin/trade/items/${id}`, data, { silentError: true }),

  /** 快速切换状态 - 返回 IToggleResponse */
  toggleStatus: (id: number) => 
    request.patch<IToggleResponse>(`/admin/trade/items/${id}/toggle`),

  /** 补卡密（一行一个） */
  restockSecrets: (itemId: number, secrets: string[]) =>
    request.post<{ added: number; skipped: number; unused: number }>(`/admin/trade/items/${itemId}/restock`, { secrets }, { silentError: true }),

  /** 卡密明细（打码显示） */
  secrets: (itemId: number) =>
    request.get<IStoreSecret[]>(`/admin/trade/items/${itemId}/secrets`),

  /** 图片头像框素材 */
  frames: () => request.get<IFrameDef[]>('/admin/trade/frames'),
  saveFrame: (body: IFrameDefSave, id?: number) =>
    request.put<{ id: number; key: string }>('/admin/trade/frames', body, { params: id ? { id } : undefined, silentError: true }),
  deleteFrame: (id: number) => request.delete(`/admin/trade/frames/${id}`, { silentError: true }),
  /** 官方头像素材 */
  avatars: () => request.get<IAvatarDef[]>('/admin/trade/avatars'),
  uploadAvatar: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request.post<{ url: string; animated: boolean }>('/admin/trade/avatars/upload', form, { silentError: true });
  },
  saveAvatar: (body: IAvatarDefSave, id?: number) =>
    request.put<{ id: number }>('/admin/trade/avatars', body, { params: id ? { id } : undefined, silentError: true }),
  deleteAvatar: (id: number) => request.delete(`/admin/trade/avatars/${id}`, { silentError: true }),

  /** 按用户名找账号（填原作者） */
  findUsers: (q: string) => request.get<{ userId: string; name: string }[]>('/admin/trade/users', { params: { q }, silentError: true }),

  /** 删除一个还没发出去的卡密 */
  deleteSecret: (itemId: number, secretId: number) =>
    request.delete<{ ok: boolean }>(`/admin/trade/items/${itemId}/secrets/${secretId}`, { silentError: true }),
};


export interface IWikiCategory {
  id: number;
  name: string;
  parentId: number | null;
  sortOrder: number;
  ownerId?: string | null;  // 用户的 Guid ID，可能为空
  ownerNickname?: string;  // 用户的昵称。加个问号 `?` 代表它是“可选”的，防止某些旧数据没有这个字段时报错
}

export interface ICategoryPayload {
  name: string;
  parentId: number | null;
  sortOrder: number;
  ownershipType: number;
  ownerId: string | null;
}
export interface ICategoryRequest {
  id: number;
  name: string;
  reason: string;
  parentId: number | null;
  sortOrder: number;
}
export const adminWikiApi = {
  /** 获取所有百科分类 (界域) 列表 */
  getAllCategories: () => 
    request.get<IWikiCategory[]>('/admin/wiki/categories'),

  /** 新增百科分类 */
  createCategory: (data: ICategoryPayload) => 
    request.post<IWikiCategory>('/admin/wiki/categories', data),

  /** 修改分类信息 (重命名、调整层级或排序) */
  updateCategory: (id: number, data: ICategoryPayload) => 
    request.put<IWikiCategory>(`/admin/wiki/categories/${id}`, data),

  /** 删除分类 (带有关联检测) */
  deleteCategory: (id: number) => 
    request.delete<any>(`/admin/wiki/categories/${id}`),

  // ================= 🌟 2. 新增：审核相关的 3 个 API =================

  /** 获取所有待审批的分类申请 */
  getCategoryRequests: () => 
    request.get<ICategoryRequest[]>('/admin/wiki/requests'),

  /** 批准申请，将其转为正式分类 */
  approveCategoryRequest: (id: number) => 
    request.post<any>(`/admin/wiki/requests/${id}/approve`),

  /** 驳回/拒绝申请 */
  rejectCategoryRequest: (id: number) => 
    request.post<any>(`/admin/wiki/requests/${id}/reject`),

  /** 获取所有文章（包含已下架的，用于管理面板） */
  getAllArticlesForManagement: () => 
    request.get<any[]>('/wiki/governance/articles/manage'),
};

// ================= 🌟 3. 新增：内容审核接口 (Revision Review) =================

// ================= 🌟 统一治理接口 (配合 WikiReviewController) =================

export interface IReviewRequest {
  approved: boolean;
  remarks: string;
}

export const wikiReviewApi = {
  /** 修正：添加 /api 前缀 */
  getPending: () => 
    request.get<any[]>(`/wiki/governance/pending`),

  /** 修正：添加 /api 前缀 */
  handle: (revisionId: number, data: IReviewRequest) => 
    request.post<any>(`/wiki/governance/revisions/${revisionId}/handle`, data),

  /** 修正：添加 /api 前缀 */
  toggleArticleArchive: (articleId: string) => 
    request.post(`/wiki/governance/articles/${articleId}/toggle-archive`, {}),

  /** 修正：添加 /api 前缀 */
  rollbackArticle: (articleId: string, targetRevisionId: number) => 
    request.post(`/wiki/governance/articles/${articleId}/rollback/${targetRevisionId}`, {}),
};