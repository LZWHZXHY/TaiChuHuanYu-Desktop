import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { layoutWorldGraph } from '@/utils/worldGraphLayout';
import { placeGraphLabels, type WorldGraphNode, type WorldGraphEdge } from '@/utils/worldGraph';
const props = defineProps<{
    data: {
        nodes: WorldGraphNode[];
        links: WorldGraphEdge[];
    };
}>();
const emit = defineEmits<{
    select: [
        id: string
    ];
}>();
const container = ref<HTMLElement | null>(null), canvas = ref<HTMLCanvasElement | null>(null);
const width = ref(1), height = ref(1), zoom = ref(1), offset = ref({ x: 0, y: 0 });
const nodes = computed(() => layoutWorldGraph(props.data.nodes, props.data.links));
const screen = computed(() => nodes.value.map(node => ({ ...node, x: node.x * zoom.value + offset.value.x, y: node.y * zoom.value + offset.value.y })));
const labels = computed(() => {
    const byId = new Map(screen.value.map(node => [node.id, node]));
    return placeGraphLabels(screen.value.map(node => ({ ...node, z: 0, width: Math.min(176, Math.max(80, [...node.title].length * 14 + 20)) })), width.value, height.value)
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
            ctx.moveTo(a.x, a.y);
            ctx.lineTo(b.x, b.y);
            if (!embedded && screen.value.length <= 250) {
                const angle = Math.atan2(b.y - a.y, b.x - a.x), x = b.x - Math.cos(angle) * 9, y = b.y - Math.sin(angle) * 9;
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
            const x = (a.x + b.x) / 2, y = (a.y + b.y) / 2, text = edge.label || '引用', w = ctx.measureText(text).width + 10;
            ctx.fillStyle = style.getPropertyValue('--paper-card').trim();
            ctx.fillRect(x - w / 2, y - 9, w, 18);
            ctx.fillStyle = style.getPropertyValue('--text-secondary').trim();
            ctx.fillText(text, x, y);
        }
    }
    for (const node of screen.value) {
        if (node.x < 0 || node.y < 0 || node.x > width.value || node.y > height.value)
            continue;
        ctx.fillStyle = node.color;
        ctx.beginPath();
        ctx.arc(node.x, node.y, 4, 0, Math.PI * 2);
        ctx.fill();
    }
}
function schedule() { if (!frame)
    frame = requestAnimationFrame(draw); }
function resetCamera() {
    if (!nodes.value.length)
        return;
    const xs = nodes.value.map(n => n.x), ys = nodes.value.map(n => n.y), minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    zoom.value = Math.min(1.4, Math.max(.03, Math.min((width.value - 120) / Math.max(100, maxX - minX), (height.value - 100) / Math.max(100, maxY - minY))));
    offset.value = { x: width.value / 2 - (minX + maxX) / 2 * zoom.value, y: height.value / 2 - (minY + maxY) / 2 * zoom.value };
}
function scale(factor: number, x = width.value / 2, y = height.value / 2) { const next = Math.max(.03, Math.min(4, zoom.value * factor)), ratio = next / zoom.value; offset.value = { x: x - (x - offset.value.x) * ratio, y: y - (y - offset.value.y) * ratio }; zoom.value = next; }
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
function down(e: PointerEvent) { canvas.value?.setPointerCapture(e.pointerId); pointers.set(e.pointerId, { x: e.offsetX, y: e.offsetY }); drag = { x: e.offsetX, y: e.offsetY, startX: e.offsetX, startY: e.offsetY, moved: pointers.size > 1 }; }
function move(e: PointerEvent) { if (!pointers.has(e.pointerId) || !drag)
    return; const before = [...pointers.values()]; pointers.set(e.pointerId, { x: e.offsetX, y: e.offsetY }); const after = [...pointers.values()]; if (before.length === 2) {
    const a = before[0]!, b = before[1]!, c = after[0]!, d = after[1]!;
    scale(Math.hypot(c.x - d.x, c.y - d.y) / Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)), (c.x + d.x) / 2, (c.y + d.y) / 2);
    drag.moved = true;
}
else {
    offset.value = { x: offset.value.x + e.offsetX - drag.x, y: offset.value.y + e.offsetY - drag.y };
    drag.moved ||= Math.hypot(e.offsetX - drag.startX, e.offsetY - drag.startY) > 4;
} drag.x = e.offsetX; drag.y = e.offsetY; }
function up(e: PointerEvent) { if (drag && !drag.moved) {
    const hit = screen.value.find(n => Math.hypot(n.x - e.offsetX, n.y - e.offsetY) < 12);
    emit('select', hit?.id || '');
} cancel(e); }
function cancel(e: PointerEvent) { pointers.delete(e.pointerId); const next = [...pointers.values()][0]; drag = next ? { ...next, startX: next.x, startY: next.y, moved: true } : null; }
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
watch(nodes, () => { resetCamera(); schedule(); });
watch([screen, width, height], schedule);
onMounted(() => { observer = new ResizeObserver(() => { width.value = container.value!.clientWidth; height.value = container.value!.clientHeight; resetCamera(); schedule(); }); observer.observe(container.value!); theme = new MutationObserver(schedule); theme.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] }); schedule(); });
onBeforeUnmount(() => { observer?.disconnect(); theme?.disconnect(); cancelAnimationFrame(frame); });
defineExpose({ resetCamera });
