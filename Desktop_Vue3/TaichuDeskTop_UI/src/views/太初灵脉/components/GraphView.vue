<!-- 太初灵脉 · 关系图：笔记之间的引用（实线箭头）+ 笔记所在的文件夹（虚线）。点节点打开那篇。 -->
<template>
  <div class="spirit-graph-container">
    <header class="graph-header">
      <div class="header-left">
        <h2>关系图 <span class="sub">笔记之间的引用关系</span></h2>
        <div class="view-selector">
          <button :class="{ active: viewMode === 'current' }" @click="switchViewMode('current')">本空间</button>
          <button :class="{ active: viewMode === 'all' }" @click="switchViewMode('all')">所有空间</button>
          <label class="toggle"><input v-model="showFolders" type="checkbox" @change="rerender" /> 显示文件夹归属</label>
          <label class="toggle"><input v-model="hideIsolated" type="checkbox" @change="rerender" /> 隐藏没有连线的</label>
        </div>
      </div>
      <div class="header-right">
        <span v-if="stats" class="graph-stats">{{ stats.nodes }} 个节点 · {{ stats.links }} 条引用</span>
        <button class="close-btn" @click="$emit('close')">✕ 关闭</button>
      </div>
    </header>

    <div class="graph-canvas-wrapper">
      <div class="graph-canvas" ref="canvasRef"></div>
      <div v-if="!loading && emptyHint" class="graph-empty">
        <p>{{ emptyHint }}</p>
        <small>在笔记正文里输入 [[ 选一篇笔记引用它，保存后这里就会连上线。</small>
      </div>
      <ul v-if="legend.length" class="graph-legend" aria-label="图例">
        <li v-for="item in legend" :key="item.type"><i :class="['lg-dot', item.shape]" :style="{ background: item.color }"></i>{{ item.label }}</li>
      </ul>
    </div>

    <div v-if="loading" class="graph-loading-overlay">
      <div class="spirit-spinner"></div>
      <p>正在生成关系图…</p>
    </div>
    <p v-if="loadError" class="graph-error" role="alert">{{ loadError }} <button @click="fetchGraphData">重试</button></p>

    <div v-if="hoveredNode" class="node-hover-card" :style="hoverCardStyle">
      <div class="hover-title">{{ hoveredNode.title }}</div>
      <div class="hover-tag">{{ typeLabel(hoveredNode.type) }} · 被引用 {{ hoveredNode.inbound }} · 引用别人 {{ hoveredNode.outbound }}</div>
      <div v-if="hoveredNode.type !== 'folder'" class="hover-hint">点击打开</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick, onUnmounted } from 'vue';
import { Network } from 'vis-network';
import { DataSet } from 'vis-data';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';
import { NOTE_TYPE_CONFIG, type NoteType } from '../../../utils/NoteType';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'select-note', noteId: string): void;
}>();

const { currentSpaceId } = useSpiritData();

interface GraphNode { id: string; title: string; type: string; folderId?: string | null }
interface GraphLink { source?: string; target?: string; from?: string; to?: string }

const canvasRef = ref<HTMLElement | null>(null);
const loading = ref(false);
const loadError = ref('');
const viewMode = ref<'current' | 'all'>('current');
const showFolders = ref(true);
const hideIsolated = ref(false);
const hoveredNode = ref<{ title: string; type: string; inbound: number; outbound: number } | null>(null);
const mousePos = ref({ x: 0, y: 0 });
const stats = ref<{ nodes: number; links: number } | null>(null);
const emptyHint = ref('');
const legend = ref<{ type: string; label: string; color: string; shape: string }[]>([]);
let raw: { nodes: GraphNode[]; links: GraphLink[] } = { nodes: [], links: [] };
let networkInstance: Network | null = null;
let version = 0;

const hoverCardStyle = computed(() => ({ top: `${mousePos.value.y + 15}px`, left: `${mousePos.value.x + 15}px` }));
const typeLabel = (t: string) => NOTE_TYPE_CONFIG[t as NoteType]?.label ?? '笔记';

/** 从网站主题里取颜色（深色模式也看得清） */
function cssVar(name: string, fallback: string) {
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return v || fallback;
}
/** 每种笔记一个颜色和形状 */
const TYPE_STYLE: Record<string, { color: string; shape: string }> = {
  folder: { color: '#d98a1c', shape: 'square' },
  note: { color: '#2f7fd1', shape: 'dot' },
  blog: { color: '#8a5cd6', shape: 'dot' },
  post: { color: '#2aa38c', shape: 'dot' },
  wiki: { color: '#c2477a', shape: 'diamond' },
  doc: { color: '#5b7083', shape: 'dot' },
  canvas: { color: '#c9a227', shape: 'star' },
  schedule: { color: '#6b8e23', shape: 'triangle' },
};
const styleOf = (t: string) => TYPE_STYLE[t] ?? TYPE_STYLE.note!;

async function fetchGraphData() {
  if (!canvasRef.value) return;
  const v = ++version;
  loading.value = true; loadError.value = '';
  try {
    const res: any = await lingmaiApi.getGraphData(currentSpaceId.value, viewMode.value);
    if (v !== version) return;
    raw = { nodes: res?.nodes ?? [], links: res?.links ?? [] };
    await nextTick();
    renderGraph();
  } catch (error) {
    if (v === version) loadError.value = '关系图加载失败，请稍后重试。';
    console.error('关系图加载失败:', error);
  } finally {
    if (v === version) loading.value = false;
  }
}

function rerender() { if (raw.nodes.length) renderGraph(); }

function renderGraph() {
  if (!canvasRef.value) return;
  const textColor = cssVar('--text-primary', '#1d1d1f');
  const lineColor = cssVar('--text-secondary', '#8e8e93');
  const accent = cssVar('--accent-color', '#34c759');
  const paper = cssVar('--paper-card', '#ffffff');

  const ids = new Set(raw.nodes.map(n => String(n.id).toLowerCase()));
  // 引用连线：去重、去掉自己连自己和连到不在图里的
  const seen = new Set<string>();
  const refEdges: { from: string; to: string }[] = [];
  for (const l of raw.links) {
    const from = String(l.from ?? l.source ?? '').toLowerCase(), to = String(l.to ?? l.target ?? '').toLowerCase();
    const key = `${from}>${to}`;
    if (!from || !to || from === to || !ids.has(from) || !ids.has(to) || seen.has(key)) continue;
    seen.add(key); refEdges.push({ from, to });
  }
  // 文件夹归属（虚线）
  const folderEdges = showFolders.value
    ? raw.nodes.filter(n => n.folderId && ids.has(String(n.folderId).toLowerCase()))
      .map(n => ({ from: String(n.folderId).toLowerCase(), to: String(n.id).toLowerCase() }))
    : [];

  const inbound = new Map<string, number>(), outbound = new Map<string, number>();
  for (const e of refEdges) { inbound.set(e.to, (inbound.get(e.to) ?? 0) + 1); outbound.set(e.from, (outbound.get(e.from) ?? 0) + 1); }
  const linked = new Set<string>([...refEdges, ...folderEdges].flatMap(e => [e.from, e.to]));
  const visible = raw.nodes.filter(n => !hideIsolated.value || linked.has(String(n.id).toLowerCase()));

  const nodes = new DataSet(visible.map(n => {
    const id = String(n.id).toLowerCase();
    const st = styleOf(n.type);
    const degree = (inbound.get(id) ?? 0) + (outbound.get(id) ?? 0);
    return {
      id,
      label: n.title || (n.type === 'folder' ? '新文件夹' : '无标题'),
      shape: st.shape,
      // 被引用越多越大
      size: n.type === 'folder' ? 16 : Math.min(28, 10 + degree * 2),
      color: { background: st.color, border: st.color, highlight: { background: accent, border: accent }, hover: { background: accent, border: accent } },
      font: { color: textColor, size: 12, face: 'system-ui', strokeWidth: 3, strokeColor: paper },
      raw: { title: n.title, type: n.type, inbound: inbound.get(id) ?? 0, outbound: outbound.get(id) ?? 0 },
    };
  }));
  const edges = new DataSet([
    ...refEdges.map(e => ({ ...e, width: 1.5, color: { color: lineColor, highlight: accent, hover: accent, opacity: 0.8 }, arrows: { to: { enabled: true, scaleFactor: 0.5 } } })),
    ...folderEdges.map(e => ({ ...e, width: 1, dashes: [4, 4], color: { color: lineColor, opacity: 0.35 }, arrows: { to: { enabled: false } } })),
  ].map((e, i) => ({ id: `e${i}`, ...e })));

  stats.value = { nodes: visible.length, links: refEdges.length };
  emptyHint.value = !raw.nodes.length ? '这里还没有笔记。' : !refEdges.length ? '笔记之间还没有互相引用。' : '';
  const types = [...new Set(visible.map(n => n.type))];
  legend.value = types.map(t => ({ type: t, label: typeLabel(t), color: styleOf(t).color, shape: styleOf(t).shape }));

  networkInstance?.destroy();
  networkInstance = new Network(canvasRef.value, { nodes, edges } as any, {
    interaction: { hover: true, tooltipDelay: 100, zoomView: true, dragView: true },
    physics: {
      solver: 'forceAtlas2Based',
      forceAtlas2Based: { gravitationalConstant: -35, centralGravity: 0.01, springConstant: 0.06, springLength: 80, damping: 0.4 },
      stabilization: { iterations: 300 },
    },
  });
  // 排好位置后停下物理运算：不会一直抖，拖动节点也不会把整张图带跑
  networkInstance.once('stabilizationIterationsDone', () => {
    networkInstance?.setOptions({ physics: false });
    networkInstance?.fit({ animation: { duration: 300, easingFunction: 'easeInOutQuad' } });
  });
  networkInstance.on('click', (params: any) => {
    const id = params.nodes?.[0];
    if (!id) return;
    const node: any = nodes.get(id);
    // 文件夹以外的都能打开（随笔、博客、帖子、百科、白板、日程……）
    if (node && node.raw.type !== 'folder') { emit('select-note', id); emit('close'); }
  });
  networkInstance.on('hoverNode', (params: any) => { const node: any = nodes.get(params.node); hoveredNode.value = node?.raw ?? null; });
  networkInstance.on('blurNode', () => { hoveredNode.value = null; });
}

const updateMousePos = (e: MouseEvent) => { mousePos.value = { x: e.clientX, y: e.clientY }; };
const switchViewMode = (mode: 'current' | 'all') => { if (viewMode.value === mode) return; viewMode.value = mode; void fetchGraphData(); };

onMounted(() => {
  window.addEventListener('mousemove', updateMousePos);
  void fetchGraphData();
});
onUnmounted(() => {
  version++;
  window.removeEventListener('mousemove', updateMousePos);
  networkInstance?.destroy();
  networkInstance = null;
});
watch(() => currentSpaceId.value, (newVal, oldVal) => {
  if (newVal !== oldVal && viewMode.value === 'current') void fetchGraphData();
});
</script>

<style scoped>
.spirit-graph-container { position: fixed; inset: 0; background: var(--paper-card); z-index: 9998; display: flex; flex-direction: column; user-select: none; }
.graph-header { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-start; gap: 10px; padding: 16px 24px; background: var(--paper-card); border-bottom: 1px solid var(--border-line); flex-shrink: 0; }
.header-left h2 { font-size: 16px; font-weight: 600; color: var(--text-primary); margin: 0; display: flex; align-items: center; gap: 8px; }
.header-left .sub { font-size: 11px; color: var(--text-secondary); font-weight: 400; }
.view-selector { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 10px; }
.view-selector button { background: var(--bg-sub); border: none; font-size: 11px; font-weight: 600; color: var(--text-primary); padding: 4px 12px; border-radius: 0; cursor: pointer; transition: all 0.2s; }
.view-selector button.active { background: var(--accent-color); color: var(--on-accent); }
.toggle { display: inline-flex; align-items: center; gap: 4px; margin-left: 6px; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
.header-right { display: flex; align-items: center; gap: 12px; }
.graph-stats { font-size: 12px; color: var(--text-secondary); }
.close-btn { background: none; border: 1px solid var(--border-line); padding: 6px 14px; border-radius: 0; font-size: 12px; font-weight: 500; color: var(--text-primary); cursor: pointer; transition: all 0.2s; }
.close-btn:hover { background: var(--bg-sub); }
.graph-canvas-wrapper { flex: 1; width: 100%; position: relative; background: var(--bg-main); }
.graph-canvas { position: absolute; inset: 0; }
.graph-empty { position: absolute; left: 50%; top: 18px; transform: translateX(-50%); display: grid; gap: 4px; padding: 10px 16px; text-align: center; background: var(--paper-card); border: 1px solid var(--border-line); pointer-events: none; }
.graph-empty p { margin: 0; font-size: 13px; color: var(--text-primary); }
.graph-empty small { font-size: 12px; color: var(--text-secondary); }
.graph-legend { position: absolute; left: 16px; bottom: 16px; list-style: none; margin: 0; padding: 8px 12px; display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); background: var(--paper-card); border: 1px solid var(--border-line); }
.graph-legend li { display: flex; align-items: center; gap: 6px; }
.lg-dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; }
.lg-dot.square { border-radius: 1px; }
.lg-dot.diamond { border-radius: 1px; transform: rotate(45deg) scale(.85); }
.graph-loading-overlay { position: absolute; inset: 0; background: var(--paper-card); display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 14px; color: var(--text-secondary); }
.graph-error { position: absolute; left: 50%; bottom: 24px; transform: translateX(-50%); margin: 0; padding: 8px 14px; font-size: 13px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-primary); }
.graph-error button { margin-left: 8px; }
.spirit-spinner { width: 32px; height: 32px; border: 3px solid var(--border-line); border-top-color: var(--accent-color); border-radius: 50%; animation: spin 1s linear infinite; }
.node-hover-card { position: fixed; background: var(--paper-card); border: 1px solid var(--border-line); padding: 10px 14px; border-radius: 0; box-shadow: 0 8px 24px rgba(0,0,0,0.08); pointer-events: none; z-index: 9999; }
.hover-title { font-size: 13px; font-weight: 600; color: var(--text-primary); margin-bottom: 2px; }
.hover-tag, .hover-hint { font-size: 11px; color: var(--text-secondary); }
.hover-hint { margin-top: 2px; color: var(--accent-color); }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>
