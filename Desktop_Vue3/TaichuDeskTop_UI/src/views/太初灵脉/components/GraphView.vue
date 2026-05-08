<template>
  <div class="spirit-graph-container">
    <header class="graph-header">
      <div class="header-left">
        <h2>太初灵脉星图 <span class="sub">Network Graph</span></h2>
        <div class="view-selector">
          <button 
            :class="{ active: viewMode === 'current' }" 
            @click="switchViewMode('current')"
          >
            本空间
          </button>
          <button 
            :class="{ active: viewMode === 'all' }" 
            @click="switchViewMode('all')"
          >
            跨空间全量
          </button>
        </div>
      </div>
      <button class="close-btn" @click="$emit('close')">✕ 关闭图谱</button>
    </header>

    <div class="graph-canvas-wrapper">
      <div class="graph-canvas" ref="canvasRef"></div>
    </div>

    <div v-if="loading" class="graph-loading-overlay">
      <div class="spirit-spinner"></div>
      <p>正在感应灵脉连线，编织网状星图...</p>
    </div>

    <div v-if="hoveredNode" class="node-hover-card" :style="hoverCardStyle">
      <div class="hover-title">{{ hoveredNode.label }}</div>
      <div class="hover-tag">{{ hoveredNode.type === 'folder' ? '📁 文件夹' : '📄 灵感碎片' }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick, onUnmounted } from 'vue';
import { Network } from 'vis-network';
import { DataSet } from 'vis-data';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'select-note', noteId: string): void;
}>();

const { currentSpaceId } = useSpiritData();

const canvasRef = ref<HTMLElement | null>(null);
const loading = ref(false);
const viewMode = ref<'current' | 'all'>('current');
const hoveredNode = ref<any>(null);
const mousePos = ref({ x: 0, y: 0 });

let networkInstance: Network | null = null;

const hoverCardStyle = computed(() => ({
  top: `${mousePos.value.y + 15}px`,
  left: `${mousePos.value.x + 15}px`
}));

const fetchGraphData = async () => {
  // 如果容器还没渲染好，退出
  if (!canvasRef.value) return;

  loading.value = true;
  try {
    const scope = viewMode.value === 'all' ? 'all' : 'current';
    const res: any = await lingmaiApi.getGraphData(currentSpaceId.value, scope);
    
    // 🌟 改进 2：在数据拿到后，使用 nextTick 保证 DOM 宽高彻底计算完成才渲染
    nextTick(() => {
      renderGraph(res.nodes, res.links);
    });
  } catch (error) {
    console.error('星图加载失败:', error);
  } finally {
    loading.value = false;
  }
};

const renderGraph = (nodesData: any[], linksData: any[]) => {
  if (!canvasRef.value) return;

  // 1. 创建节点集合
  const nodes = new DataSet(
    (nodesData || []).map((n: any) => ({
      id: String(n.id).toLowerCase(),
      label: n.title || (n.type === 'folder' ? '新文件夹' : '无标题'),
      shape: n.type === 'folder' ? 'square' : 'dot',
      size: n.type === 'folder' ? 18 : 12,
      color: {
        background: n.type === 'folder' ? '#ff9500' : '#0066cc',
        border: n.type === 'folder' ? '#cc7600' : '#004499',
        highlight: { background: '#34c759', border: '#248a3d' },
        hover: { background: '#5856d6', border: '#3b3a9e' }
      },
      font: { color: '#1d1d1f', size: 12, face: 'system-ui' },
      type: n.type
    }))
  );

  // 2. 创建连线集合
  const edges = new DataSet(
    (linksData || []).map((l: any) => {
      const fromId = l.from || l.source;
      const toId = l.to || l.target;

      return {
        id: l.id,
        from: String(fromId).toLowerCase(),
        to: String(toId).toLowerCase(),
        width: 1.5,
        color: { 
          color: '#8e8e93', 
          highlight: '#34c759', 
          hover: '#5856d6' 
        },
        arrows: { 
          to: { enabled: true, scaleFactor: 0.5 } 
        }
      };
    })
  );

  const data = { nodes, edges };
  const options = {
    interaction: { 
      hover: true, 
      tooltipDelay: 100,
      zoomView: true,
      dragView: true
    },
    // 🌟 物理引擎：适当减小斥力，防止节点被弹得太远滑出画布
    physics: {
      solver: 'forceAtlas2Based',
      forceAtlas2Based: {
        gravitationalConstant: -35,
        centralGravity: 0.01,
        springConstant: 0.06,
        springLength: 80,
        damping: 0.4
      }
    }
  };

  if (networkInstance) {
    networkInstance.destroy();
  }

  networkInstance = new Network(canvasRef.value, data, options);

  // 🌟 核心修复 3：Vis.js 专属自适应居中适配
  networkInstance.once('stabilized', () => {
    networkInstance?.fit();
  });

  networkInstance.on('click', (params: any) => {
    if (params.nodes && params.nodes.length > 0) {
      const selectedNoteId = params.nodes[0];
      const node = nodes.get(selectedNoteId);
      if (node && (node as any).type === 'note') {
        emit('select-note', selectedNoteId);
        emit('close');
      }
    }
  });

  networkInstance.on('hoverNode', (params: any) => {
    const node = nodes.get(params.node);
    if (node) hoveredNode.value = node;
  });

  networkInstance.on('blurNode', () => {
    hoveredNode.value = null;
  });

  window.addEventListener('mousemove', updateMousePos);
};

const updateMousePos = (e: MouseEvent) => {
  mousePos.value = { x: e.clientX, y: e.clientY };
};

const switchViewMode = (mode: 'current' | 'all') => {
  viewMode.value = mode;
  fetchGraphData();
};

onMounted(() => {
  fetchGraphData();
});

onUnmounted(() => {
  window.removeEventListener('mousemove', updateMousePos);
  if (networkInstance) {
    networkInstance.destroy();
  }
});

// 🌟 改进 4：只有在图谱可见且空间真实发生改变时才重新载入
watch(() => currentSpaceId.value, (newVal, oldVal) => {
  if (newVal !== oldVal && viewMode.value === 'current') {
    fetchGraphData();
  }
});
</script>

<style scoped>
.spirit-graph-container {
  position: fixed;
  inset: 0;
  background: #fbfbfd;
  z-index: 9998;
  display: flex;
  flex-direction: column;
  user-select: none;
}

.graph-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  background: white;
  border-bottom: 1px solid #f2f2f7;
  flex-shrink: 0;
}

.header-left h2 { font-size: 16px; font-weight: 600; color: #1d1d1f; margin: 0; display: flex; align-items: center; gap: 8px; }
.header-left .sub { font-size: 11px; color: #86868b; font-weight: 400; }
.view-selector { display: flex; gap: 8px; margin-top: 10px; }
.view-selector button {
  background: #f5f5f7; border: none; font-size: 11px; font-weight: 600; color: #515154;
  padding: 4px 12px; border-radius: 12px; cursor: pointer; transition: all 0.2s;
}
.view-selector button.active { background: #0066cc; color: white; }

.close-btn {
  background: none; border: 1px solid #d2d2d7; padding: 6px 14px;
  border-radius: 20px; font-size: 12px; font-weight: 500; color: #1d1d1f; cursor: pointer;
  transition: all 0.2s;
}
.close-btn:hover { background: #f5f5f7; }

/* 🌟 核心改进 1：撑满中间，且强制其内部 canvas 容器占据 100% 物理尺寸 */
.graph-canvas-wrapper {
  flex: 1;
  width: 100%;
  position: relative;
  background: #fafafa;
}

.graph-canvas {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
}

.graph-loading-overlay {
  position: absolute; inset: 0; background: rgba(255,255,255,0.85);
  display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 14px;
}

.spirit-spinner {
  width: 32px; height: 32px; border: 3px solid #f2f2f7; border-top-color: #0066cc;
  border-radius: 50%; animation: spin 1s linear infinite;
}

.node-hover-card {
  position: fixed; background: white; border: 1px solid #f0f0f5; padding: 10px 14px;
  border-radius: 8px; box-shadow: 0 8px 24px rgba(0,0,0,0.08); pointer-events: none; z-index: 9999;
}

.hover-title { font-size: 13px; font-weight: 600; color: #1d1d1f; margin-bottom: 2px; }
.hover-tag { font-size: 11px; color: #86868b; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>