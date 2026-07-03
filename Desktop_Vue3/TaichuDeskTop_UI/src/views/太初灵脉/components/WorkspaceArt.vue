<template>
  <div class="workspace-art-frame">
    <header class="art-header">
      <input :value="props.title" @input="onTitleInput" class="art-title-input" placeholder="作品名称 / Title" />

      <!-- 作品集元数据配置栏 -->
      <div class="art-meta-bar">
        <div class="meta-field">
          <label>作者</label>
          <input v-model="collectionMeta.author" @input="triggerNotify" class="meta-input" placeholder="输入作者名" />
        </div>
        <div class="meta-field">
          <label>状态</label>
          <span class="status-badge" :class="collectionMeta.status">{{ collectionMeta.status }}</span>
        </div>
        <div class="meta-field toggle-field">
          <label>水印</label>
          <input type="checkbox" v-model="collectionMeta.watermarkEnabled" @change="triggerNotify" />
        </div>
      </div>

      <!-- ========== 高级水印设置（折叠） ========== -->
      <div class="watermark-advanced-settings">
        <button class="toggle-settings-btn" @click="showWatermarkSettings = !showWatermarkSettings">
          {{ showWatermarkSettings ? '收起水印设置' : '⚙️ 水印高级设置' }}
        </button>

        <div v-show="showWatermarkSettings" class="settings-panel">
          <!-- 🆕 水印类型选择 -->
          <div class="setting-row">
            <label>水印类型</label>
            <select v-model="collectionMeta.watermarkType" @change="triggerNotify" class="meta-select">
              <option value="text">文字水印</option>
              <option value="image">图片水印</option>
              <option value="both">文字 + 图片</option>
            </select>
          </div>

          <!-- 🆕 图片水印上传（当类型为 image 或 both 时显示） -->
          <div v-if="['image', 'both'].includes(collectionMeta.watermarkType)" class="setting-row watermark-image-row">
            <label>水印图片</label>
            <div class="watermark-image-upload">
              <div v-if="collectionMeta.watermarkImageUrl" class="watermark-image-preview">
                <img :src="collectionMeta.watermarkImageUrl" alt="水印图片" />
                <button class="remove-watermark-img" @click="removeWatermarkImage">✕</button>
              </div>
              <button v-else class="upload-watermark-btn" @click="triggerWatermarkUpload">
                📤 上传 Logo/签名
              </button>
              <input ref="watermarkFileInput" type="file" accept="image/*" style="display: none" @change="handleWatermarkFileSelected" />
            </div>
          </div>

          <!-- 🆕 图片水印参数（当有水印图片时显示） -->
          <template v-if="collectionMeta.watermarkImageUrl && ['image', 'both'].includes(collectionMeta.watermarkType)">
            <div class="setting-row">
              <label>缩放比例</label>
              <input type="range" min="0.05" max="1" step="0.05" v-model.number="collectionMeta.watermarkImageScale" @input="triggerNotify" />
              <span class="range-value">{{ (collectionMeta.watermarkImageScale * 100).toFixed(0) }}%</span>
            </div>
            <div class="setting-row">
              <label>透明度</label>
              <input type="range" min="0.1" max="1" step="0.05" v-model.number="collectionMeta.watermarkImageOpacity" @input="triggerNotify" />
              <span class="range-value">{{ collectionMeta.watermarkImageOpacity.toFixed(2) }}</span>
            </div>
          </template>

          <!-- 文字水印配置（当类型为 text 或 both 时显示） -->
          <template v-if="['text', 'both'].includes(collectionMeta.watermarkType)">
            <div class="setting-row">
              <label>水印文字</label>
              <input v-model="collectionMeta.watermarkText" @input="triggerNotify" class="meta-input" />
            </div>
            <div class="setting-row">
              <label>位置</label>
              <select v-model="collectionMeta.watermarkPosition" @change="triggerNotify" class="meta-select">
                <option value="top-left">左上</option>
                <option value="top-center">上中</option>
                <option value="top-right">右上</option>
                <option value="center-left">左中</option>
                <option value="center">正中</option>
                <option value="center-right">右中</option>
                <option value="bottom-left">左下</option>
                <option value="bottom-center">下中</option>
                <option value="bottom-right" selected>右下</option>
              </select>
            </div>
            <div class="setting-row">
              <label>透明度</label>
              <input type="range" min="0.1" max="1" step="0.05" v-model.number="collectionMeta.watermarkOpacity" @input="triggerNotify" />
              <span class="range-value">{{ collectionMeta.watermarkOpacity.toFixed(2) }}</span>
            </div>
            <div class="setting-row">
              <label>字号 (px)</label>
              <input type="number" min="8" max="48" v-model.number="collectionMeta.watermarkFontSize" @input="triggerNotify" class="meta-input number-input" />
            </div>
            <div class="setting-row">
              <label>颜色</label>
              <input type="color" v-model="collectionMeta.watermarkColor" @change="triggerNotify" class="color-picker" />
            </div>
            <div class="setting-row">
              <label>旋转角度 (°)</label>
              <input type="range" min="-45" max="45" step="1" v-model.number="collectionMeta.watermarkRotation" @input="triggerNotify" />
              <span class="range-value">{{ collectionMeta.watermarkRotation }}°</span>
            </div>
          </template>
        </div>
      </div>

      <p class="art-subtitle">以图叙事的灵动画廊</p>
    </header>

    <!-- ========== 画廊图片列表 ========== -->
    <div class="gallery-container">
      <div
        v-for="(image, idx) in localImages"
        :key="image.id"
        class="art-card"
        :class="{ 'is-dragging': dragIndex === idx }"
        draggable="true"
        @dragstart="handleDragStart($event, idx)"
        @dragover="handleDragOver($event, idx)"
        @dragend="handleDragEnd"
      >
        <div class="card-drag-handle">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.5">
            <circle cx="9" cy="12" r="1.5" />
            <circle cx="15" cy="12" r="1.5" />
            <circle cx="9" cy="16" r="1.5" />
            <circle cx="15" cy="16" r="1.5" />
            <circle cx="9" cy="8" r="1.5" />
            <circle cx="15" cy="8" r="1.5" />
          </svg>
        </div>
        <div class="card-image-area">
          <img v-if="image.url" :src="image.url" class="card-image" :alt="`作品图 ${idx + 1}`" />
          <div v-else class="image-placeholder">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#ccc">
              <rect x="3" y="3" width="18" height="18" rx="2" stroke-width="1.5" />
              <circle cx="8.5" cy="8.5" r="2.5" stroke-width="1.5" />
              <path d="M21 15L16 10L5 21" stroke-width="1.5" />
            </svg>
          </div>
          <!-- 🆕 水印预览层（支持图片+文字） -->
          <div
            v-if="collectionMeta.watermarkEnabled && image.url"
            class="watermark-preview-layer"
            :style="getWatermarkStyle()"
          >
            <span v-if="['text', 'both'].includes(collectionMeta.watermarkType)">
              {{ collectionMeta.watermarkText }}
            </span>
          </div>
          <button class="upload-overlay" @click="triggerImageUpload(idx)">
            <span>{{ image.url ? '更换图片' : '上传图片' }}</span>
          </button>
          <button v-if="image.url" class="remove-image-btn" @click="removeImage(idx)">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M18 6L6 18M6 6l12 12" stroke-width="1.5" />
            </svg>
          </button>
        </div>
        <div class="card-caption-area">
          <textarea
            :value="image.caption"
            @input="updateImageCaption(idx, $event)"
            class="caption-textarea"
            placeholder="记录这张图的创作思路、技法或感悟..."
            rows="3"
          />
        </div>
        <div class="card-badge">{{ idx + 1 }}</div>
      </div>
      <button class="add-card-btn" @click="addNewImageCard">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor">
          <path d="M12 5v14M5 12h14" stroke-width="1.5" />
        </svg>
        <span>添加新画幅</span>
      </button>
    </div>

    <article class="art-editor-slot-area">
      <slot name="editor"></slot>
    </article>

    <div class="art-summary-section">
      <div class="summary-label"><span>✨ 创作总览快照（归档总结）</span></div>
      <textarea
        :value="localSummary"
        @input="onSummaryInput"
        class="summary-textarea"
        placeholder="为这组作品写下完整的总结、创作感悟或技法解析..."
        rows="5"
      />
    </div>

    <!-- 文件上传 input（图片上传） -->
    <input ref="fileInputRef" type="file" accept="image/*" style="display: none" @change="handleFileSelected" />
    <!-- 🆕 水印图片上传 input -->
    <input ref="watermarkFileInput" type="file" accept="image/*" style="display: none" @change="handleWatermarkFileSelected" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted, nextTick, onMounted } from 'vue'
import type { CSSProperties } from 'vue'
import { useSpiritData } from '@/composables/useSpiritData'
import { useCos } from '@/composables/useCos'

// ----- 类型定义 -----
interface ArtImage {
  id: string
  url: string
  caption: string
  sortOrder: number
}

type WatermarkType = 'text' | 'image' | 'both'
type WatermarkPosition = 'top-left' | 'top-center' | 'top-right' | 'center-left' | 'center' | 'center-right' | 'bottom-left' | 'bottom-center' | 'bottom-right'

interface CollectionMeta {
  author: string
  createdAt: string
  updatedAt: string
  copyright: string
  status: 'draft' | 'review' | 'published' | 'archived'
  coverImageIndex: number

  // 水印配置
  watermarkEnabled: boolean
  watermarkType: WatermarkType

  // 文字水印
  watermarkText: string
  watermarkPosition: WatermarkPosition
  watermarkFontSize: number
  watermarkOpacity: number
  watermarkColor: string
  watermarkRotation: number

  // 图片水印
  watermarkImageUrl: string
  watermarkImageWidth: number
  watermarkImageHeight: number
  watermarkImageScale: number
  watermarkImageOpacity: number
}

// ----- Props & Emits -----
const props = defineProps<{
  title: string
  noteId?: string
  extraData?: string
}>()

const emit = defineEmits(['update:title', 'change'])

const { activeNote } = useSpiritData()
const { uploadFile } = useCos()

// ----- 响应式状态 -----
const localImages = ref<ArtImage[]>([])
const localSummary = ref('')
const fileInputRef = ref<HTMLInputElement>()
const watermarkFileInput = ref<HTMLInputElement>() // 🆕 水印图片上传
const showWatermarkSettings = ref(false)

// 作品集元数据（扩展水印类型）
const collectionMeta = ref<CollectionMeta>({
  author: '',
  createdAt: '',
  updatedAt: '',
  copyright: '',
  status: 'draft',
  coverImageIndex: 0,

  watermarkEnabled: true,
  watermarkType: 'text',
  watermarkText: '',
  watermarkPosition: 'bottom-right',
  watermarkFontSize: 14,
  watermarkOpacity: 0.6,
  watermarkColor: '#ffffff',
  watermarkRotation: 0,

  watermarkImageUrl: '',
  watermarkImageWidth: 120,
  watermarkImageHeight: 120,
  watermarkImageScale: 0.3,
  watermarkImageOpacity: 0.6,
})

let pendingImageIndex: number | null = null
let dragIndex: number | null = null
let saveTimer: any = null
let isInitialized = false

// ----- 辅助函数 -----
const getCurrentUser = () => {
  try {
    const userStr = localStorage.getItem('user') || '{}'
    const user = JSON.parse(userStr)
    return user.username || '无名创作者'
  } catch {
    return '无名创作者'
  }
}

const setDefaultMeta = () => {
  const userName = getCurrentUser()
  const now = new Date().toISOString()
  collectionMeta.value = {
    author: userName,
    createdAt: now,
    updatedAt: now,
    copyright: `© ${new Date().getFullYear()} 太初灵脉`,
    status: 'draft',
    coverImageIndex: 0,

    watermarkEnabled: true,
    watermarkType: 'text',
    watermarkText: userName,
    watermarkPosition: 'bottom-right',
    watermarkFontSize: 14,
    watermarkOpacity: 0.6,
    watermarkColor: '#ffffff',
    watermarkRotation: 0,

    watermarkImageUrl: '',
    watermarkImageWidth: 120,
    watermarkImageHeight: 120,
    watermarkImageScale: 0.3,
    watermarkImageOpacity: 0.6,
  }
}

const loadCollectionMeta = () => {
  const note = activeNote.value as any
  if (!note || !Array.isArray(note.blocks)) {
    setDefaultMeta()
    return
  }
  const metaBlock = note.blocks.find((b: any) => b.type === 'art-collection-meta')
  if (metaBlock) {
    try {
      const data = JSON.parse(metaBlock.data)
      collectionMeta.value = {
        ...collectionMeta.value,
        ...data,
        // 为新字段提供后备值
        watermarkType: data.watermarkType || 'text',
        watermarkImageUrl: data.watermarkImageUrl || '',
        watermarkImageWidth: data.watermarkImageWidth ?? 120,
        watermarkImageHeight: data.watermarkImageHeight ?? 120,
        watermarkImageScale: data.watermarkImageScale ?? 0.3,
        watermarkImageOpacity: data.watermarkImageOpacity ?? 0.6,
      }
      if (!collectionMeta.value.author) collectionMeta.value.author = getCurrentUser()
      if (!collectionMeta.value.watermarkText) collectionMeta.value.watermarkText = collectionMeta.value.author
      if (!collectionMeta.value.copyright)
        collectionMeta.value.copyright = `© ${new Date().getFullYear()} 太初灵脉`
    } catch {
      setDefaultMeta()
    }
  } else {
    setDefaultMeta()
  }
}

// ========== 🆕 水印样式计算（支持图片水印） ==========
const getWatermarkStyle = (): CSSProperties => {
  const pos = collectionMeta.value.watermarkPosition
  const map: Record<string, { top?: string; bottom?: string; left?: string; right?: string; transform?: string }> = {
    'top-left': { top: '10px', left: '10px' },
    'top-center': { top: '10px', left: '50%', transform: 'translateX(-50%)' },
    'top-right': { top: '10px', right: '10px' },
    'center-left': { top: '50%', left: '10px', transform: 'translateY(-50%)' },
    center: { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' },
    'center-right': { top: '50%', right: '10px', transform: 'translateY(-50%)' },
    'bottom-left': { bottom: '10px', left: '10px' },
    'bottom-center': { bottom: '10px', left: '50%', transform: 'translateX(-50%)' },
    'bottom-right': { bottom: '10px', right: '10px' },
  }

  const base = map[pos] || map['bottom-right']
  const isImageType = ['image', 'both'].includes(collectionMeta.value.watermarkType)

  // ----- 图片水印模式 -----
  if (isImageType && collectionMeta.value.watermarkImageUrl) {
    const width = collectionMeta.value.watermarkImageWidth * collectionMeta.value.watermarkImageScale
    const height = collectionMeta.value.watermarkImageHeight * collectionMeta.value.watermarkImageScale

    return {
      top: base.top,
      bottom: base.bottom,
      left: base.left,
      right: base.right,
      width: `${width}px`,
      height: `${height}px`,
      backgroundImage: `url(${collectionMeta.value.watermarkImageUrl})`,
      backgroundSize: 'contain',
      backgroundRepeat: 'no-repeat',
      backgroundPosition: 'center',
      opacity: collectionMeta.value.watermarkImageOpacity,
      pointerEvents: 'none' as const,
      zIndex: 4,
    }
  }

  // ----- 文字水印模式 -----
  const rotation = collectionMeta.value.watermarkRotation || 0
  const transform = base.transform ? `${base.transform} rotate(${rotation}deg)` : `rotate(${rotation}deg)`

  return {
    top: base.top,
    bottom: base.bottom,
    left: base.left,
    right: base.right,
    fontSize: `${collectionMeta.value.watermarkFontSize}px`,
    opacity: collectionMeta.value.watermarkOpacity,
    color: collectionMeta.value.watermarkColor,
    transform,
    textShadow: '0 0 8px rgba(0,0,0,0.5)',
    pointerEvents: 'none' as const,
    zIndex: 4,
  }
}

// ========== 🆕 水印图片上传 ==========
const triggerWatermarkUpload = () => {
  watermarkFileInput.value?.click()
}

const handleWatermarkFileSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !file.type.startsWith('image/')) {
    input.value = ''
    return
  }

  try {
    const result = await uploadFile(file, 'watermark')
    if (result?.url) {
      collectionMeta.value.watermarkImageUrl = result.url
      // 获取图片真实尺寸
      const img = new Image()
      img.onload = () => {
        collectionMeta.value.watermarkImageWidth = img.width
        collectionMeta.value.watermarkImageHeight = img.height
        triggerNotify()
      }
      img.onerror = () => {
        // 如果加载失败，使用默认尺寸
        collectionMeta.value.watermarkImageWidth = 120
        collectionMeta.value.watermarkImageHeight = 120
        triggerNotify()
      }
      img.src = result.url
    }
  } catch (err) {
    console.error('水印图片上传失败', err)
  } finally {
    input.value = ''
  }
}

const removeWatermarkImage = () => {
  if (confirm('确定移除水印图片吗？')) {
    collectionMeta.value.watermarkImageUrl = ''
    if (collectionMeta.value.watermarkType === 'image') {
      collectionMeta.value.watermarkType = 'text'
    }
    triggerNotify()
  }
}

// ----- 核心数据加载与构建 -----
const loadFromBlocks = () => {
  if (!activeNote.value) return
  const note = activeNote.value as any
  if (!Array.isArray(note.blocks)) return

  const imageBlocks = note.blocks
    .filter((block: any) => block.type === 'image')
    .sort((a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0))

  localImages.value = imageBlocks.map((block: any) => {
    let data = {}
    try {
      data = JSON.parse(block.data || '{}')
    } catch {}
    const attrs = (data as any).attrs || {}
    return {
      id: block.id,
      url: attrs.src || '',
      caption: attrs.caption || '',
      sortOrder: block.sortOrder ?? 0,
    }
  })

  const summaryBlock = note.blocks.find((block: any) => block.type === 'art-summary')
  if (summaryBlock) {
    try {
      const data = JSON.parse(summaryBlock.data || '{}')
      localSummary.value = data.text || ''
    } catch {}
  } else {
    localSummary.value = ''
  }

  loadCollectionMeta()
}

const buildBlocksFromState = (): any[] => {
  const finalBlocks: any[] = []
  const currentNoteId = props.noteId
  if (!currentNoteId || !activeNote.value) return finalBlocks

  const currentNoteBlocks = activeNote.value.blocks || []

  const pureEditorTextBlocks = currentNoteBlocks.filter(
    (b: any) => b.type !== 'image' && b.type !== 'art-summary' && b.type !== 'art-collection-meta'
  )

  const metaBlock = {
    id: `art_meta_${currentNoteId}`,
    ownerId: currentNoteId,
    ownerType: 'art',
    type: 'art-collection-meta',
    data: JSON.stringify(collectionMeta.value),
    sortOrder: -1,
  }
  finalBlocks.push(metaBlock)

  localImages.value.forEach((img, idx) => {
    const imageData = {
      attrs: { id: img.id, src: img.url, alt: '', caption: img.caption },
      content: [],
    }
    finalBlocks.push({
      id: img.id,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'image',
      data: JSON.stringify(imageData),
      sortOrder: idx,
    })
  })

  if (localSummary.value.trim() || localImages.value.length === 0) {
    const summaryData = { text: localSummary.value, type: 'paragraph' }
    finalBlocks.push({
      id: `art_summary_${currentNoteId}`,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'art-summary',
      data: JSON.stringify(summaryData),
      sortOrder: localImages.value.length,
    })
  }

  const offset = finalBlocks.length
  pureEditorTextBlocks.forEach((b: any, index: number) => {
    b.sortOrder = offset + index
  })

  return [...finalBlocks, ...pureEditorTextBlocks]
}

const notifyChange = () => {
  if (!isInitialized || !activeNote.value) return
  const blocks = buildBlocksFromState()
  activeNote.value.blocks = blocks
  emit('change', { blocks })
}

const triggerNotify = () => {
  if (!isInitialized) return
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(() => {
    notifyChange()
  }, 500)
}

// ----- UI 操作方法 -----
const addNewImageCard = () => {
  const newId = `img_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`
  localImages.value.push({
    id: newId,
    url: '',
    caption: '',
    sortOrder: localImages.value.length,
  })
  triggerNotify()
  nextTick(() => {
    const newIndex = localImages.value.length - 1
    pendingImageIndex = newIndex
    fileInputRef.value?.click()
  })
}

const triggerImageUpload = (index: number) => {
  pendingImageIndex = index
  fileInputRef.value?.click()
}

const handleFileSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !file.type.startsWith('image/') || pendingImageIndex === null) {
    input.value = ''
    pendingImageIndex = null
    return
  }

  const index = pendingImageIndex
  const targetImage = localImages.value[index]
  if (!targetImage) {
    pendingImageIndex = null
    input.value = ''
    return
  }

  try {
    const result = await uploadFile(file, 'artwork')
    if (result?.url) {
      targetImage.url = result.url
      triggerNotify()
    }
  } catch (err) {
    console.error('图片上传失败', err)
  } finally {
    pendingImageIndex = null
    input.value = ''
  }
}

const updateImageCaption = (index: number, event: Event) => {
  const target = event.target as HTMLTextAreaElement
  if (localImages.value[index]) {
    localImages.value[index].caption = target.value
    triggerNotify()
  }
}

const removeImage = (index: number) => {
  if (confirm('确定移除此画幅吗？')) {
    localImages.value.splice(index, 1)
    triggerNotify()
  }
}

const handleDragStart = (e: DragEvent, index: number) => {
  dragIndex = index
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

const handleDragOver = (e: DragEvent, index: number) => {
  e.preventDefault()
  if (dragIndex === null || dragIndex === index) return
  const draggedItem = localImages.value[dragIndex]
  if (draggedItem) {
    const newImages = [...localImages.value]
    newImages.splice(dragIndex, 1)
    newImages.splice(index, 0, draggedItem)
    localImages.value = newImages
    dragIndex = index
    triggerNotify()
  }
}

const handleDragEnd = () => {
  dragIndex = null
}

const onSummaryInput = (e: Event) => {
  localSummary.value = (e.target as HTMLTextAreaElement).value
  triggerNotify()
}

const onTitleInput = (e: Event) => {
  emit('update:title', (e.target as HTMLInputElement).value)
  triggerNotify()
}

// ----- 生命周期与监听 -----
watch(
  () => activeNote.value?.id,
  (newId) => {
    if (!newId || !activeNote.value) return
    if ((activeNote.value as any).blocks !== undefined) {
      loadFromBlocks()
      if (!isInitialized) {
        isInitialized = true
        nextTick(() => {
          notifyChange()
        })
      }
    }
  },
  { immediate: true }
)

onMounted(() => {
  if (activeNote.value && !isInitialized) {
    loadFromBlocks()
    isInitialized = true
    notifyChange()
  }
})

onUnmounted(() => {
  if (saveTimer) clearTimeout(saveTimer)
})
</script>

<style scoped>
/* ========== 原有样式保持不变 ========== */
.workspace-art-frame {
  max-width: 900px;
  margin: 0 auto;
  padding: 40px 24px 80px;
  background: #fefefe;
}

.art-header {
  margin-bottom: 48px;
  text-align: center;
  border-bottom: 1px solid #f0f0f0;
  padding-bottom: 24px;
}

.art-title-input {
  width: 100%;
  font-size: 2.8rem;
  font-weight: 700;
  border: none;
  background: transparent;
  text-align: center;
  font-family: inherit;
  padding: 8px 0;
  letter-spacing: -0.02em;
  color: #1a1a1a;
  transition: all 0.2s;
}
.art-title-input:focus {
  outline: none;
  background: #fafafa;
  border-radius: 16px;
}

.art-meta-bar {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 32px;
  margin-top: 16px;
  flex-wrap: wrap;
}
.meta-field {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
  color: #555;
}
.meta-field label {
  font-weight: 500;
  color: #888;
}
.meta-input {
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 4px 8px;
  font-size: 0.9rem;
  background: transparent;
  transition: border-color 0.2s;
  width: auto;
  min-width: 100px;
}
.meta-input:focus {
  outline: none;
  border-color: #007aff;
}
.meta-input.number-input {
  width: 60px;
}
.meta-select {
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 4px 8px;
  font-size: 0.9rem;
  background: transparent;
}
.status-badge {
  display: inline-block;
  padding: 2px 12px;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  background: #e8e8ed;
  color: #666;
}
.status-badge.draft {
  background: #f0f0f0;
  color: #888;
}
.status-badge.review {
  background: #fff3cd;
  color: #856404;
}
.status-badge.published {
  background: #d4edda;
  color: #155724;
}
.status-badge.archived {
  background: #f8d7da;
  color: #721c24;
}
.toggle-field input[type='checkbox'] {
  width: 16px;
  height: 16px;
  cursor: pointer;
  accent-color: #007aff;
}

.art-subtitle {
  font-size: 0.85rem;
  color: #aaa;
  margin-top: 8px;
  letter-spacing: 0.3px;
}

/* ========== 水印高级设置 ========== */
.watermark-advanced-settings {
  margin-top: 12px;
  text-align: left;
}
.toggle-settings-btn {
  background: none;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  padding: 4px 16px;
  font-size: 0.85rem;
  cursor: pointer;
  color: #555;
  transition: all 0.2s;
}
.toggle-settings-btn:hover {
  border-color: #007aff;
  color: #007aff;
}
.settings-panel {
  margin-top: 12px;
  padding: 16px;
  background: #f8f8fa;
  border-radius: 8px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px 24px;
  border: 1px solid #eaeaea;
}
.setting-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
}
.setting-row label {
  font-weight: 500;
  color: #666;
  min-width: 60px;
}
.setting-row input[type='range'] {
  flex: 1;
  min-width: 80px;
  accent-color: #007aff;
}
.setting-row .range-value {
  min-width: 36px;
  text-align: center;
  color: #333;
}
.color-picker {
  width: 30px;
  height: 30px;
  padding: 0;
  border: none;
  cursor: pointer;
  background: transparent;
}

/* ========== 🆕 水印图片上传 ========== */
.watermark-image-row {
  grid-column: 1 / -1;
}
.watermark-image-upload {
  display: flex;
  align-items: center;
  gap: 12px;
}
.watermark-image-preview {
  position: relative;
  width: 80px;
  height: 80px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid #e0e0e0;
  background: #f5f5f7;
}
.watermark-image-preview img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.remove-watermark-img {
  position: absolute;
  top: 2px;
  right: 2px;
  background: rgba(0, 0, 0, 0.6);
  border: none;
  color: white;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  cursor: pointer;
  font-size: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.remove-watermark-img:hover {
  background: #e5484d;
}
.upload-watermark-btn {
  padding: 8px 16px;
  border: 2px dashed #d0d0d5;
  border-radius: 8px;
  background: transparent;
  cursor: pointer;
  font-size: 0.85rem;
  color: #666;
  transition: all 0.2s;
}
.upload-watermark-btn:hover {
  border-color: #007aff;
  color: #007aff;
  background: rgba(0, 122, 255, 0.05);
}

/* ========== 图片卡片与水印预览层 ========== */
.gallery-container {
  display: flex;
  flex-direction: column;
  gap: 40px;
  margin-bottom: 48px;
}
.art-card {
  display: flex;
  flex-direction: column;
  background: #ffffff;
  border-radius: 28px;
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.04), 0 0 0 1px rgba(0, 0, 0, 0.02);
  transition: all 0.3s cubic-bezier(0.2, 0, 0, 1);
  position: relative;
  cursor: grab;
}
.art-card.is-dragging {
  opacity: 0.5;
  cursor: grabbing;
}
.art-card:hover {
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.08), 0 0 0 1px rgba(0, 0, 0, 0.05);
  transform: translateY(-2px);
}
.card-drag-handle {
  position: absolute;
  top: 16px;
  left: 16px;
  color: #bbb;
  cursor: grab;
  z-index: 10;
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(4px);
  border-radius: 100px;
  padding: 4px;
  transition: color 0.2s;
}
.art-card:hover .card-drag-handle {
  color: #888;
}
.card-image-area {
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  border-radius: 24px 24px 16px 16px;
  overflow: hidden;
  background: #f5f5f7;
  display: flex;
  align-items: center;
  justify-content: center;
}
.card-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.4s ease;
}
.art-card:hover .card-image {
  transform: scale(1.02);
}
.image-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #f9f9fb 0%, #efeff4 100%);
  color: #ccc;
}

/* 🆕 水印预览层（支持图片+文字） */
.watermark-preview-layer {
  position: absolute;
  font-weight: 700;
  letter-spacing: 0.05em;
  white-space: nowrap;
  pointer-events: none;
  z-index: 4;
  display: flex;
  align-items: center;
  justify-content: center;
  background-size: contain;
  background-repeat: no-repeat;
  background-position: center;
  text-shadow: 0 0 8px rgba(0, 0, 0, 0.5);
  user-select: none;
}

.upload-overlay {
  position: absolute;
  bottom: 16px;
  right: 16px;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(12px);
  border: none;
  color: white;
  padding: 6px 14px;
  border-radius: 40px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s;
  z-index: 5;
}
.card-image-area:hover .upload-overlay {
  opacity: 1;
}
.remove-image-btn {
  position: absolute;
  top: 16px;
  right: 16px;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(8px);
  border: none;
  color: white;
  width: 32px;
  height: 32px;
  border-radius: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s, background 0.2s;
  z-index: 5;
}
.card-image-area:hover .remove-image-btn {
  opacity: 1;
}
.remove-image-btn:hover {
  background: #e5484d;
}
.card-caption-area {
  padding: 20px 24px;
}
.caption-textarea {
  width: 100%;
  border: none;
  background: #fafafc;
  border-radius: 20px;
  padding: 16px 20px;
  font-size: 0.95rem;
  line-height: 1.5;
  font-family: inherit;
  color: #2c2c2e;
  resize: vertical;
  transition: background 0.2s;
  border: 1px solid transparent;
}
.caption-textarea:focus {
  outline: none;
  background: #ffffff;
  border-color: #e1e1e6;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
}
.card-badge {
  position: absolute;
  top: 16px;
  right: 20px;
  font-size: 12px;
  font-weight: 500;
  color: #aaa;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(4px);
  padding: 4px 10px;
  border-radius: 40px;
  letter-spacing: 0.3px;
}
.add-card-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  background: transparent;
  border: 2px dashed #d9d9df;
  border-radius: 28px;
  padding: 32px 20px;
  cursor: pointer;
  color: #8e8e93;
  font-size: 1rem;
  font-weight: 500;
  transition: all 0.2s;
  margin-top: 16px;
}
.add-card-btn:hover {
  border-color: #007aff;
  color: #007aff;
  background: #f5f9ff;
}
.art-editor-slot-area {
  margin: 24px 0 48px;
  min-height: 200px;
}
.art-summary-section {
  margin-top: 48px;
  border-top: 2px solid #f2f2f5;
  padding-top: 40px;
}
.summary-label {
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: #aaa;
  margin-bottom: 20px;
}
.summary-textarea {
  width: 100%;
  border: none;
  background: #fafafc;
  border-radius: 28px;
  padding: 24px 28px;
  font-size: 1rem;
  line-height: 1.6;
  font-family: inherit;
  color: #1d1d1f;
  resize: vertical;
  transition: all 0.2s;
}
.summary-textarea:focus {
  outline: none;
  background: #ffffff;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.02), 0 0 0 2px #f0f0f5;
}

@media (max-width: 768px) {
  .workspace-art-frame {
    padding: 20px 16px 60px;
  }
  .art-title-input {
    font-size: 2rem;
  }
  .art-meta-bar {
    gap: 16px;
    flex-direction: column;
    align-items: stretch;
  }
  .settings-panel {
    grid-template-columns: 1fr;
  }
  .gallery-container {
    gap: 28px;
  }
  .card-caption-area {
    padding: 16px;
  }
  .caption-textarea {
    font-size: 0.9rem;
    padding: 12px 16px;
  }
  .add-card-btn {
    padding: 24px 16px;
  }
}
</style>