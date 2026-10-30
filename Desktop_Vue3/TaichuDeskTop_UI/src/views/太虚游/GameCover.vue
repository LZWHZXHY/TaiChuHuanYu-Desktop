<template>
  <!-- 社区小游戏的封面插画：按游戏画一张简洁的线稿，颜色全部跟随主题变量 -->
  <svg class="game-cover" viewBox="0 0 320 180" role="img" :aria-label="`${label} 封面`" preserveAspectRatio="xMidYMid slice">
    <defs>
      <pattern :id="`grid-${uid}`" width="20" height="20" patternUnits="userSpaceOnUse"><path d="M20 0H0V20" fill="none" stroke="currentColor" stroke-opacity=".08" /></pattern>
      <linearGradient :id="`fade-${uid}`" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="var(--signal-mint)" stop-opacity=".22" /><stop offset="1" stop-color="var(--accent-color)" stop-opacity=".06" /></linearGradient>
    </defs>
    <rect width="320" height="180" :fill="`url(#fade-${uid})`" />
    <rect width="320" height="180" :fill="`url(#grid-${uid})`" />

    <!-- 五子棋：棋盘 + 连成一线的黑子 -->
    <g v-if="kind === 'Gobang'" transform="translate(170 22)">
      <g stroke="currentColor" stroke-opacity=".45"><path v-for="i in 9" :key="'h' + i" :d="`M0 ${(i - 1) * 17}H136`" /><path v-for="i in 9" :key="'v' + i" :d="`M${(i - 1) * 17} 0V136`" /></g>
      <circle v-for="(p, i) in [[1,6],[2,5],[3,4],[4,3],[5,2]]" :key="'b' + i" :cx="p[0] * 17" :cy="p[1] * 17" r="7" fill="var(--text-primary)" />
      <circle v-for="(p, i) in [[3,3],[4,5],[5,4],[2,4]]" :key="'w' + i" :cx="p[0] * 17" :cy="p[1] * 17" r="7" fill="var(--paper-card)" stroke="currentColor" />
      <path d="M17 102 85 34" stroke="var(--signal-mint)" stroke-width="3" stroke-linecap="square" />
    </g>

    <!-- 围棋：星位 + 一块活棋 -->
    <g v-else-if="kind === 'Go'" transform="translate(176 26)">
      <rect x="-8" y="-8" width="144" height="144" fill="var(--signal-color, var(--accent-color))" fill-opacity=".08" />
      <g stroke="currentColor" stroke-opacity=".5"><path v-for="i in 9" :key="'h' + i" :d="`M0 ${(i - 1) * 16}H128`" /><path v-for="i in 9" :key="'v' + i" :d="`M${(i - 1) * 16} 0V128`" /></g>
      <circle v-for="(p, i) in [[2,2],[6,2],[4,4],[2,6],[6,6]]" :key="'s' + i" :cx="p[0] * 16" :cy="p[1] * 16" r="2.5" fill="currentColor" />
      <circle v-for="(p, i) in [[3,3],[4,3],[5,4],[3,5],[4,5]]" :key="'b' + i" :cx="p[0] * 16" :cy="p[1] * 16" r="7" fill="var(--text-primary)" />
      <circle v-for="(p, i) in [[5,3],[6,4],[5,5],[2,4],[3,6]]" :key="'w' + i" :cx="p[0] * 16" :cy="p[1] * 16" r="7" fill="var(--paper-card)" stroke="currentColor" />
    </g>

    <!-- 象棋：九宫 + 帅、将 -->
    <g v-else-if="kind === 'Xiangqi'" transform="translate(168 18)">
      <g stroke="currentColor" stroke-opacity=".45"><path v-for="i in 6" :key="'h' + i" :d="`M0 ${(i - 1) * 28}H128`" /><path v-for="i in 5" :key="'v' + i" :d="`M${(i - 1) * 32} 0V140`" /><path d="M32 0 96 56M96 0 32 56" /></g>
      <g font-size="17" font-weight="700" text-anchor="middle" dominant-baseline="central">
        <circle cx="64" cy="28" r="15" fill="var(--paper-card)" stroke="var(--danger)" stroke-width="2" /><text x="64" y="29" fill="var(--danger)">帥</text>
        <circle cx="32" cy="112" r="15" fill="var(--paper-card)" stroke="currentColor" stroke-width="2" /><text x="32" y="113" fill="currentColor">將</text>
        <circle cx="96" cy="84" r="15" fill="var(--paper-card)" stroke="var(--danger)" stroke-width="2" /><text x="96" y="85" fill="var(--danger)">炮</text>
      </g>
    </g>

    <!-- 斗地主：扇形展开的手牌 + 王 -->
    <g v-else-if="kind === 'Doudizhu'" transform="translate(236 150)">
      <g v-for="(c, i) in ddzFan" :key="i" :transform="`rotate(${c.a}) translate(-17 -112)`">
        <rect width="34" height="48" rx="3" fill="var(--paper-card)" stroke="currentColor" stroke-opacity=".55" />
        <text x="5" y="15" font-size="13" font-weight="700" :fill="c.red ? 'var(--danger)' : 'var(--text-primary)'">{{ c.t }}</text>
        <text x="5" y="30" font-size="11" :fill="c.red ? 'var(--danger)' : 'var(--text-primary)'">{{ c.s }}</text>
      </g>
    </g>

    <!-- 俄罗斯方块 -->
    <g v-else-if="kind === 'Tetris'" transform="translate(196 18)">
      <rect v-for="(b, i) in tetris" :key="i" :x="b[0] * 18" :y="b[1] * 18" width="16" height="16" :fill="b[2]" />
    </g>

    <!-- 贪吃蛇 -->
    <g v-else-if="kind === 'Snake'" transform="translate(178 26)">
      <rect v-for="(b, i) in snake" :key="i" :x="b[0] * 16" :y="b[1] * 16" width="14" height="14" fill="var(--signal-mint)" :fill-opacity="0.35 + i / snake.length * 0.65" />
      <rect x="112" y="16" width="14" height="14" fill="var(--danger)" />
    </g>

    <!-- 测试问卷：雷达图 -->
    <g v-else-if="kind === 'TestGame'" transform="translate(252 104) scale(.82)">
      <polygon v-for="r in [24, 48, 72]" :key="r" :points="hexagon(r)" fill="none" stroke="currentColor" stroke-opacity=".35" />
      <polygon :points="radar" fill="var(--signal-mint)" fill-opacity=".35" stroke="var(--signal-mint)" stroke-width="2" />
    </g>

    <!-- 自定义工坊：节点连线 -->
    <g v-else-if="kind === 'CustomStudio'" transform="translate(172 30)" stroke="currentColor" fill="none">
      <path d="M20 20 70 60 130 24M70 60 60 116M70 60 128 104" stroke-opacity=".5" />
      <rect x="8" y="8" width="24" height="24" fill="var(--paper-card)" /><rect x="58" y="48" width="24" height="24" fill="var(--signal-mint)" fill-opacity=".5" />
      <circle cx="130" cy="24" r="12" fill="var(--paper-card)" /><circle cx="60" cy="116" r="12" fill="var(--paper-card)" /><path d="M118 92h20v20h-20z" fill="var(--paper-card)" />
      <path d="M96 140 140 96l8 8-44 44h-8z" fill="var(--accent-color)" fill-opacity=".25" />
    </g>

    <!-- 其他：大号符号 -->
    <text v-else x="246" y="96" font-size="72" text-anchor="middle" dominant-baseline="central" fill="currentColor" fill-opacity=".25">{{ icon }}</text>

    <!-- 左侧标题 -->
    <text x="20" y="34" class="gc-eyebrow" fill="var(--accent-color)">{{ eyebrow }}</text>
    <text x="20" y="66" class="gc-title" fill="var(--text-primary)">{{ label }}</text>
  </svg>
</template>

<script setup lang="ts">
const props = defineProps<{ kind: string; label: string; icon?: string; eyebrow?: string }>()
const uid = `${props.kind}-${Math.random().toString(36).slice(2, 7)}`

const tetris: [number, number, string][] = [
  [3, 0, 'var(--signal-mint)'], [4, 0, 'var(--signal-mint)'], [4, 1, 'var(--signal-mint)'], [5, 1, 'var(--signal-mint)'],
  [0, 5, 'var(--accent-color)'], [1, 5, 'var(--accent-color)'], [2, 5, 'var(--accent-color)'], [1, 4, 'var(--accent-color)'],
  [3, 5, 'var(--danger)'], [3, 6, 'var(--danger)'], [4, 6, 'var(--danger)'], [5, 6, 'var(--danger)'],
  [0, 6, 'var(--text-secondary)'], [1, 6, 'var(--text-secondary)'], [2, 6, 'var(--text-secondary)'], [5, 5, 'var(--text-secondary)'],
]
const snake: [number, number][] = [[0, 6], [1, 6], [2, 6], [3, 6], [3, 5], [3, 4], [4, 4], [5, 4], [6, 4], [6, 3], [6, 2], [6, 1], [5, 1]]
function hexagon(r: number, scale: number[] = [1, 1, 1, 1, 1, 1]) {
  return scale.map((s, i) => { const a = Math.PI / 3 * i - Math.PI / 2; return `${(Math.cos(a) * r * s).toFixed(1)},${(Math.sin(a) * r * s).toFixed(1)}` }).join(' ')
}
const radar = hexagon(72, [0.9, 0.55, 0.8, 0.4, 0.95, 0.6])
const ddzFan = [
  { a: -36, t: '3', s: '♠', red: false }, { a: -22, t: '7', s: '♦', red: true }, { a: -8, t: 'J', s: '♣', red: false },
  { a: 6, t: 'A', s: '♥', red: true }, { a: 20, t: '2', s: '♠', red: false }, { a: 34, t: '王', s: '★', red: true },
]
</script>

<style scoped>
.game-cover { display: block; width: 100%; height: auto; aspect-ratio: 16 / 9; color: var(--text-primary); background: var(--paper-card); }
.gc-eyebrow { font: 600 10px var(--font-mono, monospace); letter-spacing: .14em; }
.gc-title { font-size: 22px; font-weight: 750; }
</style>
