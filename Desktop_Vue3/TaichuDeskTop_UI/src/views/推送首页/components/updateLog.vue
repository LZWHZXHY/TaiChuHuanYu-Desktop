<template>
  <div class="update-log-wrapper">
    <header class="section-header">
      <h3 class="section-title">开发动态 (Commits)</h3>
    </header>

    <div v-if="loading" class="log-loading">正在追踪代码轨迹...</div>
    
    <div v-else class="log-list">
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
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

const logs = ref<any[]>([]);
const loading = ref(false);

// 替换为你的仓库路径
const GITHUB_REPO = 'LZWHZXHY/TaiChuHuanYu-Desktop'; 

const fetchLogs = async () => {
  loading.value = true;
  try {
    // 🌟 修改：调用 commits 接口
    const response = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/commits?per_page=5`);
    logs.value = await response.json();
  } catch (e) {
    console.error('Failed to fetch GitHub commits', e);
  } finally {
    loading.value = false;
  }
};

const formatDate = (date: string) => new Date(date).toLocaleDateString('en-US', { 
  month: 'short', day: 'numeric', year: 'numeric' 
});

onMounted(fetchLogs);
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
</style>