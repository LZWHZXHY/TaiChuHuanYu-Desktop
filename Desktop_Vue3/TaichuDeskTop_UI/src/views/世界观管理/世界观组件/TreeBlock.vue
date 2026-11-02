<!-- TreeBlock.vue：「关系树」区块——像家谱、组织架构那样一层层往下排。
     节点可以连到本世界的条目，也可以只写名字；每个节点可以有好几个孩子，可以写和上一层的关系（长子、副官……）。
     editable = 编辑：上面是节点列表（加节点、删节点、改上一层、上下调整顺序），下面是实时预览；
     阅读时只显示自动排好的树（从上往下），点条目节点在右边弹出那个条目。 -->
<template>
  <div class="tr" :class="{ editable }">
    <template v-if="editable">
      <p v-if="!nodes.length" class="tr-empty">还没有节点。先加一个最上层的（比如家族的祖先、组织的首领），再给它加下一层。</p>
      <ol class="tr-outline">
        <li v-for="row in outline" :key="row.node.id" class="tr-row" :class="{ on: selectedId === row.node.id }" :style="{ '--depth': Math.min(row.depth, 8) }"
          :data-node-id="row.node.id" @focusin="selectedId = row.node.id" @click="selectedId = row.node.id">
          <span class="tr-mark" aria-hidden="true">{{ row.depth ? '└' : '◆' }}</span>
          <div class="tr-fields">
            <div class="tr-name">
              <template v-if="row.node.cardId">
                <span class="tr-chip" :title="'连到条目：' + cardTitle(row.node.cardId)">⧉ {{ cardTitle(row.node.cardId) }}</span>
                <button type="button" class="tr-mini" @click="unlink(row.node)">取消连接</button>
              </template>
              <template v-else>
                <input class="tr-label-input" :value="row.node.label ?? ''" maxlength="60" placeholder="名字" :aria-label="`第 ${row.index + 1} 个节点的名字`"
                  @input="setText(row.node, 'label', ($event.target as HTMLInputElement).value)" />
                <span class="tr-anchor">
                  <button type="button" class="tr-mini" :aria-expanded="pickFor === row.node.id" @click="pickFor = pickFor === row.node.id ? null : row.node.id">连到条目</button>
                  <CardPicker v-if="pickFor === row.node.id" class="tr-picker" :project-id="projectId" @close="pickFor = null" @confirm="ids => linkCards(row.node, ids)" />
                </span>
              </template>
            </div>
            <input v-if="row.node.parentId" class="tr-rel-input" :value="row.node.relation ?? ''" maxlength="30" placeholder="和上一层的关系：长子、副官…" :aria-label="`${nodeName(row.node)}和上一层的关系`"
              @input="setText(row.node, 'relation', ($event.target as HTMLInputElement).value)" />
            <input class="tr-note-input" :value="row.node.note ?? ''" maxlength="120" placeholder="备注（可选）：生卒年、职位…" :aria-label="`${nodeName(row.node)}的备注`"
              @input="setText(row.node, 'note', ($event.target as HTMLInputElement).value)" />
            <label class="tr-parent">上一层
              <select :value="row.node.parentId ?? ''" :aria-label="`${nodeName(row.node)}的上一层`" @change="setParent(row.node, ($event.target as HTMLSelectElement).value)">
                <option value="">（最上层）</option>
                <option v-for="opt in parentOptions(row.node)" :key="opt.node.id" :value="opt.node.id">{{ '　'.repeat(Math.min(opt.depth, 6)) + nodeName(opt.node) }}</option>
              </select>
            </label>
          </div>
          <div class="tr-ops">
            <button type="button" title="上移（同一层里往前排）" :aria-label="`上移 ${nodeName(row.node)}`" :disabled="!canMove(row.node, -1)" @click="move(row.node, -1)">↑</button>
            <button type="button" title="下移（同一层里往后排）" :aria-label="`下移 ${nodeName(row.node)}`" :disabled="!canMove(row.node, 1)" @click="move(row.node, 1)">↓</button>
            <button type="button" class="tr-add-child" :disabled="full" title="给它加一个下一层节点" @click="addNode(row.node.id)">＋ 下一层</button>
            <button type="button" class="tr-del" :aria-label="`删除 ${nodeName(row.node)}`" title="删除这个节点" @click="remove(row.node)">×</button>
          </div>
        </li>
      </ol>
      <div class="tr-add">
        <button type="button" :disabled="full" @click="addNode(null)">＋ 最上层节点</button>
        <span class="tr-anchor">
          <button type="button" :disabled="full" :aria-expanded="pickFor === BULK" @click="pickFor = pickFor === BULK ? null : BULK">＋ 一次添加几个条目{{ selectedNode ? `（加到「${nodeName(selectedNode)}」下面）` : '' }}</button>
          <CardPicker v-if="pickFor === BULK" class="tr-picker" :project-id="projectId" @close="pickFor = null" @confirm="bulkAdd" />
        </span>
        <span class="tr-count">{{ nodes.length }} / {{ TREE_MAX_NODES }}</span>
      </div>
      <p v-if="nodes.length" class="tr-preview-title">预览（点节点可以跳到上面对应的那一行）</p>
    </template>

    <!-- 自动排好的树 -->
    <div v-if="nodes.length" class="tr-chart">
      <div class="tr-zoom">
        <button type="button" title="缩小" aria-label="缩小" @click="setZoom(zoom / 1.2)">－</button>
        <span>{{ Math.round(zoom * 100) }}%</span>
        <button type="button" title="放大" aria-label="放大" @click="setZoom(zoom * 1.2)">＋</button>
        <button type="button" title="缩放到正好放下" @click="fitZoom(true)">适应</button>
      </div>
      <div ref="scroller" class="tr-scroller">
        <div class="tr-sizer" :style="{ width: chart.width * zoom + 'px', height: chart.height * zoom + 'px' }">
          <div class="tr-inner" :style="{ width: chart.width + 'px', height: chart.height + 'px', transform: `scale(${zoom})` }">
            <svg class="tr-lines" :width="chart.width" :height="chart.height" aria-hidden="true">
              <path v-for="e in chart.edges" :key="e.id" :d="e.d" />
            </svg>
            <span v-for="e in chart.edges.filter(x => x.relation)" :key="'r' + e.id" class="tr-rel" :style="{ left: e.lx + 'px', top: e.ly + 'px' }">{{ e.relation }}</span>
            <component :is="clickable(b.node) ? 'button' : 'div'" v-for="b in chart.boxes" :key="b.node.id" :type="clickable(b.node) ? 'button' : undefined"
              class="tr-node" :class="{ card: !!b.node.cardId, on: editable && selectedId === b.node.id }"
              :style="{ left: b.x + 'px', top: b.y + 'px', width: NODE_W + 'px', height: NODE_H + 'px' }"
              :title="b.node.note ? `${nodeName(b.node)}\n${b.node.note}` : nodeName(b.node)" @click="clickNode(b.node)">
              <span v-if="b.node.cardId" class="tr-cover"><img v-if="coverOf(b.node.cardId)" :src="coverOf(b.node.cardId)" alt="" /><span v-else aria-hidden="true">▢</span></span>
              <span class="tr-node-text">
                <b>{{ nodeName(b.node) }}</b>
                <small v-if="b.node.note">{{ b.node.note }}</small>
                <small v-else-if="b.node.cardId" class="tr-type">{{ cardType(b.node.cardId) }}</small>
              </span>
            </component>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel, parseCoverImage } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import { TREE_MAX_NODES, newId, type TreeNode } from '@/utils/worldLayout'
import CardPicker from './CardPicker.vue'

const props = defineProps<{ nodes?: TreeNode[]; projectId: string; editable?: boolean }>()
const emit = defineEmits<{ 'update:nodes': [nodes: TreeNode[]]; peek: [cardId: string] }>()
const store = useWorldStore()
const BULK = '__bulk'

// 编辑时在本地副本上改，改完整个发出去
const nodes = ref<TreeNode[]>([])
watch(() => props.nodes, v => { nodes.value = (v ?? []).map(n => ({ ...n })) }, { immediate: true, deep: true })
const emitNodes = () => emit('update:nodes', nodes.value.map(n => ({ ...n })))
const full = computed(() => nodes.value.length >= TREE_MAX_NODES)

const cardOf = (id?: string | null) => (id ? store.getCardById(id, props.projectId) : undefined)
const cardTitle = (id?: string | null) => cardOf(id)?.title ?? '已删除的条目'
const cardType = (id?: string | null) => getCardTypeLabel(cardOf(id)?.type ?? '')
const coverOf = (id?: string | null) => { const c = cardOf(id)?.coverImage; return c ? withThumbnail(parseCoverImage(c), 96) : '' }
const nodeName = (n: TreeNode) => (n.cardId ? cardTitle(n.cardId) : n.label?.trim() || '未命名')

// ---------- 树的结构 ----------
/** 每个节点的孩子（按数组里的先后顺序）；键 '' = 最上层 */
const kids = computed(() => {
  const ids = new Set(nodes.value.map(n => n.id))
  const map = new Map<string, TreeNode[]>()
  for (const n of nodes.value) {
    const key = n.parentId && ids.has(n.parentId) ? n.parentId : ''
    map.set(key, [...(map.get(key) ?? []), n])
  }
  return map
})
/** 从上往下、先深后广地走一遍（visited 防止编辑中途绕成圈时死循环） */
function walk(visit: (node: TreeNode, depth: number) => void) {
  const seen = new Set<string>()
  const go = (key: string, depth: number) => {
    for (const n of kids.value.get(key) ?? []) {
      if (seen.has(n.id)) continue
      seen.add(n.id)
      visit(n, depth)
      go(n.id, depth + 1)
    }
  }
  go('', 0)
}
const outline = computed(() => {
  const rows: { node: TreeNode; depth: number; index: number }[] = []
  walk((node, depth) => rows.push({ node, depth, index: rows.length }))
  return rows
})
function descendants(id: string) {
  const out = new Set<string>()
  const stack = [id]
  while (stack.length) for (const k of kids.value.get(stack.pop()!) ?? []) if (!out.has(k.id)) { out.add(k.id); stack.push(k.id) }
  return out
}
/** 能当「上一层」的节点：不能是自己，也不能是自己下面的（不然就绕成圈了） */
function parentOptions(node: TreeNode) {
  const banned = descendants(node.id)
  banned.add(node.id)
  return outline.value.filter(r => !banned.has(r.node.id))
}

// ---------- 编辑 ----------
const selectedId = ref<string | null>(null)
const selectedNode = computed(() => nodes.value.find(n => n.id === selectedId.value) ?? null)
const pickFor = ref<string | null>(null)

function setText(node: TreeNode, key: 'label' | 'relation' | 'note', value: string) {
  node[key] = value || undefined
  emitNodes()
}
async function addNode(parentId: string | null) {
  if (full.value) return
  // 加在数组最后 = 排在同一层的最后一个
  const node: TreeNode = { id: newId('n') }
  if (parentId) node.parentId = parentId
  nodes.value.push(node)
  selectedId.value = node.id
  emitNodes()
  await nextTick()
  document.querySelector<HTMLInputElement>(`.tr-row[data-node-id="${node.id}"] .tr-label-input`)?.focus()
}
function linkCards(node: TreeNode, ids: string[]) {
  pickFor.value = null
  const [first, ...rest] = ids
  if (!first) return
  node.cardId = first
  node.label = undefined
  // 一次选了好几个：其余的排在它后面，当它的兄弟节点
  let at = nodes.value.indexOf(node)
  for (const cardId of rest) {
    if (full.value) break
    const extra: TreeNode = { id: newId('n'), cardId }
    if (node.parentId) extra.parentId = node.parentId
    nodes.value.splice(++at, 0, extra)
  }
  emitNodes()
}
function bulkAdd(ids: string[]) {
  pickFor.value = null
  const parentId = selectedNode.value?.id
  for (const cardId of ids) {
    if (full.value) break
    const node: TreeNode = { id: newId('n'), cardId }
    if (parentId) node.parentId = parentId
    nodes.value.push(node)
  }
  emitNodes()
}
function unlink(node: TreeNode) {
  // 取消连接后先用条目名当名字，免得一下子变成「未命名」
  node.label = cardOf(node.cardId)?.title?.slice(0, 60) || undefined
  node.cardId = undefined
  emitNodes()
}
function setParent(node: TreeNode, parentId: string) {
  if (parentId && (parentId === node.id || descendants(node.id).has(parentId))) return
  node.parentId = parentId || undefined
  // 变成最上层就没有「和上一层的关系」了
  if (!node.parentId) node.relation = undefined
  // 挪到数组最后 = 排在新的上一层的最后一个孩子
  nodes.value = [...nodes.value.filter(n => n !== node), node]
  emitNodes()
}
const siblingsOf = (node: TreeNode) => kids.value.get(node.parentId && nodes.value.some(n => n.id === node.parentId) ? node.parentId : '') ?? []
function canMove(node: TreeNode, dir: -1 | 1) {
  const list = siblingsOf(node)
  const i = list.indexOf(node)
  return i >= 0 && i + dir >= 0 && i + dir < list.length
}
/** 同一层里和前一个 / 后一个兄弟换位置（顺序就是数组里的先后，所以交换它们在数组里的位置） */
function move(node: TreeNode, dir: -1 | 1) {
  if (!canMove(node, dir)) return
  const list = siblingsOf(node)
  const other = list[list.indexOf(node) + dir]!
  const arr = [...nodes.value]
  const a = arr.indexOf(node), b = arr.indexOf(other)
  arr[a] = other
  arr[b] = node
  nodes.value = arr
  emitNodes()
}
function remove(node: TreeNode) {
  const children = kids.value.get(node.id) ?? []
  const hasContent = node.cardId || node.label?.trim() || node.note?.trim()
  if (children.length && !window.confirm(`删除「${nodeName(node)}」？它下面的 ${children.length} 个节点会接到上一层。`)) return
  if (!children.length && hasContent && !window.confirm(`删除「${nodeName(node)}」？`)) return
  // 孩子接到它的上一层，不跟着删
  for (const child of children) child.parentId = node.parentId
  nodes.value = nodes.value.filter(n => n !== node)
  if (selectedId.value === node.id) selectedId.value = null
  emitNodes()
}

// ---------- 自动排版：每个节点占它整棵子树的宽度，自己放在中间 ----------
const NODE_W = 150
const NODE_H = 56
const GAP_X = 20
const GAP_Y = 64
const GAP_ROOT = 40
const PAD = 16
const chart = computed(() => {
  const widths = new Map<string, number>()
  const seen = new Set<string>()
  const width = (n: TreeNode): number => {
    if (widths.has(n.id)) return widths.get(n.id)!
    if (seen.has(n.id)) return NODE_W
    seen.add(n.id)
    const list = (kids.value.get(n.id) ?? []).filter(k => !widths.has(k.id) && !seen.has(k.id))
    const sum = list.reduce((a, k) => a + width(k), 0) + GAP_X * Math.max(0, list.length - 1)
    const w = Math.max(NODE_W, sum)
    widths.set(n.id, w)
    return w
  }
  const boxes: { node: TreeNode; x: number; y: number }[] = []
  const edges: { id: string; d: string; relation?: string; lx: number; ly: number }[] = []
  const placed = new Set<string>()
  let maxDepth = 0
  const place = (n: TreeNode, left: number, depth: number) => {
    if (placed.has(n.id)) return
    placed.add(n.id)
    maxDepth = Math.max(maxDepth, depth)
    const w = width(n)
    const x = left + w / 2 - NODE_W / 2
    const y = PAD + depth * (NODE_H + GAP_Y)
    boxes.push({ node: n, x, y })
    const list = (kids.value.get(n.id) ?? []).filter(k => !placed.has(k.id))
    const sum = list.reduce((a, k) => a + width(k), 0) + GAP_X * Math.max(0, list.length - 1)
    let start = left + (w - sum) / 2
    const px = x + NODE_W / 2, py = y + NODE_H, midY = py + GAP_Y / 2
    for (const k of list) {
      const kw = width(k)
      const cx = start + kw / 2, cy = y + NODE_H + GAP_Y
      // 父节点底部往下 → 横过去 → 往下到孩子顶部；关系说明写在最后那一小段竖线上
      edges.push({ id: k.id, d: `M${px} ${py}V${midY}H${cx}V${cy}`, relation: k.relation?.trim() || undefined, lx: cx, ly: cy - GAP_Y / 4 })
      place(k, start, depth + 1)
      start += kw + GAP_X
    }
  }
  let left = PAD
  for (const root of kids.value.get('') ?? []) {
    place(root, left, 0)
    left += width(root) + GAP_ROOT
  }
  return { boxes, edges, width: Math.max(left - GAP_ROOT + PAD, NODE_W + PAD * 2), height: PAD * 2 + (maxDepth + 1) * NODE_H + maxDepth * GAP_Y }
})

const clickable = (n: TreeNode) => (props.editable ? true : !!n.cardId)
function clickNode(n: TreeNode) {
  if (!props.editable) { if (n.cardId) emit('peek', n.cardId); return }
  // 编辑时点预览里的节点：跳到上面那一行
  selectedId.value = n.id
  const row = document.querySelector<HTMLElement>(`.tr-row[data-node-id="${n.id}"]`)
  row?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  row?.querySelector<HTMLInputElement>('input')?.focus({ preventScroll: true })
}

// ---------- 缩放：树太宽时自动缩小到正好放下（最小 40%），也可以手动调 ----------
const scroller = ref<HTMLElement | null>(null)
const zoom = ref(1)
let zoomTouched = false
function setZoom(z: number) { zoomTouched = true; zoom.value = Math.min(1.6, Math.max(0.3, z)) }
function fitZoom(manual = false) {
  if (manual) zoomTouched = true
  else if (zoomTouched) return
  const avail = scroller.value?.clientWidth ?? 0
  if (!avail) return
  zoom.value = Math.min(1, Math.max(0.4, avail / chart.value.width))
}
let observer: ResizeObserver | null = null
watch(scroller, el => {
  observer?.disconnect()
  if (!el) return
  fitZoom()
  if (typeof ResizeObserver !== 'undefined') { observer = new ResizeObserver(() => fitZoom()); observer.observe(el) }
})
watch(() => chart.value.width, () => fitZoom())
onMounted(() => fitZoom())
onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.tr { display: grid; gap: 8px; min-width: 0; }
.tr-empty { margin: 0; font-size: 13px; color: var(--text-secondary); }
.tr-outline { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.tr-row { display: grid; grid-template-columns: 16px minmax(0, 1fr) auto; gap: 8px; align-items: start; padding: 6px 8px 6px calc(8px + var(--depth) * 20px); background: var(--bg-sub); border-left: 2px solid transparent; }
.tr-row.on { border-left-color: var(--accent-color); background: var(--accent-soft); }
.tr-mark { padding-top: 5px; color: var(--text-secondary); font-size: 12px; text-align: center; }
.tr-fields { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); gap: 6px; min-width: 0; }
.tr-name { display: flex; align-items: center; gap: 6px; min-width: 0; }
.tr-fields input, .tr-fields select { box-sizing: border-box; width: 100%; min-width: 0; padding: 5px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.tr-fields input:focus, .tr-fields select:focus { outline: none; border-color: var(--accent-color); }
.tr-name .tr-label-input { flex: 1; font-weight: 600; }
.tr-chip { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 4px 8px; border: 1px solid var(--accent-color); background: var(--paper-card); color: var(--accent-color); font-size: 13px; font-weight: 600; }
.tr-mini { flex-shrink: 0; padding: 4px 8px; border: 1px dashed var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; white-space: nowrap; }
.tr-mini:hover { color: var(--accent-color); border-color: var(--accent-color); }
.tr-anchor { position: relative; }
.tr-picker { position: absolute; left: 0; top: calc(100% + 4px); z-index: 30; }
.tr-parent { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); white-space: nowrap; }
.tr-ops { display: flex; gap: 2px; }
.tr-ops button, .tr-add button { padding: 4px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; white-space: nowrap; }
.tr-ops button:disabled, .tr-add button:disabled { opacity: .4; cursor: default; }
.tr-ops button:hover:not(:disabled), .tr-add button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.tr-ops .tr-del:hover:not(:disabled) { color: var(--danger); border-color: var(--danger); }
.tr-add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.tr-count { margin-left: auto; font-size: 12px; color: var(--text-secondary); }
.tr-preview-title { margin: 6px 0 0; font-size: 12px; color: var(--text-secondary); }
/* min-width: 0：不然树很宽时会把整块撑宽，窄屏上不会自动缩小 */
.tr-chart { position: relative; min-width: 0; border: 1px solid var(--border-line); background: var(--paper-card); }
.tr-zoom { position: absolute; right: 6px; top: 6px; z-index: 2; display: flex; align-items: center; gap: 4px; font-size: 11px; color: var(--text-secondary); }
.tr-zoom button { min-width: 24px; height: 24px; padding: 0 6px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.tr-zoom button:hover { border-color: var(--accent-color); color: var(--accent-color); }
.tr-scroller { overflow: auto; max-height: 75vh; padding-top: 28px; }
.tr-sizer { position: relative; margin: 0 auto; }
.tr-inner { position: absolute; left: 0; top: 0; transform-origin: 0 0; }
.tr-lines { position: absolute; left: 0; top: 0; overflow: visible; }
.tr-lines path { fill: none; stroke: var(--text-secondary); stroke-width: 1.4; opacity: .7; }
.tr-rel { position: absolute; transform: translate(-50%, -50%); max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 0 6px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--accent-color); font-size: 11px; line-height: 1.6; }
.tr-node { position: absolute; box-sizing: border-box; display: flex; align-items: center; gap: 8px; padding: 6px 10px; border: 1px solid var(--border-line); border-top: 3px solid var(--text-secondary); background: var(--bg-sub); color: var(--text-primary); font: inherit; text-align: left; }
.tr-node.card { border-top-color: var(--accent-color); background: var(--paper-card); }
button.tr-node { cursor: pointer; }
button.tr-node:hover, button.tr-node:focus-visible { border-color: var(--accent-color); outline: none; box-shadow: 0 4px 12px rgba(0, 0, 0, .12); }
.tr-node.on { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.tr-cover { flex-shrink: 0; width: 36px; height: 36px; display: grid; place-items: center; overflow: hidden; background: var(--bg-sub); color: var(--text-secondary); }
.tr-cover img { width: 100%; height: 100%; object-fit: cover; }
.tr-node-text { display: grid; min-width: 0; }
.tr-node-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 600; }
.tr-node-text small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; color: var(--text-secondary); }
.tr-node-text .tr-type { color: var(--accent-color); }
@media (max-width: 720px) {
  .tr-row { grid-template-columns: 16px minmax(0, 1fr); padding-left: calc(8px + var(--depth) * 10px); }
  .tr-fields { grid-template-columns: minmax(0, 1fr); }
  .tr-ops { grid-column: 2; flex-wrap: wrap; }
}
</style>
