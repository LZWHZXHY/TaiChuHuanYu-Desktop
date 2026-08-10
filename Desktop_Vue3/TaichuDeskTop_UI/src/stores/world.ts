// src/stores/world.ts

import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { worldApi } from '@/api/worldApi';

// ============================================================
//  1. 类型定义
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

export interface WorldRelation {
  id: string;
  sourceCardId: string;
  targetCardId: string;
  relationType: string;
  createdAt: string;
}

export interface ContentBlock {
  id: string;
  cardId: string;
  blockType: string;
  displayStyle: 'compact' | 'full' | 'preview';
  order: number;
}

export interface WorldCard {
  id: string;
  projectId: string;
  title: string;
  type: string;
  subType?: string;
  aliases?: string[];
  attributes?: { key: string; value: string }[];
  description?: string;
  contentBlocks?: ContentBlock[];
  timelineEvents?: { date: string; title: string; description?: string }[];
  content: string;
  tags?: string[];
  embeddedCards?: string[];
  createdAt: string;
  updatedAt: string;
  relatedIds?: string[];
  relations?: WorldRelation[];
  coverImage?: string;
  galleryImages?: string[];   // 🆕
}

// ============================================================
//  2. 请求去重工具
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
//  3. Store 定义
// ============================================================
export const useWorldStore = defineStore('world', () => {
  // ---------- 状态 ----------
  const projects = ref<WorldProject[]>([]);
  const publicProjects = ref<WorldProject[]>([]);
  const currentProject = ref<WorldProject | null>(null);
  const cards = ref<WorldCard[]>([]);
  const currentCard = ref<WorldCard | null>(null);
  const loading = ref(false);
  const allRelations = ref<WorldRelation[]>([]);
  const cardTypes = ref<{ label: string; value: string; icon?: string }[]>([]);

  // ---------- 额外缓存 ----------
  const cardCache = new Map<string, WorldCard>();

  // ---------- 卡片类型 ----------
  async function fetchCardTypes() {
    try {
      const res = await worldApi.getCardTypes();
      cardTypes.value = res.data;
    } catch (error) {
      console.error('获取卡片类型失败:', error);
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

  // ---------- 卡片 ----------
  async function fetchCards(projectId: string) {
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
      const cardList = res.data.map((c: any) => mapCard(c));
      
      cards.value = cardList;
      // 🔧 修复：显式标注 card 类型为 WorldCard
      cardList.forEach((card: WorldCard) => cardCache.set(card.id, card));

      const relationsKey = `relations_${projectId}`;
      const relationsRes = await dedupeRequest(relationsKey, () => worldApi.getProjectRelations(projectId));
      allRelations.value = relationsRes.data.map((r: any) => ({
        id: r.id || r.Id,
        sourceCardId: r.sourceCardId || r.SourceCardId,
        targetCardId: r.targetCardId || r.TargetCardId,
        relationType: r.relationType || r.RelationType,
        createdAt: r.createdAt || r.CreatedAt,
      }));
      
      cards.value = cards.value.map((card: WorldCard) => ({
        ...card,
        relations: allRelations.value.filter((r: WorldRelation) => r.sourceCardId === card.id),
      }));

      const found = projects.value.find((p: WorldProject) => p.id === projectId) 
                 || publicProjects.value.find((p: WorldProject) => p.id === projectId);
      currentProject.value = found || null;
    } catch (error) {
      console.error('获取卡片列表失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  async function createCard(projectId: string, payload: any) {
    const { relations, ...cardPayload } = payload;
    const res = await worldApi.createCard(projectId, cardPayload);
    const newCard = mapCard(res.data);
    
    if (relations && relations.length > 0) {
      for (const rel of relations) {
        await worldApi.addRelation(newCard.id, rel.targetCardId, rel.relationType);
      }
      await fetchCards(projectId);
    } else {
      cards.value.push(newCard);
      cardCache.set(newCard.id, newCard);
    }
    return newCard;
  }

  async function updateCard(cardId: string, payload: any) {
    const existingCard = cards.value.find((c: WorldCard) => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法更新');

    const { relations, ...cardPayload } = payload;
    const res = await worldApi.updateCard(existingCard.projectId, cardId, cardPayload);
    const updatedCard = mapCard(res.data);
    
    if (relations !== undefined) {
      const oldRelations = allRelations.value.filter((r: WorldRelation) => r.sourceCardId === cardId);
      for (const rel of oldRelations) {
        await worldApi.removeRelation(cardId, rel.id);
      }
      for (const rel of relations) {
        await worldApi.addRelation(cardId, rel.targetCardId, rel.relationType);
      }
    }
    
    const index = cards.value.findIndex((c: WorldCard) => c.id === cardId);
    if (index !== -1) {
      const rels = allRelations.value.filter((r: WorldRelation) => r.sourceCardId === cardId);
      const cardWithRels = { ...updatedCard, relations: rels };
      cards.value[index] = cardWithRels;
      cardCache.set(cardId, cardWithRels);
    }
    if (currentCard.value?.id === cardId) {
      const rels = allRelations.value.filter((r: WorldRelation) => r.sourceCardId === cardId);
      currentCard.value = { ...updatedCard, relations: rels };
    }
    return updatedCard;
  }

  async function deleteCard(cardId: string) {
    const existingCard = cards.value.find((c: WorldCard) => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法删除');

    const rels = allRelations.value.filter((r: WorldRelation) => r.sourceCardId === cardId || r.targetCardId === cardId);
    for (const rel of rels) {
      if (rel.sourceCardId === cardId) {
        await worldApi.removeRelation(cardId, rel.id);
      } else {
        await worldApi.removeRelation(rel.sourceCardId, rel.id);
      }
    }
    await worldApi.deleteCard(existingCard.projectId, cardId);
    cards.value = cards.value.filter((c: WorldCard) => c.id !== cardId);
    allRelations.value = allRelations.value.filter((r: WorldRelation) => r.sourceCardId !== cardId && r.targetCardId !== cardId);
    if (currentCard.value?.id === cardId) currentCard.value = null;
    cardCache.delete(cardId);
  }

  async function fetchCardDetail(projectId: string, cardId: string) {
    if (cardCache.has(cardId)) {
      const cached = cardCache.get(cardId)!;
      console.log(`[Cache] 🚀 从缓存返回卡片: ${cardId}`);
      currentCard.value = cached;
      return cached;
    }

    const existing = cards.value.find((c: WorldCard) => c.id === cardId);
    if (existing) {
      console.log(`[Cache] ♻️ 从 cards 列表获取卡片: ${cardId}`);
      cardCache.set(cardId, existing);
      currentCard.value = existing;
      return existing;
    }

    loading.value = true;
    try {
      const cacheKey = `card_${cardId}`;
      const res = await dedupeRequest(cacheKey, () => worldApi.getCard(projectId, cardId));
      const card = mapCard(res.data);
      
      const relationsKey = `card_relations_${cardId}`;
      const relationsRes = await dedupeRequest(relationsKey, () => worldApi.getCardRelations(cardId));
      const allRels = relationsRes.data.map((r: any) => ({
        id: r.id || r.Id,
        sourceCardId: r.sourceCardId || r.SourceCardId,
        targetCardId: r.targetCardId || r.TargetCardId,
        relationType: r.relationType || r.RelationType,
        createdAt: r.createdAt || r.CreatedAt,
      }));
      
      const outRelations = allRels.filter((r: any) => r.sourceCardId === cardId);
      const inRelations = allRels.filter((r: any) => r.targetCardId === cardId);
      const relations = [
        ...outRelations.map((r: any) => ({ ...r, direction: 'out' as const })),
        ...inRelations.map((r: any) => ({ ...r, direction: 'in' as const })),
      ];
      
      const fullCard = { ...card, relations };
      cardCache.set(cardId, fullCard);
      currentCard.value = fullCard;
      
      if (!cards.value.find((c: WorldCard) => c.id === cardId)) {
        cards.value.push({ ...card, relations: [] });
      } else {
        const idx = cards.value.findIndex((c: WorldCard) => c.id === cardId);
        if (idx !== -1) cards.value[idx].relations = relations;
      }
      
      return fullCard;
    } catch (error) {
      console.error('获取卡片详情失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  // 🔧 新增：批量加载卡片详情（所有回调参数显式类型）
  async function fetchCardsByIds(projectId: string, cardIds: string[]): Promise<WorldCard[]> {
    if (cardIds.length === 0) return [];

    const missingIds = cardIds.filter((id: string) => !cards.value.find((c: WorldCard) => c.id === id));
    if (missingIds.length === 0) return [];

    loading.value = true;
    try {
      const promises = missingIds.map((id: string) =>
        dedupeRequest(`card_${id}`, () => worldApi.getCard(projectId, id))
      );
      const results = await Promise.all(promises);
      
      const newCards = results.map((res: any) => mapCard(res.data));
      
      newCards.forEach((card: WorldCard) => {
        if (!cards.value.find((c: WorldCard) => c.id === card.id)) {
          cards.value.push(card);
        }
        cardCache.set(card.id, card);
      });

      return newCards;
    } catch (error) {
      console.error('批量加载卡片失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  // ---------- 关联管理 ----------
  async function addRelation(sourceCardId: string, targetCardId: string, relationType: string) {
    const res = await worldApi.addRelation(sourceCardId, targetCardId, relationType);
    const newRelation: WorldRelation = {
      id: res.data.id || res.data.Id,
      sourceCardId: res.data.sourceCardId || res.data.SourceCardId,
      targetCardId: res.data.targetCardId || res.data.TargetCardId,
      relationType: res.data.relationType || res.data.RelationType,
      createdAt: res.data.createdAt || res.data.CreatedAt,
    };
    allRelations.value.push(newRelation);
    const card = cards.value.find((c: WorldCard) => c.id === sourceCardId);
    if (card) {
      if (!card.relations) card.relations = [];
      card.relations.push(newRelation);
      cardCache.set(sourceCardId, { ...cardCache.get(sourceCardId)!, relations: card.relations });
    }
    return newRelation;
  }

  async function removeRelation(cardId: string, relationId: string) {
    await worldApi.removeRelation(cardId, relationId);
    allRelations.value = allRelations.value.filter((r: WorldRelation) => r.id !== relationId);
    for (const card of cards.value) {
      if (card.relations) {
        card.relations = card.relations.filter((r: WorldRelation) => r.id !== relationId);
        cardCache.set(card.id, { ...cardCache.get(card.id)!, relations: card.relations });
      }
    }
  }

  function getCardTitle(cardId: string): string {
    if (cardCache.has(cardId)) {
      return cardCache.get(cardId)!.title;
    }
    const card = cards.value.find((c: WorldCard) => c.id === cardId);
    return card?.title || '已删除的卡片';
  }

  // ---------- 辅助函数 ----------
  function mapCard(c: any): WorldCard {
    return {
      id: c.id || c.Id,
      projectId: c.projectId || c.ProjectId,
      title: c.title || c.Title,
      type: c.type || c.Type,
      subType: c.subType || c.SubType,
      aliases: c.aliases || c.Aliases || [],
      attributes: c.attributes || c.Attributes || [],
      description: c.description || c.Description,
      contentBlocks: c.contentBlocks || c.ContentBlocks || [],
      timelineEvents: c.timelineEvents || c.TimelineEvents || [],
      content: c.content || c.Content || '{}',
      tags: c.tags || c.Tags || [],
      embeddedCards: c.embeddedCards || c.EmbeddedCards || [],
      createdAt: c.createdAt || c.CreatedAt,
      updatedAt: c.updatedAt || c.UpdatedAt,
      relations: [],
      coverImage: c.coverImage || c.CoverImage || '',
      galleryImages: c.galleryImages || c.GalleryImages || [],   // 🆕
    };
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
  };
});