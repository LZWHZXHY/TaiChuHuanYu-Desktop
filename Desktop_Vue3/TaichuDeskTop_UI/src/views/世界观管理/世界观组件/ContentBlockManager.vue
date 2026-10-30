<!-- src/views/世界观管理/世界观组件/ContentBlockManager.vue -->
<template>
  <div class="content-block-manager">
    <!-- 头部工具栏 -->
    <div class="blocks-header">
      <div class="insert-toolbar">
        <button class="insert-btn" @click="openPicker('')">＋ 引用已有设定</button>
      </div>
    </div>

    <!-- 已插入的卡片列表 -->
    <div class="blocks-list">
      <EmbeddedCard
        v-for="(block, idx) in localValue"
        :key="block.id"
        :card="getBlockDetail(block.cardId)"
        :context-label="block.contextLabel"
        :fallback-title="getBlockTitle(block)"
        :fallback-type="block.cardType"
        :fallback-cover="block.cardCover"
        :fallback-attributes="block.cardAttributes"
        :loading="!getBlockDetail(block.cardId)"
        :project-id="projectId"
        collapsible
        :expanded="isExpanded(block.id)"
        removable
        @update:expanded="(v: boolean) => toggleExpand(block.id, v)"
        @remove="removeBlock(idx)"
      />

      <div v-if="!localValue.length" class="blocks-empty">
        引用会展示原条目，不会复制内容，也不强制建立关系。
      </div>
    </div>

    <!-- 插入选择器浮层 -->
    <div v-if="showPicker" class="picker-overlay" @click.self="closePicker">
      <div class="picker-modal" role="dialog" aria-modal="true" aria-label="引用设定">
        <div class="picker-header">
          <span>{{ creating ? '新建设定并引用' : '引用已有设定' }}</span>
          <button class="picker-close" :disabled="creatingCard" aria-label="关闭引用选择" @click="closePicker">✕</button>
        </div>
        <div class="picker-search">
          <select v-model="pickerType" :disabled="creatingCard" aria-label="引用类型"><option v-if="!creating" value="">全部类型</option><option v-for="type in cardTypeOptions" :key="type.value" :value="type.value">{{ type.label }}</option></select>
          <input v-model="pickerSearch" :disabled="creatingCard" :placeholder="creating ? '新条目名称' : '搜索名称'" :maxlength="creating ? 200 : undefined" />
        </div>
        <div class="picker-context">
          <input
            v-model="pickerContextLabel"
            :disabled="creatingCard"
            placeholder="引用说明（可选，如：故事发生地）"
            class="context-input"
          />
        </div>
        <div v-if="!creating" class="picker-list">
          <button
            v-for="card in pickerResults"
            :key="card.id"
            class="picker-item"
            type="button"
            @click="insertBlock(card)"
          >
            <span class="picker-type">{{ getTypeLabel(card.type) }}</span>
            <span class="picker-title">{{ card.title }}</span>
            <span v-if="card.description" class="picker-summary">
              {{ card.description.slice(0, 30) }}
            </span>
          </button>
          <div v-if="!pickerResults.length" class="picker-empty">
            没有可用的卡片
          </div>
        </div>
        <p v-else class="blocks-empty">只需起个名字，详细设定可以稍后再写。当前正在编辑的内容会保留；新条目会单独保存到这个世界。</p>
        <p v-if="createError" class="blocks-empty" role="alert">{{ createError }}</p>
        <div class="picker-footer">
          <button class="btn-outline" :disabled="creatingCard" @click="creating ? creating = false : closePicker()">{{ creating ? '返回选择' : '取消' }}</button>
          <button class="btn-primary" :disabled="creatingCard || (creating && !pickerSearch.trim())" @click="handleCreateAndInsert">{{ creatingCard ? '创建中…' : creating ? '创建并引用' : '没有合适的？新建' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { ElMessage } from 'element-plus';
import { v4 as uuidv4 } from 'uuid';
import { useWorldStore } from '@/stores/world';
import type { CardDetail, CardSummary } from '@/api/types/world';
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation';
import type { ContentBlock } from '../card_type';
import { useCardPreload } from '@/composables/世界观管理/useCardPreload';
import { useExpandedItems } from '@/composables/世界观管理/useExpandedItems';
import EmbeddedCard from './EmbeddedCard.vue';

// ===== Props =====
const props = defineProps<{
  modelValue: ContentBlock[];
  projectId: string;
  cardTypeOptions: { value: string; label: string }[];
  excludeCardId?: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: ContentBlock[]): void;
}>();

// ===== Store =====
const store = useWorldStore();

// ===== 本地状态 =====
const localValue = ref([...props.modelValue]);

const { isExpanded, toggle: toggleExpand } = useExpandedItems();

// 选择器状态
const showPicker = ref(false);
const pickerType = ref('');
const creating = ref(false);
const creatingCard = ref(false);
const createError = ref('');
const pickerSearch = ref('');
const pickerContextLabel = ref('');
type PickerCard = CardSummary & Partial<Pick<CardDetail, 'description'>>;
const pickerResults = computed<PickerCard[]>(() => {
  const query = pickerSearch.value.toLowerCase();
  return store.cards.filter(card =>
    (!pickerType.value || card.type === pickerType.value) && card.id !== props.excludeCardId &&
    !localValue.value.some(block => block.cardId === card.id) &&
    (!query || card.title.toLowerCase().includes(query))
  ).slice(0, 20);
});

const { load: loadDetails, getDetail: getBlockDetail } = useCardPreload(
  () => props.projectId, () => localValue.value.map(block => block.cardId));

// ===== 兜底标题 =====
const getBlockTitle = (block: ContentBlock): string => {
  const detail = getBlockDetail(block.cardId);
  if (detail) return detail.title;
  if (block.cardTitle) return block.cardTitle;
  const card = store.cards.find((c) => c.id === block.cardId);
  return card?.title || '已删除的卡片';
};

// ===== 选择器相关 =====
const openPicker = (type: string) => {
  pickerType.value = type;
  creating.value = false;
  createError.value = '';
  pickerSearch.value = '';
  pickerContextLabel.value = '';
  showPicker.value = true;
};

const closePicker = () => {
  if (creatingCard.value) return;
  showPicker.value = false;
};

const insertBlock = async (card: CardSummary) => {
  if (localValue.value.some((b) => b.cardId === card.id)) {
    ElMessage.warning('已插入');
    return;
  }

  const contextLabel = pickerContextLabel.value.trim() || undefined;

  const blockId = uuidv4();
  localValue.value.push({
    id: blockId,
    cardId: card.id,
    cardType: card.type,
    order: localValue.value.length,
    cardTitle: card.title,
    cardCover: card.coverImage || '',
    cardSummary: '',
    cardAttributes: [],
    contextLabel,
  });

  pickerContextLabel.value = '';
  closePicker();
  ElMessage.success(`已插入：${card.title}`);

  await loadDetails([card.id]);
  toggleExpand(blockId, true);
};

const removeBlock = (idx: number) => {
  localValue.value.splice(idx, 1);
};

const handleCreateAndInsert = async () => {
  if (creatingCard.value) return;
  if (!creating.value) {
    creating.value = true;
    pickerType.value ||= 'character';
    return;
  }
  if (!pickerSearch.value.trim()) return;
  creatingCard.value = true;
  createError.value = '';
  try {
    const created = await store.createCard(props.projectId, { title: pickerSearch.value.trim(), type: pickerType.value });
    creatingCard.value = false;
    await insertBlock(created);
  } catch (error: any) {
    createError.value = error?.response?.data?.message || '创建失败，当前内容已保留，请重试';
  } finally { creatingCard.value = false; }
};

// ===== 双向绑定 =====
watch(
  localValue,
  (val) => {
    emit('update:modelValue', val);
  },
  { deep: true }
);

watch(
  () => props.modelValue,
  (val) => {
    if (JSON.stringify(val) !== JSON.stringify(localValue.value)) {
      localValue.value = [...val];
    }
  },
  { deep: true }
);

defineExpose({
  creatingCard,
  openPicker,
  closePicker,
});
</script>

<style scoped>
/* ===== 头部工具栏 ===== */
.blocks-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}
.blocks-header > span {
  font-weight: 500;
  font-size: 14px;
  color: var(--text-primary);
}
.insert-toolbar {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
.insert-btn {
  padding: 8px 14px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--paper-card);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--text-secondary);
}
.insert-btn:hover {
  background: var(--accent-soft);
  border-color: var(--accent-color);
  color: var(--accent-color);
}

/* ===== 卡片列表 ===== */
.blocks-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 8px;
  /* ✅ 删掉了 max-height: 500px 和 overflow-y: auto，让内容自然撑开 */
}

.blocks-empty {
  padding: 12px;
  text-align: center;
  color: var(--text-secondary);
  font-size: 13px;
  border: 1px dashed var(--border-line);
  border-radius: 0;
}

/* ===== 选择器浮层 ===== */
.picker-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.3);
  backdrop-filter: blur(2px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.picker-modal {
  background: var(--paper-card);
  border-radius: 0;
  width: 380px;
  max-width: 92%;
  max-height: 70vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.12);
}
.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-line);
  font-weight: 600;
  font-size: 15px;
  color: var(--text-primary);
}
.picker-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--text-secondary);
  transition: color 0.2s;
}
.picker-close:hover {
  color: var(--text-primary);
}
.picker-search {
  display: flex; gap: 8px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--border-line);
}
.picker-search input, .picker-search select {
  min-width: 0; color: var(--text-primary); box-sizing: border-box;
  width: 100%;
  padding: 6px 12px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 13px;
  background: var(--paper-card);
  transition: border-color 0.2s;
}
.picker-search select { width: auto; flex-shrink: 0; }
.picker-search input:focus {
  outline: none;
  border-color: var(--accent-color);
  background: var(--paper-card);
}
.picker-context {
  padding: 8px 12px;
  border-bottom: 1px solid var(--border-line);
}
.picker-context .context-input {
  color: var(--text-primary); box-sizing: border-box;
  width: 100%;
  padding: 6px 12px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 13px;
  background: var(--paper-card);
  transition: border-color 0.2s, background 0.2s;
}
.picker-context .context-input:focus {
  outline: none;
  border-color: var(--accent-color);
  background: var(--paper-card);
}
.picker-context .context-input::placeholder {
  color: var(--text-secondary);
}
.picker-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}
.picker-list::-webkit-scrollbar {
  width: 4px;
}
.picker-list::-webkit-scrollbar-track {
  background: transparent;
}
.picker-list::-webkit-scrollbar-thumb {
  background: var(--bg-sub);
  border-radius: 0;
}
.picker-item {
  width: 100%; border: 0; background: transparent; text-align: left; font: inherit;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  cursor: pointer;
  transition: background 0.15s;
}
.picker-item:hover {
  background: var(--bg-sub);
}
.picker-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 8px;
  border-radius: 0;
  flex-shrink: 0;
}
.picker-title {
  font-weight: 500;
  font-size: 14px;
  color: var(--text-primary);
  flex: 1;
}
.picker-summary {
  font-size: 11px;
  color: var(--text-secondary);
  max-width: 80px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.picker-empty {
  padding: 20px;
  text-align: center;
  color: var(--text-secondary);
  font-size: 14px;
}
.picker-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px 16px;
  border-top: 1px solid var(--border-line);
}
.picker-footer .btn-outline {
  padding: 6px 16px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: transparent;
  cursor: pointer;
  font-size: 13px;
  color: var(--text-secondary);
  transition: all 0.2s;
}
.picker-footer .btn-outline:hover {
  background: var(--bg-sub);
  border-color: var(--border-line);
}
.picker-footer .btn-primary {
  padding: 6px 20px;
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  border-radius: 0;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: background 0.2s;
}
.picker-footer .btn-primary:hover {
  background: var(--accent-color);
}
</style>
