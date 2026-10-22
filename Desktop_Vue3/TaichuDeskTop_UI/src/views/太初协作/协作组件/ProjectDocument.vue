<template>
  <div class="project-document-root">
    <div class="doc-workspace-layout">
      
      <aside class="doc-sidebar">
        <div class="sidebar-sticky-wrapper">
          <div class="sidebar-header">
            <span class="header-title">项目文档</span>
            <span class="doc-count">{{ documentList.length }}</span>
          </div>
          
          <div class="doc-list-flat">
            <button type="button"
              v-for="doc in documentList" 
              :key="doc.id" 
              class="doc-list-item"
              :class="{ active: selectedDocId === doc.id }" :aria-pressed="selectedDocId === doc.id"
              @click="handleSelectDocument(doc.id)"
            >
              <div class="doc-item-main">
                <span class="doc-title">{{ doc.title || '未命名文档' }}</span>
              </div>
              <div class="doc-item-meta">
                <span class="pin-user">@{{ doc.pinnedByUserName || '成员' }}</span>
                <span class="pin-date">{{ formatDate(doc.pinnedAt) }}</span>
              </div>
            </button>

            <div v-if="documentList.length === 0 && !isLoading" class="sidebar-empty">
              暂无项目文档
            </div>
          </div>
        </div>
      </aside>

      <main class="doc-viewer-content">
        <transition name="view-fade" mode="out-in">
          <div v-if="listError || detailError" class="doc-state-loading" role="alert">
            <p>{{ listError || detailError }}</p>
            <button @click="listError ? fetchProjectDocuments() : handleSelectDocument(selectedDocId!)">重新加载</button>
          </div>
          <div v-else-if="isLoading || detailLoading" class="doc-state-loading">
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
                <span>分享者：<strong>{{ currentDocDetail.pinnedByUserName || '项目成员' }}</strong></span>
                <span class="flow-sep">|</span>
                <span>分享时间：{{ formatDate(currentDocDetail.pinnedAt) }}</span>
              </div>
            </header>

            <article class="reader-body">
              <div v-for="block in currentDocDetail.blocks" :key="block.id" class="rich-paragraph">
                <SpiritPreview v-if="block.type !== 'image'" :model-value="block.data" />
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
              <h3>项目文档</h3>
              <p>选择文档开始阅读；在灵脉中将笔记分享至项目后，会显示在这里。</p>
            </div>
          </div>
        </transition>
      </main>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';
import request from '@/utils/request'; 

const props = defineProps<{
  projectId: string;
  initialData?: any; 
}>();

const listError = ref(''), detailError = ref(''), detailLoading = ref(false);
let listVersion = 0, detailVersion = 0;

const isLoading = ref(false);
const documentList = ref<any[]>([]);
const selectedDocId = ref<string | null>(null);
const currentDocDetail = ref<any>(null);

const fetchProjectDocuments = async () => {
  if (!props.projectId) return;
  const version = ++listVersion;
  isLoading.value = true; listError.value = '';
  try {
    const result: any = await request.get(`/Project/${props.projectId}/documents`);
    if (version === listVersion) documentList.value = Array.isArray(result) ? result : result?.data ?? [];
  } catch { if (version === listVersion) listError.value = '项目文档加载失败，请重试'; }
  finally { if (version === listVersion) isLoading.value = false; }
};

const handleSelectDocument = async (noteId: string) => {
  const version = ++detailVersion;
  selectedDocId.value = noteId; currentDocDetail.value = null;
  detailLoading.value = true; detailError.value = '';
  try {
    const result: any = await request.get(`/Project/${props.projectId}/documents/${noteId}`);
    if (version !== detailVersion) return;
    const metadata = documentList.value.find(doc => doc.id === noteId);
    currentDocDetail.value = { ...(result?.data ?? result), pinnedByUserName: metadata?.pinnedByUserName, pinnedAt: metadata?.pinnedAt };
  } catch { if (version === detailVersion) detailError.value = '文档读取失败或已取消分享，请重试'; }
  finally { if (version === detailVersion) detailLoading.value = false; }
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
  detailVersion++; detailLoading.value = false; detailError.value = ''; documentList.value = [];
  selectedDocId.value = null;
  currentDocDetail.value = null;
  fetchProjectDocuments();
}, { immediate: true });

onUnmounted(() => { listVersion++; detailVersion++; });
</script>

<style scoped>
.project-document-root { color: var(--text-primary); min-width: 0; }
.doc-workspace-layout { display: grid; grid-template-columns: 240px minmax(0, 1fr); gap: 24px; align-items: start; }
.doc-sidebar, .doc-viewer-content { border: 1px solid var(--border-line); border-radius: var(--radius-lg); background: var(--paper-card); min-width: 0; }
.doc-sidebar { position: sticky; top: 0; padding: 16px; }
.sidebar-header { display: flex; align-items: center; justify-content: space-between; padding: 4px 0 16px; font-weight: 600; }
.doc-count { color: var(--text-secondary); font-size: 13px; }
.doc-list-flat { display: grid; gap: 8px; max-height: 65vh; overflow-y: auto; }
.doc-list-item { display: grid; gap: 8px; padding: 12px; width: 100%; border: 1px solid transparent; border-radius: var(--radius-sm); background: transparent; color: var(--text-primary); text-align: left; cursor: pointer; }
.doc-list-item:hover { background: var(--bg-sub); }
.doc-list-item.active { background: var(--accent-soft); border-color: var(--accent-color); }
.doc-title { display: block; overflow-wrap: anywhere; font-size: 14px; font-weight: 600; }
.doc-item-meta, .reader-author-bar { display: flex; flex-wrap: wrap; gap: 8px; font-size: 12px; color: var(--text-secondary); }
.sidebar-empty { padding: 20px 0; color: var(--text-secondary); font-size: 14px; }
.doc-viewer-content { min-height: 360px; padding: clamp(20px, 3vw, 36px); }
.doc-reader-inner { max-width: 800px; margin-inline: auto; }
.reader-header { border-bottom: 1px solid var(--border-line); padding-bottom: 24px; margin-bottom: 24px; }
.reader-breadcrumbs { display: flex; flex-wrap: wrap; gap: 8px; color: var(--text-secondary); font-size: 13px; }
.reader-title { font-size: clamp(24px, 3vw, 34px); margin: 16px 0; overflow-wrap: anywhere; }
.reader-body { font-size: 16px; line-height: 1.8; }
.rich-paragraph { margin-bottom: 20px; overflow-wrap: anywhere; }
.rich-image-wrapper img { max-width: 100%; height: auto; border-radius: 8px; }
.img-caption { color: var(--text-secondary); text-align: center; font-size: 13px; }
.doc-state-loading, .layout-placeholder-inner { min-height: 280px; display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; gap: 12px; color: var(--text-secondary); }
.doc-state-loading button { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-sub); color: var(--text-primary); }
.placeholder-content p { max-width: 440px; line-height: 1.7; }
.loading-pulse { width: 32px; height: 4px; background: var(--accent-color); }
@media (max-width: 760px) { .doc-workspace-layout { grid-template-columns: minmax(0, 1fr); gap: 16px; } .doc-sidebar { position: static; } .doc-list-flat { max-height: 220px; } }
</style>
