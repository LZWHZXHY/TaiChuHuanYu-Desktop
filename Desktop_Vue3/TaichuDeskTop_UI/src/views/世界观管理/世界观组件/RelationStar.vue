<!-- 阅读模式的「关联」：上面一张小星图（这个条目在中间，关联到的在右边、被关联的在左边，连线上有流动的光点），
     下面是可以点的小卡片。点星图上的点或卡片都会打开那个条目。 -->
<template>
  <section class="rstar" aria-label="关联">
    <header class="rstar-head">
      <span class="rstar-tag">LINKS</span>
      <span class="rstar-name">关联</span>
      <span class="rstar-count" :title="`关联到 ${outs.length} 个，被 ${ins.length} 个条目关联`">{{ outs.length }} → · ← {{ ins.length }}</span>
    </header>

    <svg class="rstar-map" :viewBox="`0 0 ${W} ${H}`" role="img" :aria-label="`${title} 的关系星图`">
      <defs>
        <radialGradient :id="gid" cx="50%" cy="50%" r="50%">
          <stop offset="0" stop-color="var(--signal-mint)" stop-opacity=".28" />
          <stop offset="1" stop-color="var(--signal-mint)" stop-opacity="0" />
        </radialGradient>
      </defs>
      <circle :cx="CX" :cy="CY" r="58" :fill="`url(#${gid})`" />
      <g class="rstar-rings">
        <circle :cx="CX" :cy="CY" r="30" stroke-dasharray="3 5" />
        <circle :cx="CX" :cy="CY" r="46" stroke-dasharray="26 10 4 10" class="spin" />
      </g>
      <!-- 连线 -->
      <g v-for="(n, i) in nodes" :key="n.key" class="rstar-link" :class="n.dir" :style="{ '--d': `${i * 0.18}s`, '--tc': n.color }">
        <path :d="n.path" class="base" />
        <path :d="n.path" class="flow" pathLength="100" />
      </g>
      <!-- 节点 -->
      <g v-for="(n, i) in nodes" :key="n.key + 'n'" class="rstar-node" :class="n.dir" :style="{ '--d': `${i * 0.18}s`, '--tc': n.color }"
        tabindex="0" role="link" :aria-label="`${n.dir === 'out' ? '关联到' : '被关联'}：${n.title}${n.rel ? '（' + n.rel + '）' : ''}`"
        @click="emit('select', n.id)" @keydown.enter.prevent="emit('select', n.id)">
        <title>{{ n.title }}{{ n.rel ? ' · ' + n.rel : '' }}</title>
        <rect :x="n.x - 5" :y="n.y - 5" width="10" height="10" :transform="`rotate(45 ${n.x} ${n.y})`" />
        <text :x="n.dir === 'out' ? n.x + 10 : n.x - 10" :y="n.y + 4" :text-anchor="n.dir === 'out' ? 'start' : 'end'">{{ clip(n.title) }}</text>
      </g>
      <!-- 中心 -->
      <g class="rstar-core" :style="{ '--tc': centerColor }">
        <rect :x="CX - 9" :y="CY - 9" width="18" height="18" :transform="`rotate(45 ${CX} ${CY})`" />
        <rect :x="CX - 4" :y="CY - 4" width="8" height="8" :transform="`rotate(45 ${CX} ${CY})`" class="inner" />
      </g>
      <text v-if="moreOut" :x="W - 6" :y="H - 6" text-anchor="end" class="rstar-more">+{{ moreOut }}</text>
      <text v-if="moreIn" x="6" :y="H - 6" class="rstar-more">+{{ moreIn }}</text>
    </svg>

    <div v-if="outs.length" class="rstar-group">
      <span class="rstar-label">关联到 <i>↗</i></span>
      <div class="rstar-chips">
        <button v-for="r in outs" :key="r.id" type="button" class="rstar-chip" :style="{ '--tc': colorOf(r.targetCardType) }" @click="emit('select', r.targetCardId)">
          <span class="t">{{ r.targetCardTitle || '未命名' }}</span><span v-if="r.relationType" class="k">{{ r.relationType }}</span>
        </button>
      </div>
    </div>
    <div v-if="ins.length" class="rstar-group">
      <span class="rstar-label">被关联 <i>↙</i></span>
      <div class="rstar-chips">
        <button v-for="r in ins" :key="r.id" type="button" class="rstar-chip in" :style="{ '--tc': colorOf(r.sourceCardType) }" @click="emit('select', r.sourceCardId)">
          <span class="t">{{ r.sourceCardTitle || '未命名' }}</span><span v-if="r.relationType" class="k">{{ r.relationType }}</span>
        </button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Relation } from '@/api/types/world'
import { CardTypeMeta, type CardType } from '../card_type'

const props = defineProps<{ title: string; cardType?: string; outs: Relation[]; ins: Relation[] }>()
const emit = defineEmits<{ select: [id: string] }>()

const W = 320, H = 200, CX = W / 2, CY = H / 2, MAX = 7
const gid = `rstar-g-${Math.random().toString(36).slice(2, 8)}`
const colorOf = (type?: string | null) => CardTypeMeta[type as CardType]?.color ?? 'var(--accent-color)'
const centerColor = computed(() => colorOf(props.cardType))
const clip = (t: string) => ([...t].length > 7 ? [...t].slice(0, 6).join('') + '…' : t)
const moreOut = computed(() => Math.max(0, props.outs.length - MAX))
const moreIn = computed(() => Math.max(0, props.ins.length - MAX))

/** 右半边放「关联到」，左半边放「被关联」，按个数均匀铺在一段弧上；连线是一条略弯的曲线 */
function place(list: Relation[], dir: 'out' | 'in') {
  const shown = list.slice(0, MAX)
  const n = shown.length
  const spread = n <= 1 ? 0 : Math.min(130, 34 * (n - 1))
  return shown.map((r, i) => {
    const deg = n <= 1 ? 0 : -spread / 2 + (spread * i) / (n - 1)
    const a = (deg * Math.PI) / 180
    const rx = 96, ry = 84
    const x = dir === 'out' ? CX + Math.cos(a) * rx : CX - Math.cos(a) * rx
    const y = CY + Math.sin(a) * ry
    const mx = (CX + x) / 2, my = (CY + y) / 2 - (dir === 'out' ? 10 : -10)
    const path = dir === 'out' ? `M${CX} ${CY} Q${mx} ${my} ${x} ${y}` : `M${x} ${y} Q${mx} ${my} ${CX} ${CY}`
    const id = dir === 'out' ? r.targetCardId : r.sourceCardId
    const title = (dir === 'out' ? r.targetCardTitle : r.sourceCardTitle) || '未命名'
    const color = colorOf(dir === 'out' ? r.targetCardType : r.sourceCardType)
    return { key: dir + r.id, id, title, rel: r.relationType, x, y, path, dir, color }
  })
}
const nodes = computed(() => [...place(props.outs, 'out'), ...place(props.ins, 'in')])
</script>

<style scoped>
.rstar { min-width: 0; }
.rstar-head { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
.rstar-tag { padding: 1px 7px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .14em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
.rstar-name { font-weight: 600; font-size: 14px; color: var(--text-primary); }
.rstar-count { margin-left: auto; font: 11px var(--font-mono, monospace); color: var(--text-secondary); }

.rstar-map { display: block; width: 100%; max-width: 380px; height: auto; margin: 0 auto 6px; overflow: visible; }
.rstar-rings circle { fill: none; stroke: var(--signal-mint); stroke-width: 1; opacity: .5; }
.rstar-rings .spin { transform-origin: 160px 100px; animation: rstar-spin 18s linear infinite; }
.rstar-link .base { fill: none; stroke: var(--tc); stroke-width: 1; opacity: .35; }
.rstar-link .flow { fill: none; stroke: var(--tc); stroke-width: 2; stroke-linecap: square; stroke-dasharray: 6 94; stroke-dashoffset: 100; animation: rstar-flow 2.6s linear infinite; animation-delay: var(--d); opacity: .9; }
.rstar-link.in .flow { stroke: var(--warning); }
.rstar-node { cursor: pointer; outline: none; transform-box: fill-box; transform-origin: center; animation: rstar-pop .5s cubic-bezier(.2, .8, .2, 1.2) both; animation-delay: var(--d); }
.rstar-node rect { fill: var(--paper-card); stroke: var(--tc); stroke-width: 2; transition: fill .15s; }
.rstar-node text { font-size: 11px; fill: var(--text-primary); paint-order: stroke; stroke: var(--paper-card); stroke-width: 3px; stroke-linejoin: round; }
.rstar-node:hover rect, .rstar-node:focus-visible rect { fill: var(--tc); }
.rstar-node:hover text, .rstar-node:focus-visible text { fill: var(--accent-color); font-weight: 600; }
.rstar-core rect { fill: var(--paper-card); stroke: var(--tc); stroke-width: 2; }
.rstar-core .inner { fill: var(--tc); stroke: none; animation: rstar-pulse 2.4s ease-in-out infinite; }
.rstar-more { font: 10px var(--font-mono, monospace); fill: var(--text-secondary); }

.rstar-group { margin-top: 10px; }
.rstar-label { display: block; margin-bottom: 6px; font: 600 11px/1.4 var(--font-mono, monospace); letter-spacing: .08em; color: var(--text-secondary); }
.rstar-label i { font-style: normal; color: var(--accent-color); }
.rstar-chips { display: flex; flex-wrap: wrap; gap: 6px; }
.rstar-chip {
  --tc: var(--accent-color);
  display: inline-flex; align-items: center; gap: 6px; max-width: 100%; min-height: 28px; padding: 0 8px;
  border: 1px solid var(--border-line); border-left: 3px solid var(--tc); background: var(--paper-card); color: var(--text-primary);
  font-size: 13px; cursor: pointer; transition: border-color .15s, translate .15s, box-shadow .15s;
}
.rstar-chip:hover, .rstar-chip:focus-visible { border-color: var(--tc); translate: 0 -1px; box-shadow: 2px 2px 0 color-mix(in srgb, var(--tc) 22%, transparent); }
.rstar-chip .t { min-width: 0; max-width: 11em; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.rstar-chip .k { padding: 0 5px; font: 11px/1.6 var(--font-mono, monospace); color: var(--accent-color); background: var(--accent-soft); white-space: nowrap; }
.rstar-chip.in .k { color: var(--warning); background: var(--warning-soft); }

@keyframes rstar-spin { to { transform: rotate(360deg); } }
@keyframes rstar-flow { to { stroke-dashoffset: 0; } }
@keyframes rstar-pop { from { opacity: 0; transform: scale(.4); } }
@keyframes rstar-pulse { 50% { opacity: .35; } }
@media (prefers-reduced-motion: reduce) { .rstar-map * { animation: none !important; } }
</style>
