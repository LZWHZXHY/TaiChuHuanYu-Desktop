<template>
  <div class="update-log-wrapper">
    <header class="section-header">
      <h3 class="section-title">开发动态 (Commits)</h3>
    </header>

    <div class="log-list">
      <article v-for="log in logs" :key="log.sha" class="log-item">
        <div class="log-meta">
          <img :src="log.author?.avatar_url" class="avatar" />
          <span class="author-name">{{ log.commit.author.name }}</span>
          <span class="date">{{ formatDate(log.commit.author.date) }}</span>
        </div>
        <div class="log-body">
          <p class="commit-msg">{{ log.commit.message }}</p>
          <a :href="log.html_url" target="_blank" class="commit-link">查看详情 ></a>
        </div>
      </article>
    </div>

    <div class="load-more-container">
      <button 
        v-if="!loading" 
        class="load-more-btn" 
        @click="loadMore"
      >
        查看更早记录
      </button>
      <div v-else class="log-loading">正在追踪更多轨迹...</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

const logs = ref<any[]>([]);
const loading = ref(false);
const currentPage = ref(1); // 🌟 记录页码
const GITHUB_REPO = 'LZWHZXHY/TaiChuHuanYu-Desktop'; 

const fetchLogs = async (page: number) => {
  loading.value = true;
  try {
    // 🌟 使用 per_page=5 和 page=n 分页参数
    const response = await fetch(
      `https://api.github.com/repos/${GITHUB_REPO}/commits?per_page=5&page=${page}`
    );
    const data = await response.json();
    
    // 如果是第一页，直接覆盖；否则追加
    if (page === 1) {
      logs.value = data;
    } else {
      logs.value.push(...data);
    }
  } catch (e) {
    console.error('Failed to fetch GitHub commits', e);
  } finally {
    loading.value = false;
  }
};

const loadMore = () => {
  currentPage.value++;
  fetchLogs(currentPage.value);
};

const formatDate = (date: string) => new Date(date).toLocaleDateString('en-US', { 
  month: 'short', day: 'numeric', year: 'numeric' 
});

onMounted(() => fetchLogs(1));
</script>

<style scoped>
.update-log-wrapper { margin-top: 56px; border-top: 1px solid #f0f2f5; padding-top: 40px; }
.section-title { font-size: 0.85rem; font-weight: 500; color: #8c959f; letter-spacing: 0.1em; margin-bottom: 32px; }

.log-item { margin-bottom: 32px; border-left: 2px solid #f0f2f5; padding-left: 16px; }
.log-meta { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; font-size: 0.8rem; }
.avatar { width: 20px; height: 20px; border-radius: 50%; }
.author-name { font-weight: 600; color: #1f2328; }
.date { color: #8c959f; }

.commit-msg { font-size: 0.95rem; color: #333; margin: 0 0 4px 0; }
.commit-link { font-size: 0.75rem; color: #0066cc; text-decoration: none; }
.commit-link:hover { text-decoration: underline; }

/* 🌟 加载更多按钮样式 */
.load-more-container { margin-top: 20px; text-align: center; }
.load-more-btn { 
  background: transparent; border: 1px solid #f0f2f5; padding: 8px 16px; 
  cursor: pointer; font-size: 0.8rem; color: #8c959f; transition: 0.3s;
}
.load-more-btn:hover { border-color: #1f2328; color: #1f2328; }
</style>