<template>
  <div class="temp-holder">
    <div class="temp-card">
      <div class="temp-icon">{{ gameInfo?.icon || '🔮' }}</div>
      <h2 class="temp-title">{{ gameInfo?.label || '神秘游戏' }}</h2>
      <p class="temp-status" :class="statusClass">{{ gameInfo?.status || '开发中' }}</p>
      <p class="temp-desc">{{ gameInfo?.description || '太初寰宇团队正在精心打磨，敬请期待' }}</p>
      <div class="temp-divider"></div>
      <div v-if="gameInfo?.status === '开发中'" class="temp-placeholder">
        <div class="dash-grid">
          <span v-for="i in 6" :key="i" class="dash-block"></span>
        </div>
        <p class="temp-hint">● 预计上线时间:暂定</p>
      </div>
      <div v-else-if="gameInfo?.status === '已上线'" class="temp-placeholder">
        <p class="temp-hint">✅ 已上线，欢迎体验！</p>
      </div>
      <button v-if="gameInfo?.status === '开发中'" class="btn-line btn-notify">🔔 上线通知我</button>
    </div>
  </div>
</template>

<script setup>
import { inject, computed } from 'vue'

const gameInfo = inject('gameInfo', null)

const statusClass = computed(() => {
  if (gameInfo?.status === '已上线') return 'status-online'
  if (gameInfo?.status === '开发中') return 'status-dev'
  return 'status-unknown'
})
</script>

<style scoped>
.temp-holder {
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100%;
  height: 100%;
  min-height: 400px;
  padding: 20px;
}

.temp-card {
  background: var(--paper-card, #FCFAF7);
  border: 1px solid var(--line-raw, #D5CEC5);
  padding: 48px 40px;
  max-width: 480px;
  width: 100%;
  text-align: center;
  border-radius: 4px;
  transition: border-color 0.3s;
}

.temp-card:hover {
  border-color: var(--cinnabar, #9E2A2B);
}

.temp-icon {
  font-size: 64px;
  margin-bottom: 16px;
  display: block;
}

.temp-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.2em;
  color: var(--ink-black, #2A2826);
  margin: 0 0 8px;
}

.temp-status {
  font-size: 14px;
  letter-spacing: 0.15em;
  margin-bottom: 8px;
}
.status-dev {
  color: var(--cinnabar, #9E2A2B);
}
.status-online {
  color: #2b7a4b;
}
.status-unknown {
  color: var(--ink-gray);
}

.temp-desc {
  font-size: 14px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  margin: 8px 0 20px;
  line-height: 1.6;
}

.temp-divider {
  width: 40px;
  height: 1px;
  background: var(--line-raw, #D5CEC5);
  margin: 0 auto 24px;
}

.temp-placeholder {
  margin-bottom: 24px;
}

.dash-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  justify-items: center;
}

.dash-block {
  width: 60px;
  height: 60px;
  border: 1px dashed var(--line-raw, #D5CEC5);
  background: var(--paper-sub, #F0EBE1);
  border-radius: 2px;
  transition: background 0.3s;
}

.dash-block:hover {
  background: var(--paper-card, #FCFAF7);
  border-color: var(--cinnabar, #9E2A2B);
}

.temp-hint {
  font-size: 13px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  margin: 16px 0 0;
}

.btn-notify {
  margin-top: 20px;
  border-color: var(--cinnabar);
  color: var(--cinnabar);
  padding: 8px 24px;
  transition: all 0.3s;
  background: none;
  border: 1px solid var(--line-raw);
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
}
.btn-notify:hover {
  background: var(--cinnabar);
  color: #fff;
  border-color: var(--cinnabar);
}
</style>