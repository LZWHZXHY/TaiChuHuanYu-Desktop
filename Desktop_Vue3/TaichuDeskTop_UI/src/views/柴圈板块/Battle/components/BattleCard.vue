<!-- 约战卡片（约战大厅、我的约战共用）：一眼看到双方是谁、什么类型、走到哪一步、结果如何。
     以前卡片只有标题、规则和「发起人」，看不出对手是谁、打完是谁赢。 -->
<template>
  <article class="bc" :class="'st-' + battle.status" role="link" tabindex="0" :aria-label="`查看约战「${battle.title}」`" @click="emit('open', battle.id)" @keydown.enter.self="emit('open', battle.id)">
    <header class="bc-head">
      <span class="bc-status">{{ statusLabel(battle.status) }}</span>
      <span class="bc-tags">
        <span>{{ battle.battleType || '自定义' }}</span>
        <span>{{ battle.judgmentType === 'vote' ? '投票制' : '内定制' }}</span>
        <span v-if="!battle.isPublic">指定对手</span>
        <span v-if="role" class="bc-role">{{ role }}</span>
      </span>
      <time class="bc-time" :datetime="battle.createdAt">{{ relDay(battle.createdAt) }}</time>
    </header>

    <div class="bc-title-row">
      <img v-if="battle.coverUrl" :src="withThumbnail(battle.coverUrl, 400)" alt="" class="bc-cover" loading="lazy" />
      <h3 class="bc-title">{{ battle.title }}</h3>
    </div>

    <!-- 规则摘要（最多两行，以前大厅卡片就有） -->
    <p v-if="battle.rules" class="bc-rules">{{ battle.rules }}</p>

    <!-- 对阵：A 方 VS B 方 -->
    <div class="bc-versus">
      <div v-for="side in sides" :key="side.key" class="bc-side" :class="[side.key === 'A' ? 'a' : 'b', sideState(side)]">
        <template v-if="side.members.length">
          <span class="bc-names">
            <UserLink v-for="(m, i) in side.members.slice(0, 3)" :key="m.id" nested :id="m.userId" :name="m.userName">{{ m.userName }}{{ i < Math.min(side.members.length, 3) - 1 ? '、' : '' }}</UserLink>
            <template v-if="side.members.length > 3"> 等 {{ side.members.length }} 人</template>
          </span>
          <small class="bc-ocs">{{ ocText(side) }}</small>
        </template>
        <span v-else class="bc-names bc-wait">{{ side.pending ? `等 ${side.pending} 人应战` : '虚位以待' }}</span>
      </div>
      <span class="bc-vs" aria-hidden="true">VS</span>
    </div>

    <footer class="bc-foot">
      <!-- 迷你进度：四格，走过的涂满 -->
      <span class="bc-progress" :aria-label="`进度：${statusLabel(battle.status)}`">
        <i v-for="n in 4" :key="n" :class="{ on: stage >= n - 1 && stage >= 0 }"></i>
      </span>
      <span class="bc-count">{{ battle.participantCount ?? battle.participants?.length ?? 0 }} 人 · {{ battle.submissionCount ?? battle.submissions?.length ?? 0 }} 件作品</span>
      <span v-if="battle.status === 'finished'" class="bc-result" :class="'o-' + (outcome || 'none')">{{ headline.icon }} {{ headline.text }}</span>
      <span v-if="myResult" class="bc-mine">我：{{ myResult }}</span>
    </footer>
  </article>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import UserLink from '@/components/UserLink.vue'
import { withThumbnail } from '@/utils/imageThumb'
import type { Battle } from '../battle_api'
import { outcomeOf, relDay, resultHeadline, splitSides, stageIndex, statusLabel, type BattleSide } from '../battleView'

const props = defineProps<{
  battle: Battle
  /** 「我发起的 / 我参与的」之类的小标签（我的约战页用） */
  role?: string
  /** 我在这场的结果（我的约战页用） */
  myResult?: string
}>()
const emit = defineEmits<{ (e: 'open', id: string): void }>()

const sides = computed(() => splitSides(props.battle))
const stage = computed(() => stageIndex(props.battle.status))
const outcome = computed(() => outcomeOf(props.battle))
const headline = computed(() => resultHeadline(props.battle))

const ocText = (side: BattleSide) => {
  const all = side.members.flatMap(m => m.ocNames ?? [])
  if (!all.length) return '没选 OC'
  return all.length > 3 ? `${all.slice(0, 3).join('、')} 等 ${all.length} 个 OC` : all.join('、')
}

/** 已结束：胜方高亮、败方变淡 */
function sideState(side: BattleSide) {
  if (props.battle.status !== 'finished' || !side.members.length) return ''
  const o = outcome.value
  if (o !== 'win' && o !== 'pyrrhic') return ''
  return side.members.some(m => m.result === 'win') ? 'won' : 'lost'
}
</script>

<style scoped>
.bc {
  --side-a: var(--accent-color);
  --side-b: #d97706;
  display: grid;
  gap: 10px;
  padding: 14px 16px 12px;
  border: 1px solid var(--border-line);
  border-top: 2px solid var(--text-primary);
  background: var(--paper-card);
  color: var(--text-primary);
  cursor: pointer;
  min-width: 0;
  transition: border-color .18s, box-shadow .18s;
}
.bc:hover, .bc:focus-visible { border-color: var(--text-primary); box-shadow: 0 3px 0 var(--signal-color); }
.bc:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.bc.st-cancelled { opacity: .7; }

.bc-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; font-size: 12px; }
.bc-status { padding: 2px 8px; font-weight: 600; color: var(--paper-card); background: var(--text-secondary); }
.st-open .bc-status { background: var(--success); }
.st-ongoing .bc-status { background: var(--accent-color); color: var(--on-accent); }
.st-judging .bc-status { background: var(--caution); color: var(--caution-ink); }
.st-finished .bc-status { background: var(--text-primary); }
.bc-tags { display: flex; gap: 6px; flex-wrap: wrap; color: var(--text-secondary); min-width: 0; }
.bc-tags span { padding: 1px 6px; background: var(--bg-sub); }
.bc-tags .bc-role { color: var(--accent-color); background: var(--accent-soft); }
.bc-time { margin-left: auto; color: var(--text-secondary); white-space: nowrap; }

.bc-title-row { display: flex; gap: 10px; align-items: center; min-width: 0; }
.bc-cover { width: 44px; height: 44px; object-fit: cover; flex-shrink: 0; border: 1px solid var(--border-line); }
.bc-title { margin: 0; font-size: 16px; font-weight: 600; line-height: 1.4; overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }

.bc-rules { margin: -4px 0 0; font-size: 13px; line-height: 1.6; color: var(--text-secondary); overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.bc-versus { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 0; border: 1px solid var(--border-line); }
.bc-side { display: grid; gap: 1px; padding: 8px 10px; min-width: 0; }
.bc-side.a { background: color-mix(in srgb, var(--side-a) 9%, var(--paper-card)); box-shadow: inset 3px 0 0 var(--side-a); padding-right: 26px; }
.bc-side.b { background: color-mix(in srgb, var(--side-b) 10%, var(--paper-card)); box-shadow: inset -3px 0 0 var(--side-b); text-align: right; padding-left: 26px; }
.bc-names { font: 600 15px/1.35 var(--font-display); overflow-wrap: anywhere; }
.bc-wait { color: var(--text-secondary); font-weight: 400; }
.bc-ocs { font-size: 12px; color: var(--text-secondary); overflow-wrap: anywhere; }
.bc-side.won .bc-names::before { content: '🏆 '; }
.bc-side.lost { opacity: .55; }
.bc-vs { position: absolute; left: 50%; top: 50%; translate: -50% -50%; padding: 3px 8px; font: 700 13px/1 var(--font-display); color: var(--caution-ink); background: var(--caution); clip-path: polygon(14% 0, 100% 0, 86% 100%, 0 100%); }

.bc-foot { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; font-size: 12px; color: var(--text-secondary); }
.bc-progress { display: inline-flex; gap: 2px; }
.bc-progress i { width: 14px; height: 4px; background: var(--border-line); }
.bc-progress i.on { background: var(--accent-color); }
.st-finished .bc-progress i.on { background: var(--text-primary); }
.bc-result { margin-left: auto; padding: 1px 8px; font-weight: 600; color: var(--text-primary); background: var(--bg-sub); overflow-wrap: anywhere; }
.bc-result.o-pyrrhic { color: var(--danger); background: var(--danger-soft); }
.bc-result.o-draw { color: var(--info); background: var(--info-soft); }
.bc-result.o-mutual { color: var(--inverse-text); background: var(--inverse-bg); }
.bc-result.o-nocontest { color: var(--text-secondary); }
.bc-mine { padding: 1px 8px; color: var(--accent-color); border: 1px solid var(--accent-color); }
@media (prefers-reduced-motion: reduce) { .bc { transition: none; } }
</style>
