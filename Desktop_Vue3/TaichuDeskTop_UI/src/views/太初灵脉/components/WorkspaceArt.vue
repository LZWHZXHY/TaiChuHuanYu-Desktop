<template>
  <div class="workspace-art-frame">
    <header class="art-header">
      <input :value="props.title" @input="onTitleInput" class="art-title-input" placeholder="作品名称 / Title" />
      <p class="art-subtitle">以图叙事的灵动画廊</p>
    </header>

    <div class="gallery-container">
      <div v-for="(image, idx) in localImages" :key="image.id" class="art-card" :class="{ 'is-dragging': dragIndex === idx }"
        draggable="true" @dragstart="handleDragStart($event, idx)" @dragover="handleDragOver($event, idx)" @dragend="handleDragEnd">
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

    <article class="art-editor-slot-area">
      <slot name="editor"></slot>
    </article>

    <div class="art-summary-section">
      <div class="summary-label"><span>✨ 创作总览快照（归档总结）</span></div>
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
  extraData?: string; // 🌟 彻底松绑安全释放：右侧面板属性专属通道
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
let isInitialized = false;  

// 从 activeNote.blocks 加载画廊数据
const loadFromBlocks = () => {
  // 🌟 修复点：加入非空断言防御保护
  if (!activeNote.value) return;
  
  const note = activeNote.value as any;
  if (note.blocks === undefined || !Array.isArray(note.blocks)) return;

  // 提炼出属于画廊卡片的 image blocks
  const imageBlocks = note.blocks
    .filter((block: any) => block.type === 'image')
    .sort((a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0));

  localImages.value = imageBlocks.map((block: any) => {
    let data = {};
    try { data = JSON.parse(block.data || '{}'); } catch (e) {}
    const attrs = (data as any).attrs || {};
    return {
      id: block.id,
      url: attrs.src || '',
      caption: attrs.caption || '',
      sortOrder: block.sortOrder ?? 0
    };
  });

  // 提炼出独立的创作总览块 art-summary
  const summaryBlock = note.blocks.find((block: any) => block.type === 'art-summary');
  if (summaryBlock) {
    try {
      const data = JSON.parse(summaryBlock.data || '{}');
      localSummary.value = data.text || '';
    } catch (e) {}
  } else {
    localSummary.value = '';
  }
};

// 🌟 自治核心：合并系统排版块与编辑器原有正文块，输出完整积木链
const buildBlocksFromState = (): any[] => {
  const finalBlocks: any[] = [];
  const currentNoteId = props.noteId;
  if (!currentNoteId || !activeNote.value) return finalBlocks;

  const currentNoteBlocks = activeNote.value.blocks || [];

  // ① 剥离提取：找出原本就属于富文本编辑器的普通文本块（非 image，非 art-summary 块）
  const pureEditorTextBlocks = currentNoteBlocks.filter(
    (b: any) => b.type !== 'image' && b.type !== 'art-summary'
  );

  // ② 组装画廊特定的图片卡片块
  localImages.value.forEach((img, idx) => {
    const imageData = {
      attrs: { id: img.id, src: img.url, alt: '', caption: img.caption },
      content: []
    };
    finalBlocks.push({
      id: img.id,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'image',
      data: JSON.stringify(imageData),
      sortOrder: idx // 画廊专有顺序
    });
  });

  // ③ 组装底部的总览块
  if (localSummary.value.trim() || finalBlocks.length === 0) {
    const summaryData = { text: localSummary.value, type: 'paragraph' };
    finalBlocks.push({
      id: `art_summary_fixed_id`,
      ownerId: currentNoteId,
      ownerType: 'art',
      type: 'art-summary',
      data: JSON.stringify(summaryData),
      sortOrder: localImages.value.length
    });
  }

  // ④ 重新调整正文编辑器的序号，平滑向后无限追加顺延
  const offset = finalBlocks.length;
  pureEditorTextBlocks.forEach((b: any, index: number) => {
    b.sortOrder = offset + index;
  });

  // ⑤ 完美大合流
  return [...finalBlocks, ...pureEditorTextBlocks];
};

const notifyChange = () => {
  // 🌟 修复点：除了原有的初始化判定，追加 activeNote.value 存在性验证
  if (!isInitialized || !activeNote.value) return;  
  
  const blocks = buildBlocksFromState();
  
  // 将合流后无懈可击的全量积木数组同步写回主缓存池
  activeNote.value.blocks = blocks;
  
  emit('change', { blocks });
};

const triggerNotify = () => {
  if (!isInitialized) return;
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    notifyChange();
  }, 500);
};

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

const handleDragStart = (e: DragEvent, index: number) => {
  dragIndex = index;
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
};

const handleDragOver = (e: DragEvent, index: number) => {
  e.preventDefault();
  if (dragIndex === null || dragIndex === index) return;
  const draggedItem = localImages.value[dragIndex];
  if (draggedItem) {
    const newImages = [...localImages.value];
    newImages.splice(dragIndex, 1);
    newImages.splice(index, 0, draggedItem);
    localImages.value = newImages;
    dragIndex = index;
    triggerNotify();
  }
};

const handleDragEnd = () => { dragIndex = null; };
const onSummaryInput = (e: Event) => { localSummary.value = (e.target as HTMLTextAreaElement).value; triggerNotify(); };
const onTitleInput = (e: Event) => { emit('update:title', (e.target as HTMLInputElement).value); triggerNotify(); };

// 监听 blocks 变化
watch(
  () => activeNote.value?.id, // 🌟 采用可选链保护
  (newId) => {
    // 🌟 修复点：多加一层防护，如果 activeNote 被洗成 null（比如删除了文章），直接安全离场
    if (!newId || !activeNote.value) return;
    
    if ((activeNote.value as any).blocks !== undefined) {
      loadFromBlocks();
      if (!isInitialized) {
        isInitialized = true;
        nextTick(() => { notifyChange(); });
      }
    }
  },
  { immediate: true }
);

onMounted(() => {
  if (activeNote.value && !isInitialized) {
    loadFromBlocks();
    isInitialized = true;
    notifyChange();
  }
});

onUnmounted(() => { if (saveTimer) clearTimeout(saveTimer); });
</script>

<style scoped>
.workspace-art-frame { max-width: 900px; margin: 0 auto; padding: 40px 24px 80px; background: #fefefe; }
.art-header { margin-bottom: 48px; text-align: center; border-bottom: 1px solid #f0f0f0; padding-bottom: 24px; }
.art-title-input { width: 100%; font-size: 2.8rem; font-weight: 700; border: none; background: transparent; text-align: center; font-family: inherit; padding: 8px 0; letter-spacing: -0.02em; color: #1a1a1a; transition: all 0.2s; }
.art-title-input:focus { outline: none; background: #fafafa; border-radius: 16px; }
.art-subtitle { font-size: 0.85rem; color: #aaa; margin-top: 8px; letter-spacing: 0.3px; }
.gallery-container { display: flex; flex-direction: column; gap: 40px; margin-bottom: 48px; }
.art-card { display: flex; flex-direction: column; background: #ffffff; border-radius: 28px; box-shadow: 0 8px 28px rgba(0, 0, 0, 0.04), 0 0 0 1px rgba(0, 0, 0, 0.02); transition: all 0.3s cubic-bezier(0.2, 0, 0, 1); position: relative; cursor: grab; }
.art-card.is-dragging { opacity: 0.5; cursor: grabbing; }
.art-card:hover { box-shadow: 0 20px 40px rgba(0, 0, 0, 0.08), 0 0 0 1px rgba(0, 0, 0, 0.05); transform: translateY(-2px); }
.card-drag-handle { position: absolute; top: 16px; left: 16px; color: #bbb; cursor: grab; z-index: 10; background: rgba(255, 255, 255, 0.8); backdrop-filter: blur(4px); border-radius: 100px; padding: 4px; transition: color 0.2s; }
.art-card:hover .card-drag-handle { color: #888; }
.card-image-area { position: relative; width: 100%; aspect-ratio: 16 / 9; border-radius: 24px 24px 16px 16px; overflow: hidden; background: #f5f5f7; display: flex; align-items: center; justify-content: center; }
.card-image { width: 100%; height: 100%; object-fit: cover; transition: transform 0.4s ease; }
.art-card:hover .card-image { transform: scale(1.02); }
.image-placeholder { display: flex; align-items: center; justify-content: center; width: 100%; height: 100%; background: linear-gradient(135deg, #f9f9fb 0%, #efeff4 100%); color: #ccc; }
.upload-overlay { position: absolute; bottom: 16px; right: 16px; background: rgba(0, 0, 0, 0.6); backdrop-filter: blur(12px); border: none; color: white; padding: 6px 14px; border-radius: 40px; font-size: 12px; font-weight: 500; cursor: pointer; opacity: 0; transition: opacity 0.2s; z-index: 5; }
.card-image-area:hover .upload-overlay { opacity: 1; }
.remove-image-btn { position: absolute; top: 16px; right: 16px; background: rgba(0, 0, 0, 0.6); backdrop-filter: blur(8px); border: none; color: white; width: 32px; height: 32px; border-radius: 100%; display: flex; align-items: center; justify-content: center; cursor: pointer; opacity: 0; transition: opacity 0.2s, background 0.2s; z-index: 5; }
.card-image-area:hover .remove-image-btn { opacity: 1; }
.remove-image-btn:hover { background: #e5484d; }
.card-caption-area { padding: 20px 24px; }
.caption-textarea { width: 100%; border: none; background: #fafafc; border-radius: 20px; padding: 16px 20px; font-size: 0.95rem; line-height: 1.5; font-family: inherit; color: #2c2c2e; resize: vertical; transition: background 0.2s; border: 1px solid transparent; }
.caption-textarea:focus { outline: none; background: #ffffff; border-color: #e1e1e6; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02); }
.card-badge { position: absolute; top: 16px; right: 20px; font-size: 12px; font-weight: 500; color: #aaa; background: rgba(255,255,255,0.7); backdrop-filter: blur(4px); padding: 4px 10px; border-radius: 40px; letter-spacing: 0.3px; }
.add-card-btn { display: flex; align-items: center; justify-content: center; gap: 12px; background: transparent; border: 2px dashed #d9d9df; border-radius: 28px; padding: 32px 20px; cursor: pointer; color: #8e8e93; font-size: 1rem; font-weight: 500; transition: all 0.2s; margin-top: 16px; }
.add-card-btn:hover { border-color: #007aff; color: #007aff; background: #f5f9ff; }

/* 🌟 新增富文本插槽承载区域 */
.art-editor-slot-area { margin: 24px 0 48px; min-height: 200px; }

.art-summary-section { margin-top: 48px; border-top: 2px solid #f2f2f5; padding-top: 40px; }
.summary-label { font-size: 0.85rem; font-weight: 600; text-transform: uppercase; letter-spacing: 1px; color: #aaa; margin-bottom: 20px; }
.summary-textarea { width: 100%; border: none; background: #fafafc; border-radius: 28px; padding: 24px 28px; font-size: 1rem; line-height: 1.6; font-family: inherit; color: #1d1d1f; resize: vertical; transition: all 0.2s; }
.summary-textarea:focus { outline: none; background: #ffffff; box-shadow: 0 4px 20px rgba(0, 0, 0, 0.02), 0 0 0 2px #f0f0f5; }

@media (max-width: 768px) { .workspace-art-frame { padding: 20px 16px 60px; } .art-title-input { font-size: 2rem; } .gallery-container { gap: 28px; } .card-caption-area { padding: 16px; } .caption-textarea { font-size: 0.9rem; padding: 12px 16px; } .add-card-btn { padding: 24px 16px; } }
</style>