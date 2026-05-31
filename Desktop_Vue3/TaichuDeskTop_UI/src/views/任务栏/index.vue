<template>
  <div class="workspace-container">
    <div class="workspace-wrapper">
      
      <header class="workspace-header">
        <span class="category-tag">Workspace</span>
        <div class="header-main">
          <h1 class="page-title">任务看板</h1>
          <span class="task-count">{{ tasks.length }} 个可用项目</span>
        </div>
      </header>

      <main class="task-list">
        <section 
          v-for="task in tasks" 
          :key="task.id" 
          class="task-item"
        >
          <div class="task-item-inner">
            
            <div class="task-content">
              <div class="task-title-row">
                <span :class="['status-dot', task.status]"></span>
                <h2 class="task-title">{{ task.title }}</h2>
              </div>
              <p class="task-description">{{ task.description }}</p>
            </div>

            <div class="task-action">
              <button 
                v-if="task.status === 'available'" 
                @click="acceptTask(task.id)"
                class="btn-action btn-accept"
              >
                接取
              </button>
              
              <button 
                v-if="task.status === 'ongoing'" 
                @click="submitTask(task.id)"
                class="btn-action btn-submit"
              >
                提交 &rarr;
              </button>
              
              <span 
                v-if="task.status === 'completed'" 
                class="status-archived"
              >
                已归档
              </span>
            </div>

          </div>
        </section>
      </main>

      <footer class="workspace-footer">
        Focus on the essential.
      </footer>

    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const tasks = ref([
  {
    id: 1,
    title: '构建核心数据模型架构',
    description: '重构当前冗余的 schema 结构，优化流式数据解析器的吞吐性能，预期降低 15% 的内存占用。',
    status: 'available'
  },
  {
    id: 2,
    title: '校对并提炼品牌视觉指南',
    description: '移除多余的辅助色调，确立以字形和间距为主导的排版层级，输出极简 Markdown 文档。',
    status: 'ongoing'
  },
  {
    id: 3,
    title: '移除全局冗余动画组件',
    description: '清理过度的 CSS 过渡效果，恢复纯粹的即时状态切换，提升低端设备的响应速度。',
    status: 'completed'
  }
])

const acceptTask = (id) => {
  const task = tasks.value.find(t => t.id === id)
  if (task) task.status = 'ongoing'
}

const submitTask = (id) => {
  const task = tasks.value.find(t => t.id === id)
  if (task) task.status = 'completed'
}
</script>

<style scoped>
/* ==========================================================================
   1. 设计系统变量 (Design Tokens)
   ========================================================================== */
:theme {
  --bg-color: #fafafa;
  --text-main: #1a1a1a;
  --text-muted: #8c8c8c;
  --text-light: #b3b3b3;
  --border-color: #f0f0f0;
  
  --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
}

/* ==========================================================================
   2. 页面基础布局
   ========================================================================== */
.workspace-container {
  min-height: 100vh;
  background-color: var(--bg-color);
  color: var(--text-main);
  font-family: var(--font-sans);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  padding: 80px 32px;
}

.workspace-wrapper {
  max-width: 540px;
  margin: 0 auto;
}

/* ==========================================================================
   3. 头部排版
   ========================================================================== */
.workspace-header {
  margin-bottom: 64px;
}

.category-tag {
  display: block;
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.15em;
  color: var(--text-light);
  text-transform: uppercase;
  margin-bottom: 8px;
}

.header-main {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}

.page-title {
  font-size: 24px;
  font-weight: 300;
  letter-spacing: -0.02em;
  margin: 0;
}

.task-count {
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

/* ==========================================================================
   4. 任务列表与条目
   ========================================================================== */
.task-list {
  display: flex;
  flex-direction: column;
}

.task-item {
  position: relative;
  padding: 12px 0 32px 0;
  border-bottom: 1px solid var(--border-color);
}

.task-item:last-child {
  border-bottom: none;
}

.task-item-inner {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 24px;
}

.task-content {
  flex: 1;
}

/* 状态圆点与标题 */
.task-title-row {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  display: inline-block;
}

.status-dot.available { background-color: var(--text-light); }
.status-dot.ongoing { background-color: var(--text-main); }
.status-dot.completed { background-color: transparent; border: 1px solid var(--text-light); }

.task-title {
  font-size: 14px;
  font-weight: 500;
  letter-spacing: -0.01em;
  margin: 0;
  color: #262626;
}

.task-description {
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-muted);
  max-width: 420px;
  margin: 0;
  padding-left: 18px; /* 与圆点对齐留出空间 */
}

/* ==========================================================================
   5. 互动按钮与状态
   ========================================================================== */
.task-action {
  display: flex;
  align-items: center;
  padding-top: 2px;
}

.btn-action {
  background: none;
  border: none;
  padding: 4px 8px;
  margin-right: -8px;
  font-family: inherit;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-accept {
  color: var(--text-muted);
}

.btn-accept:hover {
  color: var(--text-main);
}

.btn-submit {
  color: var(--text-main);
  font-weight: 400;
}

.btn-submit:hover {
  letter-spacing: 0.05em; /* 极其微妙的延伸动效 */
}

.status-archived {
  font-size: 12px;
  color: #d9d9d9;
  user-select: none;
  cursor: not-allowed;
}

/* ==========================================================================
   6. 页脚
   ========================================================================== */
.workspace-footer {
  margin-top: 120px;
  padding-top: 32px;
  border-t: 1px solid var(--border-color);
  font-size: 10px;
  letter-spacing: 0.2em;
  color: #d4d4d4;
  text-transform: uppercase;
  text-align: center;
}
</style>