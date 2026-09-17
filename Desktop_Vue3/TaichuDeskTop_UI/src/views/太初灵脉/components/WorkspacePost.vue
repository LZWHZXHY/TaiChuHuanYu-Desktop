<template>
  <div class="workspace-post-frame">
    <header class="post-header-meta">
      <div class="author-avatar-placeholder">✍️</div>
      <div class="meta-info">
        <span class="meta-label">发布简语微言</span>
        <span class="meta-subtitle">瞬时的灵感，多维的交织</span>
      </div>
      <div class="post-word-count" :class="{ 'is-limit': isOverLimit }">
        {{ currentLength }} / 500 字
      </div>
    </header>

    <main class="post-main-content">
      <slot name="editor"></slot>
    </main>

    <footer class="post-footer-bar" v-if="props.tags && props.tags.length > 0">
      <div class="post-tags-preview">
        <span v-for="tag in props.tags" :key="tag" class="post-tag-item">
          # {{ tag }}
        </span>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';

const props = defineProps<{
  title: string;
  noteId?: string;
  tags?: string[];
  extraData?: string; 
}>();

const emit = defineEmits(['update:title', 'change']);

const { activeNote } = useSpiritData();
const isInitialized = ref(false);

const getTiptapBlocks = (note: any) => {
  if (!note) return [];
  if (note.content && note.content.type === 'doc' && Array.isArray(note.content.content)) {
    return note.content.content;
  }
  if (Array.isArray(note.content)) return note.content;
  return [];
};

const getTextLength = (node: any): number => {
  if (!node) return 0;
  let len = 0;
  if (node.text) len += node.text.length;
  if (node.content && Array.isArray(node.content)) {
    node.content.forEach((child: any) => {
      len += getTextLength(child);
    });
  }
  return len;
};

const currentLength = computed(() => {
  // 🌟 修复：增加非空守卫，解决 activeNote 可能为 null 的 TS 警告
  if (!activeNote.value) return 0;
  
  const blocks = getTiptapBlocks(activeNote.value);
  if (!blocks.length) return 0;
  
  let totalLen = 0;
  blocks.forEach((block: any) => {
    totalLen += getTextLength(block);
  });
  return totalLen;
});

const isOverLimit = computed(() => currentLength.value > 500);

const syncPostTitleToSidebar = () => {
  // 🌟 修复：增加非空守卫，确保后续 activeNote.value.content 访问安全
  if (!activeNote.value) return; 

  const blocks = getTiptapBlocks(activeNote.value);
  if (!blocks.length) return;

  const firstTextNode = blocks.find((b: any) => getTextLength(b) > 0);
  
  if (firstTextNode) {
    try {
      const extractPureText = (n: any): string => {
        if (!n) return '';
        if (n.text) return n.text;
        if (n.content && Array.isArray(n.content)) return n.content.map(extractPureText).join('');
        return '';
      };
      
      const pureText = extractPureText(firstTextNode).trim();
      
      if (pureText) {
        const shortTitle = pureText.length > 15 ? pureText.substring(0, 15) + '...' : pureText;
        if (props.title !== shortTitle) {
          emit('update:title', shortTitle);
          // 此时 TS 已确信 activeNote.value 不为空，报错消除
          emit('change', activeNote.value.content);
        }
      }
    } catch (e) {
      console.error('提取标题失败:', e);
    }
  }
};

watch(
  () => activeNote.value?.content,
  () => {
    if (isInitialized.value) {
      syncPostTitleToSidebar();
    }
  },
  { deep: true }
);

onMounted(() => {
  isInitialized.value = true;
  syncPostTitleToSidebar();
});
</script>

<style scoped>
.workspace-post-frame {
  max-width: 680px; /* 类似微博/Twitter的核心阅读流窄屏黄金视觉宽度 */
  margin: 0 auto;
  padding: 32px 24px 80px;
  background: #ffffff;
  border-radius: 24px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.01);
}

/* 1. 顶部创作者元信息区 */
.post-header-meta {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 28px;
  border-bottom: 1px solid #f2f2f7;
  padding-bottom: 18px;
}

.author-avatar-placeholder {
  width: 42px;
  height: 42px;
  background: #f5f5f7;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  border: 1px solid rgba(0, 0, 0, 0.03);
}

.meta-info {
  display: flex;
  flex-direction: column;
  flex: 1;
}

.meta-label {
  font-size: 15px;
  font-weight: 700;
  color: #1d1d1f;
  letter-spacing: -0.01em;
}

.meta-subtitle {
  font-size: 12px;
  color: #a1a1a6;
  margin-top: 2px;
}

.post-word-count {
  font-size: 12px;
  color: #86868b;
  background: #f5f5f7;
  padding: 4px 10px;
  border-radius: 20px;
  font-variant-numeric: tabular-nums;
  transition: all 0.2s;
}

.post-word-count.is-limit {
  color: #ff3b30;
  background: #ffeeea;
  font-weight: 600;
}

/* 2. 编辑器主体区 */
.post-main-content {
  width: 100%;
  min-height: 250px;
  font-size: 1.1rem; /* 适当放大短动态字号，提升可读性 */
  line-height: 1.6;
}

/* 消除 Tiptap 默认大标题在短动态里的违和感，将其样式弱化 */
:deep(.spirit-typography-engine h1) {
  font-size: 1.5rem;
  margin-top: 12px;
}

/* 3. 底部标签样式 */
.post-footer-bar {
  margin-top: 32px;
  padding-top: 16px;
  border-top: 1px dashed #e5e5ea;
}

.post-tags-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.post-tag-item {
  font-size: 13px;
  color: #0066cc; /* 灵脉科技感科技蓝 */
  background: rgba(0, 102, 204, 0.05);
  padding: 4px 12px;
  border-radius: 40px;
  font-weight: 500;
}

/* 移动端适配 */
@media (max-width: 768px) {
  .workspace-post-frame {
    padding: 20px 16px 40px;
    box-shadow: none;
  }
}
</style>