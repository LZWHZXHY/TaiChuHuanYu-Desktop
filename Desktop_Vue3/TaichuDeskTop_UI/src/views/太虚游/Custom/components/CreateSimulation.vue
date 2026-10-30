<template>
  <div class="create-simulation workbench-skin editor-surface community-surface tone-play">
    <header class="create-header studio-heading">
      <button class="btn-back" @click="$emit('cancel')">← 返回</button>
      <h2>🌱 创建模拟游戏</h2>
      <p>配置你的模拟游戏参数</p>
    </header>

    <div class="coming-soon-placeholder">
      <div class="placeholder-icon">⏳</div>
      <h3>模拟游戏创建器开发中</h3>
      <p>自定义模拟游戏功能正在建设中，敬请期待</p>
      <p class="placeholder-detail">未来将支持：人生模拟器、经营模拟、文字冒险等</p>
      <button class="btn-line" @click="$emit('cancel')">返回</button>
    </div>
  </div>
</template>

<script setup>
defineEmits(['cancel', 'success'])
</script>

<style scoped>
.create-simulation {
  max-width: 700px;
  margin: 0 auto;
  padding: 20px 0;
}
.create-header {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--border-line);
  padding-bottom: 12px;
}
.btn-back {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.3s;
}
.btn-back:hover {
  color: var(--accent-color);
}
.create-header h2 {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: .02em;
  color: var(--text-primary);
  margin: 0;
}
.create-header p {
  font-size: 14px;
  color: var(--text-secondary);
  letter-spacing: 0.1em;
  margin: 0 0 0 auto;
}

.coming-soon-placeholder {
  text-align: center;
  padding: 60px 20px;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
}
.placeholder-icon {
  font-size: 64px;
  margin-bottom: 16px;
}
.coming-soon-placeholder h3 {
  font-size: 22px;
  font-weight: 400;
  color: var(--text-primary);
  margin: 0 0 8px;
}
.coming-soon-placeholder p {
  font-size: 14px;
  color: var(--text-secondary);
  letter-spacing: 0.1em;
  margin: 0 0 4px;
}
.placeholder-detail {
  font-size: 13px;
  opacity: 0.7;
  margin: 8px 0 20px !important;
}
.btn-line {
  background: none;
  border: 1px solid var(--border-line);
  color: var(--text-primary);
  padding: 6px 24px;
  font-family: inherit;
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-line:hover {
  border-color: var(--accent-color);
  color: var(--accent-color);
}
</style>