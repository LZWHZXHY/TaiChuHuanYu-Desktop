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