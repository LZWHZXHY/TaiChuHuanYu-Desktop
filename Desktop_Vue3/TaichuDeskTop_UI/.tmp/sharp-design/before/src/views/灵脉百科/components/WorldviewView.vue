<template>
  <div class="worldview-container">
    <div v-if="Object.keys(groupedEntries).length === 0" class="empty-state">
      <div class="empty-icon">✧</div>
      <p>虚空之中，尚未编织任何设定...</p>
    </div>

    <section 
      v-for="(entries, categoryName) in groupedEntries" 
      :key="categoryName" 
      class="lore-section"
    >
      <div class="section-header">
        <h2 class="section-title">{{ categoryName }}</h2>
        <span class="section-count">{{ entries.length }} 篇档案</span>
      </div>
      
      <div class="lore-grid">
        <div 
          v-for="entry in entries" 
          :key="entry.id" 
          class="lore-card"
          @click="$emit('go-detail', entry.id)"
        >
          <div class="card-glow"></div>
          <div class="card-content">
            <h3 class="card-title">{{ entry.title }}</h3>
            <p class="card-desc">{{ extractText(entry.excerpt) || '引流灵脉，窥探世界的一角...' }}</p>
          </div>
          <div class="card-footer">
            <span class="ref-id">ID: {{ String(entry.id).substring(0, 6).toUpperCase() }}</span>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  entries: any[]
}>();

defineEmits(['go-detail']);

// 提取第一个 Tag 作为分组依据
const getFirstTag = (tags: any) => {
  if (!tags) return '未分类宗卷';
  if (typeof tags === 'string') return tags.split(',')[0].trim();
  if (Array.isArray(tags) && tags.length > 0) return tags[0].trim();
  return '未分类宗卷';
};

// 尝试从 JSON 摘要中提取纯文本
const extractText = (content: any) => {
  if (!content) return '';
  try {
    const parsed = typeof content === 'string' ? JSON.parse(content) : content;
    // 简单提取逻辑，可根据你的 SpiritPreview 数据结构调整
    if (parsed && parsed.content && parsed.content[0] && parsed.content[0].content) {
      return parsed.content[0].content[0].text || '';
    }
  } catch(e) {}
  return typeof content === 'string' && !content.startsWith('{') ? content : '';
};

// 核心逻辑：按 Tag 对条目进行分组聚类
const groupedEntries = computed(() => {
  const groups: Record<string, any[]> = {};
  props.entries.forEach(entry => {
    const tag = getFirstTag(entry.tags);
    if (!groups[tag]) {
      groups[tag] = [];
    }
    groups[tag].push(entry);
  });
  
  // 可以根据需要对 key 进行排序，比如把 '核心设定' 永远排在第一位
  return groups;
});
</script>

<style scoped>
.worldview-container {
  display: flex;
  flex-direction: column;
  gap: 48px; /* 分区之间的巨大留白 */
  padding-bottom: 40px;
}

.section-header {
  display: flex;
  align-items: baseline;
  gap: 16px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--c-border);
  padding-bottom: 12px;
}

.section-title {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--c-text-main);
  margin: 0;
  letter-spacing: 0.05em;
  /* 加入一点点衬线字体感觉，更适合世界观 */
  font-family: "Noto Serif SC", STSong, serif; 
}

.section-count {
  font-size: 0.85rem;
  color: var(--c-text-light);
  font-family: var(--font-mono);
}

.lore-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.lore-card {
  position: relative;
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(10px);
  border: 1px solid var(--c-border);
  border-radius: 12px;
  padding: 24px;
  cursor: pointer;
  overflow: hidden;
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  display: flex;
  flex-direction: column;
  min-height: 160px;
}

.card-glow {
  position: absolute;
  top: 0; left: 0; right: 0; height: 100%;
  background: radial-gradient(circle at top right, rgba(0,0,0,0.03), transparent 60%);
  opacity: 0;
  transition: opacity 0.4s ease;
}

.lore-card:hover {
  transform: translateY(-4px) scale(1.01);
  border-color: var(--c-brand);
  box-shadow: 0 12px 32px rgba(0,0,0,0.08);
}

.lore-card:hover .card-glow { opacity: 1; }

.card-content { flex: 1; position: relative; z-index: 1; }

.card-title {
  font-size: 1.15rem;
  font-weight: 600;
  margin: 0 0 12px;
  color: var(--c-text-main);
  line-height: 1.4;
}

.card-desc {
  font-size: 0.85rem;
  color: var(--c-text-muted);
  line-height: 1.6;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-footer {
  margin-top: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  position: relative;
  z-index: 1;
}

.ref-id {
  font-family: var(--font-mono);
  font-size: 0.7rem;
  color: var(--c-text-light);
  background: #f4f4f5;
  padding: 4px 8px;
  border-radius: 4px;
}

.card-footer svg {
  width: 16px; height: 16px;
  color: var(--c-text-light);
  transition: transform 0.3s, color 0.3s;
}

.lore-card:hover .card-footer svg {
  transform: translateX(4px);
  color: var(--c-brand);
}

.empty-state { text-align: center; padding: 100px 0; color: var(--c-text-light); }
.empty-icon { font-size: 2rem; margin-bottom: 16px; opacity: 0.5; }
</style>