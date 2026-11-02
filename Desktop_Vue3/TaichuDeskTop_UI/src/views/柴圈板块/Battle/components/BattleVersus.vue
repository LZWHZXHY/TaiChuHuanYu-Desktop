<!-- 约战详情顶部的「对阵图」：左边 A 方、右边 B 方，中间一块警示黄的 VS 牌。
     每方显示参战的人和出战 OC（有封面用封面，没有就用名字首字）；约战结束后胜方亮起、败方变灰，平手 / 同归于尽 / 无结果两边一起标出来。
     OC 封面由「出战人设」那块读到后传进来（ocs），这里不再重复请求。 -->
<template>
  <div class="vs-hero" :class="['is-' + (finished ? outcome || 'done' : 'live')]">
    <section v-for="side in sides" :key="side.key" class="vs-side" :class="[side.key === 'A' ? 'a' : 'b', mark(side)?.cls]">
      <header class="vs-side-head">
        <span class="vs-tag">{{ side.label }}</span>
        <span v-if="mark(side)" class="vs-mark">{{ mark(side)!.text }}</span>
      </header>
      <div class="vs-members" :class="{ solo: side.members.length <= 1 }">
        <div v-for="m in side.members" :key="m.id" class="vs-member">
          <div class="vs-tiles" aria-hidden="true">
            <template v-for="(id, k) in (m.ocIds ?? []).slice(0, 3)" :key="id">
              <img v-if="ocs?.[id]?.coverUrl" :src="withThumbnail(ocs?.[id]?.coverUrl, 200)" alt="" class="vs-tile" loading="lazy" />
              <span v-else class="vs-tile vs-tile-text">{{ (ocs?.[id]?.title || m.ocNames?.[k] || '?').slice(0, 1) }}</span>
            </template>
            <span v-if="(m.ocIds?.length ?? 0) > 3" class="vs-tile vs-tile-more">+{{ m.ocIds.length - 3 }}</span>
            <span v-if="!m.ocIds?.length" class="vs-tile vs-tile-text">?</span>
          </div>
          <div class="vs-who">
            <UserLink :id="m.userId" :name="m.userName" class="vs-user">{{ m.userName }}</UserLink>
            <small class="vs-ocnames">{{ m.ocNames?.length ? m.ocNames.join('、') : '没选 OC' }}</small>
          </div>
        </div>
        <p v-if="side.pending" class="vs-pending">还有 {{ side.pending }} 位受邀者没应战</p>
        <p v-if="!side.members.length && !side.pending" class="vs-empty">
          <b>虚位以待</b>
          <small>{{ isPublic ? '公开报名中，谁都可以来' : '等待对手应战' }}</small>
        </p>
      </div>
    </section>
    <div class="vs-plate" aria-hidden="true"><span>VS</span></div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import UserLink from '@/components/UserLink.vue'
import { withThumbnail } from '@/utils/imageThumb'
import type { Battle } from '../battle_api'
import { outcomeOf, splitSides, type BattleSide } from '../battleView'

const props = defineProps<{
  battle: Battle
  /** OC 编号 → 名字、封面（「出战人设」读到后传进来） */
  ocs?: Record<string, { title?: string; coverUrl?: string } | undefined>
}>()

// 模板里排版是 A 方、VS 牌、B 方：VS 牌用 CSS 网格放到中间，这样 v-for 的两边可以共用一套模板
const sides = computed(() => splitSides(props.battle))
const finished = computed(() => props.battle.status === 'finished')
const outcome = computed(() => outcomeOf(props.battle))
const isPublic = computed(() => props.battle.isPublic)

/** 结束后每一边的标记：胜方 🏆 / 🩸，败方灰掉；平手、同归于尽、无结果两边一样 */
function mark(side: BattleSide): { text: string; cls: string } | null {
  if (!finished.value || !side.members.length) return null
  const o = outcome.value
  if (o === 'draw') return { text: '🤝 平手', cls: 'draw' }
  if (o === 'mutual') return { text: '☠ 同归于尽', cls: 'fallen' }
  if (o === 'nocontest') return { text: '⏸ 不计', cls: 'void' }
  if (o === 'win' || o === 'pyrrhic') {
    const won = side.members.some(m => m.result === 'win')
    if (won) return { text: o === 'pyrrhic' ? '🩸 惨胜' : '🏆 胜', cls: o === 'pyrrhic' ? 'win pyrrhic' : 'win' }
    return { text: '败', cls: 'lose' }
  }
  return null
}
</script>

<style scoped>
.vs-hero {
  --side-a: var(--accent-color);
  --side-b: #d97706;
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 84px minmax(0, 1fr);
  grid-template-areas: 'a plate b';
  border: 1px solid var(--border-line);
  border-top: 2px solid var(--text-primary);
  background: var(--paper-card);
  overflow: hidden;
}
.vs-side { position: relative; display: grid; align-content: start; gap: 12px; padding: 20px 22px 22px; min-width: 0; transition: opacity .3s, filter .3s; }
.vs-side.a { grid-area: a; background: linear-gradient(100deg, color-mix(in srgb, var(--side-a) 13%, var(--paper-card)), var(--paper-card) 78%); box-shadow: inset 4px 0 0 var(--side-a); }
.vs-side.b { grid-area: b; text-align: right; background: linear-gradient(260deg, color-mix(in srgb, var(--side-b) 14%, var(--paper-card)), var(--paper-card) 78%); box-shadow: inset -4px 0 0 var(--side-b); }
.vs-side-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.vs-side.b .vs-side-head { flex-direction: row-reverse; }
.vs-tag { padding: 2px 8px; font-size: 12px; font-weight: 600; color: var(--paper-card); background: var(--side-a); }
.vs-side.b .vs-tag { background: var(--side-b); color: #1f1300; }
.vs-mark { padding: 2px 8px; font-size: 13px; font-weight: 700; border: 1px solid currentColor; }

.vs-members { display: grid; gap: 12px; }
.vs-member { display: flex; align-items: center; gap: 12px; min-width: 0; }
.vs-side.b .vs-member { flex-direction: row-reverse; }
.vs-tiles { display: flex; flex-shrink: 0; }
.vs-side.b .vs-tiles { flex-direction: row-reverse; }
.vs-tile { width: 48px; height: 48px; object-fit: cover; border: 2px solid var(--paper-card); background: var(--bg-sub); margin-left: -10px; }
.vs-tiles > :first-child { margin-left: 0; }
.vs-side.b .vs-tile { margin-left: 0; margin-right: -10px; }
.vs-side.b .vs-tiles > :first-child { margin-right: 0; }
.vs-tile-text { display: grid; place-items: center; font: 700 20px/1 var(--font-display); color: var(--paper-card); background: var(--side-a); }
.vs-side.b .vs-tile-text { background: var(--side-b); color: #1f1300; }
.vs-tile-more { display: grid; place-items: center; font: 600 13px/1 var(--font-display); color: var(--text-secondary); }
/* 单挑：头像放大，名字用大号窄体字 */
.vs-members.solo .vs-tile { width: 76px; height: 76px; }
.vs-members.solo .vs-tile-text { font-size: 32px; }
.vs-who { display: grid; gap: 2px; min-width: 0; }
.vs-user { font: 600 18px/1.25 var(--font-display); color: var(--text-primary); overflow-wrap: anywhere; }
.vs-side.b .vs-user { justify-content: flex-end; }
.vs-members.solo .vs-user { font-size: 24px; }
.vs-ocnames { font-size: 13px; color: var(--text-secondary); overflow-wrap: anywhere; }
.vs-pending, .vs-empty { margin: 0; padding: 10px 12px; border: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 13px; }
.vs-empty { display: grid; gap: 2px; }
.vs-empty b { font: 600 20px/1.3 var(--font-display); color: var(--text-primary); }

/* 中间的 VS 牌：斜切的警示黄块，背后一道斜向警戒线把两边分开 */
.vs-plate { grid-area: plate; position: relative; display: grid; place-items: center; }
.vs-plate::before { content: ''; position: absolute; top: -10px; bottom: -10px; left: 50%; width: 8px; translate: -50% 0; rotate: 8deg; background: var(--hazard-stripe); opacity: .85; }
.vs-plate span { position: relative; padding: 8px 18px; font: 700 30px/1 var(--font-display); letter-spacing: .02em; color: var(--caution-ink); background: var(--caution); clip-path: polygon(14% 0, 100% 0, 86% 100%, 0 100%); }

/* 结果：胜方顶边加粗，败方变灰；惨胜加一道血色；同归于尽两边都暗下去 */
.vs-side.win { opacity: 1; }
.vs-side.win .vs-mark { color: var(--paper-card); background: var(--text-primary); border-color: var(--text-primary); }
.vs-side.win.pyrrhic .vs-mark { background: var(--danger); border-color: var(--danger); }
.vs-side.a.win { box-shadow: inset 4px 0 0 var(--side-a), inset 0 4px 0 var(--side-a); }
.vs-side.b.win { box-shadow: inset -4px 0 0 var(--side-b), inset 0 4px 0 var(--side-b); }
.vs-side.lose { opacity: .62; }
.vs-side.lose .vs-tile, .vs-side.fallen .vs-tile { filter: grayscale(1); }
.vs-side.lose .vs-mark { color: var(--text-secondary); }
.vs-side.fallen .vs-mark { color: var(--text-primary); }
.vs-side.fallen { background: color-mix(in srgb, var(--text-primary) 6%, var(--paper-card)); }
.vs-side.draw .vs-mark { color: var(--info); }
.vs-side.void .vs-mark { color: var(--text-secondary); border-style: dashed; }
.vs-hero.is-mutual { background: color-mix(in srgb, var(--text-primary) 6%, var(--paper-card)); }
.vs-hero.is-nocontest .vs-plate span { background: var(--bg-sub); color: var(--text-secondary); }

/* 手机：上下排，VS 牌横在中间，B 方也改成左对齐 */
@media (max-width: 640px) {
  .vs-hero { grid-template-columns: minmax(0, 1fr); grid-template-areas: 'a' 'plate' 'b'; }
  .vs-side { padding: 16px; }
  .vs-side.b { text-align: left; background: linear-gradient(100deg, color-mix(in srgb, var(--side-b) 14%, var(--paper-card)), var(--paper-card) 78%); box-shadow: inset 4px 0 0 var(--side-b); }
  .vs-side.b.win { box-shadow: inset 4px 0 0 var(--side-b), inset 0 4px 0 var(--side-b); }
  .vs-side.b .vs-side-head, .vs-side.b .vs-member, .vs-side.b .vs-tiles { flex-direction: row; }
  .vs-side.b .vs-tile { margin-right: 0; margin-left: -10px; }
  .vs-side.b .vs-tiles > :first-child { margin-left: 0; }
  .vs-side.b .vs-user { justify-content: flex-start; }
  .vs-plate { height: 34px; }
  .vs-plate::before { top: 50%; bottom: auto; left: -10px; right: -10px; width: auto; height: 6px; translate: 0 -50%; rotate: none; }
  .vs-plate span { font-size: 22px; padding: 6px 16px; }
  .vs-members.solo .vs-tile { width: 60px; height: 60px; }
  .vs-members.solo .vs-user { font-size: 20px; }
}
@media (prefers-reduced-motion: reduce) { .vs-side { transition: none; } }
</style>
