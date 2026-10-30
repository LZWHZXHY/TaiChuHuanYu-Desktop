<template>
  <div class="xiangqi-game">
    <header class="xq-header">
      <h2>中国象棋</h2>
      <div class="xq-status">
        <span v-if="!gameOver" class="turn">{{ current === 1 ? '红方走棋' : '黑方走棋' }}</span>
        <span v-else class="winner">{{ winnerText }}</span>
      </div>
    </header>

    <div class="xq-board-wrap">
      <div class="xq-board" role="grid">
        <div class="river-text" aria-hidden="true">楚　河　　汉　界</div>
        <button
          v-for="(_, idx) in 90"
          :key="idx"
          class="xq-cell"
          :class="cellClass(Math.floor(idx / 9), idx % 9)"
          @click="onCellClick(Math.floor(idx / 9), idx % 9)"
        >
          <span v-if="pieceAt(idx)" class="piece" :class="'color-' + pieceAt(idx)!.color">
            {{ label(pieceAt(idx)) }}
          </span>
        </button>
      </div>
    </div>

    <div class="xq-actions">
      <button class="btn" @click="reset">重新开始</button>
      <button class="btn primary" :disabled="!gameOver || saved" @click="saveRecord">{{ saved ? '已保存' : '保存对局' }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useUserStore } from '@/stores/user'
import { gameRecordApi } from '@/api/gameRecord'

type Color = 1 | 2
type PieceType = 'king' | 'advisor' | 'elephant' | 'horse' | 'rook' | 'cannon' | 'pawn'
interface Piece { color: Color; type: PieceType }

const ROWS = 10, COLS = 9
const board = ref<(Piece | null)[][]>(initialBoard())
const current = ref<Color>(1)
const selected = ref<[number, number] | null>(null)
const validMoves = ref<[number, number][]>([])
const gameOver = ref(false)
const winner = ref<'' | 'player1' | 'player2'>('')
const moves = ref<{ r: number; c: number }[]>([])
const saved = ref(false)
const userStore = useUserStore()

const labels: Record<PieceType, Record<Color, string>> = {
  king: { 1: '帅', 2: '将' },
  advisor: { 1: '仕', 2: '士' },
  elephant: { 1: '相', 2: '象' },
  horse: { 1: '马', 2: '马' },
  rook: { 1: '车', 2: '车' },
  cannon: { 1: '炮', 2: '炮' },
  pawn: { 1: '兵', 2: '卒' },
}
const label = (p: Piece | null) => (p ? labels[p.type][p.color] : '')
function pieceAt(idx: number): Piece | null {
  const r = Math.floor(idx / COLS), c = idx % COLS
  return board.value[r]?.[c] ?? null
}

const winnerText = computed(() => winner.value === 'player1' ? '红方胜' : winner.value === 'player2' ? '黑方胜' : '')

function initialBoard(): (Piece | null)[][] {
  const b: (Piece | null)[][] = Array.from({ length: ROWS }, () => Array(COLS).fill(null))
  const back: PieceType[] = ['rook', 'horse', 'elephant', 'advisor', 'king', 'advisor', 'elephant', 'horse', 'rook']
  for (let c = 0; c < COLS; c++) {
    b[0][c] = { color: 2, type: back[c] }
    b[9][c] = { color: 1, type: back[c] }
  }
  b[2][1] = b[2][7] = { color: 2, type: 'cannon' }
  b[7][1] = b[7][7] = { color: 1, type: 'cannon' }
  for (let c = 0; c < COLS; c += 2) {
    b[3][c] = { color: 2, type: 'pawn' }
    b[6][c] = { color: 1, type: 'pawn' }
  }
  return b
}

function inBoard(r: number, c: number) { return r >= 0 && r < ROWS && c >= 0 && c < COLS }
function inPalace(color: Color, r: number, c: number) {
  return c >= 3 && c <= 5 && (color === 1 ? r >= 7 && r <= 9 : r >= 0 && r <= 2)
}
function onOwnSide(color: Color, r: number) { return color === 1 ? r >= 5 : r <= 4 }

function getLegalMoves(r: number, c: number): [number, number][] {
  const p = board.value[r][c]
  if (!p) return []
  const { color, type } = p
  const res: [number, number][] = []
  const enemy = (nr: number, nc: number) => !board.value[nr][nc] || board.value[nr][nc]!.color !== color

  if (type === 'king') {
    for (const [dr, dc] of [[-1, 0], [1, 0], [0, -1], [0, 1]] as [number, number][]) {
      const nr = r + dr, nc = c + dc
      if (inPalace(color, nr, nc) && enemy(nr, nc)) res.push([nr, nc])
    }
  } else if (type === 'advisor') {
    for (const [dr, dc] of [[-1, -1], [-1, 1], [1, -1], [1, 1]] as [number, number][]) {
      const nr = r + dr, nc = c + dc
      if (inPalace(color, nr, nc) && enemy(nr, nc)) res.push([nr, nc])
    }
  } else if (type === 'elephant') {
    for (const [dr, dc] of [[-2, -2], [-2, 2], [2, -2], [2, 2]] as [number, number][]) {
      const nr = r + dr, nc = c + dc
      if (!inBoard(nr, nc) || !onOwnSide(color, nr)) continue
      if (board.value[r + dr / 2][c + dc / 2]) continue
      if (enemy(nr, nc)) res.push([nr, nc])
    }
  } else if (type === 'horse') {
    for (const [dr, dc] of [[-2, -1], [-2, 1], [2, -1], [2, 1], [-1, -2], [1, -2], [-1, 2], [1, 2]] as [number, number][]) {
      const nr = r + dr, nc = c + dc
      if (!inBoard(nr, nc)) continue
      const legR = r + (Math.abs(dr) === 2 ? dr / 2 : 0)
      const legC = c + (Math.abs(dc) === 2 ? dc / 2 : 0)
      if (board.value[legR][legC]) continue
      if (enemy(nr, nc)) res.push([nr, nc])
    }
  } else if (type === 'rook') {
    for (const [dr, dc] of [[-1, 0], [1, 0], [0, -1], [0, 1]] as [number, number][]) {
      let nr = r + dr, nc = c + dc
      while (inBoard(nr, nc)) {
        if (!board.value[nr][nc]) res.push([nr, nc])
        else { if (board.value[nr][nc]!.color !== color) res.push([nr, nc]); break }
        nr += dr; nc += dc
      }
    }
  } else if (type === 'cannon') {
    for (const [dr, dc] of [[-1, 0], [1, 0], [0, -1], [0, 1]] as [number, number][]) {
      let nr = r + dr, nc = c + dc, jumped = false
      while (inBoard(nr, nc)) {
        if (!jumped) {
          if (!board.value[nr][nc]) res.push([nr, nc])
          else jumped = true
        } else if (board.value[nr][nc]) {
          if (board.value[nr][nc]!.color !== color) res.push([nr, nc])
          break
        }
        nr += dr; nc += dc
      }
    }
  } else if (type === 'pawn') {
    const forward = color === 1 ? -1 : 1
    const crossed = color === 1 ? r <= 4 : r >= 5
    const cands: [number, number][] = [[r + forward, c]]
    if (crossed) cands.push([r, c - 1], [r, c + 1])
    for (const [nr, nc] of cands) {
      if (inBoard(nr, nc) && enemy(nr, nc)) res.push([nr, nc])
    }
  }
  return res
}

function onCellClick(r: number, c: number) {
  if (gameOver.value) return
  // 点击己方棋子：选中
  const p = board.value[r][c]
  if (p && p.color === current.value) {
    selected.value = [r, c]
    validMoves.value = getLegalMoves(r, c)
    return
  }
  // 点击合法落点：走子
  if (selected.value && validMoves.value.some(([mr, mc]) => mr === r && mc === c)) {
    const [sr, sc] = selected.value
    const captured = board.value[r][c]
    board.value[r][c] = board.value[sr][sc]
    board.value[sr][sc] = null
    moves.value.push({ r, c })
    selected.value = null
    validMoves.value = []
    // 吃将/帅 → 胜
    if (captured && captured.type === 'king') {
      gameOver.value = true
      winner.value = current.value === 1 ? 'player1' : 'player2'
      return
    }
    current.value = current.value === 1 ? 2 : 1
    return
  }
  // 其它：取消选中
  selected.value = null
  validMoves.value = []
}

function cellClass(r: number, c: number) {
  const cls: Record<string, boolean> = {}
  if (selected.value && selected.value[0] === r && selected.value[1] === c) cls.selected = true
  if (validMoves.value.some(([mr, mc]) => mr === r && mc === c)) cls.valid = true
  return cls
}

async function saveRecord() {
  if (!gameOver.value || saved.value) return
  await gameRecordApi.save({
    gameType: 'chess',
    player1Name: userStore.userInfo?.username || '红方',
    player2Name: '黑方',
    winner: winner.value,
    movesCount: moves.value.length,
    movesJson: JSON.stringify(moves.value)
  })
  saved.value = true
}

function reset() {
  board.value = initialBoard()
  current.value = 1
  selected.value = null
  validMoves.value = []
  gameOver.value = false
  winner.value = ''
  moves.value = []
  saved.value = false
}
</script>

<style scoped>
.xiangqi-game { max-width: 620px; margin: 0 auto; padding: 16px; }
.xq-header { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }
.xq-header h2 { margin: 0; }
.xq-status { font-size: 13px; color: var(--text-secondary); }
.turn { color: var(--text-primary); font-weight: 600; }
.winner { color: var(--accent-color); font-weight: 700; }
.xq-board-wrap { display: flex; justify-content: center; overflow-x: auto; }
.xq-board {
  position: relative;
  display: grid;
  grid-template-columns: repeat(9, 1fr);
  grid-template-rows: repeat(10, 1fr);
  width: min(96vw, 560px);
  aspect-ratio: 9 / 10;
  background: #8a6a2f;
  gap: 1px;
  border: 3px solid #8a6a2f;
  border-radius: 4px;
  padding: 1px;
}
.river-text {
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  transform: translateY(-50%);
  text-align: center;
  font-size: clamp(11px, 3vw, 16px);
  font-weight: 700;
  color: #6b4a1f;
  letter-spacing: 1.2em;
  text-indent: 1.2em;
  pointer-events: none;
  z-index: 0;
}
.xq-cell {
  position: relative;
  border: none;
  background: #e8c98a;
  cursor: pointer;
  display: grid;
  place-items: center;
  padding: 0;
}
.xq-cell.selected { background: #f7dfa0; }
.xq-cell.valid::after {
  content: '';
  position: absolute;
  inset: 22%;
  border-radius: 50%;
  background: rgba(0, 150, 60, 0.35);
}
.piece {
  width: min(38px, 82%);
  height: min(38px, 82%);
  border-radius: 50%;
  display: grid;
  place-items: center;
  font-size: clamp(13px, 3.6vw, 19px);
  font-weight: 700;
  font-family: serif;
  border: 1.5px solid #6b4a1f;
  z-index: 1;
}
.piece.color-1 { background: #f8e6c8; color: #b02a2a; }
.piece.color-2 { background: #f8e6c8; color: #1c1c1c; }
.xq-actions { display: flex; gap: 10px; justify-content: center; margin-top: 18px; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: 0.5; cursor: default; }
</style>
