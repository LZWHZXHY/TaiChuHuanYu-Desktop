<template>
  <article class="wiki-minimalist-page" v-if="entry">
    <header class="header">
      <div class="header-actions">
        <button class="back-btn" @click="router.back()">← 返回</button>
        <button class="edit-btn" @click="handleEditRequest">申请编辑</button>
      </div>

      <h1 class="title">{{ entry.title }}</h1>
      <div class="meta">
        <span>{{ formatDate(entry.publishedAt) }}</span>
        <span v-if="tags.length">· {{ tags.join(', ') }}</span>
      </div>
    </header>

    <section class="content">
      <SpiritPreview v-if="entry.content" :modelValue="entry.content" />
    </section>

    <footer class="footer" v-if="entry.contributors?.length > 0">
      <div class="contributors">
        <span class="label">编织者:</span>
        <span v-for="c in entry.contributors" :key="c" class="c-item">{{ c }}</span>
      </div>
    </footer>
  </article>

  <div v-else class="loading-state">正在载入灵脉碎片...</div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { wikiApi } from '@/api/Wiki'; 
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const route = useRoute();
const router = useRouter();
const entry = ref<any>(null);

const tags = computed(() => {
  if (!entry.value?.tags) return [];
  return typeof entry.value.tags === 'string' ? entry.value.tags.split(',') : entry.value.tags;
});

// 🌟 这里是修改的关键
const handleEditRequest = () => {
  router.push({
    path: '/lingmai', // 确保这个路径指向你的 index.vue (编辑器)
    query: { 
      id: route.params.id as string, 
      mode: 'wiki' 
    }
  });
};

const loadDetail = async () => {
  try {
    const res = await wikiApi.getArticleDetail(route.params.id as string);
    
    if (res.content && typeof res.content === 'string') {
      try {
        // 🌟 引擎 1：尝试作为完整的单体 JSON 解析 (兼容上次的格式)
        let parsed = JSON.parse(res.content);
        
        if (parsed.type === 'doc') {
          res.content = parsed;
        } else {
          res.content = {
            type: 'doc',
            content: Array.isArray(parsed) ? parsed : [parsed]
          };
        }
      } catch (e) {
        // 🌟 引擎 2：如果走到 catch，说明遇到换行符拦截了。切换为多行切片解析 (兼容您当前这份 6.0 教程的数据)
        try {
          const lines = res.content.split('\n').filter((line: string) => line.trim() !== '');
          const nodes = lines.map((line: string) => {
            const node = JSON.parse(line);
            // 为缺失 type 的节点补全基础类型，防止 Tiptap 渲染崩溃
            if (!node.type) {
              node.type = 'paragraph'; 
            }
            return node;
          });

          res.content = {
            type: 'doc',
            content: nodes
          };
        } catch (fallbackError) {
          console.error("Content 灵脉链多行碎片解析也失败了，可能数据已损坏:", fallbackError);
        }
      }
    }
    
    entry.value = res;
  } catch (err) {
    console.error('词条读取失败:', err);
  }
};
const formatDate = (d: string) => d ? d.substring(0, 10).replace(/-/g, '/') : '';

onMounted(loadDetail);
</script>

<style scoped>
.wiki-minimalist-page { 
  max-width: 720px; 
  margin: 60px auto 120px; 
  padding: 0 20px; 
  color: #1d1d1f; 
  line-height: 1.8; 
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; 
}

.header { 
  margin-bottom: 64px; 
  position: relative;
}

.header-actions { 
  display: flex; justify-content: space-between; align-items: center; 
  margin-bottom: 40px; 
}

.back-btn { 
  display: flex; align-items: center; gap: 6px;
  background: none; border: none; font-size: 0.9rem; 
  cursor: pointer; padding: 6px 12px; margin-left: -12px;
  color: #86868b; border-radius: 6px; transition: all 0.2s;
  font-weight: 500;
}
.back-btn:hover { background: #f5f5f7; color: #1d1d1f; }

.edit-btn { 
  background: #1d1d1f; color: #fff; border: none; 
  padding: 8px 16px; font-size: 0.85rem; font-weight: 600;
  cursor: pointer; border-radius: 8px; transition: all 0.2s; 
}
.edit-btn:hover { background: #333; transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0,0,0,0.1); }

.title { 
  font-size: 3rem; 
  font-weight: 800; 
  margin: 0 0 24px; 
  letter-spacing: -0.03em; 
  line-height: 1.15; 
  /* 标题可使用更加雄浑的衬线体 */
  font-family: "Noto Serif SC", STSong, serif;
}

.meta { 
  font-size: 0.9rem; color: #86868b; font-weight: 500;
  display: flex; align-items: center; gap: 8px;
}

.meta span:not(:last-child)::after {
  content: "•"; margin-left: 8px; color: #d1d1d6;
}

.content { 
  font-size: 1.1rem; 
  margin-bottom: 80px; 
  /* 深度优化内部 Tiptap 渲染出来的元素间距 */
}
.content :deep(p) { margin-bottom: 1.5em; }
.content :deep(h2), .content :deep(h3) { margin-top: 2em; margin-bottom: 1em; font-weight: 700; }

.footer { 
  border-top: 1px solid #e5e5ea; 
  padding-top: 32px; font-size: 0.85rem; color: #86868b; 
}
.contributors { display: flex; gap: 8px; align-items: center; flex-wrap: wrap;}
.label { font-weight: 600; color: #1d1d1f; }
.c-item { background: #f5f5f7; padding: 4px 10px; border-radius: 20px; color: #1d1d1f; font-weight: 500; }

.loading-state { text-align: center; margin-top: 200px; color: #86868b; font-size: 0.9rem; letter-spacing: 0.1em; text-transform: uppercase; }
</style>