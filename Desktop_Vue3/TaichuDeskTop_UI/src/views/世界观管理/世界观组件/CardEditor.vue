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

    <!-- 关联卡片（出度，可编辑） -->
    <div class="field">
      <RelationSelector
        v-model="form.relations"
        :project-id="projectId"
      />
    </div>

    <!-- 🔗 入度关系（只读） -->
    <div class="field" v-if="incomingRelations.length">
      <RelationList
        :relations="incomingRelations"
        type="in"
        :project-id="projectId"
        :card-id="routeCardId"
        label="📌 被以下卡片关联（只读）"
        @card-click="goToCard"
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
import RelationList from './RelationList.vue'
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
} = useCardEditor(form, resetForm, setFormData, checkCanCreate, props.projectId)

// 关系差异计算
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
//  5. 入度关系展示（新增）
// ============================================================
const incomingRelations = computed(() => {
  return store.currentCard?.inRelations || []
})

const goToCard = (targetId: string) => {
  if (targetId === routeCardId.value) return
  router.push(`/world/project/${routeProjectId.value}/card/${targetId}`)
}

// ============================================================
//  6. 监听路由变化
// ============================================================
watch(
  () => route.params.cardId,
  () => {
    if (route.params.cardId) {
      loadCardData(props.cardData)
    }
  }
)

watch(
  () => props.cardData,
  (newVal) => {
    if (newVal && !routeCardId.value) {
      loadCardData(newVal)
    }
  }
)

// ============================================================
//  8. 生命周期
// ============================================================
onMounted(() => {
  loadCardData(props.cardData)

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
@import './CardEditor.css';
</style>