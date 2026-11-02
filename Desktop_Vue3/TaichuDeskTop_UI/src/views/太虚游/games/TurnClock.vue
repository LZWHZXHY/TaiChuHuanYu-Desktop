<!-- 联机棋局的回合倒计时条：放在棋盘正上方，谁在走、还剩几秒、超时会怎样，一眼看清。
     最后 10 秒变红，最后 5 秒闪烁；轮到自己时整条高亮。右边的铃铛是回合提醒音开关（观战时不显示）。 -->
<template>
  <div class="tc" :class="{ mine, low: seconds <= 10, crit: seconds <= 5 }" role="timer" :aria-label="`${label}，还剩 ${seconds} 秒`">
    <span class="tc-dial" aria-hidden="true">
      <svg viewBox="0 0 36 36">
        <circle class="tc-track" cx="18" cy="18" r="15.5" />
        <circle class="tc-arc" cx="18" cy="18" r="15.5" :stroke-dasharray="`${pct * C} ${C}`" />
      </svg>
      <b>{{ seconds }}</b>
    </span>
    <span class="tc-text">
      <b>{{ label }}</b>
      <small>{{ sub }}</small>
    </span>
    <button v-if="showSound" type="button" class="tc-sound" :aria-pressed="turnSoundOn" :title="turnSoundOn ? '回合提醒音：开（轮到你、最后 10 秒会响）' : '回合提醒音：关'" @click="toggle">
      {{ turnSoundOn ? '🔔' : '🔕' }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { turnSfx, turnSoundOn } from './turnAlert'

const props = withDefaults(defineProps<{
  seconds: number
  total: number
  /** 是不是轮到我 */
  mine?: boolean
  /** 主文字，比如「轮到你走」「等待 老王 落子」「观战中 · 红方走棋」 */
  label: string
  /** 超时的后果，比如「超时判负」 */
  penalty?: string
  showSound?: boolean
}>(), { mine: false, penalty: '超时判负', showSound: true })

const C = 2 * Math.PI * 15.5
const pct = computed(() => props.total > 0 ? Math.max(0, Math.min(1, props.seconds / props.total)) : 0)
const sub = computed(() => {
  if (props.mine && props.seconds <= 10) return `抓紧！还剩 ${props.seconds} 秒，${props.penalty}`
  return `每步 ${props.total} 秒 · ${props.penalty}`
})
function toggle() {
  turnSoundOn.value = !turnSoundOn.value
  if (turnSoundOn.value) turnSfx.turn()
}
</script>

<style scoped>
.tc { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; padding: 8px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); transition: background .2s, border-color .2s; }
.tc.mine { border-color: var(--accent-color); border-left-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 9%, var(--paper-card)); }
.tc.low { border-left-color: var(--danger); }
.tc.mine.low { border-color: var(--danger); background: color-mix(in srgb, var(--danger) 9%, var(--paper-card)); }

.tc-dial { position: relative; flex-shrink: 0; width: 44px; height: 44px; }
.tc-dial svg { width: 100%; height: 100%; transform: rotate(-90deg); }
.tc-track { fill: none; stroke: var(--bg-sub); stroke-width: 3.5; }
.tc-arc { fill: none; stroke: var(--signal-mint, #2dd4bf); stroke-width: 3.5; stroke-linecap: round; transition: stroke-dasharray .5s linear, stroke .2s; }
.tc.mine .tc-arc { stroke: var(--accent-color); }
.tc.low .tc-arc { stroke: var(--danger); }
.tc-dial b { position: absolute; inset: 0; display: grid; place-items: center; font: 700 15px var(--font-mono, monospace); font-variant-numeric: tabular-nums; color: var(--text-primary); }
.tc.low .tc-dial b { color: var(--danger); }
.tc.mine.crit .tc-dial { animation: tc-pulse .9s ease-in-out infinite; }

.tc-text { min-width: 0; flex: 1; display: grid; gap: 2px; }
.tc-text b { font-size: 15px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tc.mine .tc-text b { color: var(--accent-color); }
.tc.mine.low .tc-text b, .tc.mine.low .tc-text small { color: var(--danger); }
.tc-text small { font-size: 12px; color: var(--text-secondary); }

.tc-sound { flex-shrink: 0; width: 32px; height: 32px; border: 1px solid var(--border-line); background: transparent; font-size: 15px; line-height: 1; cursor: pointer; }
.tc-sound[aria-pressed='false'] { opacity: .6; }
.tc-sound:hover { border-color: var(--accent-color); }

@keyframes tc-pulse { 50% { transform: scale(1.12); } }
@media (prefers-reduced-motion: reduce) { .tc.mine.crit .tc-dial { animation: none; } }
</style>
