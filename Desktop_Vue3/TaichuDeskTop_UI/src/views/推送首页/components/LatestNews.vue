<template>
  <div class="news-container">
    <h3 class="section-title">最新动态</h3>

    <div v-if="newsList.length === 0" class="empty-state">
      <p>暂无新动态</p>
    </div>

    <ul v-else class="news-list">
      <li v-for="item in newsList" :key="item.id" class="news-item">
        <div class="news-meta">
          <span class="news-date">{{ item.date }}</span>
          <span class="news-separator">/</span>
          <span class="news-tag">{{ item.type }}</span>
        </div>
        <a href="javascript:void(0)" class="news-title" :title="item.title">
          {{ item.title }}
        </a>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';

interface NewsItem {
  id: number;
  title: string;
  type: string;
  date: string;
}

// 模拟动态数据
const newsList = ref<NewsItem[]>([
  { 
    id: 1, 
    title: '太初寰宇 V2.0 架构升级：视觉与交互全面重构', 
    type: '公告', 
    date: 'May 12, 2026' 
  },
  { 
    id: 2, 
    title: '新增 3D 模型与 AI 音乐生成接口，开放创作者生态测试', 
    type: '更新', 
    date: 'May 08, 2026' 
  },
  { 
    id: 3, 
    title: '周末创作者沙龙：独立游戏资产构建与分发经验分享', 
    type: '活动', 
    date: 'May 01, 2026' 
  }
]);
</script>

<style scoped>
/* 容器留白 */
.news-container {
  margin-bottom: 56px;
}

/* 极其克制的模块标题：较小字号、全大写、宽字距、弱灰色 */
.section-title {
  font-size: 0.85rem;
  font-weight: 500;
  color: #8c959f;
  letter-spacing: 0.1em;
  margin: 0 0 32px 0;
}

.empty-state {
  color: #a1aebb;
  font-size: 0.9rem;
  font-weight: 300;
}

.news-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

/* 每条动态之间的垂直留白，替代分割线 */
.news-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 36px;
}

.news-item:last-child {
  margin-bottom: 0;
}

/* 元数据：日期与分类。引入等宽字体营造技术感与秩序感 */
.news-meta {
  font-size: 0.8rem;
  color: #a1aebb;
  display: flex;
  align-items: center;
  gap: 10px;
  font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace;
}

.news-separator {
  font-weight: 300;
  opacity: 0.4;
}

.news-tag {
  letter-spacing: 0.05em;
}

/* 标题：阅读的核心视觉落点，深色、舒展的行高 */
.news-title {
  font-size: 1.1rem;
  color: #1f2328;
  text-decoration: none;
  line-height: 1.6;
  font-weight: 400;
  transition: color 0.4s ease; /* 极度平滑的过渡动画 */
}

/* Hover 状态：不做背景色变化，仅让文字变得像墨水稍微淡化 */
.news-title:hover {
  color: #6e7781;
}

/* 移动端适配：保证在小屏幕上也有足够的呼吸感 */
@media (max-width: 600px) {
  .news-item {
    margin-bottom: 28px;
  }
  .news-title {
    font-size: 1.05rem;
  }
}
</style>