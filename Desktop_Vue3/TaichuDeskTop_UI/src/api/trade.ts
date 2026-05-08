//trade.ts
import request from '../utils/request';

/** 1. 后端 TradeResult 对应的接口 */
export interface ITradeResult {
  isSuccess: boolean;
  message: string;
  payload?: string; // 兑换成功后返回的密钥或链接
}

/** 2. 账户审计状态接口 */
export interface IAccountStatus {
  experience: number;
  level: number;
  maxSpaces: number;
  usedSpaces: number;
  maxNotes: number;
  usedNotes: number;
}

/** 3. 资源项接口 (与后端 StoreItem 对齐) */
export interface ITradeItem {
  id: number;
  name: string;
  description: string;
  benefit: string;
  category: 'Quota' | 'Asset' | 'Utility' | 'Social';
  baseCost: number;
  priceMultiplier: number;
  globalStock?: number;
  baseWeight: number;
  delivery: 'None' | 'Link' | 'SecretKey';
  // 🌟 重要：购买进度数据，用于计算当前真实价格
  purchaseCount: number; 
}

export const TradeApi = {
  /** 获取当前账户的 EXP 和 配额审计状态 */
  getAccountStatus: () => 
    request.get<IAccountStatus>('/trade/status'),

  /** 获取所有流转中的资源列表 */
  getStoreItems: () => 
    request.get<ITradeItem[]>('/trade/items'),

  /** 执行兑换请求 */
  purchase: (itemId: number) => 
    request.post<ITradeResult>(`/trade/purchase/${itemId}`)
};