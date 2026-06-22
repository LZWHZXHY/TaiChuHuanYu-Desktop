<template>
  <article class="doc-body">
    <div class="content-layout">
      <!-- 左侧内容 -->
      <section class="main-content">
        <p class="lead-text">
          欢迎进入<strong>太初寰宇</strong> 
        </p>
        <div class="info-block">
          <p>这里是太初寰宇的首页，你可以在这里进行每日签到，也可以获得最新的相关资讯和信息</p>
        </div>
        
        <LatestNews />
        <feedback />
        <updateLog />
      </section>

      <!-- 右侧边栏 -->
      <aside class="side-widgets">
        <!-- 1. 签到卡片 -->
        <SignCard />
        
        <!-- 2. 数据看板 -->
        <DataPanel class="side-data-panel" />

        <!-- 3. 🌟 财政公开看板 -->
        <FinancePanel />
      </aside>
    </div>
  </article>
</template>

<script setup lang="ts">
import SignCard from '../../components/SignCard.vue'
import feedback from './components/feedback.vue';
import LatestNews from './components/LatestNews.vue';
import updateLog from './components/updateLog.vue';
import DataPanel from './components/DataPanel.vue'; 
// 🌟 引入财政报告组件
import FinancePanel from './components/FinancePanel.vue'; 
</script>

<style scoped>
.content-layout {
  display: flex;
  gap: 40px;
  align-items: flex-start;
}

.main-content {
  flex: 1;
}

.side-widgets {
  width: 400px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 24px; /* 🌟 完美的组件纵向间距，三个卡片天然对齐 */
}

.doc-body {
  width: 100%;
  color: #24292f;
}

@media (max-width: 1024px) {
  .content-layout {
    flex-direction: column;
    gap: 20px;
  }
  .side-widgets { width: 100%; }
}

.lead-text { font-size: 1.25rem; line-height: 1.6; margin-bottom: 24px; }
.info-block {
  background: #f6f8fa;
  border-left: 4px solid #24292f;
  padding: 16px 24px;
  margin-bottom: 32px;
  border-radius: 4px;
}
</style>