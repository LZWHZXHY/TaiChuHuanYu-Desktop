<template>
  <div class="project-document-root">
    
    <div class="layout-placeholder">
      <div class="placeholder-content">
        <div class="placeholder-icon">📋</div>
        <h3>文档共建维度已就绪</h3>
        <p>太初长卷，待你落笔。正在编织具体的协同业务逻辑...</p>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
// 🌟 核心修复 2：显式声明并捕获父组件强加过来的所有 Props，防止其沦为流浪的 Extraneous Attributes
defineProps<{
  projectId: string;
  initialData?: any; // 完美签收外界传进来的项目全量元数据
}>();

// 🌟 核心修复 3：显式声明对外冒泡的自定义事件，解除 Non-emits event listeners 警报
defineEmits(['updated']);
</script>

<style scoped>
/* 保持与 ProjectDetail.vue 风格完美统一的精美占位符样式 */
.project-document-root {
  width: 100%;
  animation: fadeIn 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}

.layout-placeholder {
  background: #fff;
  border: 1px solid #f5f5f5;
  height: 500px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 40px 80px rgba(0, 0, 0, 0.02);
}

.placeholder-content {
  text-align: center;
}

.placeholder-icon {
  font-size: 3rem;
  color: #eee;
  margin-bottom: 20px;
}

.placeholder-content h3 {
  font-size: 1.1rem;
  font-weight: 400;
  color: #ccc;
  margin-bottom: 8px;
  letter-spacing: 0.5px;
}

.placeholder-content p {
  font-size: 0.8rem;
  color: #ddd;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>