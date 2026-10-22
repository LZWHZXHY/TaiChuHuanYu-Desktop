<template>
  <div class="card-editor-inline" v-loading="loading">
    <!-- 新建横幅 -->
    <div v-if="isCreating" class="editor-banner">
      <span>✨ 新建卡片</span>
      <button class="cancel-btn" @click="cancelCreate">取消</button>
    </div>

    <!-- 标题 -->
    <input
      v-model="form.title"
      class="doc-title-input"
      placeholder="卡片名称"
    />

    <!-- 类型胶囊 -->
    <div class="doc-meta-row">
      <button type="button"
        v-for="type in cardTypeOptions"
        :key="type.value"
        class="type-chip"
        :class="{ active: form.type === type.value }"
        @click="selectType(type)"
      >
        {{ type.label }}
      </button>
    </div>

    <!-- 封面 -->
    <div class="doc-section">
      <div v-if="form.coverImage" class="cover-preview">
        <img :src="form.coverImage" @click="triggerFileInput" />
        <button class="cover-remove" @click.stop="form.coverImage = ''">×</button>
      </div>
      <button v-else class="add-btn-ghost" @click="triggerFileInput">
        <span>🖼️</span> 添加封面
      </button>
      <input ref="fileInput" type="file" accept="image/*" style="display:none" @change="handleFileUpload" />
      <div v-if="uploadingCover" class="upload-hint">上传中...</div>
    </div>

    <!-- 描述 -->
    <div class="doc-section">
      <textarea
        v-model="form.description"
        class="doc-desc-input"
        placeholder="描述这个卡片..."
        rows="5"
      ></textarea>
    </div>

    <!-- 图库 -->
    <details class="doc-section optional-section" :open="Boolean(form.galleryImages?.length)">
      <summary>图库<small>可选</small></summary>
      <div v-if="form.galleryImages && form.galleryImages.length" class="gallery-grid">
        <div
          v-for="(img, idx) in form.galleryImages"
          :key="idx"
          class="gallery-item"
        >
          <img :src="img" :alt="`图 ${idx + 1}`" />
          <button class="gallery-remove" @click="removeGalleryImage(idx)">×</button>
        </div>
      </div>
      <button class="add-btn-ghost" @click="triggerGalleryUpload">
        <span>+</span> 添加图片
      </button>
      <input ref="galleryInput" type="file" accept="image/*" multiple style="display:none" @change="handleGalleryUpload" />
      <div v-if="uploadingGallery" class="upload-hint">上传中...</div>
    </details>

    <!-- 类型专属属性 -->
    <div class="doc-section">
      <h2 class="section-title">{{ currentTypeLabel }}属性</h2>
      <DynamicForm v-model="form.content" :type="form.type">
        <template #location-coordinate="{ value, onChange }">
          <div class="coord-group">
            <div class="coord-item">
              <span class="coord-label">X</span>
              <input
                type="number"
                step="0.01"
                :value="value?.x ?? 0"
                @input="onChange({ ...(value || {}), x: Number(($event.target as HTMLInputElement).value) })"
              />
            </div>
            <div class="coord-item">
              <span class="coord-label">Y</span>
              <input
                type="number"
                step="0.01"
                :value="value?.y ?? 0"
                @input="onChange({ ...(value || {}), y: Number(($event.target as HTMLInputElement).value) })"
              />
            </div>
          </div>
        </template>
      </DynamicForm>
    </div>

    <!-- 自定义属性 -->
    <details class="doc-section optional-section" :open="Boolean(form.attributes?.length)">
      <summary>自定义属性<small>可选</small></summary>
      <AttributeList v-model="form.attributes" />
    </details>

    <!-- 关联内容 -->
    <details class="doc-section optional-section" :open="Boolean(form.contentBlocks?.length)">
      <summary>关联内容<small>可选</small></summary>
      <ContentBlockManager
        v-model="form.contentBlocks"
        :project-id="routeProjectId"
        :card-type-options="cardTypeOptions"
        :exclude-card-id="routeCardId"
        @create-card="handleCreateCardFromBlock"
      />
    </details>

    <!-- 关联卡片（出度） -->
    <details class="doc-section optional-section" :open="Boolean(form.relations?.length)">
      <summary>关联卡片<small>可选</small></summary>
      <RelationSelector v-model="form.relations" :project-id="projectId" />
    </details>

    <!-- 被以下卡片关联（只读） -->
    <div v-if="incomingRelations.length" class="doc-section">
      <h2 class="section-title">被以下卡片关联</h2>
      <RelationList
        :relations="incomingRelations"
        type="in"
        :project-id="projectId"
        :card-id="routeCardId"
        @card-click="goToCard"
      />
    </div>

    <!-- 标签 -->
    <div class="doc-section">
      <h2 class="section-title">标签</h2>
      <TagInput v-model="form.tags" />
    </div>

    <!-- 底部操作栏 -->
    <div class="editor-footer">
      <div class="footer-left">
        <span v-if="quotaLoading">正在读取配额…</span>
        <div v-else class="quota-info" :class="quotaStatus">
          <span>📄 {{ quotaInfo.currentCount }} / {{ quotaInfo.maxCount }}</span>
          <span v-if="quotaInfo.remaining > 0" class="quota-remaining">
            剩余 {{ quotaInfo.remaining }} 张
          </span>
          <span v-else class="quota-full-text">⚠️ 已满，请扩容</span>
        </div>
      </div>
      <div class="footer-actions">
        <button class="btn-primary" @click="handleSave" :disabled="saving || loading">
          {{ saving ? '保存中...' : '保存' }}
        </button>
        <button v-if="!isCreating" class="btn-danger" @click="handleDelete">删除</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta, type CardType } from '../card_type'
import type { CardSummary } from '@/api/worldApi'

// ===== Composables =====
import { useCardForm } from '@/composables/世界观管理/useCardForm'
import { useCardQuota } from '@/composables/世界观管理/useCardQuota'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import { useCardTypeOptions } from '@/composables/世界观管理/useCardTypeOptions'
import { useCardEditor } from '@/composables/世界观管理/useCardEditor'

// ===== 导入共用组件 =====
import AttributeList from './AttributeList.vue'
import TagInput from './TagInput.vue'
import RelationSelector from './RelationSelector.vue'
import RelationList from './RelationList.vue'
import ContentBlockManager from './ContentBlockManager.vue'
import DynamicForm from './DynamicForm.vue'

// ===== Router & Route =====
const route = useRoute()
const router = useRouter()

// ===== Props =====
const props = defineProps<{
  projectId: string
  cardData?: CardSummary | null
  inline?: boolean
}>()

const emit = defineEmits<{
  (e: 'saved'): void
  (e: 'deleted'): void
}>()

// ===== Store =====
const store = useWorldStore()

// ============================================================
//  1. Composables
// ============================================================
const { form, resetForm, setFormData } = useCardForm()

const routeProjectId = computed(() => (route.params.projectId as string) || props.projectId)
const { quotaInfo, quotaStatus, checkCanCreate, loading: quotaLoading } = useCardQuota(routeProjectId)

const { cardTypeOptions } = useCardTypeOptions()

const { uploadingCover, uploadingGallery, uploadCover, uploadGallery } = useCardUpload()

const {
  saving,
  loading,
  isCreating,
  routeCardId,
  loadCardData,
  startCreating,
  handleSave: editorHandleSave,
  handleDelete: editorHandleDelete,
} = useCardEditor(form, resetForm, setFormData, checkCanCreate, routeProjectId)


// ============================================================
//  2. 计算属性
// ============================================================
const currentTypeLabel = computed(() => {
  const meta = CardTypeMeta[form.value.type as CardType]
  return meta?.label || form.value.type
})

// ============================================================
//  3. 组件内部状态
// ============================================================
const fileInput = ref<HTMLInputElement | null>(null)
const galleryInput = ref<HTMLInputElement | null>(null)

// ============================================================
//  4. UI 事件
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
//  5. 入度关系
// ============================================================
const incomingRelations = computed(() => {
  return store.currentCard?.inRelations || []
})

const goToCard = (targetId: string) => {
  if (targetId === routeCardId.value) return
  router.push(`/world/project/${routeProjectId.value}/card/${targetId}`)
}

// ============================================================
//  6. 监听路由
// ============================================================
// 只在编辑目标变化时载入，后台刷新摘要不会覆盖未保存的表单。
watch(
  [routeProjectId, routeCardId, () => props.cardData?.id],
  () => { void loadCardData(props.cardData) },
  { immediate: true }
)

// ============================================================
//  7. 生命周期
// ============================================================
const onOpenCreateCard = ((e: CustomEvent) => {
  startCreating()
  form.value.type = e.detail?.type || 'character'
}) as EventListener
onMounted(() => window.addEventListener('open-create-card', onOpenCreateCard))
onUnmounted(() => window.removeEventListener('open-create-card', onOpenCreateCard))
</script>

<style scoped>
@import './CardEditor.css';
</style>
