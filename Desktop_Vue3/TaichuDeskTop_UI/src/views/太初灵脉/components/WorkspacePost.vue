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
}>();

const emit = defineEmits(['update:title', 'change']);

const { activeNote } = useSpiritData();
const isInitialized = ref(false);

// 🌟 深度递归计算当前动态的纯文字总长度（用于动态字数限制高亮）
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

// 响应式捕获当前 Tiptap 数据流中的文本总字数
const currentLength = computed(() => {
  const note = activeNote.value as any;
  if (!note || !note.blocks || !Array.isArray(note.blocks)) return 0;
  
  let totalLen = 0;
  note.blocks.forEach((block: any) => {
    if (block.type === 'paragraph' || block.type === 'heading') {
      try {
        const blockData = typeof block.data === 'string' ? JSON.parse(block.data) : block.data;
        totalLen += getTextLength(blockData);
      } catch (e) {}
    }
  });
  return totalLen;
});

// 对应你 index.vue 中的校验规则：thought/post 类型限制在 500 字内
const isOverLimit = computed(() => currentLength.value > 500);

// 🌟 自动对齐：短动态不需要标题，我们自动将正文的前 15 个字同步更新为该 Notes 的 title，方便在侧边栏显示
const syncPostTitleToSidebar = () => {
  const note = activeNote.value as any;
  if (!note || !note.blocks || !Array.isArray(note.blocks)) return;

  const firstPara = note.blocks.find((b: any) => b.type === 'paragraph');
  if (firstPara) {
    try {
      const blockData = typeof firstPara.data === 'string' ? JSON.parse(firstPara.data) : firstPara.data;
      let pureText = firstPara.data ? getTextLength(blockData) > 0 ? "" : "" : "";
      
      // 提取纯文本
      const extractPureText = (n: any): string => {
        if (!n) return '';
        if (n.text) return n.text;
        if (n.content && Array.isArray(n.content)) return n.content.map(extractPureText).join('');
        return '';
      };
      
      pureText = extractPureText(blockData).trim();
      
      if (pureText) {
        // 截取前 15 个字作为侧边栏和数据库里的标题
        const shortTitle = pureText.length > 15 ? pureText.substring(0, 15) + '...' : pureText;
        if (props.title !== shortTitle) {
          emit('update:title', shortTitle);
        }
      }
    } catch (e) {}
  }
};

// 监听外界数据变化，实时保持侧边栏标题平滑更新
watch(
  () => activeNote.value?.blocks,
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