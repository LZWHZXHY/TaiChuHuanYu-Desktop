// src/stores/world.ts

import { defineStore } from 'pinia';
import { ref } from 'vue';
import { worldApi } from '@/api/worldApi';
import type { CardSummary, CardDetail, Relation } from '@/api/worldApi';

// ============================================================
//  项目类型（保持不变）
// ============================================================
export interface WorldProject {
  id: string;
  name: string;
  description?: string;
  isPublic: boolean;
  createdAt: string;
  updatedAt: string;
  cardCount?: number;
  ownerName?: string;
  ownerId?: string;
}

// 为了方便，导出类型别名
export type WorldCardSummary = CardSummary;
export type WorldCardDetail = CardDetail;
export type WorldRelation = Relation;

// ============================================================
//  请求去重工具（保持不变）
// ============================================================
const pendingRequests = new Map<string, Promise<any>>();

function dedupeRequest<T>(key: string, fn: () => Promise<T>, ttl = 5000): Promise<T> {
  const existing = pendingRequests.get(key);
  if (existing) {
    console.log(`[Dedupe] ✅ 复用请求: ${key}`);
    return existing as Promise<T>;
  }

  const promise = fn()
    .then((result: T) => {
      setTimeout(() => pendingRequests.delete(key), ttl);
      return result;
    })
    .catch((error: any) => {
      pendingRequests.delete(key);
      throw error;
    });

  pendingRequests.set(key, promise);
  return promise;
}

// ============================================================
//  Store 定义
// ============================================================
export const useWorldStore = defineStore('world', () => {
  // ---------- 状态 ----------
  const projects = ref<WorldProject[]>([]);
  const publicProjects = ref<WorldProject[]>([]);
  const currentProject = ref<WorldProject | null>(null);

  // 列表卡片（精简）
  const cards = ref<CardSummary[]>([]);
  // 当前详情卡片（完整）
  const currentCard = ref<CardDetail | null>(null);

  const loading = ref(false);
  const allRelations = ref<Relation[]>([]);
  const cardTypes = ref<{ label: string; value: string; icon?: string }[]>([]);

  // 缓存（分开存储精简和完整数据）
  const cardSummaryCache = new Map<string, CardSummary>();
  const cardDetailCache = new Map<string, CardDetail>();

  // ---------- 卡片类型 ----------
  async function fetchCardTypes() {
    try {
      const res = await worldApi.getCardTypes();
      cardTypes.value = res.data;
    } catch (error) {
      console.error('获取卡片类型失败:', error);
      // 提供默认值
      cardTypes.value = [
        { label: '角色', value: 'character', icon: '🧙' },
        { label: '地点', value: 'location', icon: '📍' },
        { label: '物品', value: 'item', icon: '⚔️' },
        { label: '事件', value: 'event', icon: '📖' },
        { label: '生态', value: 'ecology', icon: '🌿' },
        { label: '派系', value: 'faction', icon: '🏛️' },
        { label: '物种', value: 'species', icon: '🐉' },
        { label: '背景设定', value: 'lore', icon: '📜' },
      ];
    }
  }

  // ---------- 项目 ----------
  async function fetchProjects() {
    loading.value = true;
    try {
      const res = await dedupeRequest('projects', () => worldApi.getProjects());
      projects.value = res.data.map((p: any) => ({
        id: p.id || p.Id,
        name: p.name || p.Name,
        description: p.description || p.Description,
        isPublic: p.isPublic ?? p.IsPublic ?? false,
        createdAt: p.createdAt || p.CreatedAt,
        updatedAt: p.updatedAt || p.UpdatedAt,
        cardCount: p.cardCount || p.CardCount || 0,
        ownerName: p.ownerName || p.OwnerName,
        ownerId: p.ownerId || p.OwnerId,
      }));
    } catch (error) {
      console.error('获取项目列表失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  async function fetchPublicProjects() {
    loading.value = true;
    try {
      const res = await dedupeRequest('publicProjects', () => worldApi.getPublicProjects());
      publicProjects.value = res.data.map((p: any) => ({
        id: p.id || p.Id,
        name: p.name || p.Name,
        description: p.description || p.Description,
        isPublic: p.isPublic ?? p.IsPublic ?? true,
        createdAt: p.createdAt || p.CreatedAt,
        updatedAt: p.updatedAt || p.UpdatedAt,
        cardCount: p.cardCount || p.CardCount || 0,
        ownerName: p.ownerName || p.OwnerName,
        ownerId: p.ownerId || p.OwnerId,
      }));
    } catch (error) {
      console.error('获取公开项目失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  async function createProject(payload: { name: string; description?: string; isPublic?: boolean }) {
    const res = await worldApi.createProject(payload);
    const newProject: WorldProject = {
      id: res.data.id || res.data.Id,
      name: res.data.name || res.data.Name,
      description: res.data.description || res.data.Description,
      isPublic: res.data.isPublic ?? res.data.IsPublic ?? false,
      createdAt: res.data.createdAt || res.data.CreatedAt,
      updatedAt: res.data.updatedAt || res.data.UpdatedAt,
      cardCount: 0,
      ownerName: res.data.ownerName || res.data.OwnerName,
      ownerId: res.data.ownerId || res.data.OwnerId,
    };
    projects.value.push(newProject);
    return newProject;
  }

  // ---------- 卡片列表（精简） ----------
  async function fetchCards(projectId: string) {
    // 如果已有相同项目的数据，直接复用
    if (cards.value.length > 0 && cards.value[0]?.projectId === projectId) {
      console.log(`[Cache] ♻️ 复用 cards 数据: projectId=${projectId}`);
      const found = projects.value.find((p: WorldProject) => p.id === projectId) 
                 || publicProjects.value.find((p: WorldProject) => p.id === projectId);
      if (found && !currentProject.value) {
        currentProject.value = found;
      }
      return;
    }

    loading.value = true;
    try {
      const cacheKey = `cards_${projectId}`;
      const res = await dedupeRequest(cacheKey, () => worldApi.getCards(projectId));
      // res.data 已经是 CardSummary[]
      const cardList = res.data as CardSummary[];
      cards.value = cardList;
      cardList.forEach(card => cardSummaryCache.set(card.id, card));

      // 获取关系（用于图谱）
      const relationsKey = `relations_${projectId}`;
      const relationsRes = await dedupeRequest(relationsKey, () => worldApi.getProjectRelations(projectId));
      allRelations.value = relationsRes.data.map((r: any) => ({
        id: r.id || r.Id,
        sourceCardId: r.sourceCardId || r.SourceCardId,
        targetCardId: r.targetCardId || r.TargetCardId,
        relationType: r.relationType || r.RelationType,
        createdAt: r.createdAt || r.CreatedAt,
        sourceCardTitle: r.sourceCardTitle || r.SourceCardTitle || '',
        targetCardTitle: r.targetCardTitle || r.TargetCardTitle || '',
        sourceCardType: r.sourceCardType || r.SourceCardType || '',
        targetCardType: r.targetCardType || r.TargetCardType || '',
      }));

      // 查找项目信息
      let found = projects.value.find((p: WorldProject) => p.id === projectId) 
               || publicProjects.value.find((p: WorldProject) => p.id === projectId);
      if (!found) {
        try {
          const projectRes = await worldApi.getProject(projectId);
          found = {
            id: projectRes.data.id || projectRes.data.Id,
            name: projectRes.data.name || projectRes.data.Name,
            description: projectRes.data.description || projectRes.data.Description,
            isPublic: projectRes.data.isPublic ?? projectRes.data.IsPublic ?? false,
            createdAt: projectRes.data.createdAt || projectRes.data.CreatedAt,
            updatedAt: projectRes.data.updatedAt || projectRes.data.UpdatedAt,
            cardCount: projectRes.data.cardCount || projectRes.data.CardCount || 0,
            ownerName: projectRes.data.ownerName || projectRes.data.OwnerName,
            ownerId: projectRes.data.ownerId || projectRes.data.OwnerId,
          };
          if (found.isPublic) {
            publicProjects.value.push(found);
          } else {
            projects.value.push(found);
          }
        } catch (error) {
          console.error('获取项目详情失败:', error);
        }
      }
      currentProject.value = found || null;
    } catch (error) {
      console.error('获取卡片列表失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  // ---------- 创建卡片 ----------
  async function createCard(projectId: string, payload: any) {
    const { relations, ...cardPayload } = payload;
    const res = await worldApi.createCard(projectId, cardPayload);
    const fullCard = res.data as CardDetail;
    // 转换为精简数据
    const summary: CardSummary = {
      id: fullCard.id,
      projectId: fullCard.projectId,
      title: fullCard.title,
      type: fullCard.type,
      coverImage: fullCard.coverImage,
      updatedAt: fullCard.updatedAt,
      outRelationCount: fullCard.outRelations?.length || 0,
      inRelationCount: fullCard.inRelations?.length || 0,
    };
    if (relations && relations.length > 0) {
      for (const rel of relations) {
        await worldApi.addRelation(summary.id, rel.targetCardId, rel.relationType);
      }
      await fetchCards(projectId); // 重新加载列表
    } else {
      cards.value.push(summary);
      cardSummaryCache.set(summary.id, summary);
    }
    return summary;
  }

  // ---------- 更新卡片 ----------
  async function updateCard(cardId: string, payload: any) {
    const existingCard = cards.value.find((c: CardSummary) => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法更新');

    const { relations, ...cardPayload } = payload;
    const res = await worldApi.updateCard(existingCard.projectId, cardId, cardPayload);
    const fullCard = res.data as CardDetail;

    // 更新精简数据
    const updatedSummary: CardSummary = {
      id: fullCard.id,
      projectId: fullCard.projectId,
      title: fullCard.title,
      type: fullCard.type,
      coverImage: fullCard.coverImage,
      updatedAt: fullCard.updatedAt,
      outRelationCount: fullCard.outRelations?.length || 0,
      inRelationCount: fullCard.inRelations?.length || 0,
    };

    // 处理关系变更
    if (relations !== undefined) {
      const oldRelations = allRelations.value.filter((r: Relation) => r.sourceCardId === cardId);
      for (const rel of oldRelations) {
        await worldApi.removeRelation(cardId, rel.id);
      }
      for (const rel of relations) {
        await worldApi.addRelation(cardId, rel.targetCardId, rel.relationType);
      }
      // 重新获取关系
      const relationsRes = await worldApi.getCardRelations(cardId);
      allRelations.value = allRelations.value.filter((r: Relation) => r.sourceCardId !== cardId);
      const newRels = relationsRes.data.map((r: any) => ({
        id: r.id || r.Id,
        sourceCardId: r.sourceCardId || r.SourceCardId,
        targetCardId: r.targetCardId || r.TargetCardId,
        relationType: r.relationType || r.RelationType,
        createdAt: r.createdAt || r.CreatedAt,
        sourceCardTitle: r.sourceCardTitle || r.SourceCardTitle || '',
        targetCardTitle: r.targetCardTitle || r.TargetCardTitle || '',
        sourceCardType: r.sourceCardType || r.SourceCardType || '',
        targetCardType: r.targetCardType || r.TargetCardType || '',
      }));
      allRelations.value.push(...newRels);
      updatedSummary.outRelationCount = newRels.filter((r: any) => r.sourceCardId === cardId).length;
      updatedSummary.inRelationCount = newRels.filter((r: any) => r.targetCardId === cardId).length;
    }

    // 更新列表和缓存
    const idx = cards.value.findIndex((c: CardSummary) => c.id === cardId);
    if (idx !== -1) {
      cards.value[idx] = updatedSummary;
      cardSummaryCache.set(cardId, updatedSummary);
    }
    if (cardDetailCache.has(cardId)) {
      cardDetailCache.set(cardId, fullCard);
    }
    if (currentCard.value?.id === cardId) {
      currentCard.value = fullCard;
    }
    return updatedSummary;
  }

  // ---------- 删除卡片 ----------
  async function deleteCard(cardId: string) {
    const existingCard = cards.value.find((c: CardSummary) => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法删除');

    const rels = allRelations.value.filter((r: Relation) => r.sourceCardId === cardId || r.targetCardId === cardId);
    for (const rel of rels) {
      if (rel.sourceCardId === cardId) {
        await worldApi.removeRelation(cardId, rel.id);
      } else {
        await worldApi.removeRelation(rel.sourceCardId, rel.id);
      }
    }
    await worldApi.deleteCard(existingCard.projectId, cardId);
    cards.value = cards.value.filter((c: CardSummary) => c.id !== cardId);
    allRelations.value = allRelations.value.filter((r: Relation) => r.sourceCardId !== cardId && r.targetCardId !== cardId);
    if (currentCard.value?.id === cardId) currentCard.value = null;
    cardSummaryCache.delete(cardId);
    cardDetailCache.delete(cardId);
  }

  // ---------- 卡片详情（完整） ----------
async function fetchCardDetail(projectId: string, cardId: string, force = false) {
  // 如果强制刷新，跳过缓存，直接从服务器获取
  if (force) {
    loading.value = true;
    try {
      console.log(`📡 强制刷新卡片: ${cardId}`);
      const res = await worldApi.getCard(projectId, cardId);
      const fullCard = res.data as CardDetail;
      cardDetailCache.set(cardId, fullCard);
      currentCard.value = fullCard;

      // 同步更新精简缓存
      const summary: CardSummary = {
        id: fullCard.id,
        projectId: fullCard.projectId,
        title: fullCard.title,
        type: fullCard.type,
        coverImage: fullCard.coverImage,
        updatedAt: fullCard.updatedAt,
        outRelationCount: fullCard.outRelations?.length || 0,
        inRelationCount: fullCard.inRelations?.length || 0,
      };
      cardSummaryCache.set(cardId, summary);
      const idx = cards.value.findIndex((c: CardSummary) => c.id === cardId);
      if (idx !== -1) cards.value[idx] = summary;
      else cards.value.push(summary);

      return fullCard;
    } catch (error) {
      console.error('强制刷新卡片失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  // 原有缓存逻辑
  if (cardDetailCache.has(cardId)) {
    const cached = cardDetailCache.get(cardId)!;
    console.log(`[Cache] 🚀 从详情缓存返回卡片: ${cardId}`);
    currentCard.value = cached;
    return cached;
  }

  loading.value = true;
  try {
    const cacheKey = `card_${cardId}`;
    const res = await dedupeRequest(cacheKey, () => worldApi.getCard(projectId, cardId));
    const fullCard = res.data as CardDetail;
    cardDetailCache.set(cardId, fullCard);
    currentCard.value = fullCard;

    // 同步更新精简缓存
    const summary: CardSummary = {
      id: fullCard.id,
      projectId: fullCard.projectId,
      title: fullCard.title,
      type: fullCard.type,
      coverImage: fullCard.coverImage,
      updatedAt: fullCard.updatedAt,
      outRelationCount: fullCard.outRelations?.length || 0,
      inRelationCount: fullCard.inRelations?.length || 0,
    };
    cardSummaryCache.set(cardId, summary);
    if (!cards.value.find((c: CardSummary) => c.id === cardId)) {
      cards.value.push(summary);
    } else {
      const idx = cards.value.findIndex((c: CardSummary) => c.id === cardId);
      if (idx !== -1) cards.value[idx] = summary;
    }
    return fullCard;
  } catch (error) {
    console.error('获取卡片详情失败:', error);
    throw error;
  } finally {
    loading.value = false;
  }
}












  // ---------- 批量加载卡片 ----------
  async function fetchCardsByIds(projectId: string, cardIds: string[]): Promise<CardSummary[]> {
    if (cardIds.length === 0) return [];
    const missingIds = cardIds.filter(id => !cardSummaryCache.has(id));
    if (missingIds.length === 0) {
      return cardIds.map(id => cardSummaryCache.get(id)!);
    }

    loading.value = true;
    try {
      const promises = missingIds.map(id =>
        dedupeRequest(`card_${id}`, () => worldApi.getCard(projectId, id))
      );
      const results = await Promise.all(promises);
      const newCards = results.map((res: any) => {
        const full = res.data as CardDetail;
        const summary: CardSummary = {
          id: full.id,
          projectId: full.projectId,
          title: full.title,
          type: full.type,
          coverImage: full.coverImage,
          updatedAt: full.updatedAt,
          outRelationCount: full.outRelations?.length || 0,
          inRelationCount: full.inRelations?.length || 0,
        };
        cardSummaryCache.set(summary.id, summary);
        cardDetailCache.set(summary.id, full);
        return summary;
      });
      newCards.forEach(card => {
        if (!cards.value.find(c => c.id === card.id)) {
          cards.value.push(card);
        }
      });
      return newCards;
    } catch (error) {
      console.error('批量加载卡片失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  async function addRelation(sourceCardId: string, targetCardId: string, relationType: string) {
  const res = await worldApi.addRelation(sourceCardId, targetCardId, relationType);
  const newRelation: Relation = {
    id: res.data.id || res.data.Id,
    sourceCardId: res.data.sourceCardId || res.data.SourceCardId,
    targetCardId: res.data.targetCardId || res.data.TargetCardId,
    relationType: res.data.relationType || res.data.RelationType,
    createdAt: res.data.createdAt || res.data.CreatedAt,
    sourceCardTitle: res.data.sourceCardTitle || res.data.SourceCardTitle || '',
    targetCardTitle: res.data.targetCardTitle || res.data.TargetCardTitle || '',
    sourceCardType: res.data.sourceCardType || res.data.SourceCardType || '',
    targetCardType: res.data.targetCardType || res.data.TargetCardType || '',
  };
  allRelations.value.push(newRelation);

  // 更新列表中卡片的计数
  const card = cards.value.find((c: CardSummary) => c.id === sourceCardId);
  if (card) {
    card.outRelationCount = (card.outRelationCount || 0) + 1;
    cardSummaryCache.set(sourceCardId, card);
  }
  const targetCard = cards.value.find((c: CardSummary) => c.id === targetCardId);
  if (targetCard) {
    targetCard.inRelationCount = (targetCard.inRelationCount || 0) + 1;
    cardSummaryCache.set(targetCardId, targetCard);
  }

  // 更新详情缓存中的关系列表
  const sourceDetail = cardDetailCache.get(sourceCardId);
  if (sourceDetail) {
    if (!sourceDetail.outRelations) sourceDetail.outRelations = [];
    sourceDetail.outRelations.push(newRelation);
    cardDetailCache.set(sourceCardId, sourceDetail);
  }
  const targetDetail = cardDetailCache.get(targetCardId);
  if (targetDetail) {
    if (!targetDetail.inRelations) targetDetail.inRelations = [];
    targetDetail.inRelations.push(newRelation);
    cardDetailCache.set(targetCardId, targetDetail);
  }

  // ✅ 强制刷新当前卡片
  if (currentCard.value?.id === sourceCardId || currentCard.value?.id === targetCardId) {
    const projectId = currentCard.value.projectId;
    const cardId = currentCard.value.id;
    cardDetailCache.delete(cardId);
    await fetchCardDetail(projectId, cardId, true);
  }

  return newRelation;
}












  async function removeRelation(cardId: string, relationId: string) {
    await worldApi.removeRelation(cardId, relationId);
    const removed = allRelations.value.find((r: Relation) => r.id === relationId);
    allRelations.value = allRelations.value.filter((r: Relation) => r.id !== relationId);
    const card = cards.value.find((c: CardSummary) => c.id === cardId);
    if (card && removed) {
      if (removed.sourceCardId === cardId) {
        card.outRelationCount = Math.max(0, (card.outRelationCount || 0) - 1);
      } else if (removed.targetCardId === cardId) {
        card.inRelationCount = Math.max(0, (card.inRelationCount || 0) - 1);
      }
      cardSummaryCache.set(cardId, card);
    }
  }

  // ---------- 辅助函数 ----------
  function getCardTitle(cardId: string): string {
    if (cardSummaryCache.has(cardId)) return cardSummaryCache.get(cardId)!.title;
    if (cardDetailCache.has(cardId)) return cardDetailCache.get(cardId)!.title;
    const card = cards.value.find((c: CardSummary) => c.id === cardId);
    return card?.title || '已删除的卡片';
  }

  function getCardById(cardId: string): CardSummary | undefined {
    return cardSummaryCache.get(cardId) || cards.value.find((c: CardSummary) => c.id === cardId);
  }

  function getCardDetailById(cardId: string): CardDetail | undefined {
    return cardDetailCache.get(cardId);
  }

  // ---------- 导出 ----------
  return {
    projects,
    publicProjects,
    currentProject,
    cards,
    currentCard,
    loading,
    cardTypes,
    allRelations,
    fetchProjects,
    fetchPublicProjects,
    fetchCardTypes,
    createProject,
    fetchCards,
    createCard,
    updateCard,
    deleteCard,
    fetchCardDetail,
    fetchCardsByIds,
    addRelation,
    removeRelation,
    getCardTitle,
    getCardById,
    getCardDetailById,
  };
});