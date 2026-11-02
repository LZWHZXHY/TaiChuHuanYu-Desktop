<!-- WatchBadge.vue：「👁 N 人观战」小按钮 + 点开的观战名单（斗地主、象棋、围棋、五子棋共用）。
     房主在开局前还能在这里开 / 关观战（canToggle 为真时显示开关，点了发 toggle 事件，由父组件交给服务器）。 -->
<template>
  <div class="wb" :class="{ open }">
    <button type="button" class="wb-btn" :class="{ off: !watch.allow }" :aria-expanded="open" aria-haspopup="dialog" :title="watch.allow ? '看看谁在观战' : '房主关了观战'" data-seat-anchor="-1" @click.stop="open = !open">
      <span aria-hidden="true">👁</span> {{ watch.list.length }} 人观战<template v-if="!watch.allow"> · 已关闭</template>
    </button>
    <div v-if="open" class="wb-pop" :class="placement" role="dialog" aria-label="观战名单" @click.stop>
      <p class="wb-title">正在观战 <small>{{ watch.list.length }} / {{ watch.max }}</small></p>
      <ul v-if="watch.list.length">
        <li v-for="w in watch.list" :key="w.userId"><span class="wb-dot" aria-hidden="true"></span>{{ w.name }}<em v-if="w.userId === myUserId">我</em></li>
      </ul>
      <p v-else class="wb-empty">{{ watch.allow ? '现在没人观战' : '房主关了观战，别人进不来' }}</p>
      <label v-if="canToggle" class="wb-toggle">
        <input type="checkbox" :checked="!watch.allow" @change="emit('toggle', !($event.target as HTMLInputElement).checked)" />
        不让观战<small>（开局前可以改，关掉会把正在看的人请出去）</small>
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import type { WatchInfo } from './spectate'

withDefaults(defineProps<{
  watch: WatchInfo
  myUserId?: string | null
  /** 房主、而且还没开局：可以开 / 关观战 */
  canToggle?: boolean
  /** 名单往上弹（放在底部时）还是往下弹 */
  placement?: 'up' | 'down'
}>(), { myUserId: null, canToggle: false, placement: 'up' })
const emit = defineEmits<{ toggle: [allow: boolean] }>()

const open = ref(false)
// 点别处、按 Esc 收起
const close = () => { open.value = false }
const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }
onMounted(() => { window.addEventListener('click', close); window.addEventListener('keydown', onKey) })
onBeforeUnmount(() => { window.removeEventListener('click', close); window.removeEventListener('keydown', onKey) })
</script>

<style scoped>
.wb { position: relative; display: inline-block; }
.wb-btn { display: inline-flex; align-items: center; gap: 4px; padding: 4px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); font: 12px var(--font-mono, monospace); cursor: pointer; touch-action: manipulation; white-space: nowrap; }
.wb-btn:hover, .wb.open .wb-btn { border-color: var(--accent-color); color: var(--accent-color); }
.wb-btn.off { border-style: dashed; }
.wb-pop { position: absolute; left: 0; z-index: 30; display: grid; gap: 8px; width: 220px; max-width: 80vw; padding: 10px 12px; border: 1px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); box-shadow: 0 12px 28px rgb(0 0 0 / 18%); }
.wb-pop.up { bottom: calc(100% + 6px); }
.wb-pop.down { top: calc(100% + 6px); }
.wb-title { margin: 0; font-size: 13px; font-weight: 600; }
.wb-title small { margin-left: 4px; color: var(--text-secondary); font: 11px var(--font-mono, monospace); font-weight: 400; }
.wb-pop ul { display: grid; gap: 4px; max-height: 200px; margin: 0; padding: 0; overflow-y: auto; list-style: none; }
.wb-pop li { display: flex; align-items: center; gap: 6px; font-size: 13px; }
.wb-pop li em { padding: 0 4px; border: 1px solid var(--accent-color); color: var(--accent-color); font-style: normal; font-size: 10px; }
.wb-dot { width: 6px; height: 6px; background: var(--signal-mint, #2dd4bf); }
.wb-empty { margin: 0; color: var(--text-secondary); font-size: 12px; }
.wb-toggle { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding-top: 8px; border-top: 1px dashed var(--border-line); font-size: 13px; cursor: pointer; }
.wb-toggle small { flex-basis: 100%; color: var(--text-secondary); font-size: 11px; }
</style>
