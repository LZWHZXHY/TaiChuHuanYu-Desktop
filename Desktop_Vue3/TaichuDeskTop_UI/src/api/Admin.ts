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
  staticPayload?: string;
  isActive: boolean;
  createdAt?: string;
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
    request.post<IStoreItem>('/admin/trade/items', data),

  /** 更新资源详情 */
  updateItem: (id: number, data: Partial<IStoreItem>) => 
    request.put<IStoreItem>(`/admin/trade/items/${id}`, data),

  /** 快速切换状态 - 返回 IToggleResponse */
  toggleStatus: (id: number) => 
    request.patch<IToggleResponse>(`/admin/trade/items/${id}/toggle`),

  /** 核心补货逻辑 */
  restockSecrets: (itemId: number, secrets: string[]) => 
    request.post<any>(`/admin/trade/items/${itemId}/restock`, { secrets }),
};


export interface IWikiCategory {
  id: number;
  name: string;
  parentId: number | null;
  sortOrder: number;
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
};

// ================= 🌟 3. 新增：内容审核接口 (Revision Review) =================

export interface IReviewRequest {
  currentUserId: string;
  isAdmin: boolean;
  approved: boolean;
  remarks: string;
}

export const wikiReviewApi = {
  /**
   * 获取所有待审核的词条修订版 (Status == 0)
   * @param userId 当前用户ID (用于分类所有者权限过滤)
   * @param isAdmin 是否为管理员 (用于绕过权限检查获取全量数据)
   */
  getPending: (userId: string, isAdmin: boolean) => 
    request.get<any[]>(`/wiki/reviews/pending?userId=${userId}&isAdmin=${isAdmin}`),

  /**
   * 提交审核处理结果 (通过或驳回)
   * @param revisionId 修订版ID
   * @param data 包含审核决策和备注的对象
   */
  handle: (revisionId: number, data: IReviewRequest) => 
    request.post<any>(`/wiki/reviews/${revisionId}/handle`, data),
};