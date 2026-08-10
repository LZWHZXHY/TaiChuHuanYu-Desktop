<!-- src/views/世界观管理/CardEditor.vue -->
<template>
  <div class="card-editor-inline" v-loading="loading">
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

    <!-- 图库 -->
    <div class="field gallery-field">
      <label class="field-label">图库</label>
      <div class="gallery-upload">
        <div v-if="form.galleryImages && form.galleryImages.length" class="gallery-grid">
          <div v-for="(img, idx) in form.galleryImages" :key="idx" class="gallery-item">
            <img :src="img" :alt="`图 ${idx + 1}`" />
            <button class="remove-gallery-btn" @click="removeGalleryImage(idx)">✕</button>
          </div>
        </div>
        <button class="upload-gallery-btn" @click="triggerGalleryUpload">+ 添加图片</button>
        <input ref="galleryInput" type="file" accept="image/*" multiple style="display:none" @change="handleGalleryUpload" />
        <span v-if="uploadingGallery" class="upload-progress">上传中...</span>
      </div>
      <p class="hint">支持多张图片，展示卡片的多个视角</p>
    </div>

    <!-- 属性 -->
    <div class="field">
      <AttributeList v-model="form.attributes" />
    </div>

    <!-- 描述 -->
    <div class="field">
      <textarea v-model="form.description" rows="3" placeholder="描述..." class="desc-area"></textarea>
    </div>

    <!-- 类型专属编辑器 -->
    <div v-if="currentTypeEditor" class="field type-editor-wrapper">
      <component
        :is="currentTypeEditor"
        v-model="form"
      />
    </div>
    <div v-else class="field type-editor-placeholder">
      <p class="placeholder-text">📝 该类型暂无专属编辑器，使用通用字段</p>
    </div>

    <!-- 关联内容 -->
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
        <div v-if="!form.contentBlocks.length" class="blocks-empty">
          点击上方按钮插入关联卡片
        </div>
      </div>
    </div>

    <!-- 标签 -->
    <div class="field">
      <TagInput v-model="form.tags" />
    </div>

    <!-- 关联卡片 -->
    <div class="field">
      <RelationSelector
        v-model="form.relations"
        :project-id="projectId"
      />
    </div>

    <!-- 操作按钮 -->
    <div class="editor-actions">
      <button class="btn-primary" @click="handleSave" :disabled="saving || loading">
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
          <button class="btn-outline" @click="closeInsertPicker">取消</button>
          <button class="btn-primary" @click="createAndInsert">新建并插入</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '@/stores/world';
import { useCos } from '@/composables/useCos';
import { v4 as uuidv4 } from 'uuid';
import { CardTypeMeta, type CardType } from '../card_type';
import type { AttributeItem } from '../card_type';
// ===== 导入 CardDetail 类型以进行断言 =====
import type { CardDetail } from '@/api/worldApi';

// ===== 导入共用组件 =====
import AttributeList from './AttributeList.vue';
import TagInput from './TagInput.vue';
import RelationSelector from './RelationSelector.vue';

// ===== 导入所有类型专属编辑器 =====
import CharacterEditor from '../type-editors/CharacterEditor.vue';
import LocationEditor from '../type-editors/LocationEditor.vue';
import ItemEditor from '../type-editors/ItemEditor.vue';
import EventEditor from '../type-editors/EventEditor.vue';
import FactionEditor from '../type-editors/FactionEditor.vue';
import SpeciesEditor from '../type-editors/SpeciesEditor.vue';
import OccupationEditor from '../type-editors/OccupationEditor.vue';
import OrganizationEditor from '../type-editors/OrganizationEditor.vue';
import CreatureEditor from '../type-editors/CreatureEditor.vue';
import SkillEditor from '../type-editors/SkillEditor.vue';
import ClimateEditor from '../type-editors/ClimateEditor.vue';
import ConceptEditor from '../type-editors/ConceptEditor.vue';

// ===== Router & Route =====
const route = useRoute();
const router = useRouter();

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

// ===== Store =====
const store = useWorldStore();
const { uploadFile } = useCos();

// ============================================================
//  从路由获取参数
// ============================================================
const routeProjectId = computed(() => (route.params.projectId as string) || props.projectId);
const routeCardId = computed(() => route.params.cardId as string | undefined);
const isEditMode = computed(() => !!routeCardId.value || !!props.cardData?.id);

// ============================================================
//  状态
// ============================================================
const saving = ref(false);
const loading = ref(false);
const tagInput = ref('');
const fileInput = ref<HTMLInputElement | null>(null);
const galleryInput = ref<HTMLInputElement | null>(null);
const uploadingCover = ref(false);
const uploadingGallery = ref(false);

const isCreating = ref(!isEditMode.value);
const showInsertPicker = ref(false);
const pickerType = ref<CardType>('character');
const pickerSearch = ref('');
const pickerResults = ref<any[]>([]);
const pickerLoading = ref(false);
const pickerContextLabel = ref('');
const newRelation = ref({ targetId: '', relationType: '' });
const searchResults = ref<any[]>([]);

// ============================================================
//  卡片类型选项
// ============================================================
const cardTypeOptions = computed(() => {
  if (store.cardTypes && store.cardTypes.length) {
    return store.cardTypes.map((t: any) => ({ value: t.id || t.value, label: t.label }));
  }
  return Object.entries(CardTypeMeta).map(([value, meta]) => ({
    value,
    label: meta.label,
  }));
});

// ============================================================
//  编辑器映射
// ============================================================
const editorMap: Record<CardType, any> = {
  character: CharacterEditor,
  location: LocationEditor,
  item: ItemEditor,
  event: EventEditor,
  faction: FactionEditor,
  species: SpeciesEditor,
  occupation: OccupationEditor,
  organization: OrganizationEditor,
  creature: CreatureEditor,
  skill: SkillEditor,
  climate: ClimateEditor,
  concept: ConceptEditor,
};

const currentTypeEditor = computed(() => {
  return editorMap[form.value.type as CardType] || null;
});

// ============================================================
//  表单数据
// ============================================================
const form = ref({
  title: '',
  type: 'character' as CardType,
  coverImage: '',
  galleryImages: [] as string[],
  attributes: [] as AttributeItem[],
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
    contextLabel?: string;
  }[],
});

// ============================================================
//  计算属性
// ============================================================
const pickerTypeLabel = computed(() => {
  const found = cardTypeOptions.value.find(t => t.value === pickerType.value);
  return found?.label || pickerType.value;
});

// ============================================================
//  关联卡片信息获取方法
// ============================================================
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

// ============================================================
//  方法
// ============================================================
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
  pickerContextLabel.value = '';
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

// ===== 修改后 =====
const insertBlock = (card: any) => {
  if (form.value.contentBlocks.some((b: any) => b.cardId === card.id)) {
    ElMessage.warning('已插入');
    return;
  }

  const contextLabel = pickerContextLabel.value.trim() || undefined;

  form.value.contentBlocks.push({
    id: uuidv4(),
    cardId: card.id,
    cardType: card.type,
    order: form.value.contentBlocks.length,
    cardTitle: card.title,
    cardCover: card.coverImage || '',
    cardSummary: '',        // 不再尝试获取
    cardAttributes: [],     // 不再尝试获取
    contextLabel,
  });

  pickerContextLabel.value = '';
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

const triggerGalleryUpload = () => {
  galleryInput.value?.click();
};

const handleGalleryUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const files = input.files;
  if (!files || !files.length) return;

  uploadingGallery.value = true;
  try {
    const uploadPromises = Array.from(files).map(file =>
      uploadFile(file, 'world/gallery')
    );
    const results = await Promise.all(uploadPromises);
    const urls = results.map(r => r.url);
    form.value.galleryImages.push(...urls);
    ElMessage.success(`成功上传 ${urls.length} 张图片`);
  } catch (error) {
    ElMessage.error('部分图片上传失败');
  } finally {
    uploadingGallery.value = false;
    input.value = '';
  }
};

const removeGalleryImage = (idx: number) => {
  form.value.galleryImages.splice(idx, 1);
};

// ============================================================
//  核心：加载卡片数据（编辑模式）
// ============================================================
const loadCardData = async () => {
  if (!isEditMode.value) {
    resetForm();
    isCreating.value = true;
    return;
  }

  const cardId = routeCardId.value || props.cardData?.id;
  if (!cardId) {
    console.warn('没有可用的 cardId，进入创建模式');
    resetForm();
    isCreating.value = true;
    return;
  }

  loading.value = true;
  try {
    const projectId = routeProjectId.value;
    if (!projectId) {
      throw new Error('缺少 projectId');
    }

    // 调用 Store 获取完整数据
    await store.fetchCardDetail(projectId, cardId);
    // 显式断言为 CardDetail，确保 TypeScript 知道完整字段存在
    const fullCard = store.currentCard as CardDetail | null;

    if (!fullCard) {
      throw new Error('卡片数据为空');
    }

    // 现在可以安全访问 description, content, attributes 等
    const rawAttributes = fullCard.attributes || [];
    const attributes: AttributeItem[] = rawAttributes.map((attr: any) => ({
      key: attr.key,
      value: attr.value,
      type: attr.type || 'short'
    }));

    // 赋值表单，对 type 进行断言
    form.value = {
      title: fullCard.title || '',
      type: fullCard.type as CardType,
      coverImage: fullCard.coverImage || '',
      galleryImages: fullCard.galleryImages || [],
      attributes,
      description: fullCard.description || '',
      content: fullCard.content || '{}',
      tags: Array.isArray(fullCard.tags) ? fullCard.tags : [],
      relations: (fullCard.outRelations || []).map((r: any) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      })),
      contentBlocks: fullCard.contentBlocks || [],
    };

    isCreating.value = false;
  } catch (error) {
    console.error('加载卡片数据失败:', error);
    ElMessage.error('加载卡片数据失败');
    resetForm();
    isCreating.value = true;
  } finally {
    loading.value = false;
  }
};

const resetForm = () => {
  form.value = {
    title: '',
    type: 'character',
    coverImage: '',
    galleryImages: [],
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

// ============================================================
//  保存和删除
// ============================================================
const handleSave = async () => {
  if (!form.value.title.trim()) {
    ElMessage.warning('请输入标题');
    return;
  }

  const payload = {
    title: form.value.title.trim(),
    type: form.value.type,
    coverImage: form.value.coverImage,
    galleryImages: form.value.galleryImages,
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
      await store.createCard(routeProjectId.value, payload);
      ElMessage.success('已创建');
    } else {
      const cardId = routeCardId.value || props.cardData?.id;
      if (!cardId) {
        throw new Error('缺少卡片 ID');
      }
      await store.updateCard(cardId, payload);
      ElMessage.success('已更新');
    }
    emit('saved');
    // 可选：跳转回列表页
    // router.push(`/world/projects/${routeProjectId.value}`);
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
    const cardId = routeCardId.value || props.cardData?.id;
    if (!cardId) {
      throw new Error('缺少卡片 ID');
    }
    await store.deleteCard(cardId);
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

// ============================================================
//  监听路由和 props 变化
// ============================================================
watch(
  () => route.params.cardId,
  () => {
    if (route.params.cardId) {
      loadCardData();
    }
  }
);

watch(
  () => props.cardData,
  (newVal) => {
    if (newVal && !routeCardId.value) {
      loadCardData();
    }
  }
);

// ============================================================
//  生命周期
// ============================================================
onMounted(() => {
  loadCardData();

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

<!-- 样式部分保持不变，请直接复制之前提供的样式代码 -->


<style scoped>
/* ============================================================
   CardEditor 完整样式（保持不变）
   ============================================================ */
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
  font-size: 13px;
  transition: color 0.2s;
}
.cancel-create:hover {
  color: #1e293b;
}

.title-input {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 8px 12px;
  font-size: 16px;
  font-weight: 500;
  background: #fafbfc;
  transition: border-color 0.2s, background 0.2s;
}
.title-input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.title-input::placeholder {
  color: #a0aec0;
  font-weight: 400;
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
  transition: all 0.2s;
  color: #475569;
}
.type-tab:hover {
  background: #f1f5f9;
  border-color: #cbd5e1;
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
  flex-wrap: wrap;
}
.cover-preview-mini {
  position: relative;
  width: 80px;
  height: 60px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid #e2e8f0;
  flex-shrink: 0;
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
  background: rgba(0,0,0,0.5);
  color: white;
  border: none;
  border-radius: 50%;
  width: 20px;
  height: 20px;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}
.remove-cover-mini:hover {
  background: #ef4444;
}
.upload-cover-btn {
  padding: 4px 16px;
  border: 1px dashed #d1d5db;
  border-radius: 8px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
}
.upload-cover-btn:hover {
  border-color: #4f46e5;
  color: #4f46e5;
  background: #f0f4ff;
}
.upload-progress-mini {
  font-size: 12px;
  color: #94a3b8;
  animation: pulse 1.2s ease-in-out infinite;
}
@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.gallery-field {
  margin-top: 4px;
}
.gallery-field .field-label {
  display: block;
  font-weight: 500;
  font-size: 14px;
  color: #334155;
  margin-bottom: 6px;
}
.gallery-upload {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
}
.gallery-item {
  position: relative;
  aspect-ratio: 1;
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid #eef2f6;
  background: #f8fafc;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}
.gallery-item:hover {
  border-color: #cbd5e1;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
}
.gallery-item img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.3s ease;
  display: block;
}
.gallery-item:hover img {
  transform: scale(1.04);
}
.remove-gallery-btn {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 26px;
  height: 26px;
  border: none;
  border-radius: 50%;
  background: rgba(0,0,0,0.55);
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  font-weight: 300;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  opacity: 0;
  line-height: 1;
  padding: 0;
}
.gallery-item:hover .remove-gallery-btn {
  opacity: 1;
}
.remove-gallery-btn:hover {
  background: #ef4444;
  transform: scale(1.12);
}
.upload-gallery-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 12px 24px;
  border: 2px dashed #d1d5db;
  border-radius: 10px;
  background: #fafbfc;
  color: #64748b;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.25s ease;
  min-height: 56px;
  width: fit-content;
  min-width: 140px;
  user-select: none;
}
.upload-gallery-btn:hover {
  border-color: #4f46e5;
  color: #4f46e5;
  background: #f0f4ff;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(79,70,229,0.12);
}
.upload-progress {
  font-size: 13px;
  color: #94a3b8;
  animation: gallery-pulse 1.2s ease-in-out infinite;
}
@keyframes gallery-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}
.gallery-field .hint {
  font-size: 12px;
  color: #94a3b8;
  margin: 4px 0 0 2px;
  font-style: italic;
}

.desc-area {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 8px 12px;
  font-family: inherit;
  font-size: 14px;
  resize: vertical;
  background: #fafbfc;
  transition: border-color 0.2s, background 0.2s;
  min-height: 60px;
}
.desc-area:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.desc-area::placeholder {
  color: #a0aec0;
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
.blocks-list::-webkit-scrollbar-thumb:hover {
  background: #9ca3af;
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
  font-size: 14px;
  transition: background 0.2s;
}
.btn-primary:hover:not(:disabled) {
  background: #4338ca;
}
.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.btn-danger {
  padding: 6px 16px;
  background: #fef2f2;
  color: #ef4444;
  border: 1px solid #fecaca;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}
.btn-danger:hover {
  background: #fee2e2;
  border-color: #fca5a5;
}

.picker-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15,23,42,0.3);
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
  box-shadow: 0 16px 48px rgba(0,0,0,0.12);
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

@media (max-width: 640px) {
  .gallery-grid {
    grid-template-columns: repeat(auto-fill, minmax(90px, 1fr));
    gap: 8px;
  }
  .upload-gallery-btn {
    width: 100%;
    min-height: 48px;
    padding: 10px 16px;
    font-size: 13px;
    justify-content: center;
  }
  .remove-gallery-btn {
    opacity: 1;
    width: 22px;
    height: 22px;
    font-size: 12px;
    top: 4px;
    right: 4px;
  }
  .gallery-item {
    border-radius: 8px;
  }
  .gallery-field .hint {
    font-size: 11px;
  }
  .picker-modal {
    max-width: 96%;
    max-height: 80vh;
  }
  .editor-actions {
    flex-direction: column;
  }
  .editor-actions .btn-primary,
  .editor-actions .btn-danger {
    width: 100%;
    justify-content: center;
    text-align: center;
  }
  .block-preview-wrapper {
    flex-wrap: wrap;
  }
  .block-cover-small {
    width: 50px;
    height: 50px;
  }
}
@media (max-width: 400px) {
  .gallery-grid {
    grid-template-columns: repeat(auto-fill, minmax(72px, 1fr));
    gap: 6px;
  }
  .type-tabs {
    gap: 3px;
  }
  .type-tab {
    font-size: 11px;
    padding: 3px 10px;
  }
  .block-title {
    font-size: 13px;
  }
}
</style>