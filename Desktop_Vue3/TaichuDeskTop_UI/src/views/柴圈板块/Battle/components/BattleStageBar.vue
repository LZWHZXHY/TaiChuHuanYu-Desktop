<!-- 约战进度条：报名 → 创作 → 判定 → 结束，当前阶段高亮，每一步下面写开始时间；创作期写明持续了多久。
     以前详情页只有一个状态小标签，看不出走到哪一步、创作期给了多久（线上有一场创作期只有 3 秒，谁都没发现）。 -->
<template>
  <div class="bsb" :class="{ cancelled }">
    <p v-if="cancelled" class="bsb-cancel">这场约战已取消</p>
    <ol class="bsb-steps">
      <li
        v-for="(s, i) in steps"
        :key="s.label"
        class="bsb-step"
        :class="{ done: !cancelled && i < current, now: !cancelled && i === current }"
        :aria-current="!cancelled && i === current ? 'step' : undefined"
      >
        <span class="bsb-num">{{ i + 1 }}</span>
        <span class="bsb-text">
          <b>{{ s.label }}</b>
          <small v-if="s.time">{{ s.time }}</small>
          <small v-else class="muted">{{ cancelled ? '—' : '还没到' }}</small>
          <small v-if="s.note" class="bsb-note" :class="{ warn: s.warn }">{{ s.note }}</small>
        </span>
      </li>
    </ol>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Battle } from '../battle_api'
import { fmtDuration, fmtTime, stageIndex } from '../battleView'

const props = defineProps<{ battle: Pick<Battle, 'status' | 'createdAt' | 'registrationDeadline' | 'submissionDeadline' | 'finishedAt'> }>()

const cancelled = computed(() => props.battle.status === 'cancelled')
const current = computed(() => stageIndex(props.battle.status))

const ms = (iso?: string) => (iso ? new Date(iso).getTime() : NaN)

const steps = computed(() => {
  const b = props.battle
  const reg = ms(b.registrationDeadline)
  const sub = ms(b.submissionDeadline)
  // 创作期：已经结束的写「持续了多久」，太短（不到 2 分钟）标成警告色，方便一眼看出是不是被误点结束的
  let creationNote = ''
  let warn = false
  if (Number.isFinite(reg) && Number.isFinite(sub)) {
    creationNote = `持续 ${fmtDuration(sub - reg)}`
    warn = sub - reg < 120000
  } else if (Number.isFinite(reg) && b.status === 'ongoing') {
    // 不到 1 分钟写「刚开始」（这里不会每秒刷新，写秒数反而像卡住了）
    creationNote = Date.now() - reg < 60000 ? '刚开始' : `已进行 ${fmtDuration(Date.now() - reg)}`
  }
  return [
    { label: '报名', time: fmtTime(b.createdAt), note: '', warn: false },
    { label: '创作', time: fmtTime(b.registrationDeadline), note: creationNote, warn },
    { label: '判定', time: fmtTime(b.submissionDeadline), note: '', warn: false },
    { label: '结束', time: fmtTime(b.finishedAt), note: '', warn: false },
  ]
})
</script>

<style scoped>
.bsb { position: relative; }
.bsb-cancel { margin: 0 0 8px; padding: 6px 10px 6px 18px; font-size: 13px; color: var(--text-primary); background: var(--warning-soft); border-left: 6px solid transparent; border-image: var(--hazard-stripe) 1; }
.bsb-steps { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); }
.bsb-step { position: relative; display: flex; gap: 10px; align-items: flex-start; padding: 12px 12px 12px 0; border-top: 3px solid var(--border-line); color: var(--text-secondary); min-width: 0; }
.bsb-step.done { border-top-color: var(--text-primary); color: var(--text-primary); }
.bsb-step.now { border-top-color: var(--accent-color); color: var(--text-primary); }
.bsb-num { flex-shrink: 0; width: 28px; height: 28px; display: grid; place-items: center; font: 600 15px/1 var(--font-display); border: 1px solid currentColor; }
.bsb-step.done .bsb-num { background: var(--text-primary); border-color: var(--text-primary); color: var(--paper-card); }
.bsb-step.now .bsb-num { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
.bsb-text { display: grid; gap: 1px; min-width: 0; }
.bsb-text b { font-size: 14px; font-weight: 600; }
.bsb-step.now .bsb-text b { color: var(--accent-color); }
.bsb-text small { font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.bsb-text small.muted { opacity: .7; }
.bsb-note { color: var(--text-secondary); }
.bsb-note.warn { color: var(--danger); font-weight: 600; }
.bsb.cancelled .bsb-steps { opacity: .55; }
/* 手机：四步排成两行两列 */
@media (max-width: 520px) {
  .bsb-steps { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .bsb-step { padding-right: 8px; }
}
</style>
