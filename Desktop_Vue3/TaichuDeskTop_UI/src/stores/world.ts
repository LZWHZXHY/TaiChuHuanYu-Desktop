import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { worldApi } from '@/api/worldApi';

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
  coverImage?:string;
}

export const useWorldStore = defineStore('world', () => {
  const projects = ref<WorldProject[]>([]);
  const publicProjects = ref<WorldProject[]>([]);
  const currentProject = ref<WorldProject | null>(null);
  const cards = ref<WorldCard[]>([]);
  const currentCard = ref<WorldCard | null>(null);
  const loading = ref(false);
  const allRelations = ref<WorldRelation[]>([]);

  // ===== 卡片类型（从后端获取） =====
  const cardTypes = ref<{ label: string; value: string; icon?: string }[]>([]);

  // ===== 获取卡片类型 =====
  async function fetchCardTypes() {
    try {
      const res = await worldApi.getCardTypes();
      cardTypes.value = res.data;
    } catch (error) {
      console.error('获取卡片类型失败:', error);
      // 降级到硬编码
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
      const res = await worldApi.getProjects();
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
      const res = await worldApi.getPublicProjects();
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
    const newProject = {
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
    loading.value = true;
    try {
      const res = await worldApi.getCards(projectId);
      cards.value = res.data.map((c: any) => mapCard(c));
      
      const relationsRes = await worldApi.getProjectRelations(projectId);
      allRelations.value = relationsRes.data.map((r: any) => ({
        id: r.id || r.Id,
        sourceCardId: r.sourceCardId || r.SourceCardId,
        targetCardId: r.targetCardId || r.TargetCardId,
        relationType: r.relationType || r.RelationType,
        createdAt: r.createdAt || r.CreatedAt,
      }));
      
      cards.value = cards.value.map(card => {
        const relations = allRelations.value.filter(r => r.sourceCardId === card.id);
        return { ...card, relations };
      });

      const found = projects.value.find(p => p.id === projectId) 
                 || publicProjects.value.find(p => p.id === projectId);
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
    }
    return newCard;
  }

  // ===== 修改：需要从卡片列表获取 projectId =====
  async function updateCard(cardId: string, payload: any) {
    // 获取卡片所在的 projectId
    const existingCard = cards.value.find(c => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法更新');

    const { relations, ...cardPayload } = payload;
    const res = await worldApi.updateCard(existingCard.projectId, cardId, cardPayload);
    const updatedCard = mapCard(res.data);
    
    if (relations !== undefined) {
      const oldRelations = allRelations.value.filter(r => r.sourceCardId === cardId);
      for (const rel of oldRelations) {
        await worldApi.removeRelation(cardId, rel.id);
      }
      for (const rel of relations) {
        await worldApi.addRelation(cardId, rel.targetCardId, rel.relationType);
      }
    }
    
    const index = cards.value.findIndex(c => c.id === cardId);
    if (index !== -1) {
      const rels = allRelations.value.filter(r => r.sourceCardId === cardId);
      cards.value[index] = { ...updatedCard, relations: rels };
    }
    if (currentCard.value?.id === cardId) {
      const rels = allRelations.value.filter(r => r.sourceCardId === cardId);
      currentCard.value = { ...updatedCard, relations: rels };
    }
    return updatedCard;
  }

  // ===== 修改：需要从卡片列表获取 projectId =====
  async function deleteCard(cardId: string) {
    const existingCard = cards.value.find(c => c.id === cardId);
    if (!existingCard) throw new Error('卡片不存在，无法删除');

    const rels = allRelations.value.filter(r => r.sourceCardId === cardId || r.targetCardId === cardId);
    for (const rel of rels) {
      // 判断当前卡片是源还是目标，以便正确调用删除 API
      if (rel.sourceCardId === cardId) {
        await worldApi.removeRelation(cardId, rel.id);
      } else {
        // 如果当前卡片是目标卡片，需要从源卡片的角度删除
        await worldApi.removeRelation(rel.sourceCardId, rel.id);
      }
    }
    await worldApi.deleteCard(existingCard.projectId, cardId);
    cards.value = cards.value.filter(c => c.id !== cardId);
    allRelations.value = allRelations.value.filter(r => r.sourceCardId !== cardId && r.targetCardId !== cardId);
    if (currentCard.value?.id === cardId) currentCard.value = null;
  }

  // ===== 修改：接受 projectId 和 cardId 两个参数 =====
  async function fetchCardDetail(projectId: string, cardId: string) {
    loading.value = true;
    try {
      const res = await worldApi.getCard(projectId, cardId);
      const card = mapCard(res.data);
      
      const relationsRes = await worldApi.getCardRelations(cardId);
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
      
      currentCard.value = { ...card, relations };
      return currentCard.value;
    } catch (error) {
      console.error('获取卡片详情失败:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  // ---------- 关联管理 ----------
  // ===== 修改：参数顺序与 worldApi 一致 =====
  async function addRelation(sourceCardId: string, targetCardId: string, relationType: string) {
    const res = await worldApi.addRelation(sourceCardId, targetCardId, relationType);
    const newRelation = {
      id: res.data.id || res.data.Id,
      sourceCardId: res.data.sourceCardId || res.data.SourceCardId,
      targetCardId: res.data.targetCardId || res.data.TargetCardId,
      relationType: res.data.relationType || res.data.RelationType,
      createdAt: res.data.createdAt || res.data.CreatedAt,
    };
    allRelations.value.push(newRelation);
    const card = cards.value.find(c => c.id === sourceCardId);
    if (card) {
      if (!card.relations) card.relations = [];
      card.relations.push(newRelation);
    }
    return newRelation;
  }

  // ===== 修改：需要 cardId 和 relationId =====
  async function removeRelation(cardId: string, relationId: string) {
    await worldApi.removeRelation(cardId, relationId);
    allRelations.value = allRelations.value.filter(r => r.id !== relationId);
    for (const card of cards.value) {
      if (card.relations) {
        card.relations = card.relations.filter(r => r.id !== relationId);
      }
    }
  }

  function getCardTitle(cardId: string): string {
    const card = cards.value.find(c => c.id === cardId);
    return card?.title || '已删除的卡片';
  }

  // ===== 辅助函数：映射后端返回的卡片数据 =====
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
    };
  }

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
    addRelation,
    removeRelation,
    getCardTitle,
  };
});