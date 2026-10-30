<template>
  <div class="go-game">
    <header class="go-header">
      <h2>围棋 · 9 路</h2>
      <div class="go-status">
        <span>黑方提子 {{ captures[1] }} · 白方提子 {{ captures[2] }}</span>
        <span v-if="!gameOver" class="turn">{{ current === 1 ? '● 黑方落子' : '○ 白方落子' }}</span>
      </div>
    </header>

    <div class="board-wrap">
      <div class="board" role="grid">
        <button
          v-for="(_, idx) in 81"
          :key="idx"
          class="cell"
          :class="cellClass(Math.floor(idx / 9), idx % 9)"
          :aria-label="`第${Math.floor(idx / 9) + 1}行第${idx % 9 + 1}列`"
          @click="placeStone(Math.floor(idx / 9), idx % 9)"
        ></button>
      </div>
    </div>

    <div class="go-actions">
      <button class="btn" :disabled="gameOver" @click="pass">停一手</button>
      <button class="btn" @click="reset">重新开始</button>
      <button class="btn primary" :disabled="!gameOver || saved" @click="saveRecord">{{ saved ? '已保存' : '保存对局' }}</button>
    </div>

    <div v-if="gameOver" class="go-result">
      <p v-if="result">黑方 {{ result.blackScore }} ：白方 {{ result.whiteScore }} · {{ winnerText }}</p>
      <p v-else>对局结束（未终局结算）</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useUserStore } from '@/stores/user'
import { gameRecordApi } from '@/api/gameRecord'

const N = 9
const board = ref<number[][]>(Array.from({ length: N }, () => Array(N).fill(0)))
const current = ref(1) // 1 黑 2 白
const captures = ref<Record<number, number>>({ 1: 0, 2: 0 })
const passes = ref(0)
const gameOver = ref(false)
const result = ref<{ blackScore: number; whiteScore: number } | null>(null)
const moves = ref<{ r: number; c: number; color: number }[]>([])
const saved = ref(false)
const userStore = useUserStore()

const winnerText = computed(() => {
  if (!result.value) return ''
  if (result.value.blackScore > result.value.whiteScore) return '黑方胜'
  if (result.value.whiteScore > result.value.blackScore) return '白方胜'
  return '平局'
})

function opponent(color: number) { return color === 1 ? 2 : 1 }
function neighbors(r: number, c: number): [number, number][] {
  return ([[r - 1, c], [r + 1, c], [r, c - 1], [r, c + 1]] as [number, number][]).filter(([x, y]) => x >= 0 && x < N && y >= 0 && y < N)
}
const key = (r: number, c: number) => `${r},${c}`

function getGroup(r: number, c: number): Set<string> {
  const color = board.value[r][c]
  const group = new Set<string>()
  const stack: [number, number][] = [[r, c]]
  const visited = new Set<string>()
  while (stack.length) {
    const [x, y] = stack.pop()!
    const k = key(x, y)
    if (visited.has(k)) continue
    visited.add(k)
    if (board.value[x][y] !== color) continue
    group.add(k)
    for (const [nx, ny] of neighbors(x, y)) stack.push([nx, ny])
  }
  return group
}

function hasLiberty(group: Set<string>): boolean {
  for (const k of group) {
    const [x, y] = k.split(',').map(Number)
    for (const [nx, ny] of neighbors(x, y)) if (board.value[nx][ny] === 0) return true
  }
  return false
}

function removeGroup(group: Set<string>) {
  for (const k of group) {
    const [x, y] = k.split(',').map(Number)
    board.value[x][y] = 0
  }
}

function placeStone(r: number, c: number) {
  if (gameOver.value || board.value[r][c] !== 0) return
  const color = current.value
  board.value[r][c] = color
  // 提掉对方无气的子
  let captured = 0
  for (const [nx, ny] of neighbors(r, c)) {
    if (board.value[nx][ny] === opponent(color)) {
      const g = getGroup(nx, ny)
      if (!hasLiberty(g)) { captured += g.size; removeGroup(g) }
    }
  }
  // 自杀检测
  const own = getGroup(r, c)
  if (!hasLiberty(own)) {
    board.value[r][c] = 0
    return
  }
  captures.value[color] += captured
  passes.value = 0
  moves.value.push({ r, c, color })
  current.value = opponent(color)
}

function pass() {
  if (gameOver.value) return
  passes.value++
  current.value = opponent(current.value)
  if (passes.value >= 2) finish()
}

function finish() {
  if (gameOver.value) return
  gameOver.value = true
  result.value = score()
}

function score() {
  let blackTerritory = 0, whiteTerritory = 0
  const visited = new Set<string>()
  for (let r = 0; r < N; r++) {
    for (let c = 0; c < N; c++) {
      if (board.value[r][c] !== 0 || visited.has(key(r, c))) continue
      const region = new Set<string>()
      const stack: [number, number][] = [[r, c]]
      let touchBlack = false, touchWhite = false
      while (stack.length) {
        const [x, y] = stack.pop()!
        const k = key(x, y)
        if (visited.has(k)) continue
        visited.add(k)
        if (board.value[x][y] !== 0) continue
        region.add(k)
        for (const [nx, ny] of neighbors(x, y)) {
          if (board.value[nx][ny] === 0) stack.push([nx, ny])
          else if (board.value[nx][ny] === 1) touchBlack = true
          else touchWhite = true
        }
      }
      if (touchBlack && !touchWhite) blackTerritory += region.size
      else if (touchWhite && !touchBlack) whiteTerritory += region.size
    }
  }
  return { blackScore: captures.value[1] + blackTerritory, whiteScore: captures.value[2] + whiteTerritory }
}

async function saveRecord() {
  if (!result.value || saved.value) return
  const winner = result.value.blackScore > result.value.whiteScore ? 'player1' : result.value.whiteScore > result.value.blackScore ? 'player2' : 'draw'
  await gameRecordApi.save({
    gameType: 'go',
    player1Name: userStore.userInfo?.username || '黑方',
    player2Name: '白方',
    winner,
    movesCount: moves.value.length,
    movesJson: JSON.stringify(moves.value)
  })
  saved.value = true
}

function reset() {
  board.value = Array.from({ length: N }, () => Array(N).fill(0))
  current.value = 1
  captures.value = { 1: 0, 2: 0 }
  passes.value = 0
  gameOver.value = false
  result.value = null
  moves.value = []
  saved.value = false
}

const starCells = new Set([20, 24, 40, 56, 60]) // 9 路星位：四隅 + 天元
function cellClass(r: number, c: number) {
  return { black: board.value[r][c] === 1, white: board.value[r][c] === 2, star: starCells.has(r * 9 + c) }
}
</script>

<style scoped>
.go-game { max-width: 560px; margin: 0 auto; padding: 16px; }
.go-header { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }
.go-header h2 { margin: 0; }
.go-status { display: flex; gap: 14px; font-size: 13px; color: var(--text-secondary); }
.turn { color: var(--text-primary); font-weight: 600; }
.board-wrap { display: flex; justify-content: center; }
.board {
  display: grid;
  grid-template-columns: repeat(9, 1fr);
  grid-template-rows: repeat(9, 1fr);
  width: min(92vw, 460px);
  aspect-ratio: 1;
  background:
    linear-gradient(to right, transparent calc(50% - 0.5px), #8a6a2f calc(50% - 0.5px), #8a6a2f calc(50% + 0.5px), transparent calc(50% + 0.5px)),
    linear-gradient(to bottom, transparent calc(50% - 0.5px), #8a6a2f calc(50% - 0.5px), #8a6a2f calc(50% + 0.5px), transparent calc(50% + 0.5px)),
    #dcb35c;
  background-size: calc(100% / 9) calc(100% / 9);
  border: 2px solid #8a6a2f;
  border-radius: 4px;
}
.cell {
  position: relative;
  border: none;
  background: transparent;
  cursor: pointer;
  padding: 0;
}
/* 星位 */
.cell.star::before {
  content: '';
  position: absolute;
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  width: 12%; height: 12%;
  border-radius: 50%;
  background: #5a3c14;
  z-index: 0;
}
/* 棋子 */
.cell.black::after, .cell.white::after {
  content: '';
  position: absolute;
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  width: 82%; height: 82%;
  border-radius: 50%;
  z-index: 1;
}
.cell.black::after { background: radial-gradient(circle at 35% 35%, #444, #000); }
.cell.white::after { background: radial-gradient(circle at 35% 35%, #fff, #ddd); border: 1px solid #999; }
.go-actions { display: flex; gap: 10px; justify-content: center; margin-top: 18px; flex-wrap: wrap; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: 0.5; cursor: default; }
.go-result { text-align: center; margin-top: 12px; font-size: 14px; color: var(--text-primary); }
.go-result p { margin: 4px 0; }
</style>
