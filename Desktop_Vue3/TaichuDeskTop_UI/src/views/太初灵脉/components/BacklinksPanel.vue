<template>
  <aside class="spirit-backlinks-panel">
    <div class="panel-header">
      <div class="header-tabs">
        <span 
          class="tab-item" 
          :class="{ active: activeTab === 'backlinks' }"
          @click="activeTab = 'backlinks'"
        >
          引用此篇 <span class="count">{{ backlinks.length }}</span>
        </span>
        <span 
          class="tab-item" 
          :class="{ active: activeTab === 'outlinks' }"
          @click="activeTab = 'outlinks'"
        >
          正向引用 <span class="count">{{ outlinks.length }}</span>
        </span>
      </div>
    </div>

    <div class="links-container">
      <div v-if="isLoading" class="panel-loading">
        <div class="mini-spinner"></div>
        <span>正在感应星图...</span>
      </div>

      <div v-else-if="currentList.length > 0" class="links-list">
        <div 
          v-for="link in currentList" 
          :key="link.id" 
          class="link-card"
          @click="handleSelect(link.id)"
        >
          <div class="link-title">{{ link.title || '无标题碎片' }}</div>
          <div v-if="link.excerpt" class="link-excerpt">{{ link.excerpt }}</div>
          <div class="link-time">{{ formatTime(link.updatedAt) }}</div>
        </div>
      </div>

      <div v-else class="empty-state">
        <div class="empty-icon">🕸️</div>
        <p>{{ activeTab === 'backlinks' ? '此碎片尚未与其他灵脉交织' : '此碎片尚未指向其他任何灵脉' }}</p>
      </div>
    </div>

    <div class="tags-section">
      <div class="label">标签 / TAGS</div>
      <div class="tag-cloud">
        <span class="tag-item">#太初</span>
        <span class="tag-item">#灵脉节点</span>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { lingmaiApi } from '../../../api/lingmai';

const props = defineProps<{
  noteId?: string; // 🌟 加上问号，将其变为可选属性
}>();

const emit = defineEmits<{
  (e: 'select', id: string): void
}>();

const activeTab = ref<'backlinks' | 'outlinks'>('backlinks');
const isLoading = ref(false);
const backlinks = ref<any[]>([]);
const outlinks = ref<any[]>([]);

const currentList = computed(() => {
  return activeTab.value === 'backlinks' ? backlinks.value : outlinks.value;
});

const fetchNoteRelations = async (id: string) => {
  if (!id) return;
  isLoading.value = true;
  try {
    const [backRes, outRes]: any = await Promise.all([
      lingmaiApi.getBacklinks(id),
      lingmaiApi.getOutlinks(id)
    ]);
    backlinks.value = backRes || [];
    outlinks.value = outRes || [];
  } catch (error) {
    console.error('拉取笔记关系失败:', error);
  } finally {
    isLoading.value = false;
  }
};

watch(() => props.noteId, (newId) => {
  if (newId) {
    fetchNoteRelations(newId);
  } else {
    // 如果没有传入合法的 ID，清空旧数据
    backlinks.value = [];
    outlinks.value = [];
  }
}, { immediate: true });

const handleSelect = (id: string) => {
  emit('select', id);
};

const formatTime = (timeStr: string) => {
  if (!timeStr) return '';
  const date = new Date(timeStr);
  return `${date.getMonth() + 1}月${date.getDate()}日 ${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
};
</script>

<style scoped>
.spirit-backlinks-panel {
  width: 280px;
  border-left: 1px solid #f2f2f7;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 24px 20px;
  user-select: none;
}

.panel-header {
  margin-bottom: 20px;
}

.header-tabs {
  display: flex;
  gap: 16px;
  border-bottom: 1px solid #f2f2f7;
  padding-bottom: 8px;
}

.tab-item {
  font-size: 11px;
  font-weight: 700;
  color: #86868b;
  cursor: pointer;
  padding-bottom: 4px;
  position: relative;
  letter-spacing: 0.05em;
  transition: all 0.2s;
}

.tab-item.active {
  color: #0066cc;
}

.tab-item.active::after {
  content: '';
  position: absolute;
  bottom: -9px;
  left: 0; right: 0;
  height: 2px;
  background: #0066cc;
}

.count {
  font-size: 10px;
  background: #f5f5f7;
  padding: 2px 6px;
  border-radius: 4px;
  color: #86868b;
  margin-left: 2px;
}

.links-container {
  flex: 1;
  overflow-y: auto;
  margin-bottom: 16px;
}

.panel-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  height: 120px;
  color: #86868b;
  font-size: 11px;
}

.mini-spinner {
  width: 16px; height: 16px;
  border: 2px solid #f2f2f7;
  border-top-color: #0066cc;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.link-card {
  padding: 12px;
  border: 1px solid #f2f2f2;
  border-radius: 12px;
  margin-bottom: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.link-card:hover {
  border-color: #0066cc;
  background: #fbfbfb;
  transform: translateY(-1px);
}

.link-title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #1d1d1f;
}

.link-excerpt {
  font-size: 11px;
  color: #86868b;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.4;
  margin-bottom: 6px;
}

.link-time {
  font-size: 10px;
  color: #c7c7cc;
}

.empty-state {
  text-align: center;
  padding-top: 60px;
  color: #d2d2d7;
}

.empty-icon { font-size: 24px; margin-bottom: 12px; opacity: 0.5; }
.empty-state p { font-size: 12px; }

.tags-section {
  margin-top: 40px;
  padding-top: 20px;
  border-top: 1px solid #f2f2f2;
}

.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.tag-item {
  font-size: 11px;
  color: #0066cc;
  background: #f0f7ff;
  padding: 4px 10px;
  border-radius: 20px;
}

@media (max-width: 1200px) {
  .spirit-backlinks-panel { display: none; }
}
</style>