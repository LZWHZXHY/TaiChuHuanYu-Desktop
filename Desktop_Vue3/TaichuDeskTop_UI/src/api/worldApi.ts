// src/api/worldApi.ts
import request from '@/utils/request';

export const worldApi = {
  // ============================================================
  //  卡片类型
  // ============================================================
  getCardTypes: async () => {
    const data = await request.get('/world/card-types');
    return { data };
  },

  // ============================================================
  //  项目
  // ============================================================
  getProjects: async () => {
    const data = await request.get('/world/projects');
    return { data };
  },

  // ✅ 修改：直接调用后端的公开项目接口
  getPublicProjects: async () => {
    const data = await request.get('/world/projects/public');
    return { data };
  },

  createProject: async (payload: { name: string; description?: string; isPublic?: boolean }) => {
    const data = await request.post('/world/projects', payload);
    return { data };
  },

  // 🆕 更新项目（Store 里有用到）
  updateProject: async (projectId: string, payload: { name?: string; description?: string; isPublic?: boolean }) => {
    const data = await request.put(`/world/projects/${projectId}`, payload);
    return { data };
  },

  // 🆕 删除项目（Store 里有用到）
  deleteProject: async (projectId: string) => {
    await request.delete(`/world/projects/${projectId}`);
    return { data: null };
  },

  // ============================================================
  //  卡片
  // ============================================================
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

  getCard: async (projectId: string, cardId: string) => {
    const data = await request.get(`/world/projects/${projectId}/cards/${cardId}`);
    return { data };
  },

  updateCard: async (projectId: string, cardId: string, payload: any) => {
    const data = await request.put(`/world/projects/${projectId}/cards/${cardId}`, payload);
    return { data };
  },

  deleteCard: async (projectId: string, cardId: string) => {
    await request.delete(`/world/projects/${projectId}/cards/${cardId}`);
    return { data: null };
  },

  // ============================================================
  //  关联管理
  // ============================================================
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

  // ✅ 修改：直接调用后端的项目级联关系接口
  getProjectRelations: async (projectId: string) => {
    const data = await request.get(`/world/projects/${projectId}/relations`);
    return { data };
  },

  // ---------- 项目（单个） ----------
getProject: async (projectId: string) => {
  const data = await request.get(`/world/projects/${projectId}`);
  return { data };
},
};