<template>
  <div class="project-document-root">
    <div class="doc-workspace-layout">
      
      <aside class="doc-sidebar">
        <div class="sidebar-sticky-wrapper">
          <div class="sidebar-header">
            <span class="header-title">项目长卷大纲</span>
            <span class="doc-count">{{ documentList.length }}</span>
          </div>
          
          <div class="doc-list-flat">
            <div 
              v-for="doc in documentList" 
              :key="doc.id" 
              class="doc-list-item"
              :class="{ active: selectedDocId === doc.id }"
              @click="handleSelectDocument(doc.id)"
            >
              <div class="doc-item-main">
                <span class="doc-title">{{ doc.title || '未命名长卷' }}</span>
              </div>
              <div class="doc-item-meta">
                <span class="pin-user">@{{ doc.pinnedByUserName || '成员' }}</span>
                <span class="pin-date">{{ formatDate(doc.pinnedAt) }}</span>
              </div>
            </div>

            <div v-if="documentList.length === 0 && !isLoading" class="sidebar-empty">
              暂无归档长卷
            </div>
          </div>
        </div>
      </aside>

      <main class="doc-viewer-content">
        <transition name="view-fade" mode="out-in">
          <div v-if="isLoading" class="doc-state-loading">
            <div class="loading-pulse"></div>
          </div>

          <div v-else-if="selectedDocId && currentDocDetail" class="doc-reader-inner">
            <header class="reader-header">
              <div class="reader-breadcrumbs">
                <span>{{ props.initialData?.name || '当前协作项目' }}</span>
                <span class="flow-sep">/</span>
                <span class="active-badge">项目文档</span>
              </div>
              <h1 class="reader-title">{{ currentDocDetail.title }}</h1>
              <div class="reader-author-bar">
                <span>编织者：<strong>{{ currentDocDetail.pinnedByUserName || '项目成员' }}</strong></span>
                <span class="flow-sep">|</span>
                <span>固化于：{{ formatDate(currentDocDetail.pinnedAt) }}</span>
              </div>
            </header>

            <article class="reader-body">
              <div v-for="block in currentDocDetail.blocks" :key="block.id" class="rich-paragraph">
                <p v-if="block.type === 'paragraph'" v-html="parseBlockContent(block.data)"></p>
                <div v-else-if="block.type === 'image'" class="rich-image-wrapper">
                  <img :src="parseImageData(block.data).src" :alt="parseImageData(block.data).caption" />
                  <p v-if="parseImageData(block.data).caption" class="img-caption">// {{ parseImageData(block.data).caption }}</p>
                </div>
              </div>
            </article>
          </div>

          <div v-else class="layout-placeholder-inner">
            <div class="placeholder-content">
              <div class="placeholder-icon">⠿</div>
              <h3>太初共建长卷已就绪</h3>
              <p>请在左侧大纲中点选项目文档，开启沉浸协同研读空间。</p>
            </div>
          </div>
        </transition>
      </main>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import request from '@/utils/request'; 

const props = defineProps<{
  projectId: string;
  initialData?: any; 
}>();

const emit = defineEmits(['updated']);

const isLoading = ref(false);
const documentList = ref<any[]>([]);
const selectedDocId = ref<string | null>(null);
const currentDocDetail = ref<any>(null);

const fetchProjectDocuments = async () => {
  if (!props.projectId) return;
  isLoading.value = true;
  try {
    const res: any = await request.get(`/Project/${props.projectId}/documents`);
    documentList.value = res.data || res || [];
  } catch (err) {
    console.error("拉取项目文档大纲失败:", err);
  } finally {
    isLoading.value = false;
  }
};

const handleSelectDocument = async (noteId: string) => {
  selectedDocId.value = noteId;
  try {
    const res: any = await request.get(`/LingMai/${noteId}`);
    const noteData = res.data || res;
    const listMeta = documentList.value.find(d => d.id === noteId);
    
    currentDocDetail.value = {
      ...noteData,
      pinnedByUserName: listMeta?.pinnedByUserName,
      pinnedAt: listMeta?.pinnedAt
    };
    
    emit('updated', noteId); 
  } catch (err) {
    console.error("抽取长卷实体失败:", err);
  }
};

const parseBlockContent = (dataStr: string): string => {
  try {
    const blockObj = JSON.parse(dataStr);
    if (blockObj.content && Array.isArray(blockObj.content)) {
      return blockObj.content.map((item: any) => {
        if (item.type === 'text') {
          let text = item.text;
          if (item.marks) {
            item.marks.forEach((mark: any) => {
              if (mark.type === 'bold') text = `<strong>${text}</strong>`;
              if (mark.type === 'link') text = `<a href="${mark.attrs.href}" target="_blank" class="doc-inner-link">${text}</a>`;
            });
          }
          return text;
        }
        if (item.type === 'hardBreak') return '<br/>';
        return '';
      }).join('');
    }
  } catch {
    return '长卷段落流转产生风暴';
  }
  return '';
};

const parseImageData = (dataStr: string) => {
  try {
    const blockObj = JSON.parse(dataStr);
    return {
      src: blockObj.attrs?.src || '',
      caption: blockObj.attrs?.caption || ''
    };
  } catch {
    return { src: '', caption: '' };
  }
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return `${d.getFullYear()}.${String(d.getMonth() + 1).padStart(2, '0')}.${String(d.getDate()).padStart(2, '0')}`;
};

watch(() => props.projectId, () => {
  selectedDocId.value = null;
  currentDocDetail.value = null;
  fetchProjectDocuments();
}, { immediate: true });

onMounted(fetchProjectDocuments);
</script>

<style scoped>
.project-document-root {
  width: 100%;
  /* 🌟 核心破局点 1：移除所有强行锁死的 height，允许高度由内容自然撑开 */
  min-height: 400px;
  background: #ffffff;
}

.doc-workspace-layout {
  display: flex;
  width: 100%;
  border-top: 1px solid #f2f2f2;
}

/* 左侧大纲目录：改造成高级的 Sticky 粘性跟随机制 */
.doc-sidebar {
  width: 240px;
  background: #ffffff;
  border-right: 1px solid #f2f2f2;
  flex-shrink: 0;
}

/* 🌟 核心破局点 2：当页面由于右侧长文档向下滚动时，左侧目录会牢牢粘在视口顶部 100px 的位置 */
.sidebar-sticky-wrapper {
  position: sticky;
  top: 40px; /* 根据你头部导航栏的实际高度微调，保证刚好粘住不滑走 */
  max-height: calc(100vh - 100px);
  display: flex;
  flex-direction: column;
  padding-top: 20px;
}

.sidebar-header {
  padding: 10px 24px 20px 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-title {
  font-size: 0.65rem;
  font-weight: 400;
  color: #aaa;
  text-transform: uppercase;
  letter-spacing: 1.5px;
}

.doc-count {
  font-size: 0.7rem;
  font-family: monospace;
  color: #bbb;
  background: #f9f9f9;
  padding: 1px 6px;
  border-radius: 2px;
}

.doc-list-flat {
  flex: 1;
  /* 🌟 核心破局点 3：目录如果真的很长，依然允许在内部微滚动，但绝不干扰右侧 */
  overflow-y: auto;
  padding-right: 12px;
}

.doc-list-flat::-webkit-scrollbar { width: 2px; }
.doc-list-flat::-webkit-scrollbar-thumb { background: #f0f0f0; }

.doc-list-item {
  padding: 16px 0;
  cursor: pointer;
  background: transparent;
  transition: all 0.3s ease;
  border-bottom: 1px solid #fcfcfc;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.doc-list-item:hover .doc-title {
  color: #1a1a1a;
}

.doc-title {
  font-size: 0.95rem;
  font-weight: 300;
  color: #888;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: color 0.3s;
}

.doc-list-item.active {
  position: relative;
}

.doc-list-item.active .doc-title {
  color: #1a1a1a;
  font-weight: 500;
}

.doc-list-item.active::after {
  content: '';
  position: absolute;
  right: -13px;
  top: 50%;
  transform: translateY(-50%);
  width: 1.5px;
  height: 16px;
  background: #1a1a1a;
}

.doc-item-meta {
  display: flex;
  gap: 8px;
  font-size: 0.7rem;
  color: #ccc;
  font-weight: 300;
}

.sidebar-empty {
  font-size: 0.85rem;
  color: #bbb;
  padding: 20px 0;
  font-weight: 300;
  font-style: italic;
}

/* 右侧文档视图：剥离内部滚动条，完全顺从主页面滚动 */
.doc-viewer-content {
  flex: 1;
  background: #ffffff;
  padding-left: 5%;
  /* 🌟 核心破局点 4：坚决去掉 overflow-y: auto，拒绝二次诞生滚动条 */
  overflow: visible; 
}

.doc-state-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 300px; /* 转换为固定高度展示 */
}

.loading-pulse {
  width: 40px;
  height: 1px;
  background: #eee;
  animation: pulse 1.5s infinite;
}

.doc-reader-inner {
  max-width: 720px;
  padding: 30px 0 100px 0;
  animation: view-fade-in 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}

.reader-header {
  margin-bottom: 40px;
  border-bottom: 1px solid #f2f2f2;
  padding-bottom: 32px;
}

.reader-breadcrumbs {
  font-size: 0.65rem;
  color: #aaa;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

.flow-sep {
  margin: 0 8px;
  color: #eee;
}

.active-badge {
  color: #1a1a1a;
  font-weight: 500;
}

.reader-title {
  font-size: 2.8rem;
  font-weight: 300;
  letter-spacing: -1.5px;
  color: #1a1a1a;
  line-height: 1.25;
  margin: 0 0 16px 0;
}

.reader-author-bar {
  display: flex;
  align-items: center;
  font-size: 0.8rem;
  color: #666;
  font-weight: 300;
}

.reader-author-bar strong {
  color: #1a1a1a;
  font-weight: 500;
}

.reader-body {
  font-size: 1rem;
  line-height: 1.8;
  color: #1a1a1a;
  font-weight: 300;
}

.rich-paragraph {
  margin-bottom: 24px;
  word-wrap: break-word;
  text-align: justify;
}

:deep(.doc-inner-link) {
  color: #1a1a1a;
  text-decoration: none;
  border-bottom: 1px solid #1a1a1a;
}

.rich-image-wrapper {
  margin: 40px 0;
  text-align: center;
}

.rich-image-wrapper img {
  max-width: 100%;
  border-radius: 2px;
  box-shadow: 0 10px 30px rgba(0,0,0,0.02);
}

.img-caption {
  font-size: 0.78rem;
  color: #bbb;
  margin-top: 12px;
  font-family: monospace;
}

/* 占位符转换：与正文大厅无缝融归 */
.layout-placeholder-inner {
  height: 400px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.layout-placeholder-inner .placeholder-content {
  text-align: center;
}

.layout-placeholder-inner .placeholder-icon {
  font-size: 3rem;
  color: #eee;
  margin-bottom: 20px;
}

.layout-placeholder-inner h3 {
  font-size: 1.1rem;
  font-weight: 400;
  color: #ccc;
  margin-bottom: 8px;
}

.layout-placeholder-inner p {
  font-size: 0.8rem;
  color: #ddd;
}

@keyframes pulse {
  0% { transform: scaleX(1); opacity: 1; }
  50% { transform: scaleX(2); opacity: 0.3; }
  100% { transform: scaleX(1); opacity: 1; }
}

@keyframes view-fade-in {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.view-fade-enter-active,
.view-fade-leave-active {
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}
.view-fade-enter-from { opacity: 0; transform: translateY(8px); }
.view-fade-leave-to { opacity: 0; transform: translateY(-8px); }
</style>