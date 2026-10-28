<template>
  <div class="card-editor-inline" v-loading="loading">
    <p v-if="loadError" class="editor-hint" role="alert">条目读取失败，暂时不能保存。<button class="cancel-btn" @click="loadCardData(cardData)">重新读取</button></p>
    <section v-if="pendingDraft" class="draft-recovery" role="region" aria-label="草稿恢复">
      <strong>找到未保存的本地草稿</strong>
      <label v-if="draft.candidates.value.length > 1" class="draft-picker">可恢复的草稿
        <select aria-label="选择草稿" :value="pendingDraft.storageKey" @change="selectDraft(($event.target as HTMLSelectElement).value)">
          <option v-for="(item, index) in draft.candidates.value" :key="item.storageKey" :value="item.storageKey">草稿 {{ index + 1 }} · {{ new Date(item.updatedAt).toLocaleString() }} · {{ item.form.title || '未命名条目' }}</option>
        </select>
      </label>
      <details v-if="draft.candidates.value.length > 1" class="draft-preview"><summary>预览所选草稿正文</summary><CardRefTextarea :model-value="pendingDraft.form.description" :project-id="routeProjectId" disabled /></details>
      <p>{{ new Date(pendingDraft.updatedAt).toLocaleString() }} · 仅保存在当前浏览器，不会自动提交。</p>
      <p v-if="draftServerChanged">服务器内容已有变化。恢复不会提交；保存时会核对双方修改，同一处的不同内容需要你选择。</p>
      <div class="leave-actions"><button class="cancel-btn" @click="draft.clear">放弃草稿</button><button class="btn-primary" @click="draft.restore">恢复草稿</button></div>
    </section>
    <p v-if="draftError" class="editor-hint" role="alert">{{ draftError }}</p>
    <section ref="conflictPanel" tabindex="-1" v-if="conflicts.length && remoteForm" class="draft-recovery conflict-review" role="region" aria-label="核对更新">
      <strong>同一处内容有不同修改，请先核对</strong>
      <p>尚未提交。未冲突的修改会一起保留；以下每一项请选择要使用的版本。</p>
      <fieldset v-for="key in conflicts" :key="key" class="conflict-field">
        <legend>{{ cardFieldLabels[key] }}</legend>
        <div class="conflict-versions">
          <div v-for="side in (['local', 'server'] as const)" :key="side">
            <label><input v-model="conflictChoices[key]" type="radio" :name="'conflict-' + key" :value="side" :aria-label="cardFieldLabels[key] + (side === 'local' ? '：采用本地草稿' : '：采用服务器版本')" />{{ side === 'local' ? '本地草稿' : '服务器版本' }}</label>
            <CardRefTextarea v-if="key === 'description'" :model-value="side === 'local' ? form.description : remoteForm.description" :project-id="routeProjectId" disabled />
            <pre v-else>{{ conflictPreview(key, side === 'local' ? form : remoteForm) }}</pre>
          </div>
        </div>
      </fieldset>
      <button class="btn-primary" :disabled="conflicts.some(key => !conflictChoices[key])" @click="resolveConflicts">应用选择，继续编辑</button>
    </section>
    <fieldset class="editor-fields" :disabled="loading || saving || loadError || Boolean(pendingDraft) || conflicts.length > 0">
    <!-- 新建横幅 -->
    <div v-if="isCreating && !inline" class="editor-banner">
      <span>新建条目</span>
      <button class="cancel-btn" @click="cancelCreate">取消</button>
    </div>

    <!-- 标题 -->
    <input
      v-model="form.title"
      class="doc-title-input"
      aria-label="条目名称"
      placeholder="条目名称"
    />

    <p class="editor-hint">先写名称和描述即可保存。图片、属性、时间线和关联都可以以后再补充。</p>
    <p v-if="isDirty" class="save-state" role="status">{{ draftSavedAt ? '未提交 · 已备份到当前浏览器 ' + new Date(draftSavedAt).toLocaleTimeString() : '有未保存的修改' }}</p>
    <label class="entry-type">条目类型
      <select aria-label="条目类型" :value="form.type" @change="selectType({ value: ($event.target as HTMLSelectElement).value })">
        <option v-for="type in cardTypeOptions" :key="type.value" :value="type.value">{{ type.label }}</option>
      </select>
    </label>
    <div v-if="isCreating" class="quick-types" aria-label="常用条目类型">
      <button v-for="type in [{ value: 'character', label: '人物' }, { value: 'location', label: '地点' }, { value: 'organization', label: '组织' }]" :key="type.value" type="button" :aria-pressed="form.type === type.value" @click="selectType(type)">{{ type.label }}</button>
    </div>

    <!-- 封面 -->
    <div class="doc-section">
      <div v-if="form.coverImage" class="cover-preview">
        <img :src="parseCoverImage(form.coverImage)" :alt="form.title || '条目封面'" @click="triggerFileInput" />
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
      <CardRefTextarea
        :disabled="loading || saving || loadError || Boolean(pendingDraft) || conflicts.length > 0"
        v-model="form.description"
        :project-id="routeProjectId"
        :exclude-card-id="routeCardId"
        placeholder="写下这个人物、地点或事物的设定…（输入 @ 或 [[ 引用其它设定）"
        :rows="5"
      />
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
    <details v-if="cardSchemas[form.type]?.length" class="doc-section optional-section">
      <summary>{{ currentTypeLabel }}属性<small>可选</small></summary>
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
    </details>

    <!-- 自定义属性 -->
    <details class="doc-section optional-section" :open="!isCreating && form.attributes.some(attr => String(attr.value ?? '').trim())">
      <summary>自定义属性<small>可选</small></summary>
      <AttributeList v-model="form.attributes" />
    </details>

    <!-- 时间线事件 -->
    <details class="doc-section optional-section" :open="Boolean(form.timelineEvents?.length)">
      <summary>时间线事件<small>可选</small></summary>
      <div class="timeline-events">
        <div v-for="(ev, idx) in form.timelineEvents" :key="idx" class="timeline-event-row">
          <input v-model="ev.date" class="ev-date" placeholder="时间（如 100年）" />
          <input v-model="ev.era" class="ev-era" placeholder="纪元（如 创世纪元）" />
          <input v-model="ev.title" class="ev-title" placeholder="事件标题" />
          <button type="button" class="ev-remove" @click="removeTimelineEvent(idx)">×</button>
          <textarea v-model="ev.description" class="ev-desc" rows="2" placeholder="描述（可选）"></textarea>
        </div>
        <button class="add-btn-ghost" @click="addTimelineEvent">+ 添加事件</button>
      </div>
    </details>

    <!-- 关联内容 -->
    <details class="doc-section optional-section" :open="Boolean(form.contentBlocks?.length)">
      <summary>引用设定<small>可选 · 在正文下展示其他条目</small></summary>
      <ContentBlockManager
        ref="contentBlocksEditor"
        v-model="form.contentBlocks"
        :project-id="routeProjectId"
        :card-type-options="cardTypeOptions"
        :exclude-card-id="routeCardId"
      />
    </details>

    <!-- 关联卡片（出度） -->
    <details class="doc-section optional-section" :open="Boolean(form.relations?.length)">
      <summary>设定关系<small>可选 · 如居住地、所属组织</small></summary>
      <RelationSelector v-model="form.relations" :project-id="projectId" :exclude-card-id="routeCardId" />
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
        <button v-else-if="quotaError" class="cancel-btn" @click="loadQuota">配额读取失败，点击重试</button>
        <div v-else class="quota-info" :class="quotaStatus">
          <span>📄 {{ quotaInfo.currentCount }} / {{ quotaInfo.maxCount }}</span>
          <span v-if="quotaInfo.remaining > 0" class="quota-remaining">
            剩余 {{ quotaInfo.remaining }} 张
          </span>
          <span v-else class="quota-full-text">⚠️ 已满，请扩容</span>
        </div>
      </div>
      <div class="footer-actions">
        <button class="btn-primary" @click="handleSave" :disabled="saving || loading || uploadingCover || uploadingGallery">
          {{ saving ? '保存中...' : '保存' }}
        </button>
        <button v-if="!isCreating" class="btn-danger" @click="handleDelete">删除</button>
      </div>
    </div>
    </fieldset>
    <el-dialog v-model="showLeaveDialog" title="有未保存的修改" width="min(440px, calc(100vw - 32px))"
      append-to-body :close-on-click-modal="false" :close-on-press-escape="!saving" :show-close="!saving"
      @close="finishLeave(false)">
      <p>离开前要保存当前条目吗？继续编辑会保留填写的内容。</p>
      <p v-if="leaveSaveFailed" role="alert">保存未成功，内容仍然保留。请重试，或继续编辑。</p>
      <template #footer>
        <div class="leave-actions">
          <el-button :disabled="saving" @click="finishLeave(false)">继续编辑</el-button>
          <el-button :disabled="saving" @click="discardAndLeave">不保存，离开</el-button>
          <el-button type="primary" :loading="saving" @click="saveAndLeave">保存并离开</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { parseCoverImage } from '@/utils/worldPresentation'
import { ref, watch, computed, onMounted, onScopeDispose } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta, type CardType } from '../card_type'
import { cardSchemas } from '../card-schemas'
import { templateAttributes } from '../card_templates'
import type { CardSummary } from '@/api/worldApi'

// ===== Composables =====
import { cardFieldLabels, type CardForm, useCardForm } from '@/composables/世界观管理/useCardForm'
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
import CardRefTextarea from './CardRefTextarea.vue'

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
  (e: 'saved', cardId: string): void
  (e: 'deleted'): void
}>()

// ===== Store =====
const store = useWorldStore()

// ============================================================
//  1. Composables
// ============================================================
const { form, resetForm, setFormData } = useCardForm()

const routeProjectId = computed(() => (route.params.projectId as string) || props.projectId)
const { quotaInfo, quotaStatus, checkCanCreate, loading: quotaLoading, error: quotaError, loadQuota } = useCardQuota(routeProjectId)
watch(() => store.cards.filter(card => card.projectId === routeProjectId.value).length, () => { void loadQuota() })

const { cardTypeOptions } = useCardTypeOptions()

const { uploadingCover, uploadingGallery, uploadCover, uploadGallery } = useCardUpload()

const {
  draft,
  conflicts, conflictChoices, remoteForm, resolveConflicts,
  saving,
  isDirty,
  markClean,
  loading,
  loadError,
  isCreating,
  routeCardId,
  loadCardData,
  handleSave: editorHandleSave,
  handleDelete: editorHandleDelete,
} = useCardEditor(form, resetForm, setFormData, checkCanCreate, routeProjectId)

const { pending: pendingDraft, savedAt: draftSavedAt, error: draftError, serverChanged: draftServerChanged } = draft

function selectDraft(key: string) { draft.pending.value = draft.candidates.value.find(item => item.storageKey === key) || null }
function conflictPreview(key: keyof CardForm, value: CardForm) {
  const name = (id: string) => store.cards.find(card => card.id === id)?.title || '不可用的条目'
  if (key === 'relations') return value.relations.map(item => `${name(item.targetCardId)} · ${item.relationType || '关联'}`).join('\n') || '无'
  if (key === 'contentBlocks') return value.contentBlocks.map(item => `${name(item.cardId)} · ${item.contextLabel || '引用'}`).join('\n') || '无'
  return typeof value[key] === 'string' ? value[key] || '空' : JSON.stringify(value[key], null, 2)
}

const conflictPanel = ref<HTMLElement | null>(null)
watch(() => conflicts.value.length, count => {
  if (count) { conflictPanel.value?.focus({ preventScroll: true }); conflictPanel.value?.scrollIntoView({ block: 'start' }) }
}, { flush: 'post' })

const contentBlocksEditor = ref<InstanceType<typeof ContentBlockManager> | null>(null)
const busy = computed(() => saving.value || uploadingCover.value || uploadingGallery.value || contentBlocksEditor.value?.creatingCard)
const showLeaveDialog = ref(false)
const leaveSaveFailed = ref(false)
let leavePromise: Promise<boolean> | undefined
let resolveLeave: ((leave: boolean) => void) | undefined
function finishLeave(leave: boolean) {
  const resolve = resolveLeave
  resolveLeave = undefined
  leavePromise = undefined
  showLeaveDialog.value = false
  resolve?.(leave)
}
function requestLeave(): Promise<boolean> {
  if (leavePromise) return leavePromise
  if (busy.value) {
    ElMessage.info('正在保存、上传或创建引用，请稍候再离开')
    return Promise.resolve(false)
  }
  if (!isDirty.value) return Promise.resolve(true)
  leaveSaveFailed.value = false
  showLeaveDialog.value = true
  leavePromise = new Promise(resolve => { resolveLeave = resolve })
  return leavePromise
}
async function discardAndLeave() { await draft.clear(); markClean(); finishLeave(true) }
async function saveAndLeave() {
  let saved = false
  await editorHandleSave(() => { saved = true })
  if (saved) finishLeave(true)
  else if (conflicts.value.length) finishLeave(false)
  else leaveSaveFailed.value = true
}
function beforeUnload(event: BeforeUnloadEvent) {
  if (busy.value || (isDirty.value && !draft.flush())) { event.preventDefault(); event.returnValue = '' }
}
onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onScopeDispose(() => { if (typeof window !== 'undefined') window.removeEventListener('beforeunload', beforeUnload); finishLeave(false) })
defineExpose({ requestLeave })


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
  if (isCreating.value && form.value.attributes.every(attr => !String(attr.value ?? '').trim())) form.value.attributes = []
  applyTemplateIfEmpty(type.value)
}

// 新建卡片时，如果还没有自定义属性，套用该类型的预设模板
const applyTemplateIfEmpty = (type: string) => {
  if (!form.value.attributes?.length) {
    form.value.attributes = templateAttributes(type as CardType)
  }
}

// 进入新建态时套用当前类型模板
watch(isCreating, (creating) => {
  if (creating) applyTemplateIfEmpty(form.value.type)
})

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

const addTimelineEvent = () => {
  form.value.timelineEvents.push({ date: '', era: null, title: '', description: null })
}
const removeTimelineEvent = (idx: number) => {
  form.value.timelineEvents.splice(idx, 1)
}

const cancelCreate = async () => {
  if (isCreating.value && await requestLeave()) {
    emit('deleted')
  }
}

const handleSave = async () => {
  if (uploadingCover.value || uploadingGallery.value) return
  await editorHandleSave((cardId) => {
    emit('saved', cardId)
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
  return !isCreating.value && store.currentCard?.id === routeCardId.value ? store.currentCard?.inRelations || [] : []
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
  [routeProjectId, () => props.cardData?.id],
  () => { void loadCardData(props.cardData) },
  { immediate: true }
)

</script>

<style scoped>
@import './CardEditor.css';
.editor-fields { min-width: 0; border: 0; padding: 0; margin: 0; }.quick-types { display: flex; flex-wrap: wrap; gap: 8px; margin: -12px 0 24px; }.quick-types button { border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); padding: 8px 16px; cursor: pointer; }.quick-types button[aria-pressed=true] { border-color: var(--accent-color); background: var(--accent-soft); color: var(--accent-color); }
.entry-type { display: flex; align-items: center; gap: 12px; margin: 0 0 24px; font-size: 13px; color: var(--text-secondary); }
.entry-type select { padding: 8px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); font: inherit; }
.editor-hint { color: var(--text-secondary); font-size: 13px; line-height: 1.7; margin: 12px 0 20px; }
.draft-recovery { margin: 0 0 24px; padding: 20px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--accent-soft); color: var(--text-primary); overflow-wrap: anywhere; }.draft-recovery .cancel-btn { border: 1px solid var(--border-line); border-radius: 6px; padding: 9px 16px; background: var(--paper-card); color: var(--text-primary); font: inherit; cursor: pointer; }
.draft-recovery p { font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.draft-picker { display: grid; gap: 8px; margin-top: 12px; }.draft-picker select { width: 100%; min-width: 0; padding: 8px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: 6px; }
.conflict-review { scroll-margin-top: 16px; }.conflict-versions :deep(.ref-help), .draft-preview :deep(.ref-help) { display: none; }.draft-preview { margin-top: 12px; }.draft-preview summary { cursor: pointer; margin-bottom: 12px; }
.conflict-field { min-width: 0; border: 1px solid var(--border-line); border-radius: 8px; margin: 16px 0; padding: 12px; }.conflict-versions { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }.conflict-versions > div { min-width: 0; }.conflict-versions label { display: flex; gap: 8px; align-items: center; margin-bottom: 12px; }.conflict-versions pre { white-space: pre-wrap; overflow-wrap: anywhere; max-height: 260px; overflow-y: auto; font: inherit; font-size: 13px; }.conflict-versions :deep(.ProseMirror) { max-height: 260px; overflow-y: auto; }
@media (max-width: 720px) { .conflict-versions { grid-template-columns: 1fr; } }
.save-state { color: var(--accent-color); font-size: 13px; margin: 0 0 16px; }
.leave-actions { display: flex; flex-wrap: wrap; gap: 8px; justify-content: flex-end; }
.leave-actions :deep(.el-button) { margin-left: 0; }
</style>
