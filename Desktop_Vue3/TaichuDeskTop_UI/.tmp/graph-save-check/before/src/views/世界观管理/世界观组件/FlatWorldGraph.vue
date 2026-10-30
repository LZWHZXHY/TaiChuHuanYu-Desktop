<template><div ref="container" class="flat-graph"><canvas ref="canvas" tabindex="0" aria-label="关系画布，可拖动、缩放，方向键移动，0 键适应画布" @pointerdown="down" @pointermove="move" @pointerup="up" @pointercancel="cancel" @wheel.prevent="wheel" @keydown="key" /><div class="labels"><button v-for="label in labels" :key="label.id" :class="{ 'is-selected': selected.has(label.id) || focusId === label.id }" :title="label.title" :style="{left:label.left+'px',top:label.top+'px',width:label.width+'px'}" @pointerdown="labelDown($event, label)" @keydown.enter.prevent="emit('select', label.id)" @keydown.space.prevent="emit('select', label.id)">{{ label.title }}</button></div></div></template>
<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { layoutWorldGraph, fitWorldGraph } from '@/utils/worldGraphLayout';
import { placeGraphLabels, type WorldGraphNode, type WorldGraphEdge } from '@/utils/worldGraph';
const props = defineProps<{
  data: {
    nodes: WorldGraphNode[];
    links: WorldGraphEdge[];
  };
  layoutData?: { nodes: WorldGraphNode[]; links: WorldGraphEdge[] };
  focusId?: string;
  positions?: Record<string, { x: number; y: number }>;
}>();
const emit = defineEmits<{
  select: [
    id: string
  ];
  'update:positions': [
    positions: Record<string, { x: number; y: number }>
  ];
}>();
const container = ref<HTMLElement | null>(null), canvas = ref<HTMLCanvasElement | null>(null);
const width = ref(1), height = ref(1), zoom = ref(1), offset = ref({ x: 0, y: 0 });
const layout = computed(() => layoutWorldGraph((props.layoutData || props.data).nodes, (props.layoutData || props.data).links));
const nodes = computed(() => { const visible = new Set(props.data.nodes.map(n => n.id)); return layout.value.filter(n => visible.has(n.id)); });
// 手动拖动过的节点：记录世界坐标，覆盖自动布局
const pinned = ref(new Map<string, { x: number; y: number }>());
let nodeDrag: { id: string; startX: number; startY: number; origX: number; origY: number; moved: boolean } | null = null;
// 外部传入的持久化坐标：只在首次加载时注入；之后以本地拖拽结果为准，避免保存回包把节点弹回原位
let positionsSeeded = false;
watch(() => props.positions, p => {
  if (positionsSeeded || !p || !Object.keys(p).length) return;
  pinned.value = new Map(Object.entries(p).filter(([,v]) => Number.isFinite(v.x) && Number.isFinite(v.y)));
  // 下一帧已获得画布尺寸后重新按持久化位置取景。
  requestAnimationFrame(() => resetCamera());
  positionsSeeded = true;
}, { immediate: true });
const worldNodes = computed(() => nodes.value.map(node => ({ ...node, ...(pinned.value.get(node.id) || {}) })));
const screen = computed(() => worldNodes.value.map(node => ({ ...node, x: node.x * zoom.value + offset.value.x, y: node.y * zoom.value + offset.value.y })));
const edgeOffsets = computed(() => {
  const offsets = new Map<WorldGraphEdge, number>();
  const groups = new Map<string, WorldGraphEdge[]>();
  for (const e of props.data.links) {
    const k = [e.source, e.target].sort().join('|');
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k)!.push(e);
  }
  for (const arr of groups.values()) {
    if (arr.length < 2) continue;
    arr.forEach((e, i) => offsets.set(e, (i - (arr.length - 1) / 2) * 44));
  }
  return offsets;
});
// ===== 多选 / 框选 / 批量拖拽 =====
const selected = ref(new Set<string>());
let marquee: { x0: number; y0: number; x1: number; y1: number } | null = null;
let dragOrigins = new Map<string, { x: number; y: number }>();
const labels = computed(() => {
  // 缩得太小时只画圆点：固定尺寸的卡片会挡住大量节点
  if (zoom.value < 0.2 && !props.focusId) return [];
  const byId = new Map(screen.value.map(node => [node.id, node]));
  return placeGraphLabels(screen.value.map(node => ({ ...node, z: 0, priority: node.id === props.focusId || selected.value.has(node.id), width: Math.min(176, Math.max(80, [...node.title].length * 14 + 20)) })), width.value, height.value)
    .map(label => ({ ...label, title: byId.get(label.id)!.title }));
});
let observer: ResizeObserver, theme: MutationObserver, frame = 0;
function draw() {
  frame = 0;
  const ctx = canvas.value?.getContext('2d');
  if (!ctx || !container.value)
    return;
  const ratio = Math.min(devicePixelRatio || 1, 2);
  canvas.value!.width = width.value * ratio;
  canvas.value!.height = height.value * ratio;
  ctx.scale(ratio, ratio);
  ctx.clearRect(0, 0, width.value, height.value);
  const style = getComputedStyle(container.value), map = new Map(screen.value.map(node => [node.id, node]));
  ctx.strokeStyle = style.getPropertyValue('--text-secondary').trim();
  ctx.lineWidth = 1;
  for (const embedded of [false, true]) {
    ctx.globalAlpha = screen.value.length > 250 ? .16 : .4;
    ctx.setLineDash(embedded ? [3, 5] : []);
    ctx.beginPath();
    for (const edge of props.data.links) {
      if (edge.isEmbed !== embedded)
        continue;
      const a = map.get(edge.source), b = map.get(edge.target);
      if (!a || !b)
        continue;
      if (Math.max(a.x, b.x) < 0 || Math.min(a.x, b.x) > width.value || Math.max(a.y, b.y) < 0 || Math.min(a.y, b.y) > height.value)
        continue;
      // A→B 与 B→A 的垂直方向相反，乘上方向符号，保证两条弧线朝相反两侧弯
      const off = (edgeOffsets.value.get(edge) ?? 0) * ([edge.source, edge.target].sort()[0] === edge.source ? 1 : -1);
      const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy) || 1;
      const cx = (a.x + b.x) / 2 - dy / len * off, cy = (a.y + b.y) / 2 + dx / len * off;
      const curved = off !== 0;
      ctx.moveTo(a.x, a.y);
      if (curved)
        ctx.quadraticCurveTo(cx, cy, b.x, b.y);
      else
        ctx.lineTo(b.x, b.y);
      if (!embedded && screen.value.length <= 250) {
        // 箭头沿曲线切线方向，避免多条边的箭头叠在同一个点上
        const tx = curved ? b.x - cx : dx, ty = curved ? b.y - cy : dy;
        const angle = Math.atan2(ty, tx), x = b.x - Math.cos(angle) * 9, y = b.y - Math.sin(angle) * 9;
        ctx.moveTo(x - Math.cos(angle - .45) * 6, y - Math.sin(angle - .45) * 6);
        ctx.lineTo(x, y);
        ctx.lineTo(x - Math.cos(angle + .45) * 6, y - Math.sin(angle + .45) * 6);
      }
    }
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
  ctx.setLineDash([]);
  if (screen.value.length <= 25) {
    ctx.font = '12px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    for (const edge of props.data.links) {
      const a = map.get(edge.source), b = map.get(edge.target);
      if (!a || !b || Math.hypot(a.x - b.x, a.y - b.y) < 70)
        continue;
      const raw = (edgeOffsets.value.get(edge) ?? 0) * ([edge.source, edge.target].sort()[0] === edge.source ? 1 : -1);
      const ldx = b.x - a.x, ldy = b.y - a.y, llen = Math.hypot(ldx, ldy) || 1;
      const mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2;
      const cx = mx - ldy / llen * raw, cy = my + ldx / llen * raw;
      // 标签放在弧线中点，避免多条关系的文字叠在一起
      const x = 0.25 * a.x + 0.5 * cx + 0.25 * b.x, y = 0.25 * a.y + 0.5 * cy + 0.25 * b.y;
      const text = edge.label || '引用', w = ctx.measureText(text).width + 10;
      ctx.fillStyle = style.getPropertyValue('--paper-card').trim();
      ctx.fillRect(x - w / 2, y - 9, w, 18);
      ctx.fillStyle = style.getPropertyValue('--text-secondary').trim();
      ctx.fillText(text, x, y);
    }
  }
  const dotR = Math.max(2.5, Math.min(8, 4.5 * Math.max(.7, Math.min(1.6, zoom.value))));
  const dotRing = style.getPropertyValue('--paper-card').trim();
  for (const node of screen.value) {
    if (node.x < 0 || node.y < 0 || node.x > width.value || node.y > height.value)
      continue;
    ctx.beginPath();
    ctx.arc(node.x, node.y, dotR, 0, Math.PI * 2);
    ctx.fillStyle = node.color;
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = dotRing;
    ctx.stroke();
  }
  if (marquee) {
    const x = Math.min(marquee.x0, marquee.x1), y = Math.min(marquee.y0, marquee.y1);
    const w = Math.abs(marquee.x1 - marquee.x0), h = Math.abs(marquee.y1 - marquee.y0);
    const accent = style.getPropertyValue('--accent-color').trim() || '#c0392b';
    ctx.save();
    ctx.globalAlpha = .12;
    ctx.fillStyle = accent;
    ctx.fillRect(x, y, w, h);
    ctx.globalAlpha = .85;
    ctx.setLineDash([4, 4]);
    ctx.strokeStyle = accent;
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, w, h);
    ctx.restore();
  }
}
function schedule() { if (!frame)
  frame = requestAnimationFrame(draw); }
function resetCamera() {
  const focused = props.focusId ? worldNodes.value.filter(node => node.id === props.focusId) : worldNodes.value.filter(node => selected.value.has(node.id));
  const fit = fitWorldGraph(focused.length ? focused : worldNodes.value, width.value, height.value);
  if (fit) { zoom.value = fit.zoom; offset.value = fit.offset; }
}
function scale(factor: number, x = width.value / 2, y = height.value / 2) { const next = Math.max(.0001, Math.min(4, zoom.value * factor)), ratio = next / zoom.value; offset.value = { x: x - (x - offset.value.x) * ratio, y: y - (y - offset.value.y) * ratio }; zoom.value = next; }
let drag: {
  x: number;
  y: number;
  startX: number;
  startY: number;
  moved: boolean;
} | null = null;
const pointers = new Map<number, {
  x: number;
  y: number;
}>();
function down(e: PointerEvent) {
  canvas.value?.setPointerCapture(e.pointerId);
  if (e.shiftKey) {
    marquee = { x0: e.offsetX, y0: e.offsetY, x1: e.offsetX, y1: e.offsetY };
    schedule();
    return;
  }
  selected.value = new Set();
  pointers.set(e.pointerId, { x: e.offsetX, y: e.offsetY });
  drag = { x: e.offsetX, y: e.offsetY, startX: e.offsetX, startY: e.offsetY, moved: pointers.size > 1 };
}
function move(e: PointerEvent) { if (marquee) { marquee.x1 = e.offsetX; marquee.y1 = e.offsetY; schedule(); return; } if (!pointers.has(e.pointerId) || !drag)
  return; const before = [...pointers.values()]; pointers.set(e.pointerId, { x: e.offsetX, y: e.offsetY }); const after = [...pointers.values()]; if (before.length === 2) {
  const a = before[0]!, b = before[1]!, c = after[0]!, d = after[1]!;
  scale(Math.hypot(c.x - d.x, c.y - d.y) / Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)), (c.x + d.x) / 2, (c.y + d.y) / 2);
  drag.moved = true;
}
else {
  offset.value = { x: offset.value.x + e.offsetX - drag.x, y: offset.value.y + e.offsetY - drag.y };
  drag.moved ||= Math.hypot(e.offsetX - drag.startX, e.offsetY - drag.startY) > 4;
} drag.x = e.offsetX; drag.y = e.offsetY; }
function up(e: PointerEvent) {
  if (marquee) {
    const m = marquee;
    marquee = null;
    const l = Math.min(m.x0, m.x1), r = Math.max(m.x0, m.x1), t = Math.min(m.y0, m.y1), b = Math.max(m.y0, m.y1);
    selected.value = new Set(screen.value.filter(n => n.x >= l && n.x <= r && n.y >= t && n.y <= b).map(n => n.id));
    schedule();
    cancel(e);
    return;
  }
  if (drag && !drag.moved) {
    const hit = screen.value.find(n => Math.hypot(n.x - e.offsetX, n.y - e.offsetY) < 12);
    if (hit) emit('select', hit.id);
  }
  cancel(e);
}
function cancel(e: PointerEvent) { pointers.delete(e.pointerId); const next = [...pointers.values()][0]; drag = next ? { ...next, startX: next.x, startY: next.y, moved: true } : null; }

// ===== 节点拖拽（按住节点拖动可自由摆放） =====
// 用 window 监听而不是 pointer capture：节点标签在拖动中可能被重新布局/移除，
// 一旦元素消失 capture 就会丢失，导致拖拽卡死、松手无反应。
function labelDown(e: PointerEvent, label: { id: string }) {
  e.preventDefault();
  e.stopPropagation();
  // Shift+点击：加入/移出多选
  if (e.shiftKey) {
    const next = new Set(selected.value);
    if (next.has(label.id)) next.delete(label.id);
    else next.add(label.id);
    selected.value = next;
    return;
  }
  if (!selected.value.has(label.id)) selected.value = new Set([label.id]);
  // 记录所有选中节点的起始坐标：拖动时整组一起移动
  dragOrigins = new Map();
  for (const id of selected.value) {
    const node = nodes.value.find(n => n.id === id);
    const p = pinned.value.get(id);
    const x = p ? p.x : node?.x;
    const y = p ? p.y : node?.y;
    if (x !== undefined && y !== undefined) dragOrigins.set(id, { x, y });
  }
  nodeDrag = { id: label.id, startX: e.clientX, startY: e.clientY, origX: 0, origY: 0, moved: false };
  window.addEventListener('pointermove', onWindowMove);
  window.addEventListener('pointerup', onWindowUp);
  window.addEventListener('pointercancel', onWindowCancel);
}
function onWindowMove(e: PointerEvent) {
  const d = nodeDrag;
  if (!d) return;
  const dx = e.clientX - d.startX;
  const dy = e.clientY - d.startY;
  if (!d.moved && Math.hypot(dx, dy) > 4) d.moved = true;
  if (!d.moved) return;
  const next = new Map(pinned.value);
  // 整组选中节点同步位移
  for (const [id, origin] of dragOrigins) next.set(id, { x: origin.x + dx / zoom.value, y: origin.y + dy / zoom.value });
  positionsSeeded = true;
  pinned.value = next;
  schedule();
}
function detachWindow() {
  window.removeEventListener('pointermove', onWindowMove);
  window.removeEventListener('pointerup', onWindowUp);
  window.removeEventListener('pointercancel', onWindowCancel);
}
function onWindowUp() {
  const d = nodeDrag;
  nodeDrag = null;
  detachWindow();
  if (!d) return;
  if (!d.moved) emit('select', d.id);
  else emit('update:positions', Object.fromEntries(pinned.value));
}
function onWindowCancel() {
  nodeDrag = null;
  detachWindow();
}
function wheel(e: WheelEvent) { scale(Math.exp(-e.deltaY * .001), e.offsetX, e.offsetY); }
function key(e: KeyboardEvent) { if (e.key === '0')
  resetCamera();
else if (e.key === '+' || e.key === '=')
  scale(1.2);
else if (e.key === '-')
  scale(1 / 1.2);
else if (e.key.startsWith('Arrow')) {
  offset.value = { x: offset.value.x + (e.key === 'ArrowLeft' ? 40 : e.key === 'ArrowRight' ? -40 : 0), y: offset.value.y + (e.key === 'ArrowUp' ? 40 : e.key === 'ArrowDown' ? -40 : 0) };
}
else
  return; e.preventDefault(); }
// 只在「节点集合发生变化」时重置相机；保存回包/重渲染不再让视野跳走
watch(() => props.focusId, () => { selected.value = new Set(); resetCamera(); }, { flush: 'post' });
// 批量引用返回后连通分组会变化，也要按新布局取景；标题修改不会触发。
watch(() => layout.value.map(n => `${n.id}:${n.x}:${n.y}`).join('|'), resetCamera, { flush: 'post' });
let lastNodeKey = '';
watch(() => props.data.nodes.map(n => n.id).join('|'), key => {
  if (key === lastNodeKey) return;
  lastNodeKey = key;
  const visible = new Set(props.data.nodes.map(n => n.id));
  selected.value = new Set([...selected.value].filter(id => visible.has(id)));
  resetCamera();
  schedule();
}, { immediate: true });
watch([screen, width, height], schedule);
onMounted(() => { observer = new ResizeObserver(() => { width.value = container.value!.clientWidth; height.value = container.value!.clientHeight; resetCamera(); schedule(); }); observer.observe(container.value!); theme = new MutationObserver(schedule); theme.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] }); schedule(); });
onBeforeUnmount(() => { detachWindow(); observer?.disconnect(); theme?.disconnect(); cancelAnimationFrame(frame); });
defineExpose({ resetCamera });
</script>
<style scoped>
.flat-graph,canvas,.labels{position:absolute;inset:0;width:100%;height:100%}canvas{touch-action:none;cursor:grab}canvas:active{cursor:grabbing}.labels{pointer-events:none;overflow:hidden}.labels button{position:absolute;pointer-events:auto;box-sizing:border-box;height:30px;padding:4px 11px;border:1px solid var(--border-line);border-radius:8px;background:var(--paper-card);color:var(--text-primary);font:500 13px/20px ui-sans-serif,system-ui,sans-serif;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;cursor:grab;touch-action:none;box-shadow:0 1px 3px rgba(0,0,0,.08);transition:border-color .12s,box-shadow .12s}.labels button:active{cursor:grabbing}.labels button.is-selected{border-color:var(--accent-color);background:var(--accent-soft);color:var(--accent-color);box-shadow:0 0 0 2px var(--accent-soft)}.labels button:hover,.labels button:focus-visible{border-color:var(--accent-color);outline:1px solid var(--accent-color)}
</style>
