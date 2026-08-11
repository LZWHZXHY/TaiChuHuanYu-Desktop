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
import { ref, watch, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta, type CardType } from '../card_type'
import type { CardDetail } from '@/api/worldApi'

// ===== Composables =====
import { useCardForm } from '@/composables/世界观管理/useCardForm'
import { useCardQuota } from '@/composables/世界观管理/useCardQuota'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import { useCardTypeOptions } from '@/composables/世界观管理/useCardTypeOptions'
import { useCardEditor } from '@/composables/世界观管理/useCardEditor'
import { useRelationDiff } from '@/composables/世界观管理/useRelationDiff'

// ===== 导入共用组件 =====
import AttributeList from './AttributeList.vue'
import TagInput from './TagInput.vue'
import RelationSelector from './RelationSelector.vue'
import ContentBlockManager from './ContentBlockManager.vue'

// ===== 导入所有类型专属编辑器 =====
import CharacterEditor from '../type-editors/CharacterEditor.vue'
import LocationEditor from '../type-editors/LocationEditor.vue'
import ItemEditor from '../type-editors/ItemEditor.vue'
import EventEditor from '../type-editors/EventEditor.vue'
import FactionEditor from '../type-editors/FactionEditor.vue'
import SpeciesEditor from '../type-editors/SpeciesEditor.vue'
import OccupationEditor from '../type-editors/OccupationEditor.vue'
import OrganizationEditor from '../type-editors/OrganizationEditor.vue'
import CreatureEditor from '../type-editors/CreatureEditor.vue'
import SkillEditor from '../type-editors/SkillEditor.vue'
import ClimateEditor from '../type-editors/ClimateEditor.vue'
import ConceptEditor from '../type-editors/ConceptEditor.vue'

// ===== Router & Route =====
const route = useRoute()
const router = useRouter()

// ===== Props =====
const props = defineProps<{
  projectId: string
  cardData?: any | null
  inline?: boolean
}>()

const emit = defineEmits<{
  (e: 'saved'): void
  (e: 'deleted'): void
}>()

// ===== Store =====
const store = useWorldStore()

// ============================================================
//  1. 使用 Composables
// ============================================================

// 表单管理
const { form, resetForm, setFormData } = useCardForm()

// 配额管理
const routeProjectId = computed(() => (route.params.projectId as string) || props.projectId)
const { quotaInfo, quotaStatus, checkCanCreate } = useCardQuota(routeProjectId)

// 卡片类型选项
const { cardTypeOptions } = useCardTypeOptions()

// 图片上传
const { uploadingCover, uploadingGallery, uploadCover, uploadGallery } = useCardUpload()

// 编辑器核心逻辑
const {
  saving,
  loading,
  isCreating,
  routeCardId,
  loadCardData,
  handleSave: editorHandleSave,
  handleDelete: editorHandleDelete,
} = useCardEditor(form, resetForm, setFormData, checkCanCreate)

// 关系差异计算（虽然目前未直接使用，但保留以备后续扩展）
const { computeDiff } = useRelationDiff()

// ============================================================
//  2. 组件内部状态（仅 UI 相关）
// ============================================================
const fileInput = ref<HTMLInputElement | null>(null)
const galleryInput = ref<HTMLInputElement | null>(null)
const tagInput = ref('')
const newRelation = ref({ targetId: '', relationType: '' })
const searchResults = ref<any[]>([])

// ============================================================
//  3. 编辑器映射
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
}

const currentTypeEditor = computed(() => {
  return editorMap[form.value.type as CardType] || null
})

// ============================================================
//  4. UI 事件处理
// ============================================================
const selectType = (type: any) => {
  form.value.type = type.value
}

const triggerFileInput = () => fileInput.value?.click()
const triggerGalleryUpload = () => galleryInput.value?.click()

const handleFileUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const url = await uploadCover(file)
  if (url) form.value.coverImage = url
  input.value = ''
}

const handleGalleryUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const files = input.files
  if (!files || !files.length) return
  const urls = await uploadGallery(files)
  form.value.galleryImages.push(...urls)
  input.value = ''
}

const removeGalleryImage = (idx: number) => {
  form.value.galleryImages.splice(idx, 1)
}

const handleCreateCardFromBlock = (type: string) => {
  window.dispatchEvent(
    new CustomEvent('open-create-card', { detail: { type, fromBlock: true } })
  )
}

const cancelCreate = () => {
  if (isCreating.value) {
    emit('deleted')
  }
}

// 封装保存和删除，以触发 emit
const handleSave = async () => {
  await editorHandleSave(() => {
    emit('saved')
  })
}

const handleDelete = async () => {
  await editorHandleDelete(() => {
    emit('deleted')
  })
}

// ============================================================
//  5. 监听路由变化
// ============================================================
watch(
  () => route.params.cardId,
  () => {
    if (route.params.cardId) {
      loadCardData()
    }
  }
)

watch(
  () => props.cardData,
  (newVal) => {
    if (newVal && !routeCardId.value) {
      loadCardData()
    }
  }
)

// ============================================================
//  6. 监听 store.currentCard 同步关系
// ============================================================
watch(
  () => store.currentCard,
  (newCard) => {
    if (newCard && routeCardId.value) {
      const newRelations = (newCard.outRelations || []).map((r: any) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      }))
      form.value.relations = newRelations
    }
  },
  { deep: true }
)

// ============================================================
//  7. 生命周期
// ============================================================
onMounted(() => {
  loadCardData()

  window.addEventListener(
    'open-create-card',
    ((e: CustomEvent) => {
      const type = e.detail?.type || 'character'
      resetForm()
      form.value.type = type
      isCreating.value = true
    }) as EventListener
  )
})
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