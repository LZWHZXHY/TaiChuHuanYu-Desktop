<!-- 约战结果选择器（录入结果、管理员修正结果共用）：
     先单选结果种类（一方胜 / 惨胜 / 平手 / 同归于尽 / 无结果），选了一方胜或惨胜再单选胜者；下面实时显示「战绩会这样记」。
     以前是一排多选框，两边都能勾成胜利，平局也只是又一个多选框，结果线上出现过两边都胜的约战。 -->
<template>
  <div class="bop">
    <fieldset class="bop-group">
      <legend>这场的结果是？</legend>
      <div class="bop-kinds">
        <label v-for="o in OUTCOMES" :key="o.key" class="bop-kind" :class="[o.key, { on: outcome === o.key }]">
          <input type="radio" :name="uid + '-kind'" :value="o.key" :checked="outcome === o.key" :disabled="disabled" @change="pickOutcome(o.key)" />
          <span class="bop-icon" aria-hidden="true">{{ o.icon }}</span>
          <span class="bop-kind-text">
            <b>{{ o.label }}</b>
            <small>{{ o.desc }}</small>
            <small class="bop-rule">战绩：{{ o.record }}</small>
          </span>
        </label>
      </div>
    </fieldset>

    <fieldset v-if="needsWinner" class="bop-group">
      <legend>谁赢了？<small>{{ team ? '团队战：选一队，整队都算赢' : '只能选一个' }}</small></legend>
      <div class="bop-winners">
        <label v-for="c in choices" :key="c.value" class="bop-winner" :class="[c.side, { on: normalizedWinner === c.value }]">
          <input type="radio" :name="uid + '-winner'" :value="c.value" :checked="normalizedWinner === c.value" :disabled="disabled" @change="emit('update:winnerId', c.value)" />
          <span class="bop-winner-text">
            <b>{{ c.title }}</b>
            <small>{{ c.sub }}</small>
          </span>
        </label>
      </div>
    </fieldset>

    <!-- 战绩预览：确认前就能看到每个人会记什么 -->
    <div v-if="preview.length" class="bop-preview" aria-live="polite">
      <b>{{ previewHeading }}</b>
      <ul>
        <li v-for="l in preview" :key="l.participant.id" :class="l.result">
          <span class="bop-pname">{{ l.participant.userName }}<small>{{ l.participant.ocNames?.length ? `（${l.participant.ocNames.join('、')}）` : '' }}</small></span>
          <span class="bop-presult">{{ RECORD_TEXT[l.result] }}</span>
        </li>
      </ul>
    </div>
    <p v-else-if="needsWinner" class="bop-todo">再选一下谁赢了，下面会显示战绩怎么记。</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { BattleOutcome, BattleParticipant } from '../battle_api'
import { OUTCOMES, RECORD_TEXT, isTeamBattle, outcomeMeta, recordPreview } from '../battleView'

const props = defineProps<{
  participants: BattleParticipant[]
  outcome: BattleOutcome | ''
  /** 胜者：单挑是参与者编号；团队战是那一队任意一个参与者的编号 */
  winnerId: string
  disabled?: boolean
  /** 战绩预览的标题（录入时是「公布后」，修正时是「确认后」） */
  previewTitle?: string
}>()
const emit = defineEmits<{
  (e: 'update:outcome', v: BattleOutcome): void
  (e: 'update:winnerId', v: string): void
}>()

// 同一页可能有两个选择器（录入 / 修正），单选框的 name 要不一样
const uid = `bop-${Math.random().toString(36).slice(2, 8)}`

const needsWinner = computed(() => !!outcomeMeta(props.outcome)?.needsWinner)
const team = computed(() => isTeamBattle(props.participants ?? []))

/** 胜者选项：团队战给「A 队 / B 队」，否则每个参与者一个 */
const choices = computed(() => {
  const ps = props.participants ?? []
  if (team.value) {
    return ([1, 2] as const).flatMap(n => {
      const members = ps.filter(p => p.teamNumber === n)
      if (!members.length) return []
      return [{
        value: members[0].id,
        side: n === 1 ? 'a' : 'b',
        title: n === 1 ? 'A 队' : 'B 队',
        sub: members.map(m => `${m.userName}${m.ocNames?.length ? `（${m.ocNames.join('、')}）` : ''}`).join('、'),
      }]
    })
  }
  return ps.map((p, i) => ({
    value: p.id,
    side: p.teamNumber === 2 || (!p.teamNumber && i > 0) ? 'b' : 'a',
    title: p.userName,
    sub: p.ocNames?.length ? p.ocNames.join('、') : '没选 OC',
  }))
})

/** 团队战时，已经选的胜者换成「那一队的代表」，这样单选框能对上（比如修正结果时带进来的是队里第二个人） */
const normalizedWinner = computed(() => {
  if (!team.value || !props.winnerId) return props.winnerId
  const w = props.participants.find(p => p.id === props.winnerId)
  return choices.value.find(c => props.participants.find(p => p.id === c.value)?.teamNumber === w?.teamNumber)?.value ?? props.winnerId
})

function pickOutcome(o: BattleOutcome) {
  emit('update:outcome', o)
  // 换成不需要胜者的结果时，把选过的胜者清掉，免得后端误会
  if (!outcomeMeta(o)?.needsWinner && props.winnerId) emit('update:winnerId', '')
}

const preview = computed(() => recordPreview(props.participants ?? [], props.outcome, needsWinner.value ? normalizedWinner.value : ''))
const previewHeading = computed(() => props.previewTitle || '战绩会这样记（每个出战 OC 各记一次）')
</script>

<style scoped>
.bop { display: grid; gap: 16px; }
.bop-group { margin: 0; padding: 0; border: 0; min-width: 0; }
.bop-group legend { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; padding: 0; margin-bottom: 8px; font-size: 14px; font-weight: 600; color: var(--text-primary); }
.bop-group legend small { font-size: 12px; font-weight: 400; color: var(--text-secondary); }
.bop-kinds { display: grid; gap: 6px; }
.bop-kind, .bop-winner { position: relative; display: flex; gap: 12px; align-items: flex-start; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); cursor: pointer; transition: border-color .15s, background-color .15s; }
.bop-kind:hover, .bop-winner:hover { border-color: var(--text-secondary); }
.bop-kind.on, .bop-winner.on { border-color: var(--accent-color); background: var(--accent-soft); box-shadow: inset 4px 0 0 var(--accent-color); }
.bop-kind.pyrrhic.on { border-color: var(--danger); background: var(--danger-soft); box-shadow: inset 4px 0 0 var(--danger); }
.bop-kind.mutual.on { border-color: var(--text-primary); background: color-mix(in srgb, var(--text-primary) 8%, var(--paper-card)); box-shadow: inset 4px 0 0 var(--text-primary); }
.bop-kind.nocontest.on { border-color: var(--text-secondary); background: var(--bg-sub); box-shadow: inset 4px 0 0 var(--text-secondary); }
.bop-kind input, .bop-winner input { margin: 3px 0 0; width: 16px; height: 16px; flex-shrink: 0; accent-color: var(--accent-color); }
.bop-kind:has(input:focus-visible), .bop-winner:has(input:focus-visible) { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.bop-icon { flex-shrink: 0; width: 26px; font-size: 20px; line-height: 1.2; text-align: center; }
.bop-kind-text, .bop-winner-text { display: grid; gap: 1px; min-width: 0; }
.bop-kind-text b, .bop-winner-text b { font-size: 14px; }
.bop-kind-text small, .bop-winner-text small { font-size: 12px; color: var(--text-secondary); line-height: 1.6; overflow-wrap: anywhere; }
.bop-rule { color: var(--text-primary) !important; opacity: .8; }
.bop-winners { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 200px), 1fr)); gap: 6px; }
.bop-winner.a { border-left: 3px solid var(--accent-color); }
.bop-winner.b { border-left: 3px solid #d97706; }
.bop-winner.b.on { box-shadow: inset 4px 0 0 #d97706; }
.bop-preview { padding: 10px 12px; border: 1px dashed var(--border-line); background: var(--bg-sub); }
.bop-preview > b { display: block; margin-bottom: 6px; font-size: 13px; }
.bop-preview ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.bop-preview li { display: flex; justify-content: space-between; gap: 12px; font-size: 13px; }
.bop-pname { min-width: 0; overflow-wrap: anywhere; }
.bop-pname small { color: var(--text-secondary); }
.bop-presult { flex-shrink: 0; font-weight: 600; }
.bop-preview li.win .bop-presult { color: var(--success); }
.bop-preview li.lose .bop-presult { color: var(--danger); }
.bop-preview li.draw .bop-presult { color: var(--info); }
.bop-preview li.none .bop-presult { color: var(--text-secondary); }
.bop-todo { margin: 0; font-size: 13px; color: var(--text-secondary); }
@media (prefers-reduced-motion: reduce) { .bop-kind, .bop-winner { transition: none; } }
</style>
