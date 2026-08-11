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

    <!-- 配额提示 -->
    <div class="field">
      <div class="quota-hint" :class="quotaStatus">
        <span>📄 当前世界词汇量：{{ quotaInfo.currentCount }} / {{ quotaInfo.maxCount }}</span>
        <span v-if="quotaInfo.remaining > 0" class="quota-remaining">
          剩余 {{ quotaInfo.remaining }} 张
        </span>
        <span v-else class="quota-full">⚠️ 已满，请扩容</span>
      </div>
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

    <!-- 关联内容（已抽离为独立组件） -->
    <div class="field">
      <ContentBlockManager
        v-model="form.contentBlocks"
        :project-id="routeProjectId"
        :card-type-options="cardTypeOptions"
        :exclude-card-id="routeCardId"
        @create-card="handleCreateCardFromBlock"
      />
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
import type { CardDetail } from '@/api/worldApi';
import { worldApi } from '@/api/worldApi';

// ===== 导入共用组件 =====
import AttributeList from './AttributeList.vue';
import TagInput from './TagInput.vue';
import RelationSelector from './RelationSelector.vue';
import ContentBlockManager from './ContentBlockManager.vue';

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
//  加载卡片数据（编辑模式）
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

    await store.fetchCardDetail(projectId, cardId);
    const fullCard = store.currentCard as CardDetail | null;

    if (!fullCard) {
      throw new Error('卡片数据为空');
    }

    const rawAttributes = fullCard.attributes || [];
    const attributes: AttributeItem[] = rawAttributes.map((attr: any) => ({
      key: attr.key,
      value: attr.value,
      type: attr.type || 'short'
    }));

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
//  卡片配额
// ============================================================
const quotaInfo = ref({
  currentCount: 0,
  maxCount: 0,
  remaining: 0,
  canAdd: true,
});
const quotaLoading = ref(false);

const quotaStatus = computed(() => {
  if (quotaInfo.value.remaining <= 0) return 'quota-full';
  if (quotaInfo.value.remaining <= 10) return 'quota-warning';
  return 'quota-normal';
});

const loadQuota = async () => {
  if (!routeProjectId.value) return;
  quotaLoading.value = true;
  try {
    const { data } = await worldApi.canAddCard(routeProjectId.value);
    quotaInfo.value = {
      currentCount: data.currentCount,
      maxCount: data.maxCount,
      remaining: data.maxCount - data.currentCount,
      canAdd: data.canAdd,
    };
  } catch (error) {
    console.error('获取卡片配额失败:', error);
  } finally {
    quotaLoading.value = false;
  }
};

// ============================================================
//  保存和删除
// ============================================================
const handleSave = async () => {
  if (!form.value.title.trim()) {
    ElMessage.warning('请输入标题');
    return;
  }

  // 1. 检查配额（仅创建时）
  if (isCreating.value) {
    try {
      const { data } = await worldApi.canAddCard(routeProjectId.value);
      if (!data.canAdd) {
        ElMessage.warning({
          message: data.message || '当前世界卡片数量已达上限，请扩容',
          duration: 5000,
          showClose: true,
        });
        return;
      }
    } catch (error) {
      console.error('检查卡片配额失败:', error);
    }
  }

  // ============================================================
  //  2. 准备基本信息 payload（不包含 relations）
  // ============================================================
  const cardPayload = {
    title: form.value.title.trim(),
    type: form.value.type,
    coverImage: form.value.coverImage,
    galleryImages: form.value.galleryImages,
    attributes: form.value.attributes,
    description: form.value.description.trim(),
    content: form.value.content || '{}',
    tags: form.value.tags,
    // 注意：不包含 relations，也不包含 contentBlocks（若有需求可类似处理）
  };

  // ============================================================
  //  3. 处理关系变更（仅编辑模式）
  // ============================================================
  let toRemove: any[] = [];
  let toAdd: { targetCardId: string; relationType: string }[] = [];

  if (!isCreating.value) {
    const cardId = routeCardId.value || props.cardData?.id;
    if (!cardId) throw new Error('缺少卡片 ID');

    // 获取当前卡片已有关系（从缓存或加载）
    let existingRelations = store.getCardDetailById(cardId)?.outRelations || [];
    if (existingRelations.length === 0) {
      // 如果缓存未加载，强制获取
      await store.fetchCardDetail(routeProjectId.value, cardId);
      existingRelations = store.getCardDetailById(cardId)?.outRelations || [];
    }

    const newRelations = form.value.relations || [];

    // 计算需要删除的（在旧列表但不在新列表，按 targetCardId 匹配）
    toRemove = existingRelations.filter(old =>
      !newRelations.some(n => n.targetCardId === old.targetCardId)
    );
    // 计算需要新增的
    toAdd = newRelations.filter(n =>
      !existingRelations.some(old => old.targetCardId === n.targetCardId)
    );
  }

  // ============================================================
  //  4. 执行保存
  // ============================================================
  saving.value = true;
  try {
    let cardId = routeCardId.value || props.cardData?.id;

    if (isCreating.value) {
      // 4a. 创建卡片（不包含关系）
      const newCard = await store.createCard(routeProjectId.value, cardPayload);
      cardId = newCard.id;
      // 创建后，添加所有新关系
      for (const rel of form.value.relations || []) {
        await store.addRelation(cardId, rel.targetCardId, rel.relationType);
      }
      ElMessage.success('已创建');
    } else {
      // 4b. 更新卡片基本信息（不包含关系）
      await store.updateCard(cardId, cardPayload);

      // 4c. 执行关系变更
      for (const rel of toRemove) {
        await store.removeRelation(cardId, rel.id);
      }
      for (const rel of toAdd) {
        await store.addRelation(cardId, rel.targetCardId, rel.relationType);
      }

      // 4d. 强制刷新卡片详情（确保前端显示最新关系）
      await store.fetchCardDetail(routeProjectId.value, cardId, true);
      ElMessage.success('已更新');
    }

    emit('saved');
  } catch (error: any) {
    console.error('保存失败:', error);
    if (error?.response?.data?.code === 'CARD_LIMIT_EXCEEDED') {
      ElMessage.warning({
        message: error.response.data.message || '卡片数量已达上限，请扩容',
        duration: 5000,
        showClose: true,
      });
    } else {
      ElMessage.error('保存失败');
    }
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

// ===== 处理从 ContentBlockManager 发出的"新建并插入"事件 =====
const handleCreateCardFromBlock = (type: string) => {
  window.dispatchEvent(
    new CustomEvent('open-create-card', { detail: { type, fromBlock: true } })
  );
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

// 监听 projectId 变化重新加载配额
watch(routeProjectId, () => {
  if (routeProjectId.value) {
    loadQuota();
  }
});

// ============================================================
//  🆕 监听 store.currentCard 变化，同步更新 form.relations
// ============================================================
watch(
  () => store.currentCard,
  (newCard) => {
    console.log('🔄 CardEditor 收到 currentCard 变化:', newCard?.title, newCard?.outRelations?.length);
    if (newCard && isEditMode.value) {
      const newRelations = (newCard.outRelations || []).map((r: any) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      }));
      form.value.relations = newRelations;
      console.log('✅ form.relations 已更新:', form.value.relations);
    }
  },
  { deep: true, immediate: true }
);

// ============================================================
//  生命周期
// ============================================================
onMounted(() => {
  loadCardData();
  loadQuota();

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
/* ============================================================
   CardEditor 完整样式
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

/* ===== 配额提示 ===== */
.quota-hint {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 14px;
  border-radius: 8px;
  font-size: 13px;
  transition: all 0.2s;
}
.quota-normal {
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #16a34a;
}
.quota-warning {
  background: #fefce8;
  border: 1px solid #fde68a;
  color: #d97706;
}
.quota-full {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #dc2626;
}
.quota-remaining {
  font-weight: 500;
}
.quota-full .quota-full {
  font-weight: 600;
}

/* ===== 类型选择 ===== */
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

/* ===== 封面图 ===== */
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
  background: rgba(0, 0, 0, 0.5);
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

/* ===== 图库 ===== */
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
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
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
  background: rgba(0, 0, 0, 0.55);
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
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.12);
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

/* ===== 描述 ===== */
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

/* ===== 操作按钮 ===== */
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

/* ===== 响应式 ===== */
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
  .editor-actions {
    flex-direction: column;
  }
  .editor-actions .btn-primary,
  .editor-actions .btn-danger {
    width: 100%;
    justify-content: center;
    text-align: center;
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
}
</style>