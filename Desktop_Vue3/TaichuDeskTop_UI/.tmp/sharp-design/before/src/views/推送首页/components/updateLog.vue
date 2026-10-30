<template>
  <div class="update-log-wrapper">
    <header class="section-header">
      <h3 class="section-title">开发提交记录</h3>
    </header>

    <p v-if="error" class="log-error" role="alert">{{ error }}</p>
    <div class="log-list">
      <article v-for="log in logs" :key="log.sha" class="log-item">
        <div class="log-meta">
          <img :src="log.author?.avatar_url" class="avatar" />
          <span class="author-name">{{ log.commit.author.name }}</span>
          <span class="date">{{ formatDate(log.commit.author.date) }}</span>
        </div>
        <div class="log-body">
          <p class="commit-msg">{{ log.commit.message }}</p>
          <a :href="log.html_url" target="_blank" rel="noopener noreferrer" class="commit-link">查看详情 ></a>
        </div>
      </article>
    </div>

    <div class="load-more-container">
      <button 
        v-if="!loading && (hasMore || error)" 
        class="load-more-btn" 
        @click="loadMore"
      >
        {{ error ? '重试加载' : '查看更早记录' }}
      </button>
      <div v-else class="log-loading">正在追踪更多轨迹...</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

const logs = ref<any[]>([]);
const loading = ref(false);
const error = ref('');
const hasMore = ref(true);
const currentPage = ref(1); // 🌟 记录页码
const GITHUB_REPO = 'LZWHZXHY/TaiChuHuanYu-Desktop'; 

const fetchLogs = async (page: number) => {
  if (loading.value) return;
  loading.value = true; error.value = '';
  try {
    // 🌟 使用 per_page=5 和 page=n 分页参数
    const response = await fetch(
      `https://api.github.com/repos/${GITHUB_REPO}/commits?per_page=5&page=${page}`
    );
    if (!response.ok) throw new Error('请求失败');
    const data = await response.json();
    if (!Array.isArray(data)) throw new Error('响应格式错误');
    hasMore.value = data.length === 5;
    currentPage.value = page;
    
    // 如果是第一页，直接覆盖；否则追加
    if (page === 1) {
      logs.value = data;
    } else {
      logs.value.push(...data);
    }
  } catch (e) {
    error.value = '开发记录暂时无法读取，已加载的记录仍保留。';
  } finally {
    loading.value = false;
  }
};

const loadMore = () => {
  fetchLogs(logs.value.length ? currentPage.value + 1 : 1);
};

const formatDate = (date: string) => new Date(date).toLocaleDateString('zh-CN', { 
  month: 'short', day: 'numeric', year: 'numeric' 
});

onMounted(() => fetchLogs(1));
</script>

<style scoped>
.update-log-wrapper { margin-top: 32px; border-top: 1px solid var(--border-line); padding-top: 24px; }
.section-title { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); letter-spacing: 0.1em; margin-bottom: 16px; }

.log-item { margin-bottom: 0; border-bottom: 1px solid var(--border-line); padding: 14px 0; }
.log-meta { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; font-size: 0.8rem; }
.avatar { width: 20px; height: 20px; border-radius: 50%; }
.author-name { font-weight: 600; color: var(--text-primary); }
.date { color: var(--text-secondary); }

.commit-msg { font-size: 0.95rem; color: var(--text-primary); margin: 0 0 4px 0; white-space: pre-wrap; overflow-wrap: anywhere; }
.commit-link { font-size: 0.75rem; color: var(--accent-color); text-decoration: none; }
.commit-link:hover { text-decoration: underline; }

/* 🌟 加载更多按钮样式 */
.load-more-container { margin-top: 20px; text-align: center; }
.load-more-btn { 
  background: transparent; border: 1px solid var(--bg-sub); padding: 8px 16px; 
  cursor: pointer; font-size: 0.8rem; color: var(--text-secondary); transition: 0.3s;
}
.load-more-btn:hover { border-color: var(--text-primary); color: var(--text-primary); }
.log-error { color: var(--danger); font-size: 13px; }
.log-body { display: flex; align-items: baseline; justify-content: space-between; gap: 20px; }.commit-link { white-space: nowrap; }.log-meta { flex-wrap: wrap; }
@media(max-width:600px) { .log-body { display: block; } }
</style>