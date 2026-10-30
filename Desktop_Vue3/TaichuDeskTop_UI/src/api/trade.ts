//trade.ts
import request from '../utils/request';

/** 1. 后端 TradeResult 对应的接口 */
export interface ITradeResult {
  isSuccess: boolean;
  message: string;
  payload?: string; // 兑换成功后返回的密钥或链接
}

/** 2. 账户状态：经验、等级、土豆、配额 */
export interface IAccountStatus {
  experience: number;
  level: number;
  potatoes: number;
  maxSpaces: number;
  usedSpaces: number;
  maxNotes: number;
  usedNotes: number;
  maxProjectCount: number;
  usedProjectCount: number;
  /** 世界观：已创建 / 上限；每个世界的节点上限（所有世界通用）。旧后端没有这些字段 */
  usedWorldCount?: number;
  maxWorldCount?: number;
  maxCardsPerWorld?: number;
}

export type TradeCurrency = 'exp' | 'potato';
export type ItemEffect = 'title' | 'frame' | 'bg' | 'pbg' | 'avatar' | 'rename';

/** 3. 商品（价格由后端按自己的购买次数算好） */
export interface ITradeItem {
  id: number;
  name: string;
  description: string;
  benefit: string;
  category: 'Quota' | 'Asset' | 'Utility' | 'Social';
  baseCost: number;
  priceMultiplier: number;
  baseWeight: number;
  delivery: 'None' | 'Link' | 'SecretKey';
  purchaseCount: number;
  /** 当前经验价 */
  expCost: number;
  /** 当前土豆价；null = 不能用土豆买 */
  potatoCost: number | null;
  /** 只能用经验买（发卡密 / 链接的商品） */
  expOnly: boolean;
  effect: ItemEffect | null;
  effectValue: string | null;
  /** 头衔、头像框已经拥有 */
  owned: boolean;
  /** 剩余库存；null = 不限 */
  stock: number | null;
  /** 官方背景商品的预览图 */
  preview?: { title: string; kind: 'image' | 'video' | 'animated'; url: string; poster: string | null; slot?: 'site' | 'banner' | 'page' | 'avatar' } | null;
}

/** 官方头像（静态 / 动图） */
export interface IOfficialAvatar {
  id: number;
  name: string;
  url: string;
  animated: boolean;
  /** 在交易行出售，要先兑换 */
  locked: boolean;
  owned: boolean;
  storeItemId: number | null;
  author: string | null;
}

/** 4. 我的装扮 */
export interface IInventory {
  titles: { key: string; source: string; at: string }[];
  frames: { key: string; source: string; at: string }[];
  /** 兑换过的官方背景（key 是背景 Id） */
  backgrounds?: { key: string; source: string; at: string }[];
  /** 兑换过的主页横幅 / 整页背景 */
  profileBackgrounds?: { key: string; source: string; at: string }[];
  /** 兑换过的官方头像 */
  avatars?: { key: string; source: string; at: string }[];
  equippedTitle: string | null;
  equippedFrame: string | null;
  renameCards: number;
}

export const TradeApi = {
  /** 获取当前账户的经验、土豆和配额 */
  getAccountStatus: () =>
    request.get<IAccountStatus>('/trade/status'),

  /** 获取上架中的商品 */
  getStoreItems: () =>
    request.get<ITradeItem[]>('/trade/items'),

  /** 兑换（currency：exp 经验 / potato 土豆） */
  purchase: (itemId: number, currency: TradeCurrency = 'exp') =>
    request.post<ITradeResult>(`/trade/purchase/${itemId}`, null, { params: { currency }, silentError: true }),

  /** 我拥有的头衔、头像框、改名卡 */
  inventory: () => request.get<IInventory>('/trade/inventory', { silentError: true }),

  /** 佩戴 / 取下（key 传 null 表示取下） */
  equip: (kind: 'title' | 'frame', key: string | null) =>
    request.post<{ kind: string; key: string | null }>('/trade/equip', { kind, key }, { silentError: true }),

  /** 官方头像列表 */
  avatars: () => request.get<IOfficialAvatar[]>('/trade/avatars', { silentError: true }),
  /** 换上官方头像 */
  useAvatar: (id: number) => request.post<{ avatar: string }>(`/trade/avatars/${id}/use`, null, { silentError: true }),
  /** 上传自己的头像（只收静态图） */
  uploadAvatar: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request.post<{ avatar: string }>('/User/avatar', form, { silentError: true });
  },

  /** 某个人佩戴的头衔和头像框 */
  appearance: (userId: string) =>
    request.get<{ title: string | null; frame: string | null }>(`/trade/appearance/${userId}`, { silentError: true, skipAuth: true }),
};

/** 从接口错误里取出后端给的提示 */
export const tradeError = (e: any, fallback: string) => e?.response?.data?.message || e?.friendlyMessage || fallback;
