// src/api/worldApi.ts
import request from '@/utils/request';

export const worldApi = {
  // ---------- 卡片类型 ----------
  getCardTypes: async () => {
    const data = await request.get('/world/card-types');
    return { data };
  },

  // ---------- 项目 ----------
  getProjects: async () => {
    const data = await request.get('/world/projects');
    return { data };
  },

  getPublicProjects: async () => {
    const data = await request.get('/world/projects');
    const publicProjects = data.filter((p: any) => p.isPublic);
    return { data: publicProjects };
  },

  createProject: async (payload: { name: string; description?: string; isPublic?: boolean }) => {
    const data = await request.post('/world/projects', payload);
    return { data };
  },

  updateProject: async (projectId: string, payload: any) => {
    const data = await request.put(`/world/projects/${projectId}`, payload);
    return { data };
  },

  deleteProject: async (projectId: string) => {
    await request.delete(`/world/projects/${projectId}`);
    return { data: null };
  },

  // ---------- 卡片（需要 projectId） ----------
  getCards: async (projectId: string, type?: string) => {
    const url = type
      ? `/world/projects/${projectId}/cards?type=${type}`
      : `/world/projects/${projectId}/cards`;
    const data = await request.get(url);
    return { data };
  },

  createCard: async (projectId: string, payload: any) => {
    const data = await request.post(`/world/projects/${projectId}/cards`, payload);
    return { data };
  },

  // 修改：增加 projectId 参数
  getCard: async (projectId: string, cardId: string) => {
    const data = await request.get(`/world/projects/${projectId}/cards/${cardId}`);
    return { data };
  },

  // 修改：增加 projectId 参数
  updateCard: async (projectId: string, cardId: string, payload: any) => {
    const data = await request.put(`/world/projects/${projectId}/cards/${cardId}`, payload);
    return { data };
  },

  // 修改：增加 projectId 参数
  deleteCard: async (projectId: string, cardId: string) => {
    await request.delete(`/world/projects/${projectId}/cards/${cardId}`);
    return { data: null };
  },

  // ---------- 关联管理 ----------
  // 增加 cardId 参数，因为后端需要从 URL 获取
  addRelation: async (cardId: string, targetCardId: string, relationType: string) => {
    const data = await request.post(`/world/cards/${cardId}/relations`, {
      targetCardId,
      relationType,
    });
    return { data };
  },

  // 删除关联需要 cardId 和 relationId
  removeRelation: async (cardId: string, relationId: string) => {
    await request.delete(`/world/cards/${cardId}/relations/${relationId}`);
    return { data: null };
  },

  getCardRelations: async (cardId: string) => {
    const data = await request.get(`/world/cards/${cardId}/relations`);
    return { data };
  },

  getProjectRelations: async (projectId: string) => {
    const cards = await request.get(`/world/projects/${projectId}/cards`);
    let allRelations: any[] = [];
    for (const card of cards) {
      const relations = await request.get(`/world/cards/${card.id}/relations`);
      allRelations = allRelations.concat(relations);
    }
    return { data: allRelations };
  },
};