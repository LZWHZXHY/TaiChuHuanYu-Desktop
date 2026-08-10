// src/api/worldApi.ts
import request from '@/utils/request';

// ============================================================
//  类型定义（供外部使用）
// ============================================================

/**
 * 卡片精简数据（用于列表页，后端返回 CardSummaryDto）
 */
export interface CardSummary {
  id: string;
  projectId: string;
  title: string;
  type: string;
  coverImage: string | null;   // JSON 数组字符串，前端解析取第一张
  updatedAt: string;
  outRelationCount: number;
  inRelationCount: number;
}

/**
 * 卡片完整数据（用于详情页，包含所有字段和关系对象）
 */
export interface CardDetail extends CardSummary {
  description: string;
  content: string;
  contentBlocks: any[];
  timelineEvents: any[];
  aliases: string[];
  attributes: any[];
  tags: string[];
  embeddedCards: any[];
  galleryImages: string[];
  outRelations: Relation[];
  inRelations: Relation[];
}

/**
 * 关系对象
 */
export interface Relation {
  id: string;
  sourceCardId: string;
  targetCardId: string;
  relationType: string;
  createdAt: string;
  sourceCardTitle: string;
  targetCardTitle: string;
  sourceCardType: string;
  targetCardType: string;
}

// ============================================================
//  API 函数
// ============================================================

export const worldApi = {
  // ----- 卡片类型 -----
  getCardTypes: async () => {
    const data = await request.get('/world/card-types');
    return { data };
  },

  // ----- 项目 -----
  getProjects: async () => {
    const data = await request.get('/world/projects');
    return { data };
  },

  getPublicProjects: async () => {
    const data = await request.get('/world/projects/public');
    return { data };
  },

  createProject: async (payload: { name: string; description?: string; isPublic?: boolean }) => {
    const data = await request.post('/world/projects', payload);
    return { data };
  },

  updateProject: async (projectId: string, payload: { name?: string; description?: string; isPublic?: boolean }) => {
    const data = await request.put(`/world/projects/${projectId}`, payload);
    return { data };
  },

  deleteProject: async (projectId: string) => {
    await request.delete(`/world/projects/${projectId}`);
    return { data: null };
  },

  getProject: async (projectId: string) => {
    const data = await request.get(`/world/projects/${projectId}`);
    return { data };
  },

  // ----- 卡片 -----

  /** 获取卡片列表（精简） */
  getCards: async (projectId: string, type?: string) => {
    const url = type
      ? `/world/projects/${projectId}/cards?type=${type}`
      : `/world/projects/${projectId}/cards`;
    // 明确指定返回类型为 CardSummary[]
    const data = await request.get<CardSummary[]>(url);
    return { data };
  },

  /** 创建卡片（payload 可根据需要定义更具体的类型） */
  createCard: async (projectId: string, payload: any) => {
    const data = await request.post(`/world/projects/${projectId}/cards`, payload);
    return { data };
  },

  /** 获取卡片详情（完整） */
  getCard: async (projectId: string, cardId: string) => {
    const data = await request.get<CardDetail>(`/world/projects/${projectId}/cards/${cardId}`);
    return { data };
  },

  /** 更新卡片 */
  updateCard: async (projectId: string, cardId: string, payload: any) => {
    const data = await request.put(`/world/projects/${projectId}/cards/${cardId}`, payload);
    return { data };
  },

  /** 删除卡片 */
  deleteCard: async (projectId: string, cardId: string) => {
    await request.delete(`/world/projects/${projectId}/cards/${cardId}`);
    return { data: null };
  },

  // ----- 关联管理 -----
  addRelation: async (cardId: string, targetCardId: string, relationType: string) => {
    const data = await request.post(`/world/cards/${cardId}/relations`, {
      targetCardId,
      relationType,
    });
    return { data };
  },

  removeRelation: async (cardId: string, relationId: string) => {
    await request.delete(`/world/cards/${cardId}/relations/${relationId}`);
    return { data: null };
  },

  getCardRelations: async (cardId: string) => {
    const data = await request.get(`/world/cards/${cardId}/relations`);
    return { data };
  },

  getProjectRelations: async (projectId: string) => {
    const data = await request.get(`/world/projects/${projectId}/relations`);
    return { data };
  },
};