<template>
  <article class="doc-body">
    <div class="content-layout">
      <section class="main-content">
        <p class="lead-text">
          欢迎进入<strong>太初寰宇</strong> 。
        </p>
        <div class="info-block">
          <p>这里是你的个人枢纽。你可以在此查看最新的系统推送、管理灵脉数据以及进行每日签到。</p>
        </div>
        
        <div class="placeholder-section">
          <h3>最新动态</h3>
          <p style="color: #8c959f;">暂无新动态...</p>
        </div>
      </section>

      <aside class="side-widgets">
        <SignCard />
      </aside>
    </div>
  </article>
</template>

<script setup lang="ts">
import SignCard from '../../components/SignCard.vue'
</script>

<style scoped>
/* PC 端双栏布局 */
.content-layout {
  display: flex;
  gap: 40px;
  align-items: flex-start;
}

.main-content {
  flex: 1; /* 占据剩余空间 */
}

.side-widgets {
  width: 400px; /* PC 端固定一个精致的宽度 */
  flex-shrink: 0;
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
  
  .side-widgets {
    width: 100%; 
  }
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