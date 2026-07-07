// src/api/worldApi.ts
import request from '@/utils/request';  // 使用你项目中的 request 实例

// ============================================================
// ========== API 对象 ==========================================
// ============================================================

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
    // 后端暂未实现公开项目列表，先获取所有项目然后前端过滤
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

  // ---------- 卡片 ----------
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

  updateCard: async (cardId: string, payload: any) => {
    // 后端 PUT 需要 projectId，这里先获取卡片信息
    const card = await request.get(`/world/cards/${cardId}`);
    const data = await request.put(`/world/projects/${card.projectId}/cards/${cardId}`, payload);
    return { data };
  },

  deleteCard: async (cardId: string) => {
    const card = await request.get(`/world/cards/${cardId}`);
    await request.delete(`/world/projects/${card.projectId}/cards/${cardId}`);
    return { data: null };
  },

  getCard: async (cardId: string) => {
    const data = await request.get(`/world/cards/${cardId}`);
    return { data };
  },

  // ---------- 关联管理 ----------
  addRelation: async (sourceCardId: string, targetCardId: string, relationType: string) => {
    const data = await request.post(`/world/cards/${sourceCardId}/relations`, {
      targetCardId,
      relationType,
    });
    return { data };
  },

  removeRelation: async (relationId: string) => {
    // 先获取关联信息，获取 sourceCardId
    const relations = await request.get(`/world/cards/relations/${relationId}`);
    // 或者直接调用删除，如果后端支持 DELETE /api/world/relations/{relationId}
    // 这里假设后端支持直接删除
    await request.delete(`/world/relations/${relationId}`);
    return { data: null };
  },

  getCardRelations: async (cardId: string) => {
    const data = await request.get(`/world/cards/${cardId}/relations`);
    return { data };
  },

  getProjectRelations: async (projectId: string) => {
    // 后端暂未实现获取项目所有关联的接口
    // 目前只能通过遍历卡片获取
    const cards = await request.get(`/world/projects/${projectId}/cards`);
    let allRelations: any[] = [];
    for (const card of cards) {
      const relations = await request.get(`/world/cards/${card.id}/relations`);
      allRelations = allRelations.concat(relations);
    }
    return { data: allRelations };
  },
};