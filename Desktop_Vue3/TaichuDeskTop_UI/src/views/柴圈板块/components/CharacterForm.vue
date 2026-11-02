<template>
  <!-- OC 设定工作台：左边写设定（身份、属性、图库），右边实时预览、完成度、发布设置和保存。
       表单只包住左边的字段；父组件可以往 #extra（左栏最下面，比如能力面板）和 #aside（右栏，比如世界观）塞东西，
       它们不在 <form> 里，里面按回车不会误提交 OC。 -->
  <div class="oc-studio">
    <div class="oc-main">
      <form :id="formId" class="character-form" @submit.prevent="handleSubmit">
        <!-- ===== 身份 ===== -->
        <section class="oc-card oc-identity">
          <header class="oc-card-head"><span class="oc-tag">01</span><h3>身份</h3><span class="oc-card-sub">名字、一句话简介、立绘</span></header>
          <div class="identity-grid">
            <div class="cover-slot" :class="{ empty: !form.coverUrl, busy: uploadingCover }">
              <img v-if="form.coverUrl" :src="form.coverUrl" alt="封面图" />
              <button v-if="!form.coverUrl" type="button" class="cover-empty" :disabled="uploadingCover" @click="uploadCover">
                <span class="cover-plus" aria-hidden="true">＋</span>
                <span>上传立绘 / 封面</span>
                <small>竖图效果最好，400×400 以上</small>
              </button>
              <div v-else class="cover-ops">
                <button type="button" :disabled="uploadingCover" @click="uploadCover">更换</button>
                <button type="button" :disabled="uploadingCover" @click="form.coverUrl = ''">移除</button>
              </div>
              <div v-if="uploadingCover" class="cover-progress" role="status" aria-live="polite">
                <span>上传中 {{ coverProgress }}%</span><i :style="{ width: coverProgress + '%' }"></i>
              </div>
            </div>
            <div class="identity-fields">
              <label class="field">
                <span class="field-label">角色名 <b class="required">*</b></span>
                <input v-model="form.title" class="input-title" aria-label="角色标题" placeholder="给角色取个名字" maxlength="60" required />
              </label>
              <label class="field">
                <span class="field-label">一句话简介 <em>{{ form.description.length }}/200</em></span>
                <textarea v-model="form.description" class="input-desc" aria-label="一句话简介" rows="3" maxlength="200" placeholder="用一两句话让别人记住TA：身份、标志、最打动人的一点"></textarea>
              </label>
            </div>
          </div>
        </section>

        <!-- ===== 属性 ===== -->
        <section class="oc-card">
          <header class="oc-card-head">
            <span class="oc-tag">02</span><h3>角色属性</h3><span class="oc-card-sub">{{ form.attributes.length }} 条</span>
            <span class="grow"></span>
            <button type="button" class="chip-btn" :aria-pressed="importOpen" @click="importOpen = !importOpen">📋 粘贴整段导入</button>
            <button type="button" class="chip-btn primary" @click="addAttribute()">＋ 添加属性</button>
          </header>

          <!-- 常用属性：点一下就加一条（已经有的不再显示） -->
          <div v-if="quickKeys.length" class="quick-keys" aria-label="常用属性">
            <span class="quick-label">常用</span>
            <button v-for="q in quickKeys" :key="q.key" type="button" class="quick-key" @click="addAttribute(q.key, q.type)">＋ {{ q.key }}</button>
          </div>

          <!-- 粘贴整段导入：不用一条条选短文本 / 长文本再填。只会追加（或在确认后替换），不会悄悄改掉已有属性 -->
          <div v-if="importOpen" class="import-box">
            <p class="hint">把写好的设定整段贴进来：「名字：内容」这样的一行变成一条属性；「【背景故事】」「## 背景故事」或「背景故事：」单独一行时，下面的段落都算它的内容；没有标题的段落放进「设定」。短的自动用短文本，长的或分段的自动用长文本。导入后还可以逐条修改。</p>
            <textarea v-model="importText" rows="9" aria-label="粘贴整段设定" :placeholder="importPlaceholder"></textarea>
            <div v-if="importParsed.length" class="import-preview">
              <p class="hint">识别出 {{ importParsed.length }} 条属性：</p>
              <ul>
                <li v-for="(a, i) in importParsed" :key="i">
                  <b>{{ a.key }}</b><span class="import-type">{{ a.type === 'long' ? '长文本' : '短文本' }}</span>
                  <span class="import-value">{{ a.value.length > 60 ? a.value.slice(0, 60) + '…' : a.value }}</span>
                </li>
              </ul>
            </div>
            <div class="import-actions">
              <button type="button" class="chip-btn primary" :disabled="!importParsed.length" @click="applyImport('append')">追加到属性列表</button>
              <button v-if="form.attributes.length" type="button" class="chip-btn" :disabled="!importParsed.length" @click="applyImport('replace')">替换现有属性…</button>
              <button type="button" class="chip-btn" :disabled="!importText.trim()" @click="importAsOne">整段作为一条长文本</button>
              <button type="button" class="chip-btn" @click="importOpen = false">收起</button>
            </div>
          </div>

          <TransitionGroup tag="ol" name="attr" class="attr-list">
            <li v-for="(attr, index) in form.attributes" :key="attrKey(attr)" class="attr-row" :class="attr.type">
              <span class="attr-no" aria-hidden="true">{{ String(index + 1).padStart(2, '0') }}</span>
              <input v-model="attr.key" class="attr-key" :aria-label="`第 ${index + 1} 条属性的名字`" placeholder="属性名" maxlength="40" />
              <div class="attr-type" role="group" :aria-label="`第 ${index + 1} 条属性的类型`">
                <button type="button" :aria-pressed="attr.type === 'short'" title="短文本：一行，像 性别、年龄" @click="attr.type = 'short'">短</button>
                <button type="button" :aria-pressed="attr.type === 'long'" title="长文本：多段，像 背景故事" @click="attr.type = 'long'">长</button>
              </div>
              <div class="attr-actions">
                <button type="button" :disabled="index === 0" title="上移" :aria-label="`上移第 ${index + 1} 条`" @click="moveAttributeUp(index)">↑</button>
                <button type="button" :disabled="index === form.attributes.length - 1" title="下移" :aria-label="`下移第 ${index + 1} 条`" @click="moveAttributeDown(index)">↓</button>
                <button type="button" class="danger" title="删除" :aria-label="`删除第 ${index + 1} 条`" @click="removeAttribute(index)">×</button>
              </div>
              <input v-if="attr.type === 'short'" v-model="attr.value" class="attr-value" :aria-label="`${attr.key || '属性'}的内容`" placeholder="内容"
                @paste="onShortPaste($event, attr)" />
              <textarea v-else v-model="attr.value" class="attr-value" :aria-label="`${attr.key || '属性'}的内容`" rows="4" placeholder="详细内容，可以分段"></textarea>
            </li>
          </TransitionGroup>
          <div v-if="!form.attributes.length" class="empty-hint">还没有属性：点上面的常用属性，或者「粘贴整段导入」一次填好</div>
        </section>

        <!-- ===== 图库 ===== -->
        <section class="oc-card">
          <header class="oc-card-head"><span class="oc-tag">03</span><h3>图库</h3><span class="oc-card-sub">{{ form.images.length }} 张 · 三视图、表情包、场景图…</span></header>
          <div class="gallery-grid-form">
            <figure v-for="(img, index) in form.images" :key="img.url + index" class="gallery-item-form">
              <img :src="img.url" :alt="img.alt || '图库图片'" />
              <input v-model="img.alt" placeholder="描述（可选）" class="gallery-alt-input" :aria-label="`第 ${index + 1} 张图的描述`" />
              <button type="button" class="btn-remove-small" :aria-label="`删除第 ${index + 1} 张图`" @click="removeImage(index)">×</button>
            </figure>
            <div v-if="uploadingGallery" class="gallery-uploading" role="status">
              <span>上传中 {{ galleryProgress }}%</span><i :style="{ width: galleryProgress + '%' }"></i>
            </div>
            <button type="button" class="gallery-add" :disabled="uploadingGallery" @click="uploadGalleryImage"><span aria-hidden="true">＋</span>添加图片</button>
          </div>
        </section>
      </form>
      <slot name="extra" />
    </div>

    <!-- ===== 右栏：预览 / 完成度 / 发布 / 保存 ===== -->
    <aside class="oc-aside">
      <div class="aside-sticky">
        <section class="oc-card preview-card" aria-label="角色卡预览">
          <div class="pv-cover">
            <img v-if="form.coverUrl" :src="form.coverUrl" alt="" />
            <span v-else class="pv-glyph" aria-hidden="true">{{ [...(form.title.trim() || '柴')][0] }}</span>
            <span class="pv-status" :class="form.status">{{ form.status === 'published' ? '已发布' : '草稿' }}</span>
          </div>
          <div class="pv-body">
            <strong class="pv-name">{{ form.title.trim() || '未命名角色' }}</strong>
            <p class="pv-desc">{{ form.description.trim() || '一句话简介会显示在这里' }}</p>
            <dl v-if="previewAttrs.length" class="pv-attrs">
              <div v-for="a in previewAttrs" :key="a.key"><dt>{{ a.key }}</dt><dd>{{ a.value }}</dd></div>
            </dl>
          </div>
        </section>

        <section class="oc-card progress-card" aria-label="完成度">
          <div class="pg-head"><span class="oc-tag">CHECK</span><span>完成度</span><b>{{ progress.done }}/{{ progress.items.length }}</b></div>
          <div class="pg-bar"><i :style="{ width: progress.done / progress.items.length * 100 + '%' }"></i></div>
          <ul class="pg-list">
            <li v-for="it in progress.items" :key="it.label" :class="{ ok: it.ok }"><span aria-hidden="true">{{ it.ok ? '◆' : '◇' }}</span>{{ it.label }}</li>
          </ul>
        </section>

        <section class="oc-card publish-card" aria-label="发布设置">
          <div class="pub-row">
            <span class="pub-label">状态</span>
            <div class="seg" role="radiogroup" aria-label="状态">
              <button type="button" role="radio" :aria-checked="form.status === 'draft'" @click="form.status = 'draft'">草稿</button>
              <button type="button" role="radio" :aria-checked="form.status === 'published'" @click="form.status = 'published'">发布</button>
            </div>
          </div>
          <p class="pub-hint">{{ form.status === 'published' ? '保存后大家就能在柴圈看到这个 OC' : '草稿只有你自己看得到' }}</p>
          <label class="pub-row switch-row">
            <span class="pub-label">允许参与约战</span>
            <input v-model="form.isBattleEnabled" type="checkbox" class="switch" role="switch" :aria-checked="form.isBattleEnabled" />
          </label>
          <p class="pub-hint">开启后，这个角色可以在约战里被选用</p>
        </section>

        <slot name="aside" />

        <div class="aside-actions wide-only">
          <span v-if="dirty" class="dirty-dot" role="status">● 有没保存的修改</span>
          <button type="button" class="btn-ghost" @click="router.back()">取消</button>
          <button type="submit" :form="formId" class="btn-save" :disabled="loading || uploadingCover || uploadingGallery">{{ loading ? '保存中…' : saveLabel }}</button>
        </div>
      </div>
    </aside>
    <!-- 窄屏：保存条贴在屏幕底部（直接挂在整个工作台下面，往上翻到哪都看得见） -->
    <div class="aside-actions narrow-only">
      <span v-if="dirty" class="dirty-dot" role="status">● 有没保存的修改</span>
      <button type="button" class="btn-ghost" @click="router.back()">取消</button>
      <button type="submit" :form="formId" class="btn-save" :disabled="loading || uploadingCover || uploadingGallery">{{ loading ? '保存中…' : saveLabel }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { useCos } from '@/composables/useCos'
import { computed } from 'vue'
import { parseCharacterText } from './characterTextImport'
import { ElMessage } from 'element-plus'

// ===== 类型定义 =====
interface AttributeInput {
  key: string
  value: string
  type: 'short' | 'long'
}

interface ImageInput {
  url: string
  alt: string
}

interface FormData {
  title: string
  description: string
  coverUrl: string
  status: 'draft' | 'published'
  isBattleEnabled: boolean          // 新增
  attributes: AttributeInput[]
  images: ImageInput[]
}

interface InitialData {
  id?: string
  title: string
  description?: string
  coverUrl?: string
  status: 'draft' | 'published' | 'archived'   // 增加 'archived'
  isBattleEnabled?: boolean
  attributes?: {
    id?: string
    key: string
    value?: string          // 改为可选
    sortOrder: number
    type: 'short' | 'long'
  }[]
  images?: {
    id?: string
    url: string
    alt?: string
    sortOrder: number
  }[]
}

const props = defineProps<{
  initialData?: InitialData
  loading?: boolean
  /** 保存按钮上的字，默认「保存角色」 */
  submitText?: string
}>()

const emit = defineEmits<{
  submit: [data: any]
}>()

const router = useRouter()
const { uploadFile } = useCos()

// ===== 粘贴整段导入 =====
const importOpen = ref(false)
const importText = ref('')
const importPlaceholder = '例如：\n林夜是来自北境的少年剑士。\n\n姓名：林夜\n年龄：17\n性格：外冷内热\n\n【背景故事】\n出生在北境的雪原上……'
const importParsed = computed(() => parseCharacterText(importText.value))
function applyImport(mode: 'append' | 'replace') {
  const items = importParsed.value.map(a => ({ key: a.key, value: a.value, type: a.type }))
  if (!items.length) return
  if (mode === 'replace') {
    // 替换会清掉现在的属性（保存前都还能撤回：不保存就不会改到服务器上的数据）
    if (!confirm(`要用导入的 ${items.length} 条替换现在的 ${form.attributes.length} 条属性吗？现有属性会被清掉（还没保存前，离开页面不保存就能撤回）。`)) return
    form.attributes = items
  } else {
    form.attributes.push(...items)
  }
  importText.value = ''
  importOpen.value = false
}
function importAsOne() {
  const value = importText.value.replace(/\r\n?/g, '\n').trim()
  if (!value) return
  form.attributes.push({ key: '设定', value, type: 'long' })
  importText.value = ''
  importOpen.value = false
}
// 往短文本框里粘贴一大段（带换行或很长）：自动换成长文本，换行也保留（短文本框会吃掉换行）
function onShortPaste(e: ClipboardEvent, attr: AttributeInput) {
  const text = e.clipboardData?.getData('text/plain') ?? ''
  if (!text.includes('\n') && text.length <= 40) return
  e.preventDefault()
  attr.value = (attr.value ? attr.value + '\n' : '') + text.replace(/\r\n?/g, '\n').trim()
  attr.type = 'long'
}

// ===== 上传状态 =====
const uploadingCover = ref(false)
const coverProgress = ref(0)
const uploadingGallery = ref(false)
const galleryProgress = ref(0)

const form = reactive<FormData>({
  title: '',
  description: '',
  coverUrl: '',
  status: 'draft',
  isBattleEnabled: true,   // 默认开启
  attributes: [],
  images: [],
})

onMounted(() => {
  if (props.initialData) {
    form.title = props.initialData.title || ''
    form.description = props.initialData.description || ''
    form.coverUrl = props.initialData.coverUrl || ''
    form.status = props.initialData.status === 'archived' ? 'draft' : (props.initialData.status || 'draft')
    // 如果状态是 archived，强制变为 draft（或保留，但 select 无 archived 选项，所以设为 draft）
    form.isBattleEnabled = props.initialData.isBattleEnabled ?? true

    // ===== 加载属性时按 sortOrder 排序（防御性编程） =====
    if (props.initialData.attributes?.length) {
      const sorted = [...props.initialData.attributes]
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
      form.attributes = sorted.map(a => ({
        key: a.key,
        value: a.value || '',
        type: a.type || 'short'
      }))
    }

    // 图库也按 sortOrder 排序（可选，建议加上）
    if (props.initialData.images?.length) {
      const sortedImages = [...props.initialData.images]
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
      form.images = sortedImages.map(i => ({
        url: i.url,
        alt: i.alt || '',
      }))
    }
  }
})

// ===== 上传封面图 =====
async function uploadCover() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'

  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement
    const file = target.files?.[0]
    if (!file) return

    uploadingCover.value = true
    coverProgress.value = 0

    try {
      const result = await uploadFile(file, 'stickman/cover', p => { coverProgress.value = p })
      form.coverUrl = result.url
    } catch (error) {
      console.error('封面上传失败:', error)
      ElMessage.error('封面上传失败，请重试')
    } finally {
      uploadingCover.value = false
      coverProgress.value = 0
      target.value = ''
    }
  }

  input.click()
}

// ===== 上传图库图片 =====
async function uploadGalleryImage() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'
  input.multiple = true

  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement
    const files = target.files
    if (!files || files.length === 0) return

    uploadingGallery.value = true
    galleryProgress.value = 0

    try {
      for (let i = 0; i < files.length; i++) {
        const file = files[i]
        const result = await uploadFile(file, 'stickman/gallery', p => { galleryProgress.value = Math.round(((i + p / 100) / files.length) * 100) })
        form.images.push({
          url: result.url,
          alt: ''
        })
        galleryProgress.value = Math.round(((i + 1) / files.length) * 100)
      }
    } catch (error) {
      console.error('图库上传失败:', error)
      ElMessage.error('部分图片上传失败，请重试')
    } finally {
      uploadingGallery.value = false
      galleryProgress.value = 0
      target.value = ''
    }
  }

  input.click()
}

// ===== 操作函数 =====
function addAttribute(key = '', type: 'short' | 'long' = 'short') {
  form.attributes.push({ key, value: '', type })
  // 新加的那条直接能打字：有名字就聚焦内容，没名字就聚焦名字
  requestAnimationFrame(() => {
    const rows = document.querySelectorAll<HTMLElement>(`#${formId} .attr-row`)
    const row = rows[rows.length - 1]
    row?.querySelector<HTMLElement>(key ? '.attr-value' : '.attr-key')?.focus()
  })
}

function removeAttribute(index: number) {
  form.attributes.splice(index, 1)
}

// ===== 新增：上移 / 下移属性 =====
function moveAttributeUp(index: number) {
  if (index <= 0) return
  const temp = form.attributes[index - 1]
  form.attributes[index - 1] = form.attributes[index]
  form.attributes[index] = temp
}

function moveAttributeDown(index: number) {
  if (index >= form.attributes.length - 1) return
  const temp = form.attributes[index + 1]
  form.attributes[index + 1] = form.attributes[index]
  form.attributes[index] = temp
}

function removeImage(index: number) {
  form.images.splice(index, 1)
}

function handleSubmit() {
  if (props.loading || uploadingCover.value || uploadingGallery.value) return
  if (!form.title.trim()) {
    ElMessage.warning('请输入角色名')
    return
  }

  // 检查重复 Key
  const keys = form.attributes.map(a => a.key.trim()).filter(k => k)
  const duplicateKeys = keys.filter((k, i) => keys.indexOf(k) !== i)
  if (duplicateKeys.length) {
    ElMessage.warning(`属性名重复：${[...new Set(duplicateKeys)].join('、')}，请修改后重试`)
    return
  }

  const validAttributes = form.attributes
    .filter(a => a.key.trim() && a.value !== undefined)
    .map((a, index) => ({
      key: a.key.trim(),
      value: a.value || '',
      sortOrder: index,
      type: a.type || 'short'
    }))

  const validImages = form.images
    .filter(i => i.url.trim())
    .map((i, index) => ({
      url: i.url.trim(),
      alt: i.alt || '',
      sortOrder: index,
    }))

  const submitData = {
    title: form.title.trim(),
    description: form.description.trim() || undefined,
    coverUrl: form.coverUrl.trim() || undefined,
    status: form.status,
    isBattleEnabled: form.isBattleEnabled,   // 提交
    attributes: validAttributes.length ? validAttributes : undefined,
    images: validImages.length ? validImages : undefined,
  }

  emit('submit', submitData)
}

// ===== 工作台：表单 id、预览、完成度、常用属性、有没有没保存的修改 =====
const formId = `oc-form-${Math.random().toString(36).slice(2, 8)}`
const saveLabel = computed(() => props.submitText || '保存角色')
/** 属性行的 key：同一个对象一直用同一个号，上下移动时动画跟着走 */
const attrIds = new WeakMap<AttributeInput, number>()
let attrSeq = 0
function attrKey(attr: AttributeInput) {
  let id = attrIds.get(attr)
  if (id === undefined) { id = ++attrSeq; attrIds.set(attr, id) }
  return id
}
const QUICK: { key: string; type: 'short' | 'long' }[] = [
  { key: '性别', type: 'short' }, { key: '年龄', type: 'short' }, { key: '种族', type: 'short' }, { key: '身高', type: 'short' },
  { key: '生日', type: 'short' }, { key: '性格', type: 'short' }, { key: '口头禅', type: 'short' }, { key: '外貌', type: 'long' },
  { key: '能力', type: 'long' }, { key: '背景故事', type: 'long' }, { key: '人际关系', type: 'long' },
]
const quickKeys = computed(() => QUICK.filter(q => !form.attributes.some(a => a.key.trim() === q.key)))
const previewAttrs = computed(() => form.attributes.filter(a => a.type === 'short' && a.key.trim() && String(a.value ?? '').trim()).slice(0, 4))
const progress = computed(() => {
  const items = [
    { label: '起了名字', ok: Boolean(form.title.trim()) },
    { label: '写了一句话简介', ok: Boolean(form.description.trim()) },
    { label: '上传了立绘 / 封面', ok: Boolean(form.coverUrl) },
    { label: '至少 3 条属性', ok: form.attributes.filter(a => a.key.trim() && String(a.value ?? '').trim()).length >= 3 },
    { label: '写了长一点的设定（背景、能力…）', ok: form.attributes.some(a => a.type === 'long' && String(a.value ?? '').trim().length >= 30) },
    { label: '图库里有图', ok: form.images.length > 0 },
  ]
  return { items, done: items.filter(i => i.ok).length }
})
const snapshot = () => JSON.stringify(form)
const saved = ref('')
const dirty = computed(() => { const now = snapshot(); return Boolean(saved.value) && now !== saved.value })
onMounted(() => { saved.value = snapshot() })
// 有没保存的修改时关页面 / 刷新先问一句
function onBeforeUnload(e: BeforeUnloadEvent) { if (dirty.value && !props.loading) { e.preventDefault(); e.returnValue = '' } }
window.addEventListener('beforeunload', onBeforeUnload)
onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))
</script>

<style scoped>
/* ========== 布局：左边写，右边看 ========== */
.oc-studio { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 24px; align-items: start; }
.oc-main { display: grid; gap: 20px; min-width: 0; }
.character-form { display: grid; gap: 20px; }
.oc-aside { min-width: 0; }
.aside-sticky { position: sticky; top: 16px; display: grid; gap: 14px; }

/* ========== 卡片 ========== */
.oc-card { position: relative; padding: 18px 20px 20px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 2px solid var(--surface-color, var(--accent-color)); }
.oc-card::after { content: ''; position: absolute; right: -1px; top: -2px; width: 12px; height: 12px; background: linear-gradient(225deg, var(--surface-color, var(--accent-color)) 50%, transparent 50%); pointer-events: none; }
.oc-card-head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 10px; margin-bottom: 14px; }
.oc-card-head h3 { margin: 0; font-family: var(--font-display, inherit); font-size: 17px; font-weight: 600; letter-spacing: .04em; color: var(--text-primary); }
.oc-card-sub { font-size: 12px; color: var(--text-secondary); }
.oc-tag { padding: 1px 7px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .14em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
.grow { flex: 1; }
.hint { margin: 0; color: var(--text-secondary); font-size: 12px; line-height: 1.7; }
.empty-hint { padding: 22px; text-align: center; border: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 13px; }
.required { color: var(--danger); font-weight: 400; }

input, textarea, select { box-sizing: border-box; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; transition: border-color .15s, box-shadow .15s; }
input:focus, textarea:focus, select:focus { outline: none; border-color: var(--accent-color); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent-color) 14%, transparent); }
textarea { resize: vertical; line-height: 1.75; }

.chip-btn { padding: 5px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; transition: border-color .15s, color .15s, translate .15s; }
.chip-btn:hover:not(:disabled), .chip-btn[aria-pressed=true] { border-color: var(--accent-color); color: var(--accent-color); }
.chip-btn.primary { border-color: var(--accent-color); color: var(--accent-color); }
.chip-btn.primary:hover:not(:disabled) { background: var(--accent-soft); translate: 0 -1px; }
.chip-btn:disabled { opacity: .5; cursor: not-allowed; }

/* ========== 身份 ========== */
.identity-grid { display: grid; grid-template-columns: 180px minmax(0, 1fr); gap: 20px; }
.cover-slot { position: relative; aspect-ratio: 3 / 4; overflow: hidden; border: 1px solid var(--border-line);
  background: linear-gradient(var(--grid-line) 1px, transparent 1px) 0 0 / 16px 16px, linear-gradient(90deg, var(--grid-line) 1px, transparent 1px) 0 0 / 16px 16px, var(--bg-sub); }
.cover-slot img { width: 100%; height: 100%; object-fit: cover; display: block; }
.cover-empty { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; border: 1px dashed var(--accent-color); margin: 8px; background: transparent; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.cover-empty:hover { background: color-mix(in srgb, var(--accent-color) 6%, transparent); }
.cover-empty small { color: var(--text-secondary); font-size: 11px; }
.cover-plus { width: 40px; height: 40px; display: grid; place-items: center; border: 1px solid currentColor; font-size: 22px; }
.cover-ops { position: absolute; left: 0; right: 0; bottom: 0; display: flex; gap: 1px; opacity: 0; transition: opacity .2s; }
.cover-slot:hover .cover-ops, .cover-slot:focus-within .cover-ops { opacity: 1; }
.cover-ops button { flex: 1; padding: 7px 0; border: 0; background: color-mix(in srgb, var(--inverse-bg) 82%, transparent); color: var(--inverse-text); font: inherit; font-size: 12px; cursor: pointer; }
.cover-ops button:hover { background: var(--accent-color); color: var(--on-accent); }
.cover-progress { position: absolute; left: 8px; right: 8px; bottom: 8px; padding: 6px 8px; background: var(--paper-card); border: 1px solid var(--border-line); font: 11px var(--font-mono, monospace); }
.cover-progress i, .gallery-uploading i { display: block; height: 3px; margin-top: 4px; background: var(--accent-color); transition: width .2s; }
.identity-fields { display: grid; gap: 14px; align-content: start; }
.field { display: grid; gap: 6px; }
.field-label { display: flex; align-items: baseline; gap: 6px; font-size: 12px; font-weight: 600; color: var(--text-secondary); letter-spacing: .04em; }
.field-label em { margin-left: auto; font: 11px var(--font-mono, monospace); color: var(--text-secondary); opacity: .7; }
.input-title { padding: 10px 12px; font-family: var(--font-display, inherit); font-size: 22px; font-weight: 600; }
.input-desc { padding: 10px 12px; }

/* ========== 属性 ========== */
.quick-keys { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-bottom: 12px; }
.quick-label { font: 11px var(--font-mono, monospace); color: var(--text-secondary); letter-spacing: .08em; margin-right: 2px; }
.quick-key { padding: 3px 10px; border: 1px dashed var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; transition: border-color .15s, color .15s; }
.quick-key:hover { border-style: solid; border-color: var(--accent-color); color: var(--accent-color); }
.import-box { display: grid; gap: 10px; margin: 0 0 14px; padding: 12px; border: 1px dashed var(--accent-color); background: var(--bg-sub); }
.import-box textarea { width: 100%; padding: 10px; }
.import-preview ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; max-height: 220px; overflow: auto; }
.import-preview li { display: flex; gap: 8px; align-items: baseline; font-size: 13px; }
.import-preview li b { flex-shrink: 0; max-width: 8em; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.import-type { flex-shrink: 0; padding: 0 5px; background: var(--paper-card); color: var(--text-secondary); font-size: 11px; }
.import-value { min-width: 0; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.import-actions { display: flex; flex-wrap: wrap; gap: 8px; }

.attr-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
/* 短属性：一行排完（编号 · 名字 · 内容 · 短/长 · 操作）；长属性：名字一行，内容另起一行 */
.attr-row { position: relative; display: grid; grid-template-columns: 28px minmax(84px, 10em) minmax(0, 1fr) auto auto; grid-template-areas: "no key value type ops"; align-items: center; gap: 8px;
  padding: 8px 8px 8px 0; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); transition: border-color .15s, box-shadow .15s; }
.attr-row.long { grid-template-columns: 28px minmax(84px, 1fr) auto auto; grid-template-areas: "no key type ops" ". value value value"; align-items: center; border-left-color: var(--signal-mint); }
.attr-row:focus-within { border-color: var(--accent-color); border-left-color: var(--accent-color); box-shadow: 2px 2px 0 color-mix(in srgb, var(--accent-color) 14%, transparent); }
.attr-no { grid-area: no; text-align: center; font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.attr-key { grid-area: key; padding: 6px 8px; font-weight: 600; font-size: 13px; }
.attr-value { grid-area: value; width: 100%; padding: 6px 8px; font-size: 14px; }
textarea.attr-value { min-height: 96px; }
.attr-type { grid-area: type; display: inline-flex; border: 1px solid var(--border-line); }
.attr-type button { width: 28px; height: 28px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.attr-type button + button { border-left: 1px solid var(--border-line); }
.attr-type button[aria-pressed=true] { background: var(--inverse-bg); color: var(--inverse-text); }
.attr-actions { grid-area: ops; display: inline-flex; gap: 2px; }
.attr-actions button { width: 26px; height: 28px; border: 1px solid transparent; background: transparent; color: var(--text-secondary); font-size: 13px; cursor: pointer; }
.attr-actions button:hover:not(:disabled) { border-color: var(--border-line); color: var(--accent-color); }
.attr-actions button.danger:hover { color: var(--danger); border-color: var(--danger); }
.attr-actions button:disabled { opacity: .3; cursor: not-allowed; }
.attr-move, .attr-enter-active, .attr-leave-active { transition: transform .25s cubic-bezier(.2, .8, .2, 1), opacity .2s; }
.attr-enter-from, .attr-leave-to { opacity: 0; transform: translateX(-8px); }
.attr-leave-active { position: absolute; width: 100%; }

/* ========== 图库 ========== */
.gallery-grid-form { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 10px; }
.gallery-item-form { position: relative; margin: 0; aspect-ratio: 1; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub); }
.gallery-item-form img { width: 100%; height: 100%; object-fit: cover; display: block; transition: scale .4s; }
.gallery-item-form:hover img { scale: 1.05; }
.gallery-alt-input { position: absolute; left: 0; right: 0; bottom: 0; width: 100%; padding: 5px 8px; border: 0; background: color-mix(in srgb, var(--inverse-bg) 78%, transparent); color: var(--inverse-text); font-size: 12px; }
.gallery-alt-input::placeholder { color: color-mix(in srgb, var(--inverse-text) 60%, transparent); }
.btn-remove-small { position: absolute; top: 4px; right: 4px; width: 24px; height: 24px; border: 0; background: color-mix(in srgb, var(--inverse-bg) 80%, transparent); color: var(--inverse-text); font-size: 14px; cursor: pointer; opacity: 0; transition: opacity .15s; }
.gallery-item-form:hover .btn-remove-small, .btn-remove-small:focus-visible { opacity: 1; }
.btn-remove-small:hover { background: var(--danger); }
.gallery-uploading, .gallery-add { aspect-ratio: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; border: 1px dashed var(--border-line); font-size: 12px; color: var(--text-secondary); }
.gallery-uploading { padding: 0 14px; align-items: stretch; text-align: center; font-family: var(--font-mono, monospace); }
.gallery-add { background: transparent; font: inherit; font-size: 12px; cursor: pointer; transition: border-color .15s, color .15s; }
.gallery-add span { font-size: 24px; line-height: 1; }
.gallery-add:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); border-style: solid; }

/* ========== 右栏 ========== */
.preview-card { padding: 0; overflow: hidden; }
.pv-cover { position: relative; aspect-ratio: 4 / 3; background: radial-gradient(circle at 50% 55%, color-mix(in srgb, var(--signal-mint) 22%, transparent), transparent 70%), var(--bg-sub); display: grid; place-items: center; overflow: hidden; }
.pv-cover img { width: 100%; height: 100%; object-fit: cover; }
.pv-glyph { font: 700 64px/1 var(--font-display, inherit); color: color-mix(in srgb, var(--accent-color) 55%, transparent); }
.pv-status { position: absolute; left: 0; top: 10px; padding: 2px 10px; font: 11px/1.6 var(--font-mono, monospace); letter-spacing: .1em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--warning); }
.pv-status.published { border-left-color: var(--signal-mint); }
.pv-body { padding: 12px 14px 14px; display: grid; gap: 6px; }
.pv-name { font-family: var(--font-display, inherit); font-size: 18px; overflow-wrap: anywhere; }
.pv-desc { margin: 0; font-size: 12px; line-height: 1.7; color: var(--text-secondary); display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
.pv-attrs { margin: 4px 0 0; display: grid; grid-template-columns: 1fr 1fr; gap: 4px; }
.pv-attrs div { min-width: 0; padding: 4px 6px; background: var(--bg-sub); border-left: 2px solid var(--accent-color); }
.pv-attrs dt { font-size: 10px; color: var(--text-secondary); }
.pv-attrs dd { margin: 0; font-size: 12px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.progress-card, .publish-card { padding: 14px 16px; }
.pg-head { display: flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 600; }
.pg-head b { margin-left: auto; font: 600 14px var(--font-mono, monospace); color: var(--accent-color); }
.pg-bar { height: 4px; margin: 10px 0; background: var(--bg-sub); overflow: hidden; }
.pg-bar i { display: block; height: 100%; background: linear-gradient(90deg, var(--accent-color), var(--signal-mint)); transition: width .35s cubic-bezier(.2, .8, .2, 1); }
.pg-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.pg-list li { display: flex; gap: 6px; }
.pg-list li span { color: var(--border-line); }
.pg-list li.ok { color: var(--text-primary); }
.pg-list li.ok span { color: var(--signal-mint); }

.pub-row { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.pub-label { font-size: 13px; font-weight: 600; }
.pub-hint { margin: 4px 0 12px; font-size: 12px; color: var(--text-secondary); }
.pub-hint:last-child { margin-bottom: 0; }
.seg { display: inline-flex; border: 1px solid var(--border-line); }
.seg button { padding: 5px 14px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.seg button + button { border-left: 1px solid var(--border-line); }
.seg button[aria-checked=true] { background: var(--accent-color); color: var(--on-accent); }
.switch-row { cursor: pointer; }
.switch { appearance: none; position: relative; width: 40px; height: 22px; margin: 0; border: 1px solid var(--border-line); background: var(--bg-sub); cursor: pointer; transition: background .2s, border-color .2s; }
.switch::after { content: ''; position: absolute; top: 2px; left: 2px; width: 16px; height: 16px; background: var(--text-secondary); transition: translate .2s, background .2s; }
.switch:checked { background: var(--accent-soft); border-color: var(--accent-color); }
.switch:checked::after { translate: 18px 0; background: var(--accent-color); }

.aside-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 14px 16px; background: var(--paper-card); border: 1px solid var(--border-line); }
.dirty-dot { flex-basis: 100%; font-size: 12px; color: var(--warning); }
.btn-ghost { padding: 9px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn-ghost:hover { border-color: var(--text-primary); }
.btn-save { flex: 1; padding: 10px 16px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 14px; font-weight: 600; letter-spacing: .1em; cursor: pointer;
  clip-path: polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px)); transition: box-shadow .15s, translate .15s; }
.btn-save:hover:not(:disabled) { translate: 0 -1px; box-shadow: inset 0 -3px 0 var(--signal-mint); }
.btn-save:disabled { opacity: .55; cursor: not-allowed; }

/* ========== 窄屏：右栏移到下面，保存条贴底 ========== */
.narrow-only { display: none; }
@media (max-width: 1060px) {
  .oc-studio { grid-template-columns: minmax(0, 1fr); }
  .aside-sticky { position: static; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); }
  .wide-only { display: none; }
  .narrow-only { display: flex; position: sticky; bottom: 0; z-index: 5; box-shadow: 0 -6px 18px rgb(0 0 0 / 10%); }
  .narrow-only .dirty-dot { flex-basis: auto; margin-right: auto; }
}
@media (max-width: 640px) {
  .identity-grid { grid-template-columns: minmax(0, 1fr); }
  .cover-slot { max-width: 220px; }
  .oc-card { padding: 14px 12px 16px; }
  .attr-row, .attr-row.long { grid-template-columns: 22px minmax(0, 1fr) auto auto; grid-template-areas: "no key type ops" ". value value value"; }
  .gallery-grid-form { grid-template-columns: repeat(auto-fill, minmax(96px, 1fr)); }
}
@media (prefers-reduced-motion: reduce) { .attr-move, .attr-enter-active, .attr-leave-active, .pg-bar i { transition: none; } }
</style>
