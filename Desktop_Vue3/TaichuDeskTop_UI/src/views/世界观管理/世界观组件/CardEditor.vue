<template>
  <div ref="root" class="card-editor-inline workbench-skin editor-surface community-surface tone-world" v-loading="loading">
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
    <fieldset class="editor-fields" :disabled="locked">
    <!-- 新建横幅 -->
    <div v-if="isCreating && !inline" class="editor-banner">
      <span>新建条目</span>
      <button class="cancel-btn" @click="cancelCreate">取消</button>
    </div>

    <!-- ================= 顶部：名称、类型、别名、属性、标签 + 右边封面 ================= -->
    <header class="ed-hero" :class="{ 'has-cover': form.coverImage || uploadingCover }">
      <div class="ed-main">
        <input v-model="form.title" class="doc-title-input" aria-label="条目名称" placeholder="条目名称" />
        <div class="ed-meta">
          <label class="entry-type">类型
            <select aria-label="条目类型" :value="form.type" @change="selectType({ value: ($event.target as HTMLSelectElement).value })">
              <option v-for="type in cardTypeOptions" :key="type.value" :value="type.value">{{ type.label }}</option>
            </select>
          </label>
          <div v-if="isCreating" class="quick-types" aria-label="常用条目类型">
            <button v-for="type in [{ value: 'character', label: '人物' }, { value: 'location', label: '地点' }, { value: 'organization', label: '组织' }]" :key="type.value" type="button" :aria-pressed="form.type === type.value" @click="selectType(type)">{{ type.label }}</button>
          </div>
          <span v-if="isDirty" class="save-state" role="status">{{ draftSavedAt ? '未提交 · 已备份到当前浏览器 ' + new Date(draftSavedAt).toLocaleTimeString() : '有未保存的修改' }}</span>
        </div>

        <!-- 别名 -->
        <div class="chip-row">
          <span class="chip-label"><i aria-hidden="true">⌘</i>别名</span>
          <span v-for="(a, i) in form.aliases" :key="a + i" class="chip">{{ a }}<button type="button" :aria-label="`删除别名 ${a}`" @click="form.aliases.splice(i, 1)">×</button></span>
          <input v-model="aliasDraft" class="chip-input" placeholder="＋ 添加" aria-label="添加别名" maxlength="50" @keydown.enter.prevent="addAlias" @blur="addAlias" />
        </div>

        <!-- 这个类型的基本信息 -->
        <div v-if="cardSchemas[form.type]?.length" class="type-fields">
          <DynamicForm v-model="form.content" :type="form.type">
            <template #location-coordinate="{ value, onChange }">
              <div class="coord-group">
                <div class="coord-item">
                  <span class="coord-label">X</span>
                  <input type="number" step="0.01" :value="value?.x ?? 0" @input="onChange({ ...(value || {}), x: Number(($event.target as HTMLInputElement).value) })" />
                </div>
                <div class="coord-item">
                  <span class="coord-label">Y</span>
                  <input type="number" step="0.01" :value="value?.y ?? 0" @input="onChange({ ...(value || {}), y: Number(($event.target as HTMLInputElement).value) })" />
                </div>
              </div>
            </template>
          </DynamicForm>
        </div>

        <!-- 属性：两列紧凑 -->
        <div v-if="form.attributes.length" ref="attrGrid" class="prop-grid-edit">
          <div v-for="(attr, idx) in form.attributes" :key="idx" class="prop-cell">
            <input v-model="attr.key" class="pe-key" placeholder="属性名" :aria-label="`第 ${idx + 1} 个属性的名称`" />
            <textarea v-if="attr.type === 'long'" v-model="attr.value" class="pe-val" rows="2" placeholder="内容" :aria-label="`${attr.key || '属性'}的内容`"></textarea>
            <select v-else-if="attr.type === 'boolean'" v-model="attr.value" class="pe-val" :aria-label="`${attr.key || '属性'}的内容`"><option :value="true">是</option><option :value="false">否</option></select>
            <input v-else v-model="attr.value" class="pe-val" :type="attr.type === 'number' ? 'number' : attr.type === 'date' ? 'date' : 'text'" placeholder="内容" :aria-label="`${attr.key || '属性'}的内容`" />
            <select v-model="attr.type" class="pe-type" aria-label="属性类型" title="属性类型">
              <option value="short">短文本</option><option value="long">长文本</option><option value="number">数字</option><option value="date">日期</option><option value="boolean">是/否</option>
            </select>
            <button type="button" class="pe-del" :aria-label="`删除属性 ${attr.key}`" @click="form.attributes.splice(idx, 1)">×</button>
          </div>
        </div>
        <div class="prop-add">
          <button type="button" class="ghost-link" @click="addAttribute()">＋ 添加属性</button>
          <button v-for="s in quickAttrs" :key="s.key" type="button" class="attr-chip" :title="`添加属性「${s.key}」`" @click="addAttribute(s.key)">＋ {{ s.key }}</button>
        </div>

        <!-- 标签 -->
        <div class="chip-row">
          <span class="chip-label"><i aria-hidden="true">#</i>标签</span>
          <span v-for="(t, i) in form.tags" :key="t" class="chip tag">#{{ t }}<button type="button" :aria-label="`删除标签 ${t}`" @click="form.tags.splice(i, 1)">×</button></span>
          <input v-if="form.tags.length < 10" v-model="tagDraft" class="chip-input" placeholder="＋ 添加" aria-label="添加标签" maxlength="30" @keydown.enter.prevent="addTag" @blur="addTag" />
        </div>
      </div>

      <!-- 封面 -->
      <div class="ed-cover">
        <div v-if="form.coverImage" class="cover-preview">
          <img :src="parseCoverImage(form.coverImage)" :alt="form.title || '条目封面'" title="点击更换封面" @click="triggerFileInput" />
          <button class="cover-remove" aria-label="移除封面" @click.stop="form.coverImage = ''">×</button>
        </div>
        <button v-else type="button" class="cover-add" :disabled="uploadingCover" @click="triggerFileInput">{{ uploadingCover ? '上传中…' : '＋ 封面' }}</button>
      </div>
      <input ref="fileInput" type="file" accept="image/*" style="display:none" @change="handleFileUpload" />
    </header>

    <!-- ================= 区块：每行 1～2 块 ================= -->
    <div v-for="row in form.layout.rows" :key="row.id" class="ed-row" :class="{ split: row.blocks.length > 1 }">
      <section v-for="block in row.blocks" :key="block.id" class="ed-block" :class="{ 'is-hidden': block.hidden }" :data-block-id="block.id">
        <header class="eb-head">
          <span class="eb-icon" aria-hidden="true">{{ BLOCK_META[block.kind].icon }}</span>
          <input class="eb-title" :value="block.title ?? ''" :placeholder="BLOCK_META[block.kind].label" maxlength="60" :aria-label="`${BLOCK_META[block.kind].label}区块的标题`" @input="block.title = ($event.target as HTMLInputElement).value" />
          <span v-if="block.hideTitle" class="eb-flag" title="阅读时不显示这个标题">标题已隐藏</span>
          <span v-if="block.hidden" class="eb-flag" title="访客看不到这一块">仅成员可见</span>
          <div class="eb-anchor">
            <button type="button" class="eb-more" :aria-expanded="menu?.id === block.id" aria-haspopup="menu" :aria-label="`${blockTitle(block)}区块的操作`" @click="toggleMenu(block.id)">⋮</button>
            <div v-if="menu?.id === block.id" class="eb-menu" role="menu">
              <template v-if="menu.step === 'main'">
                <button type="button" role="menuitem" @click="goKinds('above')"><span>↑</span>在上方添加区块<em>›</em></button>
                <button type="button" role="menuitem" @click="goKinds('below')"><span>↓</span>在下方添加区块<em>›</em></button>
                <button type="button" role="menuitem" :disabled="!canPlaceBeside(form.layout, block.id)" title="一行最多放两块" @click="goKinds('left')"><span>←</span>在左侧添加区块<em>›</em></button>
                <button type="button" role="menuitem" :disabled="!canPlaceBeside(form.layout, block.id)" title="一行最多放两块" @click="goKinds('right')"><span>→</span>在右侧添加区块<em>›</em></button>
                <hr />
                <button type="button" role="menuitem" @click="act(() => moveBlock(form.layout, block.id, -1))"><span>⤒</span>上移</button>
                <button type="button" role="menuitem" @click="act(() => moveBlock(form.layout, block.id, 1))"><span>⤓</span>下移</button>
                <button v-if="row.blocks.length > 1" type="button" role="menuitem" @click="act(() => swapRow(form.layout, block.id))"><span>⇆</span>左右互换</button>
                <hr />
                <button type="button" role="menuitem" @click="act(() => { block.hideTitle = !block.hideTitle || undefined })"><span>▭</span>{{ block.hideTitle ? '显示标题栏' : '隐藏标题栏' }}</button>
                <button v-if="HIDEABLE_KINDS.includes(block.kind)" type="button" role="menuitem" @click="act(() => { block.hidden = !block.hidden || undefined })"><span>◌</span>{{ block.hidden ? '对访客公开' : '对访客隐藏' }}</button>
                <button v-if="!isSingleton(block.kind)" type="button" role="menuitem" @click="act(() => duplicateBlock(form.layout, block.id))"><span>⧉</span>复制块</button>
                <button type="button" role="menuitem" class="danger" @click="deleteBlock(block)"><span>🗑</span>删除块</button>
              </template>
              <template v-else>
                <button type="button" class="eb-back" @click="goKinds(null)">‹ {{ WHERE_LABEL[menu.where] }}添加</button>
                <button v-for="k in BLOCK_KINDS" :key="k" type="button" role="menuitem" :disabled="isSingleton(k) && hasKind(form.layout, k)"
                  :title="isSingleton(k) && hasKind(form.layout, k) ? '这个条目已经有了' : BLOCK_META[k].hint" @click="addBlock(k, block.id, menu.where)">
                  <span>{{ BLOCK_META[k].icon }}</span>{{ BLOCK_META[k].label }}<small>{{ BLOCK_META[k].hint }}</small>
                </button>
              </template>
            </div>
          </div>
        </header>

        <!-- 正文 / 文本块：空行输入 / 可以继续添加区块 -->
        <CardRefTextarea v-if="block.kind === 'description'" v-model="form.description" :disabled="locked"
          :project-id="routeProjectId" :exclude-card-id="routeCardId" :commands="slashItems" :rows="5"
          placeholder="写下这个人物、地点或事物的设定…（@ 引用其它设定，空行输入 / 添加区块）" @command="id => runSlash(id, block.id)" />
        <CardRefTextarea v-else-if="block.kind === 'text'" :model-value="block.text ?? ''" @update:model-value="v => { block.text = v }" :disabled="locked"
          :project-id="routeProjectId" :exclude-card-id="routeCardId" :commands="slashItems" :rows="4"
          placeholder="写点什么…（@ 引用其它设定，空行输入 / 添加区块）" @command="id => runSlash(id, block.id)" />
        <!-- 图片 -->
        <div v-else-if="block.kind === 'media' || block.kind === 'gallery'" class="media-edit">
          <div v-for="(img, idx) in (block.kind === 'media' ? block.images ?? [] : form.galleryImages)" :key="img + idx" class="gallery-item">
            <img :src="img" :alt="`图 ${idx + 1}`" />
            <button class="gallery-remove" aria-label="移除这张图片" @click="(block.kind === 'media' ? block.images! : form.galleryImages).splice(idx, 1)">×</button>
          </div>
          <button type="button" class="media-add" :disabled="uploadingGallery" @click="pickImages(block.id)">{{ uploadingGallery && uploadTarget === block.id ? '上传中…' : '＋ 图片' }}</button>
        </div>
        <!-- 地图 -->
        <MapBlock v-else-if="block.kind === 'map'" v-model:image="block.image" v-model:pins="block.pins" :project-id="routeProjectId" editable />
        <!-- 参考来源 -->
        <SourcesBlock v-else-if="block.kind === 'sources'" :sources="block.sources" editable @update:sources="v => { block.sources = v }" />
        <!-- 时间线 -->
        <TimelineAxis v-else-if="block.kind === 'timeline'" ref="timelineEditor" v-model:events="form.timelineEvents" :project-id="routeProjectId" editable />
        <!-- 引用设定 -->
        <ContentBlockManager v-else-if="block.kind === 'embeds'" ref="contentBlocksEditor" v-model="form.contentBlocks"
          :project-id="routeProjectId" :card-type-options="cardTypeOptions" :exclude-card-id="routeCardId" />
        <!-- 设定关系 -->
        <RelationSelector v-else-if="block.kind === 'relations'" v-model="form.relations" :project-id="projectId" :exclude-card-id="routeCardId" />
      </section>
    </div>
    <input ref="galleryInput" type="file" accept="image/*" multiple style="display:none" @change="handleImages" />

    <!-- 添加区块 -->
    <div class="add-bar">
      <div class="add-anchor">
        <button ref="addButton" type="button" class="add-trigger" :aria-expanded="menu?.id === END" aria-haspopup="menu" @click="toggleMenu(END, 'kinds')">
          <span aria-hidden="true">＋</span> 添加区块
        </button>
        <div v-if="menu?.id === END" class="eb-menu up" role="menu">
          <button v-for="k in BLOCK_KINDS" :key="k" type="button" role="menuitem" :disabled="isSingleton(k) && hasKind(form.layout, k)"
            :title="isSingleton(k) && hasKind(form.layout, k) ? '这个条目已经有了' : BLOCK_META[k].hint" @click="addBlock(k, null, 'end')">
            <span>{{ BLOCK_META[k].icon }}</span>{{ BLOCK_META[k].label }}<small>{{ BLOCK_META[k].hint }}</small>
          </button>
        </div>
      </div>
      <span class="add-tip">每块右上角 ⋮ 可以在上下左右加块、调整顺序、对访客隐藏；文本里空行输入 <kbd>/</kbd> 也能加</span>
    </div>

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
import { ref, watch, computed, nextTick, onMounted, onScopeDispose } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta, type CardType } from '../card_type'
import { cardSchemas } from '../card-schemas'
import { CARD_TEMPLATES } from '../card_templates'
import type { CardSummary } from '@/api/worldApi'
import {
  BLOCK_KINDS, BLOCK_META, HIDEABLE_KINDS, blockTitle, canPlaceBeside, createBlock, duplicateBlock, findBlock, hasKind,
  insertBlock, isSingleton, moveBlock, newId, removeBlock, swapRow, type BlockKind, type LayoutBlock, type Where,
} from '@/utils/worldLayout'

// ===== Composables =====
import { cardFieldLabels, type CardForm, useCardForm } from '@/composables/世界观管理/useCardForm'
import { useCardQuota } from '@/composables/世界观管理/useCardQuota'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import { useCardTypeOptions } from '@/composables/世界观管理/useCardTypeOptions'
import { useCardEditor } from '@/composables/世界观管理/useCardEditor'

// ===== 导入共用组件 =====
import RelationSelector from './RelationSelector.vue'
import RelationList from './RelationList.vue'
import ContentBlockManager from './ContentBlockManager.vue'
import DynamicForm from './DynamicForm.vue'
import CardRefTextarea from './CardRefTextarea.vue'
import TimelineAxis from './TimelineAxis.vue'
import MapBlock from './MapBlock.vue'
import SourcesBlock from './SourcesBlock.vue'

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
const locked = computed(() => loading.value || saving.value || loadError.value || Boolean(pendingDraft.value) || conflicts.value.length > 0)

function selectDraft(key: string) { draft.pending.value = draft.candidates.value.find(item => item.storageKey === key) || null }
function conflictPreview(key: keyof CardForm, value: CardForm) {
  const name = (id: string) => store.cards.find(card => card.id === id)?.title || '不可用的条目'
  if (key === 'relations') return value.relations.map(item => `${name(item.targetCardId)} · ${item.relationType || '关联'}`).join('\n') || '无'
  if (key === 'contentBlocks') return value.contentBlocks.map(item => `${name(item.cardId)} · ${item.contextLabel || '引用'}`).join('\n') || '无'
  if (key === 'layout') return value.layout.rows.map(row => row.blocks.map(b =>
    `【${blockTitle(b)}】${b.kind === 'text' ? (b.text || '').slice(0, 60) : b.kind === 'media' ? `${b.images?.length ?? 0} 张图片` : b.kind === 'map' ? `地图 · ${b.pins?.length ?? 0} 个图钉` : b.kind === 'sources' ? `参考来源 ${b.sources?.length ?? 0} 条` : ''}${b.hidden ? '（仅成员可见）' : ''}`).join('  |  ')).join('\n') || '无'
  return typeof value[key] === 'string' ? value[key] || '空' : JSON.stringify(value[key], null, 2)
}

const conflictPanel = ref<HTMLElement | null>(null)
watch(() => conflicts.value.length, count => {
  if (count) { conflictPanel.value?.focus({ preventScroll: true }); conflictPanel.value?.scrollIntoView({ block: 'start' }) }
}, { flush: 'post' })

const contentBlocksEditor = ref<any>(null)
const busy = computed(() => saving.value || uploadingCover.value || uploadingGallery.value || [contentBlocksEditor.value].flat().some((c: any) => c?.creatingCard))
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
//  2. 顶部：类型、别名、标签、属性
// ============================================================
const currentTypeLabel = computed(() => CardTypeMeta[form.value.type as CardType]?.label || form.value.type)
const root = ref<HTMLElement | null>(null)
async function focusIn(selector: string, index = -1) {
  await nextTick()
  const all = root.value?.querySelectorAll<HTMLElement>(selector)
  if (!all?.length) return
  const target = all[index < 0 ? all.length + index : index]
  target?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  target?.focus({ preventScroll: true })
}

const selectType = (type: any) => {
  form.value.type = type.value
  // 新建时换类型：把还没填的属性清掉（常用属性按需添加，不再一次性铺满）
  if (isCreating.value) form.value.attributes = form.value.attributes.filter(attr => String(attr.value ?? '').trim())
}

const aliasDraft = ref('')
function addAlias() {
  const value = aliasDraft.value.trim()
  aliasDraft.value = ''
  if (value && !form.value.aliases.includes(value) && form.value.aliases.length < 20) form.value.aliases.push(value)
}
const tagDraft = ref('')
function addTag() {
  const value = tagDraft.value.trim().replace(/^#/, '')
  tagDraft.value = ''
  if (!value) return
  if (form.value.tags.includes(value)) return
  if (form.value.tags.length >= 10) { ElMessage.warning('最多 10 个标签'); return }
  form.value.tags.push(value)
}

// 这个类型常用、还没添加的属性
const suggestedAttrs = computed(() => {
  const have = new Set(form.value.attributes.map(attr => (attr.key || '').trim()))
  return (CARD_TEMPLATES[form.value.type as CardType] || []).filter(t => !have.has(t.key))
})
const quickAttrs = computed(() => suggestedAttrs.value.slice(0, 5))
function addAttribute(key = '') {
  const template = (CARD_TEMPLATES[form.value.type as CardType] || []).find(t => t.key === key)
  const existing = key ? form.value.attributes.findIndex(attr => (attr.key || '').trim() === key) : -1
  if (existing < 0) form.value.attributes.push({ key, value: '', type: template?.type ?? 'short' })
  // 有名字就直接填内容（已经有这一项就跳过去），没名字先填名字
  void focusIn(key ? '.prop-cell .pe-val' : '.prop-cell .pe-key', existing)
}

// ============================================================
//  3. 区块
// ============================================================
const END = '__end'
const WHERE_LABEL: Record<Where, string> = { above: '在上方', below: '在下方', left: '在左侧', right: '在右侧', end: '在最后' }
const menu = ref<{ id: string; step: 'main' | 'kinds'; where: Where } | null>(null)
function toggleMenu(id: string, step: 'main' | 'kinds' = 'main') {
  menu.value = menu.value?.id === id ? null : { id, step, where: id === END ? 'end' : 'below' }
}
function act(fn: () => unknown) { fn(); menu.value = null }
function goKinds(where: Where | null) {
  if (!menu.value) return
  if (where) menu.value = { ...menu.value, step: 'kinds', where }
  else menu.value = { ...menu.value, step: 'main' }
}
function onDocPointer(event: PointerEvent) {
  if (menu.value && !(event.target as HTMLElement)?.closest?.('.eb-anchor, .add-anchor')) menu.value = null
}
function onDocKey(event: KeyboardEvent) { if (event.key === 'Escape' && menu.value) menu.value = null }
onMounted(() => { document.addEventListener('pointerdown', onDocPointer); document.addEventListener('keydown', onDocKey) })
onScopeDispose(() => {
  if (typeof document === 'undefined') return
  document.removeEventListener('pointerdown', onDocPointer)
  document.removeEventListener('keydown', onDocKey)
})
watch(() => [routeProjectId.value, props.cardData?.id], () => { menu.value = null })

const timelineEditor = ref<any>(null)
async function addBlock(kind: BlockKind, anchorId: string | null, where: Where) {
  menu.value = null
  if (isSingleton(kind) && hasKind(form.value.layout, kind)) return
  const block = createBlock(kind)
  insertBlock(form.value.layout, block, anchorId, where)
  await nextTick()
  const el = root.value?.querySelector<HTMLElement>(`[data-block-id="${block.id}"]`)
  el?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  if (kind === 'text') el?.querySelector<HTMLInputElement>('.eb-title')?.focus({ preventScroll: true })
  else if (kind === 'media' || kind === 'gallery') pickImages(block.id)
  else if (kind === 'timeline' && !form.value.timelineEvents.length) [timelineEditor.value].flat()[0]?.addEvent?.()
  else if (kind === 'sources') {
    // 新建时先放一条空的，直接就能填（要改 layout 里那份响应式的块）
    const live = findBlock(form.value.layout, block.id)?.block
    if (live) live.sources = [{ id: newId('s'), title: '', url: '', note: '' }]
    await nextTick()
    el?.querySelector<HTMLInputElement>('.src-title-input')?.focus({ preventScroll: true })
  }
}

const KIND_FIELD: Partial<Record<BlockKind, () => boolean>> = {
  description: () => Boolean(form.value.description.trim()),
  gallery: () => form.value.galleryImages.length > 0,
  timeline: () => form.value.timelineEvents.length > 0,
  embeds: () => form.value.contentBlocks.length > 0,
  relations: () => form.value.relations.length > 0,
}
function blockFilled(block: LayoutBlock) {
  if (block.kind === 'text') return Boolean(block.text?.trim())
  if (block.kind === 'media') return Boolean(block.images?.length)
  if (block.kind === 'map') return Boolean(block.image || block.pins?.length)
  if (block.kind === 'sources') return Boolean(block.sources?.some(s => s.title?.trim() || s.url?.trim()))
  return KIND_FIELD[block.kind]?.() ?? false
}
async function deleteBlock(block: LayoutBlock) {
  menu.value = null
  if (blockFilled(block)) {
    try {
      await ElMessageBox.confirm(`删除「${blockTitle(block)}」会把里面的内容一起删掉（保存后才生效），确定吗？`, '删除区块', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' })
    } catch { return }
  }
  // 固定区块的内容存在条目字段里，一起清掉，否则下次打开又会自动出现
  if (block.kind === 'description') form.value.description = ''
  else if (block.kind === 'gallery') form.value.galleryImages = []
  else if (block.kind === 'timeline') form.value.timelineEvents = []
  else if (block.kind === 'embeds') form.value.contentBlocks = []
  else if (block.kind === 'relations') form.value.relations = []
  removeBlock(form.value.layout, block.id)
}

// 文本里输入 / ：在这一块下面加区块，或者在顶部加属性
const slashItems = computed(() => [
  ...BLOCK_KINDS.filter(k => !(isSingleton(k) && hasKind(form.value.layout, k)))
    .map(k => ({ id: 'block:' + k, label: BLOCK_META[k].label, hint: BLOCK_META[k].hint, icon: BLOCK_META[k].icon, group: '内容', keywords: BLOCK_META[k].keywords })),
  { id: 'attr:', label: '属性', hint: '在顶部加一项属性', icon: '≡', group: '内容', keywords: 'shuxing attribute 字段' },
  ...suggestedAttrs.value.map(t => ({ id: 'attr:' + t.key, label: t.key, hint: '属性', icon: '≡', group: `${currentTypeLabel.value}常用属性` })),
])
function runSlash(id: string, anchorId: string) {
  if (id.startsWith('attr:')) return addAttribute(id.slice(5).trim())
  if (id.startsWith('block:')) void addBlock(id.slice(6) as BlockKind, anchorId, 'below')
}

// 图片上传：图库或某个图片块
const fileInput = ref<HTMLInputElement | null>(null)
const galleryInput = ref<HTMLInputElement | null>(null)
const uploadTarget = ref<string | null>(null)
function pickImages(blockId: string) { uploadTarget.value = blockId; galleryInput.value?.click() }
async function handleImages(e: Event) {
  const input = e.target as HTMLInputElement
  const files = input.files
  const target = uploadTarget.value
  if (!files?.length || !target) { input.value = ''; return }
  const urls = await uploadGallery(files)
  input.value = ''
  if (!urls.length) return
  if (target === 'gallery') { form.value.galleryImages.push(...urls); return }
  const found = findBlock(form.value.layout, target)
  if (found?.block.kind === 'media') found.block.images = [...(found.block.images ?? []), ...urls]
}

const triggerFileInput = () => fileInput.value?.click()
const handleFileUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const url = await uploadCover(file)
  if (url) form.value.coverImage = url
  input.value = ''
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
//  4. 入度关系
// ============================================================
const incomingRelations = computed(() => {
  return !isCreating.value && store.currentCard?.id === routeCardId.value ? store.currentCard?.inRelations || [] : []
})

const goToCard = (targetId: string) => {
  if (targetId === routeCardId.value) return
  router.push(`/world/project/${routeProjectId.value}/card/${targetId}`)
}

// ============================================================
//  5. 监听路由
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
.editor-fields { min-width: 0; border: 0; padding: 0; margin: 0; }
.quick-types { display: flex; flex-wrap: wrap; gap: 6px; }.quick-types button { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); padding: 4px 12px; font: inherit; font-size: 13px; cursor: pointer; }.quick-types button[aria-pressed=true] { border-color: var(--accent-color); background: var(--accent-soft); color: var(--accent-color); }
.entry-type { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--text-secondary); }
.entry-type select { padding: 5px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.editor-hint { color: var(--text-secondary); font-size: 13px; line-height: 1.7; margin: 12px 0 20px; }
.draft-recovery { margin: 0 0 24px; padding: 20px; border: 1px solid var(--border-line); border-radius: 0; background: var(--accent-soft); color: var(--text-primary); overflow-wrap: anywhere; }.draft-recovery .cancel-btn { border: 1px solid var(--border-line); border-radius: 0; padding: 9px 16px; background: var(--paper-card); color: var(--text-primary); font: inherit; cursor: pointer; }
.draft-recovery p { font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.draft-picker { display: grid; gap: 8px; margin-top: 12px; }.draft-picker select { width: 100%; min-width: 0; padding: 8px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: 0; }
.conflict-review { scroll-margin-top: 16px; }.conflict-versions :deep(.ref-help), .draft-preview :deep(.ref-help) { display: none; }.draft-preview { margin-top: 12px; }.draft-preview summary { cursor: pointer; margin-bottom: 12px; }
.conflict-field { min-width: 0; border: 1px solid var(--border-line); border-radius: 0; margin: 16px 0; padding: 12px; }.conflict-versions { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }.conflict-versions > div { min-width: 0; }.conflict-versions label { display: flex; gap: 8px; align-items: center; margin-bottom: 12px; }.conflict-versions pre { white-space: pre-wrap; overflow-wrap: anywhere; max-height: 260px; overflow-y: auto; font: inherit; font-size: 13px; }.conflict-versions :deep(.ProseMirror) { max-height: 260px; overflow-y: auto; }
@media (max-width: 720px) { .conflict-versions { grid-template-columns: 1fr; } }
.save-state { color: var(--accent-color); font-size: 12px; }
.leave-actions { display: flex; flex-wrap: wrap; gap: 8px; justify-content: flex-end; }
.leave-actions :deep(.el-button) { margin-left: 0; }

/* ===== 顶部 ===== */
.ed-hero { display: grid; grid-template-columns: minmax(0, 1fr) 180px; gap: 24px; margin-bottom: 28px; padding: 20px 22px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); }
.ed-main { min-width: 0; display: flex; flex-direction: column; gap: 12px; }
.ed-main .doc-title-input { margin-bottom: 0; }
.ed-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 14px; }
.chip-row { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 13px; }
.chip-label { display: inline-flex; align-items: center; gap: 6px; min-width: 56px; color: var(--text-secondary); }
.chip-label i { width: 14px; text-align: center; font-style: normal; }
.chip { display: inline-flex; align-items: center; gap: 2px; padding: 1px 4px 1px 8px; border: 1px solid var(--border-line); color: var(--text-primary); }
.chip.tag { color: var(--accent-color); background: var(--accent-soft); border-color: transparent; }
.chip button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; padding: 0 3px; }
.chip button:hover { color: var(--danger); }
.chip-input { width: 96px; border: 1px dashed var(--border-line); background: none; color: var(--text-primary); font: inherit; font-size: 12px; padding: 2px 8px; outline: none; }
.chip-input:focus { width: 160px; border-style: solid; border-color: var(--accent-color); }
.type-fields { padding: 10px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); }
.prop-grid-edit { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 6px 16px; }
.prop-cell { display: grid; grid-template-columns: minmax(56px, 0.8fr) minmax(0, 1.6fr) 58px 20px; gap: 4px; align-items: start; }
.prop-cell :is(input, textarea, select) { min-width: 0; box-sizing: border-box; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; padding: 4px 6px; border-radius: 0; }
.prop-cell :is(input, textarea, select):focus { outline: none; border-color: var(--accent-color); }
.pe-key { color: var(--text-secondary) !important; }
.pe-val { resize: vertical; }
.pe-type { font-size: 11px !important; padding: 4px 2px !important; color: var(--text-secondary) !important; }
.pe-del { border: 0; background: none; color: var(--text-secondary); cursor: pointer; padding: 4px 0; }
.pe-del:hover { color: var(--danger); }
.prop-add { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.ghost-link { border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; padding: 2px 0; margin-right: 6px; }
.ghost-link:hover { color: var(--accent-color); }
.attr-chip { padding: 2px 8px; border: 1px dashed var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.attr-chip:hover { color: var(--accent-color); border-color: var(--accent-color); }
.ed-cover { min-width: 0; }
.ed-cover .cover-preview { max-width: none; margin: 0; }
.cover-add { width: 100%; aspect-ratio: 3 / 4; border: 1px dashed var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.cover-add:hover { color: var(--accent-color); border-color: var(--accent-color); }

/* ===== 区块 ===== */
.ed-row { display: grid; grid-template-columns: minmax(0, 1fr); gap: 16px; margin-bottom: 16px; }
.ed-row.split { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.ed-block { min-width: 0; padding: 10px 14px 14px; border: 1px solid var(--border-line); background: var(--paper-card); scroll-margin: 16px; }
.ed-block.is-hidden { border-style: dashed; }
.eb-head { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; padding-bottom: 6px; border-bottom: 1px solid var(--border-line); }
.eb-icon { width: 18px; text-align: center; color: var(--accent-color); }
.eb-title { flex: 1; min-width: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 15px; font-weight: 600; padding: 2px 0; outline: none; }
.eb-title::placeholder { color: var(--text-secondary); font-weight: 600; }
.eb-flag { flex-shrink: 0; font-size: 11px; padding: 0 6px; border: 1px dashed var(--border-line); color: var(--text-secondary); }
.eb-anchor, .add-anchor { position: relative; }
.eb-more { width: 28px; height: 28px; border: 1px solid transparent; background: none; color: var(--text-secondary); font-size: 16px; line-height: 1; cursor: pointer; }
.eb-more:hover, .eb-more[aria-expanded=true] { border-color: var(--border-line); background: var(--bg-sub); color: var(--text-primary); }
.eb-menu { position: absolute; right: 0; top: calc(100% + 4px); z-index: 120; width: 230px; padding: 4px 0; background: var(--paper-card); border: 1px solid var(--border-line); box-shadow: 0 12px 32px rgba(0, 0, 0, .16); }
.eb-menu.up { left: 0; right: auto; top: calc(100% + 6px); width: 280px; }
.eb-menu button { white-space: nowrap; width: 100%; display: flex; align-items: center; gap: 8px; padding: 7px 12px; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
.eb-menu button:hover:not(:disabled) { background: var(--accent-soft); }
.eb-menu button:disabled { opacity: .45; cursor: not-allowed; }
.eb-menu button > span { width: 18px; text-align: center; color: var(--accent-color); flex-shrink: 0; }
.eb-menu button em { margin-left: auto; font-style: normal; color: var(--text-secondary); }
.eb-menu button small { margin-left: auto; padding-left: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; color: var(--text-secondary); }
.eb-menu button.danger { color: var(--danger); }
.eb-menu button.danger > span { color: var(--danger); }
.eb-menu .eb-back { color: var(--text-secondary); font-size: 12px; }
.eb-menu hr { margin: 4px 0; border: 0; border-top: 1px solid var(--border-line); }
.media-edit { display: grid; grid-template-columns: repeat(auto-fill, minmax(100px, 1fr)); gap: 8px; }
.media-add { aspect-ratio: 1; border: 1px dashed var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.media-add:hover { color: var(--accent-color); border-color: var(--accent-color); }
.add-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin: 8px 0 28px; }
.add-trigger { display: inline-flex; align-items: center; gap: 6px; padding: 8px 16px; border: 1px dashed var(--accent-color); background: var(--accent-soft); color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.add-trigger:hover, .add-trigger[aria-expanded=true] { background: var(--accent-color); color: var(--on-accent); }
.add-tip { font-size: 12px; color: var(--text-secondary); }
.add-tip kbd { padding: 0 5px; border: 1px solid var(--border-line); font: inherit; font-size: 11px; }
@media (max-width: 760px) {
  .ed-hero { grid-template-columns: minmax(0, 1fr); }
  .ed-cover { order: -1; max-width: 200px; }
  .cover-add { aspect-ratio: auto; padding: 14px; }
  .ed-row.split, .prop-grid-edit { grid-template-columns: minmax(0, 1fr); }
  .add-tip { display: none; }
  .eb-menu, .eb-menu.up { position: fixed; left: 16px; right: 16px; top: auto; bottom: 16px; width: auto; max-height: 70vh; overflow-y: auto; }
}
</style>
