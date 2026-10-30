<!-- 围棋：联机对战（服务器判定、记战绩）+ 本地双人。两种模式都保持挂载，切换时不丢棋局。 -->
<template>
  <div class="go-game">
    <header class="go-head">
      <div>
        <p class="eyebrow">WEIQI · {{ mode === 'online' ? 'ONLINE MATCH' : 'LOCAL MATCH' }} / 围棋</p>
        <h2>围棋</h2>
      </div>
      <div class="head-right">
        <span v-if="mode === 'online'" class="link-state" :class="{ on: connState === 'on' }"><i aria-hidden="true"></i>{{ connState === 'on' ? '已连接对战服务器' : connState === 'wait' ? '正在连接…' : '未连接' }}</span>
        <div class="mode-switch" role="tablist" aria-label="对局方式">
          <button type="button" role="tab" :aria-selected="mode === 'online'" :class="{ on: mode === 'online' }" @click="switchMode('online')">联机对战</button>
          <button type="button" role="tab" :aria-selected="mode === 'local'" :class="{ on: mode === 'local' }" @click="switchMode('local')">本地双人</button>
        </div>
      </div>
    </header>

    <GoOnline v-show="mode === 'online'" ref="online" @conn="connState = $event" />
    <GoLocal v-show="mode === 'local'" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import GoOnline from './GoOnline.vue'
import GoLocal from './GoLocal.vue'

const mode = ref<'online' | 'local'>('online')
const connState = ref<'wait' | 'on' | 'off'>('wait')
const online = ref<InstanceType<typeof GoOnline> | null>(null)

function switchMode(m: 'online' | 'local') {
  if (m === mode.value) return
  if (m === 'local' && online.value?.isPlaying()) { online.value.say('联机对局还在进行，先下完或认输再切换', 'error'); return }
  mode.value = m
}

/** 切换游戏 / 离开页面前确认（太虚游页面会调用） */
async function handleBeforeLeave() {
  return online.value ? await online.value.handleBeforeLeave() : true
}
defineExpose({ handleBeforeLeave })
</script>

<style scoped>
.go-game { display: grid; gap: 18px; color: var(--text-primary); }
.go-head { position: relative; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 10px 16px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); }
.go-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.eyebrow { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.go-head h2 { margin: 0; font-size: 26px; letter-spacing: .04em; }
.head-right { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.link-state { display: inline-flex; align-items: center; gap: 8px; font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.link-state i { width: 7px; height: 7px; background: #d97706; }
.link-state.on i { background: var(--signal-mint, #2dd4bf); box-shadow: 0 0 0 3px color-mix(in srgb, var(--signal-mint, #2dd4bf) 25%, transparent); }
.mode-switch { display: inline-flex; border: 1px solid var(--border-line); }
.mode-switch button { padding: 7px 14px; border: 0; border-right: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.mode-switch button:last-child { border-right: 0; }
.mode-switch button.on { background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
</style>
