<!-- src/views/世界观管理/CardEditor.vue -->
<template>
  <div class="card-editor-inline">
    <div v-if="isCreating" class="create-banner">
      <span>✨ 新建卡片</span>
      <button class="cancel-create" @click="cancelCreate">取消</button>
    </div>

    <!-- 标题 -->
    <div class="field">
      <input v-model="form.title" placeholder="卡片名称" class="title-input" />
    </div>

    <!-- 类型选择 -->
    <div class="field">
      <div class="type-tabs">
        <button
          v-for="type in cardTypeOptions"
          :key="type.value"
          class="type-tab"
          :class="{ active: form.type === type.value }"
          @click="selectType(type)"
        >
          {{ type.label }}
        </button>
      </div>
    </div>

    <!-- 封面图 -->
    <div class="field cover-field">
      <div v-if="form.coverImage" class="cover-preview-mini">
        <img :src="form.coverImage" />
        <button class="remove-cover-mini" @click="form.coverImage = ''">×</button>
      </div>
      <button v-else class="upload-cover-btn" @click="triggerFileInput">+ 封面图</button>
      <input ref="fileInput" type="file" accept="image/*" style="display:none" @change="handleFileUpload" />
      <div v-if="uploadingCover" class="upload-progress-mini">上传中...</div>
    </div>

    <!-- ===== 属性（共用组件） ===== -->
    <div class="field">
      <AttributeList v-model="form.attributes" />
    </div>

    <!-- 描述 -->
    <div class="field">
      <textarea v-model="form.description" rows="3" placeholder="描述..." class="desc-area"></textarea>
    </div>

    <!-- ===== 🔥 类型专属编辑器（动态组件） ===== -->
    <div v-if="currentTypeEditor" class="field type-editor-wrapper">
      <component
        :is="currentTypeEditor"
        v-model="form"
      />
    </div>
    <div v-else class="field type-editor-placeholder">
      <p class="placeholder-text">📝 该类型暂无专属编辑器，使用通用字段</p>
    </div>

    <!-- 内容块（关联卡片预览） -->
    <div class="field">
      <div class="blocks-header">
        <span>关联内容</span>
        <div class="insert-toolbar">
          <button
            v-for="type in cardTypeOptions"
            :key="type.value"
            class="insert-btn"
            @click="openInsertPicker(type.value)"
          >
            +{{ type.label }}
          </button>
        </div>
      </div>
      <div class="blocks-list">
        <div v-for="(block, idx) in form.contentBlocks" :key="block.id" class="block-card">
          <div class="block-preview-wrapper">
            <div v-if="getBlockCover(block)" class="block-cover-small">
              <img :src="getBlockCover(block)" />
            </div>
            <div class="block-info">
              <div class="block-title-row">
                <span class="block-type-badge">{{ getBlockTypeLabel(block) }}</span>
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
        <div v-if="!form.contentBlocks.length" class="blocks-empty">
          点击上方按钮插入关联卡片
        </div>
      </div>
    </div>

    <!-- ===== 标签（共用组件） ===== -->
    <div class="field">
      <TagInput v-model="form.tags" />
    </div>

    <!-- ===== 关联卡片（共用组件） ===== -->
    <div class="field">
      <RelationSelector
        v-model="form.relations"
        :project-id="projectId"
      />
    </div>

    <!-- 操作按钮 -->
    <div class="editor-actions">
      <button class="btn-primary" @click="handleSave" :disabled="saving">
        {{ saving ? '保存中...' : '保存' }}
      </button>
      <button v-if="!isCreating" class="btn-danger" @click="handleDelete">删除</button>
    </div>

    <!-- 插入选择器浮层 -->
    <div v-if="showInsertPicker" class="picker-overlay" @click.self="closeInsertPicker">
      <div class="picker-modal">
        <div class="picker-header">
          <span>插入 {{ pickerTypeLabel }}</span>
          <button class="picker-close" @click="closeInsertPicker">✕</button>
        </div>
        <div class="picker-search">
          <input v-model="pickerSearch" placeholder="搜索..." />
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
          <button class="btn-outline" @click="closeInsertPicker">取消</button>
          <button class="btn-primary" @click="createAndInsert">新建并插入</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '@/stores/world';
import { useCos } from '@/composables/useCos';
import { v4 as uuidv4 } from 'uuid';
import { CardTypeMeta, type CardType } from '../card_type';

// ===== 导入共用组件 =====
import AttributeList from './AttributeList.vue';
import TagInput from './TagInput.vue';
import RelationSelector from './RelationSelector.vue';

// ===== 🔥 导入所有类型专属编辑器 =====
import CharacterEditor from '../type-editors/CharacterEditor.vue';
import LocationEditor from '../type-editors/LocationEditor.vue';
import ItemEditor from '../type-editors/ItemEditor.vue';
import EventEditor from '../type-editors/EventEditor.vue';
import FactionEditor from '../type-editors/FactionEditor.vue';
import SpeciesEditor from '../type-editors/SpeciesEditor.vue';
import EcologyEditor from '../type-editors/EcologyEditor.vue';
import LoreEditor from '../type-editors/LoreEditor.vue';
import OccupationEditor from '../type-editors/OccupationEditor.vue';
import NationEditor from '../type-editors/NationEditor.vue';
import ContinentEditor from '../type-editors/ContinentEditor.vue';
import OrganizationEditor from '../type-editors/OrganizationEditor.vue';
import CreatureEditor from '../type-editors/CreatureEditor.vue';
import BuildingEditor from '../type-editors/BuildingEditor.vue';
import WeaponEditor from '../type-editors/WeaponEditor.vue';
import DeityEditor from '../type-editors/DeityEditor.vue';
import SkillEditor from '../type-editors/SkillEditor.vue';
// ✅ 新增：气候编辑器
import ClimateEditor from '../type-editors/ClimateEditor.vue';

// ===== Props =====
const props = defineProps<{
  projectId: string;
  cardData?: any | null;
  inline?: boolean;
}>();

const emit = defineEmits<{
  (e: 'saved'): void;
  (e: 'deleted'): void;
}>();

// ===== Store & Composables =====
const store = useWorldStore();
const { uploadFile } = useCos();

// ===== 状态 =====
const saving = ref(false);
const tagInput = ref('');
const fileInput = ref<HTMLInputElement | null>(null);
const uploadingCover = ref(false);

const isCreating = ref(!props.cardData);
const showInsertPicker = ref(false);
const pickerType = ref<CardType>('character');
const pickerSearch = ref('');
const pickerResults = ref<any[]>([]);
const pickerLoading = ref(false);
const newRelation = ref({ targetId: '', relationType: '' });
const searchResults = ref<any[]>([]);

// ===== 卡片类型选项 =====
const cardTypeOptions = computed(() => {
  if (store.cardTypes && store.cardTypes.length) {
    return store.cardTypes.map((t: any) => ({ value: t.id || t.value, label: t.label }));
  }
  return Object.entries(CardTypeMeta).map(([value, meta]) => ({
    value,
    label: meta.label,
  }));
});

// ===== 🔥 编辑器映射 =====
const editorMap: Record<CardType, any> = {
  character: CharacterEditor,
  location: LocationEditor,
  item: ItemEditor,
  event: EventEditor,
  faction: FactionEditor,
  species: SpeciesEditor,
  ecology: EcologyEditor,
  lore: LoreEditor,
  occupation: OccupationEditor,
  nation: NationEditor,
  continent: ContinentEditor,
  organization: OrganizationEditor,
  creature: CreatureEditor,
  building: BuildingEditor,
  weapon: WeaponEditor,
  deity: DeityEditor,
  skill: SkillEditor,
  // ✅ 新增：气候
  climate: ClimateEditor,
};

// ===== 🔥 当前使用的编辑器 =====
const currentTypeEditor = computed(() => {
  return editorMap[form.value.type as CardType] || null;
});

// ===== 表单数据 =====
const form = ref({
  title: '',
  type: 'character' as CardType,
  coverImage: '',
  attributes: [] as { key: string; value: string }[],
  description: '',
  content: '{}',
  tags: [] as string[],
  relations: [] as { targetCardId: string; relationType: string }[],
  contentBlocks: [] as {
    id: string;
    cardId: string;
    cardType: string;
    order: number;
    cardTitle?: string;
    cardCover?: string;
    cardSummary?: string;
    cardAttributes?: { key: string; value: string }[];
  }[],
});

// ===== 计算属性 =====
const pickerTypeLabel = computed(() => {
  const found = cardTypeOptions.value.find(t => t.value === pickerType.value);
  return found?.label || pickerType.value;
});

// ===== 关联卡片信息获取方法 =====
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
  if (block.cardSummary) return block.cardSummary;
  const card = store.cards.find((c: any) => c.id === block.cardId);
  if (!card) return '';
  if (card.description) return card.description;
  try {
    const data = JSON.parse(card.content || '{}');
    return data.description || data.summary || '';
  } catch {
    return '';
  }
};

const getBlockAttributes = (block: any) => {
  if (block.cardAttributes) return block.cardAttributes;
  const card = store.cards.find((c: any) => c.id === block.cardId);
  return card?.attributes || [];
};

// ===== 方法 =====
const getTypeLabel = (type: string) => {
  const meta = CardTypeMeta[type as CardType];
  return meta?.label || type;
};

const selectType = (type: any) => {
  form.value.type = type.value;
};

const searchCards = (query: string) => {
  const cards = store.cards.filter((c: any) => c.id !== props.cardData?.id);
  if (!query) {
    searchResults.value = cards.slice(0, 10);
    return;
  }
  const lower = query.toLowerCase();
  searchResults.value = cards.filter((c: any) => c.title.toLowerCase().includes(lower)).slice(0, 10);
};

const openInsertPicker = (type: CardType) => {
  pickerType.value = type;
  pickerSearch.value = '';
  showInsertPicker.value = true;
  loadPickerCards(type);
};

const closeInsertPicker = () => {
  showInsertPicker.value = false;
};

const loadPickerCards = (type: CardType) => {
  pickerLoading.value = true;
  const cards = store.cards.filter(
    (c: any) => c.type === type && c.id !== props.cardData?.id
  );
  pickerResults.value = cards.slice(0, 20);
  pickerLoading.value = false;
};

const insertBlock = (card: any) => {
  if (form.value.contentBlocks.some((b: any) => b.cardId === card.id)) {
    ElMessage.warning('已插入');
    return;
  }

  let summary = card.description || '';
  if (!summary) {
    try {
      const data = JSON.parse(card.content || '{}');
      summary = data.description || data.summary || '';
    } catch {}
  }

  form.value.contentBlocks.push({
    id: uuidv4(),
    cardId: card.id,
    cardType: card.type,
    order: form.value.contentBlocks.length,
    cardTitle: card.title,
    cardCover: card.coverImage || '',
    cardSummary: summary,
    cardAttributes: card.attributes || [],
  });
  closeInsertPicker();
  ElMessage.success(`已插入：${card.title}`);
};

const removeBlock = (idx: number) => {
  form.value.contentBlocks.splice(idx, 1);
};

const createAndInsert = () => {
  closeInsertPicker();
  window.dispatchEvent(
    new CustomEvent('open-create-card', { detail: { type: pickerType.value } })
  );
};

const triggerFileInput = () => fileInput.value?.click();

const handleFileUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  if (!file.type.startsWith('image/')) {
    ElMessage.warning('请上传图片');
    return;
  }
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('最大5MB');
    return;
  }
  uploadingCover.value = true;
  try {
    const result = await uploadFile(file, 'world/covers');
    form.value.coverImage = result.url;
    ElMessage.success('上传成功');
  } catch (error) {
    ElMessage.error('上传失败');
  } finally {
    uploadingCover.value = false;
    input.value = '';
  }
};

const resetForm = () => {
  form.value = {
    title: '',
    type: 'character',
    coverImage: '',
    attributes: [],
    description: '',
    content: '{}',
    tags: [],
    relations: [],
    contentBlocks: [],
  };
  tagInput.value = '';
  newRelation.value = { targetId: '', relationType: '' };
  searchResults.value = [];
};

const loadCardData = () => {
  if (props.cardData) {
    const data = props.cardData;
    form.value = {
      title: data.title || '',
      type: data.type || 'character',
      coverImage: data.coverImage || '',
      attributes: data.attributes || [],
      description: data.description || '',
      content: data.content || '{}',
      tags: Array.isArray(data.tags)
        ? data.tags
        : (() => {
            try {
              return JSON.parse(data.tags || '[]');
            } catch {
              return [];
            }
          })(),
      relations: (data.relations || []).map((r: any) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      })),
      contentBlocks: data.contentBlocks || [],
    };
    isCreating.value = false;
  } else {
    resetForm();
    isCreating.value = true;
  }
  searchCards('');
};

const handleSave = async () => {
  if (!form.value.title.trim()) {
    ElMessage.warning('请输入标题');
    return;
  }
  const payload = {
    title: form.value.title.trim(),
    type: form.value.type,
    coverImage: form.value.coverImage,
    attributes: form.value.attributes,
    description: form.value.description.trim(),
    content: form.value.content || '{}',
    tags: form.value.tags,
    relations: form.value.relations,
    contentBlocks: form.value.contentBlocks,
  };
  saving.value = true;
  try {
    if (isCreating.value) {
      await store.createCard(props.projectId, payload);
      ElMessage.success('已创建');
    } else {
      await store.updateCard(props.cardData.id, payload);
      ElMessage.success('已更新');
    }
    emit('saved');
  } catch (error) {
    console.error('保存失败:', error);
    ElMessage.error('保存失败');
  } finally {
    saving.value = false;
  }
};

const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('确定删除吗？', '提示', { type: 'warning' });
    await store.deleteCard(props.cardData.id);
    ElMessage.success('已删除');
    emit('deleted');
  } catch (error) {
    if (error !== 'cancel') console.error(error);
  }
};

const cancelCreate = () => {
  if (isCreating.value) {
    emit('deleted');
  }
};

// ===== 监听 =====
watch(() => props.cardData, loadCardData, { immediate: true });

// ===== 生命周期 =====
onMounted(() => {
  window.addEventListener(
    'open-create-card',
    ((e: CustomEvent) => {
      const type = e.detail?.type || 'character';
      resetForm();
      form.value.type = type;
      isCreating.value = true;
    }) as EventListener
  );
});
</script>

<style scoped>
/* 样式保持不变，与之前相同 */
.card-editor-inline {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.create-banner {
  display: flex;
  justify-content: space-between;
  background: #eef2ff;
  padding: 6px 12px;
  border-radius: 8px;
  font-weight: 500;
  color: #4f46e5;
}
.cancel-create {
  background: none;
  border: none;
  color: #64748b;
  cursor: pointer;
}

.title-input {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 8px 12px;
  font-size: 16px;
  font-weight: 500;
  background: #fafbfc;
}
.title-input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}

.type-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.type-tab {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  background: white;
  font-size: 12px;
  cursor: pointer;
  transition: 0.2s;
}
.type-tab:hover {
  background: #f1f5f9;
}
.type-tab.active {
  border-color: #4f46e5;
  background: #eef2ff;
  color: #4f46e5;
}

.cover-field {
  display: flex;
  align-items: center;
  gap: 8px;
}
.cover-preview-mini {
  position: relative;
  width: 80px;
  height: 60px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid #e2e8f0;
}
.cover-preview-mini img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.remove-cover-mini {
  position: absolute;
  top: 2px;
  right: 2px;
  background: rgba(0, 0, 0, 0.5);
  color: white;
  border: none;
  border-radius: 50%;
  width: 20px;
  height: 20px;
  cursor: pointer;
  font-size: 14px;
}
.upload-cover-btn {
  padding: 4px 16px;
  border: 1px dashed #d1d5db;
  border-radius: 8px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
}
.upload-progress-mini {
  font-size: 12px;
  color: #94a3b8;
}

.desc-area {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 8px 12px;
  font-family: inherit;
  resize: vertical;
  background: #fafbfc;
}
.desc-area:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}

.type-editor-wrapper {
  margin: 8px 0;
  padding: 0;
}

.type-editor-placeholder {
  padding: 20px;
  background: #f8fafc;
  border-radius: 8px;
  border: 1px dashed #d1d5db;
  text-align: center;
  margin: 8px 0;
}
.placeholder-text {
  color: #94a3b8;
  font-size: 14px;
  margin: 0;
}

.blocks-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
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
}
.insert-btn:hover {
  background: #eef2ff;
  border-color: #4f46e5;
}

.blocks-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 4px;
  max-height: 300px;
  overflow-y: auto;
}

.block-card {
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 10px 12px;
  background: #fafbfc;
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

.editor-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
  padding-top: 12px;
  border-top: 1px solid #f1f3f5;
}
.btn-primary {
  padding: 6px 20px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
}
.btn-primary:hover:not(:disabled) {
  background: #4338ca;
}
.btn-primary:disabled {
  opacity: 0.6;
}
.btn-danger {
  padding: 6px 16px;
  background: #fef2f2;
  color: #ef4444;
  border: 1px solid #fecaca;
  border-radius: 8px;
  cursor: pointer;
}
.btn-danger:hover {
  background: #fee2e2;
}

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
  padding: 12px 16px;
  border-bottom: 1px solid #f1f3f5;
  font-weight: 600;
}
.picker-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
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
}
.picker-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}
.picker-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  cursor: pointer;
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
}
.picker-title {
  font-weight: 500;
  flex: 1;
}
.picker-summary {
  font-size: 11px;
  color: #94a3b8;
}
.picker-empty {
  padding: 20px;
  text-align: center;
  color: #94a3b8;
}
.picker-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px 16px;
  border-top: 1px solid #f1f3f5;
}
.btn-outline {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: transparent;
  cursor: pointer;
}
.btn-outline:hover {
  background: #f3f4f6;
}
</style>