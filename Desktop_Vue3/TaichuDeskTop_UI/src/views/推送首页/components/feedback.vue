<template>
  <div class="feedback-container">
    <div class="feedback-header">
      <h3 class="section-title">意见反馈与公示</h3>
      <button class="submit-btn">我要反馈</button>
    </div>

    <div v-if="feedbackList.length === 0" class="empty-state">
      <p>暂无反馈记录</p>
    </div>

    <ul v-else class="feedback-list">
      <li v-for="item in feedbackList" :key="item.id" class="feedback-item">
        <div class="feedback-meta">
          <span :class="['status-text', item.status]">
            {{ statusMap[item.status] }}
          </span>
          <span class="separator">/</span>
          <span class="user">{{ item.user }}</span>
          <span class="separator">/</span>
          <span class="date">{{ item.date }}</span>
        </div>
        
        <h4 class="feedback-title">{{ item.title }}</h4>
        <p class="feedback-content">{{ item.content }}</p>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';

type FeedbackStatus = 'pending' | 'processing' | 'resolved';

interface FeedbackItem {
  id: number;
  title: string;
  content: string;
  status: FeedbackStatus;
  user: string;
  date: string;
}

// 极简状态映射，只保留文字
const statusMap: Record<FeedbackStatus, string> = {
  pending: '待处理',
  processing: '跟进中',
  resolved: '已解决'
};

const feedbackList = ref<FeedbackItem[]>([
  {
    id: 1,
    title: '签到页面在移动端显示错位',
    content: '使用手机浏览器打开时，签到卡片的右侧被截断了，希望能修复一下响应式布局。',
    status: 'resolved',
    user: '星空行者',
    date: 'May 10, 2026'
  },
  {
    id: 2,
    title: '建议增加深色模式',
    content: '晚上浏览太初寰宇的时候背景太亮了，希望尽早推出 Dark Mode。',
    status: 'processing',
    user: 'CyberOwl',
    date: 'May 11, 2026'
  },
  {
    id: 3,
    title: '文章加载速度偏慢',
    content: '今天访问首页时，图片和动态的加载时间明显变长，不知道是不是服务器波动。',
    status: 'pending',
    user: '匿名用户',
    date: 'May 12, 2026'
  }
]);
</script>

<style scoped>
/* 容器留白与顶部极简分割线 */
.feedback-container {
  margin-top: 56px;
  padding-top: 40px;
  border-top: 1px solid #f0f2f5;
}

.feedback-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 32px;
}

/* 极其克制的模块标题 */
.section-title {
  font-size: 0.85rem;
  font-weight: 500;
  color: #8c959f;
  letter-spacing: 0.1em;
  margin: 0;
}

/* 极简按钮：去边框、去背景，仅用下划线交互 */
.submit-btn {
  background: transparent;
  border: none;
  color: #1f2328;
  font-size: 0.85rem;
  padding: 0;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 4px;
  transition: color 0.3s ease;
}

.submit-btn:hover {
  color: #8c959f;
}

.empty-state {
  color: #a1aebb;
  font-size: 0.9rem;
  font-weight: 300;
}

.feedback-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

/* 摒弃卡片，用超大间距做物理隔离 */
.feedback-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 48px;
}

.feedback-item:last-child {
  margin-bottom: 0;
}

/* 元数据：引入等宽字体，营造冷静、理性的技术感 */
.feedback-meta {
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  gap: 10px;
  font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace;
}

/* 状态颜色的克制表达：仅用灰度区分 */
.status-text { font-weight: 500; }
.pending { color: #8c959f; }      /* 待处理：浅灰 */
.processing { color: #1f2328; }   /* 处理中：深黑（视觉重心高） */
.resolved { color: #d0d7de; }     /* 已解决：极淡（视觉弱化，表示已完结） */

.separator {
  color: #d0d7de;
  font-weight: 300;
}

.user, .date {
  color: #a1aebb;
}

/* 标题排版 */
.feedback-title {
  margin: 0;
  font-size: 1.1rem;
  color: #1f2328;
  line-height: 1.5;
  font-weight: 400;
}

/* 内容排版：弱化颜色，减小字号，避免喧宾夺主 */
.feedback-content {
  margin: 0;
  font-size: 0.95rem;
  color: #6e7781;
  line-height: 1.6;
  font-weight: 300;
}

/* 移动端适配 */
@media (max-width: 600px) {
  .feedback-item {
    margin-bottom: 36px;
  }
  .feedback-title {
    font-size: 1.05rem;
  }
}
</style>