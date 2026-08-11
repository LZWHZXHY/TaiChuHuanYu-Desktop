<template>
  <div class="content-block-manager">
    <!-- 头部工具栏 -->
    <div class="blocks-header">
      <span>关联内容</span>
      <div class="insert-toolbar">
        <button
          v-for="type in cardTypeOptions"
          :key="type.value"
          class="insert-btn"
          @click="openPicker(type.value)"
        >
          +{{ type.label }}
        </button>
      </div>
    </div>

    <!-- 已插入的卡片列表 -->
    <div class="blocks-list">
      <div v-for="(block, idx) in localValue" :key="block.id" class="block-card">
        <div class="block-preview-wrapper">
          <div v-if="getBlockCover(block)" class="block-cover-small">
            <img :src="getBlockCover(block)" />
          </div>
          <div class="block-info">
            <div class="block-title-row">
              <span class="block-type-badge">{{ getBlockTypeLabel(block) }}</span>
              <span v-if="block.contextLabel" class="block-context-label">
                {{ block.contextLabel }} →
              </span>
              <span class="block-title">{{ getBlockTitle(block) }}</span>
            </div>
            <p class="block-summary">{{ getBlockSummary(block) }}</p>
            <div v-if="getBlockAttributes(block).length" class="block-attr-tags">
              <span
                v-for="attr in getBlockAttributes(block).slice(0, 4)"
                :key="attr.key"
                class="block-attr-tag"
              >
                {{ attr.key }}: {{ attr.value }}
              </span>
              <span v-if="getBlockAttributes(block).length > 4" class="block-attr-more">
                +{{ getBlockAttributes(block).length - 4 }}
              </span>
            </div>
          </div>
          <button class="remove-block-btn" @click="removeBlock(idx)">✕</button>
        </div>
      </div>
      <div v-if="!localValue.length" class="blocks-empty">
        点击上方按钮插入关联卡片
      </div>
    </div>

    <!-- 插入选择器浮层 -->
    <div v-if="showPicker" class="picker-overlay" @click.self="closePicker">
      <div class="picker-modal">
        <div class="picker-header">
          <span>插入 {{ pickerTypeLabel }}</span>
          <button class="picker-close" @click="closePicker">✕</button>
        </div>
        <div class="picker-search">
          <input v-model="pickerSearch" placeholder="搜索卡片..." />
        </div>
        <div class="picker-context">
          <input
            v-model="pickerContextLabel"
            placeholder="关系描述（如：出生地、武器、所属势力...）"
            class="context-input"
          />
        </div>
        <div class="picker-list">
          <div
            v-for="card in pickerResults"
            :key="card.id"
            class="picker-item"
            @click="insertBlock(card)"
          >
            <span class="picker-type">{{ getTypeLabel(card.type) }}</span>
            <span class="picker-title">{{ card.title }}</span>
            <span v-if="card.description" class="picker-summary">
              {{ card.description.slice(0, 30) }}
            </span>
          </div>
          <div v-if="!pickerResults.length && !pickerLoading" class="picker-empty">
            没有可用的卡片
          </div>
        </div>
        <div class="picker-footer">
          <button class="btn-outline" @click="closePicker">取消</button>
          <button class="btn-primary" @click="handleCreateAndInsert">新建并插入</button>
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
import { CardTypeMeta, type CardType } from '../card_type';

// ===== Props =====
const props = defineProps<{
  modelValue: any[];
  projectId: string;
  cardTypeOptions: { value: string; label: string }[];
  excludeCardId?: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: any[]): void;
  (e: 'createCard', type: string): void;
}>();

// ===== Store =====
const store = useWorldStore();

// ===== 本地状态 =====
const localValue = ref([...props.modelValue]);

// 选择器状态
const showPicker = ref(false);
const pickerType = ref<CardType>('character');
const pickerSearch = ref('');
const pickerContextLabel = ref('');
const pickerResults = ref<any[]>([]);
const pickerLoading = ref(false);

// ===== 计算属性 =====
const pickerTypeLabel = computed(() => {
  const found = props.cardTypeOptions.find(t => t.value === pickerType.value);
  return found?.label || pickerType.value;
});

// ===== 方法：获取卡片信息 =====
const getBlockTitle = (block: any) => {
  if (block.cardTitle) return block.cardTitle;
  const card = store.cards.find((c: any) => c.id === block.cardId);
  return card?.title || '已删除的卡片';
};

const getBlockTypeLabel = (block: any) => {
  if (block.cardType) {
    const meta = CardTypeMeta[block.cardType as CardType];
    return meta?.label || block.cardType;
  }
  const card = store.cards.find((c: any) => c.id === block.cardId);
  if (card) {
    const meta = CardTypeMeta[card.type as CardType];
    return meta?.label || card.type;
  }
  return '未知';
};

const getBlockCover = (block: any) => {
  if (block.cardCover) return block.cardCover;
  const card = store.cards.find((c: any) => c.id === block.cardId);
  return card?.coverImage || '';
};

const getBlockSummary = (block: any) => {
  return block.cardSummary || '';
};

const getBlockAttributes = (block: any) => {
  return block.cardAttributes || [];
};

const getTypeLabel = (type: string) => {
  const meta = CardTypeMeta[type as CardType];
  return meta?.label || type;
};

// ===== 选择器相关 =====
const openPicker = (type: string) => {
  const cardType = type as CardType;
  pickerType.value = cardType;
  pickerSearch.value = '';
  pickerContextLabel.value = '';
  showPicker.value = true;
  loadPickerCards(cardType);  // ✅ 传入 CardType
};

const closePicker = () => {
  showPicker.value = false;
};

const loadPickerCards = (type: CardType) => {
  pickerLoading.value = true;
  const cards = store.cards.filter(
    (c: any) => c.type === type && c.id !== props.excludeCardId
  );
  pickerResults.value = cards.slice(0, 20);
  pickerLoading.value = false;
};

const insertBlock = (card: any) => {
  if (localValue.value.some((b: any) => b.cardId === card.id)) {
    ElMessage.warning('已插入');
    return;
  }

  const contextLabel = pickerContextLabel.value.trim() || undefined;

  localValue.value.push({
    id: uuidv4(),
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
};

const removeBlock = (idx: number) => {
  localValue.value.splice(idx, 1);
};

const handleCreateAndInsert = () => {
  closePicker();
  emit('createCard', pickerType.value);
};

// ===== 搜索过滤 =====
watch(pickerSearch, (val) => {
  const cards = store.cards.filter(
    (c: any) => c.type === pickerType.value && c.id !== props.excludeCardId
  );
  if (!val) {
    pickerResults.value = cards.slice(0, 20);
    return;
  }
  const lower = val.toLowerCase();
  pickerResults.value = cards
    .filter((c: any) => c.title.toLowerCase().includes(lower))
    .slice(0, 20);
});

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
  openPicker,
  closePicker,
});
</script>

<style scoped>
/* ===== 关联内容样式 ===== */
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
  color: #334155;
}
.insert-toolbar {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
.insert-btn {
  padding: 2px 10px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: white;
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s;
  color: #475569;
}
.insert-btn:hover {
  background: #eef2ff;
  border-color: #4f46e5;
  color: #4f46e5;
}

.blocks-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 4px;
  max-height: 300px;
  overflow-y: auto;
}
.blocks-list::-webkit-scrollbar {
  width: 4px;
}
.blocks-list::-webkit-scrollbar-track {
  background: transparent;
}
.blocks-list::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 4px;
}

.block-card {
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 10px 12px;
  background: #fafbfc;
  transition: border-color 0.2s;
}
.block-card:hover {
  border-color: #cbd5e1;
}

.block-preview-wrapper {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}
.block-cover-small {
  flex-shrink: 0;
  width: 60px;
  height: 60px;
  border-radius: 6px;
  overflow: hidden;
  background: #f1f5f9;
}
.block-cover-small img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.block-info {
  flex: 1;
  min-width: 0;
}
.block-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.block-type-badge {
  font-size: 10px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 500;
  flex-shrink: 0;
}
.block-context-label {
  font-size: 12px;
  font-weight: 500;
  color: #4f46e5;
  background: #eef2ff;
  padding: 0 8px;
  border-radius: 4px;
  flex-shrink: 0;
}
.block-title {
  font-weight: 500;
  font-size: 14px;
  color: #0f172a;
}
.block-summary {
  font-size: 13px;
  color: #64748b;
  margin: 2px 0 4px 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.4;
}
.block-attr-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.block-attr-tag {
  font-size: 11px;
  color: #94a3b8;
  background: #f1f5f9;
  padding: 0 8px;
  border-radius: 4px;
}
.block-attr-more {
  font-size: 11px;
  color: #c0c4cc;
}
.remove-block-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 16px;
  padding: 4px;
  transition: color 0.2s;
}
.remove-block-btn:hover {
  color: #ef4444;
}
.blocks-empty {
  padding: 12px;
  text-align: center;
  color: #94a3b8;
  font-size: 13px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
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
  background: white;
  border-radius: 16px;
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
  border-bottom: 1px solid #f1f3f5;
  font-weight: 600;
  font-size: 15px;
  color: #0f172a;
}
.picker-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: #94a3b8;
  transition: color 0.2s;
}
.picker-close:hover {
  color: #1e293b;
}
.picker-search {
  padding: 8px 12px;
  border-bottom: 1px solid #f1f3f5;
}
.picker-search input {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  font-size: 13px;
  background: #fafbfc;
  transition: border-color 0.2s;
}
.picker-search input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.picker-context {
  padding: 8px 12px;
  border-bottom: 1px solid #f1f3f5;
}
.picker-context .context-input {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  font-size: 13px;
  background: #fafbfc;
  transition: border-color 0.2s, background 0.2s;
}
.picker-context .context-input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.picker-context .context-input::placeholder {
  color: #a0aec0;
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
  background: #d1d5db;
  border-radius: 4px;
}
.picker-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  cursor: pointer;
  transition: background 0.15s;
}
.picker-item:hover {
  background: #f1f5f9;
}
.picker-type {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 8px;
  border-radius: 10px;
  flex-shrink: 0;
}
.picker-title {
  font-weight: 500;
  font-size: 14px;
  color: #0f172a;
  flex: 1;
}
.picker-summary {
  font-size: 11px;
  color: #94a3b8;
  max-width: 80px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.picker-empty {
  padding: 20px;
  text-align: center;
  color: #94a3b8;
  font-size: 14px;
}
.picker-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px 16px;
  border-top: 1px solid #f1f3f5;
}
.picker-footer .btn-outline {
  padding: 6px 16px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: transparent;
  cursor: pointer;
  font-size: 13px;
  color: #475569;
  transition: all 0.2s;
}
.picker-footer .btn-outline:hover {
  background: #f3f4f6;
  border-color: #9ca3af;
}
.picker-footer .btn-primary {
  padding: 6px 20px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: background 0.2s;
}
.picker-footer .btn-primary:hover {
  background: #4338ca;
}
</style>