<template>
  <div class="universe-workspace" :class="[`mode-${mode}`, { 'view-mode': isViewMode }]">
    <!-- ============================================================ -->
    <!-- 顶部导航栏 -->
    <!-- ============================================================ -->
    <header class="workspace-header">
      <div class="header-left">
        <div class="breadcrumb">
          <span class="icon">🌌</span> 灵脉宇宙 /
          <span class="highlight">{{ mode === 'wiki' ? '纪元流传' : '本源设定' }}</span>
          <span v-if="entryType" class="entry-type-badge" :style="{ background: typeColor }">
            {{ entryTypeLabel }}
          </span>
        </div>
      </div>

      <div class="header-right">
        <!-- 视图切换开关 -->
        <div class="view-toggle">
          <button 
            class="toggle-btn" 
            :class="{ active: isViewMode }" 
            @click="isViewMode = true"
          >👁️ 查看</button>
          <button 
            class="toggle-btn" 
            :class="{ active: !isViewMode }" 
            @click="isViewMode = false"
          >✏️ 编辑</button>
        </div>

        <span class="save-status" :class="{ saving: isSaving }">
          {{ saveStatusText }}
        </span>

        <button class="action-btn primary" @click="handleSave">
          💾 保存快照
        </button>
      </div>
    </header>

    <!-- ============================================================ -->
    <!-- 主布局 -->
    <!-- ============================================================ -->
    <div class="workspace-layout">
      <!-- 左侧主内容区 -->
      <main class="narrative-canvas">
        <div class="canvas-inner">
          <!-- 标题区 -->
          <div class="entry-header">
            <div class="title-wrapper">
              <input 
                v-model="internalTitle" 
                class="title-input" 
                placeholder="赋予此存在一个真名..." 
                :disabled="isViewMode"
              />
              <span v-if="charCount" class="char-count">{{ charCount }} 字符</span>
            </div>
            <div class="header-meta" v-if="!isViewMode">
              <span class="meta-hint">当前处于编辑模式</span>
            </div>
          </div>

          <!-- 编辑器插槽 -->
          <div class="editor-portal" :class="{ 'readonly-shadow': isViewMode }">
            <slot name="editor"></slot>
          </div>

          <!-- 底部信息 -->
          <div class="entry-footer" v-if="lastModified">
            <span>🕒 最后修订：{{ formattedDate(lastModified) }}</span>
          </div>
        </div>
      </main>

      <!-- 右侧侧边栏（可折叠） -->
      <aside class="inspector-panel">
        <!-- 1. 身份与元数据 -->
        <details class="inspector-card" open>
          <summary class="card-header">
            <h3>📌 身份与元数据</h3>
            <span class="badge" v-if="status">{{ statusLabel }}</span>
          </summary>
          <div class="card-body meta-grid">
            <div class="meta-row">
              <span class="meta-label">类型</span>
              <span class="meta-value type-tag-display">{{ entryTypeLabel }}</span>
            </div>
            <div class="meta-row" v-if="createdAt">
              <span class="meta-label">创建</span>
              <span class="meta-value">{{ formattedDate(createdAt) }}</span>
            </div>
            <div class="meta-row" v-if="lastModified">
              <span class="meta-label">修改</span>
              <span class="meta-value">{{ formattedDate(lastModified) }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">状态</span>
              <div class="status-control">
                <select v-model="editableStatus" class="status-select" :disabled="isViewMode">
                  <option value="draft">📄 草稿</option>
                  <option value="review">🔍 审核中</option>
                  <option value="published">✨ 已发布</option>
                  <option value="archived">📦 已归档</option>
                </select>
                <span v-if="!isViewMode" class="edit-hint">(可编辑)</span>
              </div>
            </div>
          </div>
        </details>

        <!-- 2. 核心界定（自定义属性） -->
        <details class="inspector-card" open>
          <summary class="card-header">
            <h3>⚙️ 核心界定</h3>
            <button class="add-btn" @click.stop="addProperty" v-if="!isViewMode">+ 添加</button>
          </summary>
          <div class="card-body property-grid">
            <div v-if="propertyList.length === 0" class="empty-state">
              <span>暂无界定属性，点击添加</span>
            </div>
            <div v-for="(prop, index) in propertyList" :key="index" class="prop-item">
              <div class="prop-fields">
                <input 
                  v-model="prop.key" 
                  class="prop-key" 
                  placeholder="属性名 (如: 位阶)" 
                  :disabled="isViewMode"
                />
                <span class="prop-separator">:</span>
                <input 
                  v-model="prop.value" 
                  class="prop-value" 
                  placeholder="属性值 (如: 传奇)" 
                  :disabled="isViewMode"
                />
              </div>
              <button 
                v-if="!isViewMode" 
                class="remove-btn" 
                @click="removeProperty(index)"
                title="删除此属性"
              >✕</button>
            </div>
          </div>
        </details>

        <!-- 3. 灵脉羁绊（关联关系） -->
        <details class="inspector-card" open>
          <summary class="card-header">
            <h3>🔗 灵脉羁绊</h3>
            <button class="add-btn" @click.stop="openRelationPicker" v-if="!isViewMode">+ 链接</button>
          </summary>
          <div class="card-body relation-grid">
            <div v-if="relations.length === 0" class="empty-state">
              <span>还没有关联，点下方添加与其他笔记的关联</span>
            </div>
            <div class="relation-chips">
              <div 
                v-for="rel in relations" 
                :key="rel.id" 
                class="relation-chip"
                @click="$emit('jump', rel.id)"
              >
                <span class="chip-icon">🔮</span>
                <span class="chip-title">{{ rel.title }}</span>
                <span class="chip-type">{{ rel.relationType || '相关' }}</span>
                <button 
                  v-if="!isViewMode" 
                  class="chip-remove" 
                  @click.stop="removeRelation(rel.id)"
                >✕</button>
              </div>
            </div>
          </div>
        </details>

        <!-- 4. 跃迁信标（快速导航） -->
        <details class="inspector-card ghost-card" open>
          <summary class="card-header">
            <h3>🧭 跃迁信标</h3>
          </summary>
          <div class="card-body">
            <div class="search-wrapper">
              <span class="search-icon">🔍</span>
              <input 
                v-model="searchQuery" 
                class="search-input" 
                placeholder="搜索笔记标题…" 
              />
            </div>
            <div class="index-scroll">
              <div 
                v-for="note in filteredNotes" 
                :key="note.id" 
                class="index-item" 
                @click="$emit('jump', note.id)"
              >
                <span class="item-icon">{{ getTypeIcon(note.type) }}</span>
                <span class="item-name">{{ note.title }}</span>
                <span class="item-type">{{ getTypeLabel(note.type) }}</span>
              </div>
              <div v-if="filteredNotes.length === 0 && searchQuery" class="empty-state">
                没有找到匹配的笔记
              </div>
            </div>
          </div>
        </details>
      </aside>
    </div>

    <!-- ============================================================ -->
    <!-- 关系选择器浮窗 (轻量级) -->
    <!-- ============================================================ -->
    <Teleport to="body">
      <div v-if="showRelationPicker" class="picker-overlay" @click.self="showRelationPicker = false">
        <div class="picker-modal">
          <div class="picker-header">
            <h4>🔗 建立羁绊</h4>
            <button class="close-picker" @click="showRelationPicker = false">✕</button>
          </div>
          <input 
            v-model="relationSearch" 
            class="picker-search" 
            placeholder="搜索要关联的条目..." 
            autofocus
          />
          <div class="picker-list">
            <div 
              v-for="note in relationCandidates" 
              :key="note.id" 
              class="picker-item"
              @click="addRelation(note)"
            >
              <span class="pi-icon">{{ getTypeIcon(note.type) }}</span>
              <span class="pi-title">{{ note.title }}</span>
              <span class="pi-type">{{ getTypeLabel(note.type) }}</span>
              <button class="pi-add">+ 关联</button>
            </div>
            <div v-if="relationCandidates.length === 0" class="empty-state picker-empty">
              没有可关联的笔记（或已全部关联）
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';

// ============================================================
// Props & Emits
// ============================================================
const props = defineProps<{
  mode?: 'wiki' | 'setting';
  title: string;
  allNotes?: any[];
  entryType?: string;               // 'character' | 'location' | 'faction' | 'race' | 'ability' | 'item' | 'event' | 'concept'
  properties?: Array<{ key: string; value: string }>;
  relations?: Array<{ id: string; title: string; relationType?: string }>;
  status?: 'draft' | 'review' | 'published' | 'archived';
  createdAt?: string;
  lastModified?: string;
}>();

const emit = defineEmits<{
  (e: 'jump', id: string): void;
  (e: 'update:title', val: string): void;
  (e: 'save', payload: any): void;
  (e: 'update:properties', props: Array<{ key: string; value: string }>): void;
  (e: 'update:relations', rels: Array<{ id: string; title: string; relationType?: string }>): void;
  (e: 'update:status', status: string): void;
}>();

// ============================================================
// 内部状态
// ============================================================
const internalTitle = ref(props.title);
const isViewMode = ref(true);         // true=查看, false=编辑
const editableStatus = ref(props.status || 'draft');
const propertyList = ref<Array<{ key: string; value: string }>>(props.properties || []);
const relations = ref<Array<{ id: string; title: string; relationType?: string }>>(props.relations || []);
const searchQuery = ref('');
const showRelationPicker = ref(false);
const relationSearch = ref('');
const isSaving = ref(false);

// ============================================================
// 计算属性 (工具)
// ============================================================
const typeColor = computed(() => {
  const colors: Record<string, string> = {
    character: '#e8f5e9', location: '#e3f2fd', faction: '#fce4ec',
    race: '#f3e5f5', ability: '#fff3e0', item: '#e0f7fa',
    event: '#ffebee', concept: '#f5f5f5'
  };
  return colors[props.entryType || ''] || '#e5f0ff';
});

const entryTypeLabel = computed(() => {
  const map: Record<string, string> = {
    character: '角色', location: '地点', organization: '组织',
    race: '种族', ability: '能力', item: '物品',
    event: '事件', concept: '概念', faction: '势力',
    nation: '国家', geography: '地理', wiki: '词条',
    setting: '设定'
  };
  return map[props.entryType || ''] || props.entryType || '条目';
});

const statusLabel = computed(() => {
  const map: Record<string, string> = {
    draft: '草稿', review: '审核中', published: '已发布', archived: '已归档'
  };
  return map[editableStatus.value] || editableStatus.value;
});

const saveStatusText = computed(() => {
  if (isSaving.value) return '⏳ 保存中...';
  return '☁️ 已同步';
});

const charCount = computed(() => {
  // 简单字符统计（依赖父组件编辑器内容，这里仅作为示意占位）
  // 实际项目中可通过 slot 传递，这里保留 UI 位置
  return 0; 
});

// ============================================================
// 过滤与搜索
// ============================================================
const filteredNotes = computed(() => {
  if (!props.allNotes) return [];
  if (!searchQuery.value) return props.allNotes.slice(0, 6);
  const q = searchQuery.value.toLowerCase();
  return props.allNotes
    .filter(n => n.title.toLowerCase().includes(q))
    .slice(0, 6);
});

const relationCandidates = computed(() => {
  if (!props.allNotes) return [];
  const q = relationSearch.value.toLowerCase();
  return props.allNotes
    .filter(n => !relations.value.some(r => r.id === n.id))
    .filter(n => n.title.toLowerCase().includes(q))
    .slice(0, 8);
});

// ============================================================
// 方法
// ============================================================
const formattedDate = (dateStr?: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return d.toLocaleDateString('zh-CN', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
};

const getTypeIcon = (type?: string) => {
  const map: Record<string, string> = {
    character: '🧑', location: '📍', organization: '🏛️',
    race: '🧬', ability: '⚡', item: '📦',
    event: '🎭', concept: '💡', faction: '⚔️',
    nation: '🏳️', geography: '🌍', wiki: '📜',
    note: '📄', post: '💬', blog: '📝'
  };
  return map[type || ''] || '📄';
};

const getTypeLabel = (type?: string) => {
  const map: Record<string, string> = {
    character: '角色', location: '地点', organization: '组织',
    race: '种族', ability: '能力', item: '物品',
    event: '事件', concept: '概念', faction: '势力',
    nation: '国家', geography: '地理', wiki: '词条',
    note: '笔记', post: '帖子', blog: '博客'
  };
  return map[type || ''] || '笔记';
};

// 属性操作
const addProperty = () => {
  propertyList.value.push({ key: '', value: '' });
};
const removeProperty = (index: number) => {
  propertyList.value.splice(index, 1);
};

// 关系操作
const openRelationPicker = () => {
  showRelationPicker.value = true;
  relationSearch.value = '';
};
const addRelation = (note: any) => {
  if (!relations.value.some(r => r.id === note.id)) {
    relations.value.push({ 
      id: note.id, 
      title: note.title, 
      relationType: '相关' 
    });
  }
  showRelationPicker.value = false;
};
const removeRelation = (id: string) => {
  relations.value = relations.value.filter(r => r.id !== id);
};

// 保存操作
const handleSave = async () => {
  isSaving.value = true;
  try {
    await emit('save', {
      title: internalTitle.value,
      properties: propertyList.value,
      relations: relations.value,
      status: editableStatus.value,
    });
    // 模拟保存延迟
    await new Promise(resolve => setTimeout(resolve, 600));
  } finally {
    isSaving.value = false;
  }
};

// ============================================================
// 响应式双向绑定 (Watch)
// ============================================================
watch(internalTitle, (val) => emit('update:title', val));
watch(propertyList, (val) => emit('update:properties', val), { deep: true });
watch(relations, (val) => emit('update:relations', val), { deep: true });
watch(editableStatus, (val) => emit('update:status', val));

// 同步外部 Props 变化 (当父组件切换条目时)
watch(() => props.title, (val) => { internalTitle.value = val; });
watch(() => props.properties, (val) => { if (val) propertyList.value = val; }, { deep: true });
watch(() => props.relations, (val) => { if (val) relations.value = val; }, { deep: true });
watch(() => props.status, (val) => { if (val) editableStatus.value = val; });
</script>

<style scoped>
/* ================================================================ */
/* 全局与布局 */
/* ================================================================ */
.universe-workspace {
  min-height: 100vh;
  background-color: var(--bg-main);
  color: var(--text-primary);
  font-family: -apple-system, BlinkMacSystemFont, "PingFang SC", sans-serif;
  padding: 30px 40px 60px;
  box-sizing: border-box;
  transition: background 0.3s;
}

/* 查看模式下的柔和背景 */
.universe-workspace.view-mode .narrative-canvas {
  background: var(--paper-card);
}

/* ================================================================ */
/* 顶部导航栏 */
/* ================================================================ */
.workspace-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 32px;
  max-width: 1440px;
  margin-left: auto;
  margin-right: auto;
  flex-wrap: wrap;
  gap: 12px;
}

.header-left {
  display: flex;
  align-items: center;
}

.breadcrumb {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
  letter-spacing: 0.03em;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.breadcrumb .highlight {
  color: var(--accent-color);
}
.entry-type-badge {
  padding: 2px 12px;
  border-radius: 0;
  font-size: 11px;
  font-weight: 700;
  color: var(--text-primary);
  background: var(--accent-soft);
  letter-spacing: 0.02em;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

/* 视图切换胶囊 */
.view-toggle {
  display: flex;
  background: var(--bg-sub);
  border-radius: 0;
  padding: 3px;
  border: 1px solid var(--border-line);
}
.toggle-btn {
  border: none;
  background: transparent;
  padding: 4px 16px;
  border-radius: 0;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
}
.toggle-btn.active {
  background: var(--paper-card);
  color: var(--text-primary);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
}
.toggle-btn:hover:not(.active) {
  color: var(--text-primary);
}

.save-status {
  font-size: 12px;
  color: var(--success);
  font-weight: 500;
  transition: all 0.3s;
}
.save-status.saving {
  color: var(--warning);
}

.action-btn {
  padding: 6px 18px;
  border-radius: 0;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn:hover {
  background: var(--bg-sub);
}
.action-btn.primary {
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
}
.action-btn.primary:hover {
  background: var(--accent-color);
  box-shadow: 0 4px 12px rgba(0, 102, 204, 0.3);
}

/* ================================================================ */
/* 主布局 Grid */
/* ================================================================ */
.workspace-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 340px;
  gap: 32px;
  max-width: 1440px;
  margin: 0 auto;
  align-items: start;
}

/* ================================================================ */
/* 左侧主内容 */
/* ================================================================ */
.narrative-canvas {
  background: var(--paper-card);
  border-radius: 0;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.04), 0 1px 3px rgba(0, 0, 0, 0.02);
  min-height: 70vh;
  padding: 40px 56px;
  transition: all 0.3s;
}

.canvas-inner {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.entry-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  border-bottom: 1px solid var(--border-line);
  padding-bottom: 16px;
  gap: 20px;
  flex-wrap: wrap;
}
.title-wrapper {
  flex: 1;
  position: relative;
}
.title-input {
  width: 100%;
  font-size: 2.6rem;
  font-weight: 800;
  border: none;
  background: transparent;
  outline: none;
  color: var(--text-primary);
  padding: 0;
  line-height: 1.2;
  transition: opacity 0.2s;
}
.title-input:disabled {
  opacity: 0.8;
  cursor: default;
}
.title-input::placeholder {
  color: var(--text-secondary);
}
.char-count {
  position: absolute;
  right: 0;
  bottom: -20px;
  font-size: 11px;
  color: var(--text-secondary);
  font-weight: 500;
}
.header-meta .meta-hint {
  font-size: 12px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 4px 12px;
  border-radius: 0;
  font-weight: 500;
}

.editor-portal {
  flex: 1;
  min-height: 300px;
}
.editor-portal.readonly-shadow {
  opacity: 0.85;
  pointer-events: none; /* 让编辑器内容不可编辑，但保留滚动 */
}
/* 让插槽内的编辑器在查看模式下视觉变灰 */
.editor-portal.readonly-shadow :deep(.ProseMirror) {
  cursor: default;
}

.entry-footer {
  border-top: 1px solid var(--border-line);
  padding-top: 16px;
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  justify-content: flex-end;
}

/* ================================================================ */
/* 右侧侧边栏 */
/* ================================================================ */
.inspector-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  position: sticky;
  top: 20px;
}

.inspector-card {
  background: var(--paper-card);
  border-radius: 0;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.03);
  overflow: hidden;
  transition: all 0.2s;
}
.inspector-card:hover {
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.04);
}
.ghost-card {
  background: transparent;
  box-shadow: none;
  border: none;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
  cursor: pointer;
  user-select: none;
  transition: background 0.2s;
  list-style: none;
}
.card-header::-webkit-details-marker {
  display: none;
}
.card-header:hover {
  background: var(--paper-card);
}
.card-header h3 {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
  letter-spacing: 0.02em;
}
.card-header .badge {
  font-size: 10px;
  background: var(--bg-sub);
  padding: 2px 10px;
  border-radius: 0;
  font-weight: 600;
  color: var(--text-secondary);
}
.card-header .add-btn {
  background: none;
  border: 1px dashed var(--border-line);
  color: var(--text-secondary);
  border-radius: 0;
  padding: 2px 12px;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}
.card-header .add-btn:hover {
  border-color: var(--accent-color);
  color: var(--accent-color);
  background: var(--bg-main);
}

.card-body {
  padding: 0 20px 20px;
}

/* --- 元数据 --- */
.meta-grid {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.meta-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
}
.meta-label {
  color: var(--text-secondary);
  font-weight: 500;
}
.meta-value {
  color: var(--text-primary);
  font-weight: 500;
}
.type-tag-display {
  background: var(--bg-sub);
  padding: 2px 10px;
  border-radius: 0;
  font-size: 12px;
}
.status-control {
  display: flex;
  align-items: center;
  gap: 8px;
}
.status-select {
  border: 1px solid var(--border-line);
  border-radius: 0;
  padding: 4px 8px;
  font-size: 12px;
  background: var(--paper-card);
  outline: none;
  font-weight: 500;
}
.status-select:disabled {
  background: var(--bg-main);
  color: var(--text-secondary);
}
.edit-hint {
  font-size: 10px;
  color: var(--success);
  font-weight: 500;
}

/* --- 属性 --- */
.property-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.prop-item {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--paper-card);
  border-radius: 0;
  padding: 4px 4px 4px 12px;
  border: 1px solid transparent;
  transition: all 0.2s;
}
.prop-item:focus-within {
  border-color: var(--accent-color);
  background: var(--paper-card);
  box-shadow: 0 0 0 2px rgba(0, 102, 204, 0.05);
}
.prop-fields {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 4px;
}
.prop-key {
  flex: 1;
  border: none;
  background: transparent;
  padding: 6px 0;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  outline: none;
  min-width: 60px;
}
.prop-key:disabled {
  color: var(--text-secondary);
}
.prop-separator {
  color: var(--text-secondary);
  font-weight: 300;
}
.prop-value {
  flex: 2;
  border: none;
  background: transparent;
  padding: 6px 0;
  font-size: 12px;
  color: var(--text-primary);
  outline: none;
}
.prop-value:disabled {
  color: var(--text-secondary);
}
.remove-btn {
  background: none;
  border: none;
  color: var(--danger);
  cursor: pointer;
  font-size: 14px;
  padding: 0 8px;
  opacity: 0.5;
  transition: opacity 0.2s;
}
.remove-btn:hover {
  opacity: 1;
}

.empty-state {
  font-size: 12px;
  color: var(--text-secondary);
  text-align: center;
  padding: 12px 0;
  font-weight: 400;
}

/* --- 关系 --- */
.relation-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.relation-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.relation-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--bg-sub);
  padding: 4px 10px 4px 8px;
  border-radius: 0;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}
.relation-chip:hover {
  background: var(--accent-soft);
  border-color: var(--accent-color);
  transform: translateY(-1px);
}
.chip-icon {
  font-size: 12px;
}
.chip-title {
  font-weight: 500;
  color: var(--text-primary);
}
.chip-type {
  font-size: 9px;
  color: var(--text-secondary);
  background: var(--paper-card);
  padding: 1px 6px;
  border-radius: 0;
}
.chip-remove {
  background: none;
  border: none;
  color: var(--danger);
  font-size: 12px;
  cursor: pointer;
  padding: 0 2px;
  opacity: 0.4;
}
.chip-remove:hover {
  opacity: 1;
}

/* --- 搜索与信标 --- */
.search-wrapper {
  position: relative;
  margin-bottom: 12px;
}
.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 14px;
  color: var(--text-secondary);
}
.search-input {
  width: 100%;
  padding: 8px 12px 8px 36px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 13px;
  outline: none;
  background: var(--paper-card);
  box-sizing: border-box;
  transition: box-shadow 0.2s;
}
.search-input:focus {
  box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.08);
  border-color: var(--accent-color);
}

.index-scroll {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: 220px;
  overflow-y: auto;
}
.index-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 10px;
  border-radius: 0;
  cursor: pointer;
  transition: background 0.15s;
}
.index-item:hover {
  background: var(--bg-sub);
}
.item-icon {
  font-size: 14px;
}
.item-name {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.item-type {
  font-size: 10px;
  color: var(--text-secondary);
  background: var(--bg-sub);
  padding: 1px 8px;
  border-radius: 0;
}

/* ================================================================ */
/* 浮层选择器 (Teleport) */
/* ================================================================ */
.picker-overlay {
  position: fixed;
  inset: 0;
  background: var(--overlay);
  backdrop-filter: blur(6px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}
.picker-modal {
  background: var(--paper-card);
  border-radius: 0;
  padding: 24px;
  width: 420px;
  max-width: 94%;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 30px 80px rgba(0, 0, 0, 0.2);
  animation: pickerIn 0.25s cubic-bezier(0.16, 1, 0.3, 1);
}
@keyframes pickerIn {
  from { opacity: 0; transform: scale(0.95) translateY(10px); }
  to { opacity: 1; transform: scale(1) translateY(0); }
}
.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.picker-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
}
.close-picker {
  background: var(--bg-sub);
  border: none;
  width: 30px;
  height: 30px;
  border-radius: 0;
  font-size: 16px;
  cursor: pointer;
  transition: background 0.2s;
}
.close-picker:hover {
  background: var(--bg-sub);
}
.picker-search {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 14px;
  outline: none;
  margin-bottom: 16px;
  box-sizing: border-box;
}
.picker-search:focus {
  border-color: var(--accent-color);
}
.picker-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 300px;
}
.picker-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  border-radius: 0;
  cursor: pointer;
  transition: background 0.15s;
}
.picker-item:hover {
  background: var(--bg-sub);
}
.pi-icon { font-size: 16px; }
.pi-title { flex: 1; font-weight: 500; }
.pi-type { font-size: 11px; color: var(--text-secondary); }
.pi-add {
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  padding: 2px 12px;
  border-radius: 0;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
}
.pi-add:hover {
  background: var(--accent-color);
}
.picker-empty {
  padding: 30px 0;
}

/* ================================================================ */
/* 响应式 */
/* ================================================================ */
@media (max-width: 1024px) {
  .universe-workspace {
    padding: 16px;
  }
  .workspace-layout {
    grid-template-columns: 1fr;
    gap: 24px;
  }
  .inspector-panel {
    position: static;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
  }
  .ghost-card {
    grid-column: 1 / -1;
  }
  .narrative-canvas {
    padding: 24px;
  }
  .title-input {
    font-size: 2rem;
  }
  .picker-modal {
    width: 90%;
  }
}

@media (max-width: 640px) {
  .inspector-panel {
    grid-template-columns: 1fr;
  }
  .workspace-header {
    flex-direction: column;
    align-items: stretch;
  }
  .header-right {
    justify-content: flex-start;
  }
}
</style>