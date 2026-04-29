<template>
  <aside class="spirit-backlinks-panel">
    <div class="panel-header">
      <span class="label">引用此篇 / BACKLINKS</span>
      <span class="count">{{ linkedNotes.length }}</span>
    </div>

    <div class="links-container">
      <div v-if="linkedNotes.length > 0" class="links-list">
        <div 
          v-for="link in linkedNotes" 
          :key="link.id" 
          class="link-card"
          @click="$emit('select', link.id)"
        >
          <div class="link-title">{{ link.title }}</div>
          <div class="link-excerpt">{{ link.excerpt }}</div>
        </div>
      </div>

      <div v-else class="empty-state">
        <div class="empty-icon">🕸️</div>
        <p>此碎片尚未与其他灵脉交织</p>
      </div>
    </div>

    <div class="tags-section">
      <div class="label">标签 / TAGS</div>
      <div class="tag-cloud">
        <span class="tag-item">#太初</span>
        <span class="tag-item">#宇宙</span>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref } from 'vue';

// 1. 定义双向链接的接口，防止被推断为 never
interface Backlink {
  id: string;
  title: string;
  excerpt: string;
}

// 2. 显式指定 ref 的泛型类型为 Backlink[]
// 这样即使现在数组是空的，TS 也知道以后它会装载符合 Backlink 结构的对象
const linkedNotes = ref<Backlink[]>([
  // 模拟一条数据看看效果，以后这里通过 useSpiritData 计算得出
  /*
  { 
    id: '101', 
    title: '混沌纪元', 
    excerpt: '在那个时代，灵脉初生，万物之始皆刻印于虚空之中...' 
  }
  */
]);

defineEmits<{
  (e: 'select', id: string): void
}>();
</script>

<style scoped>
/* 样式保持不变... */
.spirit-backlinks-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #ffffff;
  padding: 24px 20px;
  user-select: none;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.label {
  font-size: 11px;
  font-weight: 700;
  color: #86868b;
  letter-spacing: 0.1em;
}

.count {
  font-size: 10px;
  background: #f5f5f7;
  padding: 2px 6px;
  border-radius: 4px;
  color: #86868b;
}

.links-container {
  flex: 1;
  overflow-y: auto;
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
}

.link-title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 4px;
}

.link-excerpt {
  font-size: 11px;
  color: #86868b;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
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