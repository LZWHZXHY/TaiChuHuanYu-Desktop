<template>
  <div class="card-detail" v-loading="loading">
    <div v-if="card" class="detail-container">
      <!-- 返回按钮 -->
      <button class="back-btn" @click="goBack">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="20" height="20">
          <line x1="19" y1="12" x2="5" y2="12"/>
          <polyline points="12 19 5 12 12 5"/>
        </svg>
        返回项目
      </button>

      <!-- 卡片头部 -->
      <div class="card-header">
        <div class="header-left">
          <div class="title-row">
            <span class="card-icon">{{ getTypeIcon(card.type) }}</span>
            <h1>{{ card.title }}</h1>
            <span class="type-badge">{{ getTypeLabel(card.type) }}</span>
            <span v-if="card.subType" class="subtype-badge">{{ card.subType }}</span>
          </div>
          <div class="meta-row">
            <span>创建于 {{ formatDate(card.createdAt) }}</span>
            <span>· 更新于 {{ formatDate(card.updatedAt) }}</span>
            <span class="tag" :class="isPublic ? 'public' : 'private'">
              {{ isPublic ? '🌐 公开' : '🔒 私有' }}
            </span>
          </div>
        </div>
        <div class="header-right">
          <button class="action-btn" @click="editCard" title="编辑">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="18" height="18">
              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
            </svg>
          </button>
          <button class="action-btn danger" @click="handleDelete" title="删除">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="18" height="18">
              <polyline points="3 6 5 6 21 6"/>
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- ===== 别名 ===== -->
      <div v-if="card.aliases && card.aliases.length" class="section">
        <h3>🏷️ 别名</h3>
        <div class="alias-list">
          <span v-for="alias in card.aliases" :key="alias" class="alias-item">{{ alias }}</span>
        </div>
      </div>

      <!-- ===== 属性 ===== -->
      <div v-if="card.attributes && card.attributes.length" class="section">
        <h3>📋 属性</h3>
        <div class="attribute-grid">
          <div v-for="attr in card.attributes" :key="attr.key" class="attribute-row">
            <span class="attr-key-label">{{ attr.key }}</span>
            <span class="attr-value-label">{{ attr.value }}</span>
          </div>
        </div>
      </div>

      <!-- ===== 描述（渲染嵌入式卡片引用） ===== -->
      <div v-if="card.description" class="section">
        <h3>📄 描述</h3>
        <div class="description-body" v-html="renderedDescription"></div>
      </div>

      <!-- ===== 内容（兼容旧数据） ===== -->
      <div v-if="formattedContent && formattedContent !== '{}'" class="section">
        <h3>📦 内容数据</h3>
        <div class="content-body">
          <pre>{{ formattedContent }}</pre>
        </div>
      </div>

      <!-- ===== 标签 ===== -->
      <div v-if="cardTags.length" class="section">
        <h3>🏷️ 标签</h3>
        <div class="tag-list">
          <span v-for="tag in cardTags" :key="tag" class="tag-item">#{{ tag }}</span>
        </div>
      </div>

      <!-- ===== 时间线 ===== -->
      <div v-if="card.timelineEvents && card.timelineEvents.length" class="section">
        <h3>⏳ 时间线</h3>
        <div class="timeline">
          <div v-for="(evt, idx) in card.timelineEvents" :key="idx" class="timeline-item">
            <span class="tl-date">{{ evt.date }}</span>
            <span class="tl-title">{{ evt.title }}</span>
            <span v-if="evt.description" class="tl-desc">{{ evt.description }}</span>
          </div>
        </div>
      </div>

      <!-- ===== 关联卡片 ===== -->
      <div class="section">
        <h3>🔗 关联卡片（{{ allRelations.length }}）</h3>
        <div v-if="allRelations.length === 0" class="empty-hint">暂无关联</div>
        <div class="relation-groups">
          <div v-if="outRelations.length" class="relation-group">
            <div class="group-label">此卡片关联了</div>
            <div class="relation-cards">
              <div
                v-for="rel in outRelations"
                :key="rel.id"
                class="relation-card"
                @click="goToCard(rel.targetCardId)"
              >
                <span class="rel-icon">{{ getTypeIcon(getCardType(rel.targetCardId)) }}</span>
                <span class="rel-title">{{ getCardTitle(rel.targetCardId) }}</span>
                <span class="rel-type">「{{ rel.relationType }}」</span>
              </div>
            </div>
          </div>
          <div v-if="inRelations.length" class="relation-group">
            <div class="group-label">被以下卡片关联</div>
            <div class="relation-cards">
              <div
                v-for="rel in inRelations"
                :key="rel.id"
                class="relation-card"
                @click="goToCard(rel.sourceCardId)"
              >
                <span class="rel-icon">{{ getTypeIcon(getCardType(rel.sourceCardId)) }}</span>
                <span class="rel-title">{{ getCardTitle(rel.sourceCardId) }}</span>
                <span class="rel-type">「{{ rel.relationType }}」</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="loading" class="loading-state">
      <span>加载卡片...</span>
    </div>
    <div v-else class="not-found">
      <span>卡片不存在</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '@/stores/world';

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

const cardId = route.params.cardId as string;
const projectId = route.params.projectId as string;
const loading = ref(false);
const card = ref<any>(null);

const TYPE_LABELS: Record<string, string> = {
  character: '角色',
  location: '地点',
  item: '物品',
  event: '事件',
  ecology: '生态',
  faction: '派系',
  species: '物种',
  lore: '背景设定',
};

const TYPE_ICONS: Record<string, string> = {
  character: '🧙',
  location: '📍',
  item: '⚔️',
  event: '📖',
  ecology: '🌿',
  faction: '🏛️',
  species: '🐉',
  lore: '📜',
};

const isPublic = computed(() => {
  const project = store.projects.find(p => p.id === projectId);
  return project?.isPublic || false;
});

const formattedContent = computed(() => {
  if (!card.value) return '';
  try {
    const data = JSON.parse(card.value.content);
    return JSON.stringify(data, null, 2);
  } catch {
    return card.value.content || '';
  }
});

const cardTags = computed(() => {
  const tags = card.value?.tags;
  if (!tags) return [];
  if (Array.isArray(tags)) return tags;
  try {
    return JSON.parse(tags);
  } catch {
    return [];
  }
});

// ===== 渲染描述，替换 {CARD:uuid} 为可点击标签 =====
const renderedDescription = computed(() => {
  const desc = card.value?.description || '';
  if (!desc) return '';
  // 替换占位符
  return desc.replace(/\{CARD:([^}]+)\}/g, (_match: string, id: string) => {
    const targetCard = store.cards.find(c => c.id === id);
    if (targetCard) {
      const icon = TYPE_ICONS[targetCard.type] || '📄';
      return `<span class="embedded-card" data-card-id="${id}">${icon} ${targetCard.title}</span>`;
    }
    return `<span class="embedded-card broken">⚠️ 已删除的卡片</span>`;
  });
});

// ===== 点击嵌入式卡片跳转 =====
const handleEmbeddedClick = (e: MouseEvent) => {
  const target = (e.target as HTMLElement).closest('.embedded-card') as HTMLElement | null;
  if (target?.dataset?.cardId) {
    const id = target.dataset.cardId;
    if (id === cardId) return;
    router.push(`/world/project/${projectId}/card/${id}`);
  }
};

const allRelations = computed(() => card.value?.relations || []);
const outRelations = computed(() => allRelations.value.filter((r: any) => r.direction === 'out'));
const inRelations = computed(() => allRelations.value.filter((r: any) => r.direction === 'in'));

const getTypeIcon = (type: string) => TYPE_ICONS[type] || '📄';
const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;

const getCardTitle = (id: string) => {
  const c = store.cards.find(c => c.id === id);
  return c?.title || '未知卡片';
};

const getCardType = (id: string) => {
  const c = store.cards.find(c => c.id === id);
  return c?.type || 'unknown';
};

const formatDate = (dateStr: string) => {
  const d = new Date(dateStr);
  return d.toLocaleString('zh-CN');
};

const loadData = async () => {
  loading.value = true;
  try {
    // 传入 projectId 和 cardId
    const data = await store.fetchCardDetail(projectId, cardId);
    card.value = data;
  } catch (error) {
    console.error('加载卡片失败:', error);
    ElMessage.error('加载卡片失败');
  } finally {
    loading.value = false;
  }
};



const goBack = () => {
  router.push(`/world/project/${projectId}`);
};

const goToCard = (targetId: string) => {
  if (targetId === cardId) return;
  router.push(`/world/project/${projectId}/card/${targetId}`);
};

const editCard = () => {
  router.push(`/world/project/${projectId}?edit=${cardId}`);
};

const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('确定要删除这张卡片吗？此操作不可恢复。', '确认删除', {
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
      type: 'warning',
    });
    await store.deleteCard(cardId);
    ElMessage.success('卡片已删除');
    router.push(`/world/project/${projectId}`);
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除失败:', error);
      ElMessage.error('删除失败');
    }
  }
};

onMounted(() => {
  loadData();
  // 监听嵌入式卡片点击事件
  document.addEventListener('click', handleEmbeddedClick);
});
</script>

<style scoped>
.card-detail {
  min-height: 100vh;
  background: #f8f9fc;
  padding: 24px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
.detail-container {
  max-width: 900px;
  margin: 0 auto;
}
.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  color: #64748b;
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 10px;
  transition: all 0.2s;
  margin-bottom: 20px;
}
.back-btn:hover {
  background: #eef2f6;
  color: #1e293b;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  background: white;
  border-radius: 20px;
  padding: 28px 32px;
  margin-bottom: 24px;
  border: 1px solid #f1f3f5;
  flex-wrap: wrap;
  gap: 16px;
}
.header-left {
  flex: 1;
}
.title-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.card-icon {
  font-size: 32px;
}
.title-row h1 {
  margin: 0;
  font-size: 26px;
  font-weight: 700;
  color: #0f172a;
}
.type-badge {
  font-size: 13px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 2px 14px;
  border-radius: 20px;
  font-weight: 500;
}
.subtype-badge {
  font-size: 12px;
  color: #64748b;
  background: #f1f5f9;
  padding: 2px 12px;
  border-radius: 20px;
}
.meta-row {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-top: 10px;
  font-size: 14px;
  color: #94a3b8;
  flex-wrap: wrap;
}
.tag {
  font-size: 12px;
  font-weight: 500;
  padding: 2px 12px;
  border-radius: 20px;
  height: 26px;
  line-height: 26px;
}
.tag.public {
  background: #dcfce7;
  color: #16a34a;
}
.tag.private {
  background: #f1f3f5;
  color: #64748b;
}
.header-right {
  display: flex;
  gap: 8px;
}
.action-btn {
  padding: 8px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  background: white;
  color: #64748b;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}
.action-btn.danger:hover {
  background: #fef2f2;
  color: #ef4444;
  border-color: #fecaca;
}

/* ===== 通用区块 ===== */
.section {
  background: white;
  border-radius: 20px;
  padding: 24px 32px;
  margin-bottom: 20px;
  border: 1px solid #f1f3f5;
}
.section h3 {
  margin: 0 0 12px 0;
  font-size: 16px;
  color: #0f172a;
}

/* ===== 别名 ===== */
.alias-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.alias-item {
  font-size: 14px;
  color: #64748b;
  background: #f1f5f9;
  padding: 4px 16px;
  border-radius: 20px;
}

/* ===== 属性 ===== */
.attribute-grid {
  display: grid;
  grid-template-columns: 1fr 2fr;
  gap: 4px 16px;
}
.attribute-row {
  display: contents;
}
.attr-key-label {
  font-weight: 500;
  color: #64748b;
  padding: 4px 0;
  border-bottom: 1px solid #f1f3f5;
}
.attr-value-label {
  color: #0f172a;
  padding: 4px 0;
  border-bottom: 1px solid #f1f3f5;
}

/* ===== 描述（嵌入式卡片引用样式） ===== */
.description-body {
  font-size: 15px;
  color: #1e293b;
  line-height: 1.8;
  white-space: pre-wrap;
}
.description-body .embedded-card {
  display: inline-block;
  padding: 2px 12px 2px 8px;
  background: #eef2ff;
  color: #4f46e5;
  border-radius: 16px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
  border: 1px solid transparent;
  margin: 0 2px;
}
.description-body .embedded-card:hover {
  background: #c7d2fe;
  border-color: #4f46e5;
  transform: translateY(-1px);
}
.description-body .embedded-card.broken {
  background: #fef2f2;
  color: #ef4444;
  cursor: not-allowed;
}

/* ===== 内容 ===== */
.content-body pre {
  font-family: 'Monaco', 'Menlo', monospace;
  font-size: 14px;
  background: #f8f9fc;
  padding: 16px;
  border-radius: 12px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  margin: 0;
  color: #1e293b;
}

/* ===== 标签 ===== */
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag-item {
  font-size: 13px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 4px 14px;
  border-radius: 20px;
  font-weight: 500;
}

/* ===== 时间线 ===== */
.timeline {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.timeline-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 6px 0;
  border-bottom: 1px solid #f1f3f5;
}
.tl-date {
  font-weight: 500;
  color: #64748b;
  min-width: 110px;
  font-size: 14px;
}
.tl-title {
  font-weight: 500;
  color: #0f172a;
  font-size: 14px;
}
.tl-desc {
  color: #94a3b8;
  font-size: 13px;
}

/* ===== 关联 ===== */
.empty-hint {
  color: #94a3b8;
  font-size: 14px;
  padding: 8px 0;
}
.relation-group {
  margin-bottom: 12px;
}
.group-label {
  font-size: 13px;
  font-weight: 500;
  color: #64748b;
  margin-bottom: 6px;
}
.relation-cards {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.relation-card {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #f8f9fc;
  padding: 4px 12px 4px 8px;
  border-radius: 16px;
  border: 1px solid #e2e8f0;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}
.relation-card:hover {
  background: #eef2ff;
  border-color: #4f46e5;
  transform: translateY(-1px);
}
.rel-icon {
  font-size: 16px;
}
.rel-title {
  font-weight: 500;
  color: #0f172a;
}
.rel-type {
  color: #4f46e5;
  font-size: 12px;
}

.loading-state,
.not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: #94a3b8;
  font-size: 18px;
}
</style>