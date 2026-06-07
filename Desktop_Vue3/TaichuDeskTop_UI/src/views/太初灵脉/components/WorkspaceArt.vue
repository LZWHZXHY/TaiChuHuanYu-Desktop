<template>
  <!-- 模板部分不变，与之前相同 -->
  <div class="workspace-art-frame">
    <header class="art-header">
      <input :value="props.title" @input="onTitleInput" class="art-title-input" placeholder="作品名称 / Title" />
      <p class="art-subtitle">以图叙事的灵动画廊</p>
    </header>

    <div class="gallery-container">
      <div v-for="(image, idx) in localImages" :key="image.id" class="art-card" :class="{ 'is-dragging': dragIndex === idx }"
        draggable="true" @dragstart="handleDragStart($event, idx)" @dragover="handleDragOver($event, idx)" @dragend="handleDragEnd">
        <!-- 卡片内容：拖拽手柄、图片区、说明区、角标 -->
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
          <textarea :value="image.caption" @input="updateImageCaption(idx, $event)" class="caption-textarea"
            placeholder="记录这张图的创作思路、技法或感悟..." rows="3" />
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

    <div class="art-summary-section">
      <div class="summary-label"><span>✨ 创作总览</span></div>
      <textarea :value="localSummary" @input="onSummaryInput" class="summary-textarea"
        placeholder="为这组作品写下完整的总结、创作感悟或技法解析..." rows="5" />
    </div>

    <input ref="fileInputRef" type="file" accept="image/*" style="display: none" @change="handleFileSelected" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted, nextTick, onMounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  title: string;
  noteId?: string;
}>();

const emit = defineEmits(['update:title', 'change']);

const { activeNote } = useSpiritData();
const { uploadFile } = useCos();

interface ArtImage {
  id: string;
  url: string;
  caption: string;
  sortOrder: number;
}

const localImages = ref<ArtImage[]>([]);
const localSummary = ref('');
const fileInputRef = ref<HTMLInputElement>();
let pendingImageIndex: number | null = null;
let dragIndex: number | null = null;
let saveTimer: any = null;
let isInitialized = false;  // 🌟 关键标志：初始化完成前不发送 change 事件

// 从 activeNote.blocks 加载画廊数据
const loadFromBlocks = () => {
  const note = activeNote.value as any;
  if (!note || note.blocks === undefined) {
    // 没有 blocks 数据时，不要清空 UI，保留上次显示内容
    return;
  }

  // 提取 image blocks
  const imageBlocks = note.blocks
    .filter((block: any) => block.type === 'image')
    .sort((a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0));

  const images: ArtImage[] = imageBlocks.map((block: any) => {
    let data = {};
    try {
      data = JSON.parse(block.data || '{}');
    } catch (e) { }
    const attrs = (data as any).attrs || {};
    return {
      id: block.id,
      url: attrs.src || '',
      caption: attrs.caption || '',
      sortOrder: block.sortOrder ?? 0
    };
  });
  localImages.value = images;

  // 提取 summary block（支持 art-summary 或普通 paragraph 带标记）
  const summaryBlock = note.blocks.find((block: any) => block.type === 'art-summary');
  if (summaryBlock) {
    try {
      const data = JSON.parse(summaryBlock.data || '{}');
      localSummary.value = data.text || '';
    } catch (e) { }
  } else {
    // 兼容旧数据：找第一个 paragraph 作为总结（可选）
    const paraBlock = note.blocks.find((block: any) => block.type === 'paragraph');
    if (paraBlock) {
      try {
        const data = JSON.parse(paraBlock.data || '{}');
        localSummary.value = data.text || '';
      } catch (e) { }
    } else {
      localSummary.value = '';
    }
  }
};

// 将当前状态转换为 blocks 数组（用于通知父组件）
const buildBlocksFromState = (): any[] => {
  const blocks: any[] = [];
  const currentNoteId = props.noteId;
  if (!currentNoteId) return blocks;

  // 图片 blocks
  localImages.value.forEach((img, idx) => {
    const imageData = {
      attrs: {
        id: img.id,
        src: img.url,
        alt: '',
        caption: img.caption
      },
      content: []
    };
    blocks.push({
      id: img.id,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'image',
      data: JSON.stringify(imageData),
      sortOrder: idx
    });
  });

  // 总结 block（如果总结非空，或者没有任何图片时也保存一个占位）
  if (localSummary.value.trim() || blocks.length === 0) {
    const summaryData = {
      text: localSummary.value,
      type: 'paragraph'
    };
    blocks.push({
      id: `summary_${Date.now()}`,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'art-summary',
      data: JSON.stringify(summaryData),
      sortOrder: localImages.value.length
    });
  }

  return blocks;
};

// 通知父组件保存（仅当初始化完成后才允许）
const notifyChange = () => {
  if (!isInitialized) return;  // 🌟 初始化期间不发送，防止空内容覆盖
  const blocks = buildBlocksFromState();
  emit('change', { blocks, type: 'art-gallery' });
};

// 防抖通知（只有用户编辑才会触发）
const triggerNotify = () => {
  if (!isInitialized) return;
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    notifyChange();
  }, 500);
};

// 添加新图片卡片
const addNewImageCard = () => {
  const newId = `img_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
  localImages.value.push({
    id: newId,
    url: '',
    caption: '',
    sortOrder: localImages.value.length
  });
  triggerNotify();
  nextTick(() => {
    const newIndex = localImages.value.length - 1;
    pendingImageIndex = newIndex;
    fileInputRef.value?.click();
  });
};

const triggerImageUpload = (index: number) => {
  pendingImageIndex = index;
  fileInputRef.value?.click();
};

const handleFileSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file || !file.type.startsWith('image/') || pendingImageIndex === null) {
    input.value = '';
    pendingImageIndex = null;
    return;
  }

  const index = pendingImageIndex;
  const targetImage = localImages.value[index];
  if (!targetImage) {
    pendingImageIndex = null;
    input.value = '';
    return;
  }

  try {
    const result = await uploadFile(file, 'artwork');
    if (result?.url) {
      targetImage.url = result.url;
      triggerNotify();
    }
  } catch (err) {
    console.error('图片上传失败', err);
    alert('图片上传失败，请重试');
  } finally {
    pendingImageIndex = null;
    input.value = '';
  }
};

const updateImageCaption = (index: number, event: Event) => {
  const target = event.target as HTMLTextAreaElement;
  if (localImages.value[index]) {
    localImages.value[index].caption = target.value;
    triggerNotify();
  }
};

const removeImage = (index: number) => {
  if (confirm('确定移除此画幅吗？')) {
    localImages.value.splice(index, 1);
    triggerNotify();
  }
};

// 拖拽排序
const handleDragStart = (e: DragEvent, index: number) => {
  dragIndex = index;
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
};

const handleDragOver = (e: DragEvent, index: number) => {
  e.preventDefault();
  if (dragIndex === null || dragIndex === index) return;
  const draggedItem = localImages.value[dragIndex];
  const targetItem = localImages.value[index];
  if (draggedItem && targetItem) {
    const newImages = [...localImages.value];
    newImages.splice(dragIndex, 1);
    newImages.splice(index, 0, draggedItem);
    localImages.value = newImages;
    dragIndex = index;
    triggerNotify();
  }
};

const handleDragEnd = () => {
  dragIndex = null;
};

const onSummaryInput = (e: Event) => {
  const target = e.target as HTMLTextAreaElement;
  localSummary.value = target.value;
  triggerNotify();
};

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
  triggerNotify();
};

// 监听 blocks 变化，但只在真正加载完成后才允许发送 change
watch(
  () => activeNote.value,
  (newNote) => {
    if (!newNote) return;
    // 如果 blocks 存在（说明已经从后端获取到了）
    if ((newNote as any).blocks !== undefined) {
      loadFromBlocks();
      if (!isInitialized) {
        isInitialized = true;
        // 延迟一帧，确保 UI 渲染完成
        nextTick(() => {
          notifyChange();  // 发送当前最新的 blocks 给父组件
        });
      }
    }
  },
  { immediate: true, deep: true }
);

onMounted(() => {
  // 如果 activeNote 已经存在且 blocks 已加载，watch 会处理；如果未触发，手动调一次
  if (activeNote.value && !isInitialized) {
    loadFromBlocks();
    isInitialized = true;
    notifyChange();
  }
});

onUnmounted(() => {
  if (saveTimer) clearTimeout(saveTimer);
});
</script>

<style scoped>
.workspace-art-frame {
  max-width: 900px;
  margin: 0 auto;
  padding: 40px 24px 80px;
  background: #fefefe;
}

/* 标题区 */
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

.art-subtitle {
  font-size: 0.85rem;
  color: #aaa;
  margin-top: 8px;
  letter-spacing: 0.3px;
}

/* 画廊网格 */
.gallery-container {
  display: flex;
  flex-direction: column;
  gap: 40px;
  margin-bottom: 56px;
}

/* 卡片样式 */
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
  padding: 20px 24px 20px 24px;
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
  background: rgba(255,255,255,0.7);
  backdrop-filter: blur(4px);
  padding: 4px 10px;
  border-radius: 40px;
  letter-spacing: 0.3px;
}

/* 新增卡片按钮 */
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

/* 总结区域 */
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

/* 移动端适配 */
@media (max-width: 768px) {
  .workspace-art-frame {
    padding: 20px 16px 60px;
  }
  
  .art-title-input {
    font-size: 2rem;
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