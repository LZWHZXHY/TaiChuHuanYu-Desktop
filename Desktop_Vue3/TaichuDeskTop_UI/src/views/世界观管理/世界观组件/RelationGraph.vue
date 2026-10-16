<template>
  <div class="relation-graph-wrapper">
    <!-- 工具栏 -->
    <div class="graph-toolbar">
      <div class="toolbar-left">
        <span class="graph-title">关系图谱</span>
        <span class="graph-info">
          {{ displayedNodes.length }} 节点 · {{ displayedEdges.length }} 连接
        </span>
      </div>
      <div class="toolbar-right">
        <div class="legend">
          <span class="legend-item">
            <span class="legend-line solid"></span>
            <span>显式关系</span>
          </span>
          <span class="legend-item">
            <span class="legend-line dashed"></span>
            <span>嵌入引用</span>
          </span>
        </div>
        <button class="toolbar-btn" @click="toggleRotate">
          {{ autoRotate ? '暂停旋转' : '自动旋转' }}
        </button>
        <button class="toolbar-btn" @click="resetCamera">重置视角</button>
      </div>
    </div>

    <!-- 3D 容器 -->
    <div ref="graphContainer" class="graph-container" v-loading="graphLoading"></div>

    <!-- 空状态 -->
    <div v-if="!displayedNodes.length && !graphLoading" class="graph-empty">
      <p>暂无卡片</p>
    </div>

    <!-- 节点浮窗 -->
    <div v-if="selectedNode" class="node-popup">
      <div class="popup-header">
        <span
          class="popup-type"
          :style="{
            background: getTypeColor(selectedNode.type) + '20',
            color: getTypeColor(selectedNode.type),
          }"
        >
          {{ getTypeLabel(selectedNode.type) }}
        </span>
        <span class="popup-title">{{ selectedNode.title }}</span>
        <button class="popup-close" @click="selectedNode = null">×</button>
      </div>
      <div class="popup-body">
        <p v-if="getNodePreview(selectedNode.id)" class="popup-desc">
          {{ getNodePreview(selectedNode.id) }}
        </p>
        <div v-if="getNodeTags(selectedNode.id).length" class="popup-tags">
          <span v-for="tag in getNodeTags(selectedNode.id)" :key="tag" class="popup-tag">
            #{{ tag }}
          </span>
        </div>
        <div class="popup-actions">
          <button class="popup-btn" @click="openCardDetail(selectedNode.id)">
            查看详情
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue';
import { useRouter } from 'vue-router';
import ForceGraph3D from '3d-force-graph';
import * as THREE from 'three';
import { useWorldStore } from '@/stores/world';
import type { CardSummary, CardDetail } from '@/api/worldApi';

const props = defineProps<{ projectId: string }>();
const emit = defineEmits<{ (e: 'cardInserted'): void }>();

const store = useWorldStore();
const router = useRouter();

// ============================================================
//  类型元信息
// ============================================================
const TYPE_LABELS: Record<string, string> = {
  character: '角色', location: '地点', item: '物品',
  event: '事件', faction: '派系', species: '物种',
  occupation: '职业', organization: '组织', creature: '生物',
  skill: '技能', climate: '气候', concept: '设定',
};

const TYPE_COLORS: Record<string, string> = {
  character: '#4f46e5',
  location: '#059669',
  item: '#d97706',
  event: '#dc2626',
  faction: '#8b5cf6',
  species: '#f59e0b',
  occupation: '#f97316',
  organization: '#7c3aed',
  creature: '#84cc16',
  skill: '#06b6d4',
  climate: '#0ea5e9',
  concept: '#6366f1',
};

const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;
const getTypeColor = (type: string) => TYPE_COLORS[type] || '#64748b';

// ============================================================
//  状态
// ============================================================
const graphContainer = ref<HTMLElement | null>(null);
const graphLoading = ref(false);
const autoRotate = ref(true);
const selectedNode = ref<CardSummary | null>(null);

// 详情缓存（用于嵌入关系）
const detailMap = ref<Record<string, CardDetail>>({});

let Graph: any = null;

// ============================================================
//  数据
// ============================================================
const allCards = computed(() => store.cards);
const allRelations = computed(() => store.allRelations);

// ============================================================
//  节点：所有卡片
// ============================================================
const displayedNodes = computed(() => {
  return allCards.value.map(card => ({
    id: card.id,
    title: card.title,
    type: card.type,
    color: getTypeColor(card.type),
  }));
});

// ============================================================
//  边：显式关系（实线）+ 嵌入引用（虚线）
// ============================================================
const displayedEdges = computed(() => {
  const allCardIds = new Set(allCards.value.map(c => c.id));
  const result: any[] = [];

  // 1. 显式关系
  const relationKeys = new Set<string>();
  for (const rel of allRelations.value) {
    if (!allCardIds.has(rel.sourceCardId) || !allCardIds.has(rel.targetCardId)) continue;
    const key = `${rel.sourceCardId}__${rel.targetCardId}`;
    relationKeys.add(key);
    result.push({
      source: rel.sourceCardId,
      target: rel.targetCardId,
      label: rel.relationType,
      color: '#94a3b8',
      width: 1.5,
      isEmbed: false,
    });
  }

  // 2. 嵌入引用（用虚线效果：浅色 + 细线）
  for (const card of allCards.value) {
    const detail = detailMap.value[card.id] || store.getCardDetailById(card.id);
    if (!detail?.contentBlocks?.length) continue;

    for (const block of detail.contentBlocks) {
      const targetId = block.cardId;
      if (!targetId || targetId === card.id) continue;
      if (!allCardIds.has(targetId)) continue;

      const key = `${card.id}__${targetId}`;
      if (relationKeys.has(key)) continue;  // 显式关系优先

      result.push({
        source: card.id,
        target: targetId,
        label: '',
        color: '#cbd5e1',
        width: 0.8,
        isEmbed: true,
      });
    }
  }

  return result;
});

// ============================================================
//  节点 3D 对象（Sprite + 中文）
// ============================================================
const createNodeObject = (node: any) => {
  const scale = 4;
  const baseW = 160;
  const baseH = 48;

  const textLen = node.title.length;
  const width = Math.max(baseW, baseW + (textLen - 6) * 14);
  const height = baseH;

  const canvas = document.createElement('canvas');
  canvas.width = width * scale;
  canvas.height = height * scale;
  const ctx = canvas.getContext('2d')!;
  ctx.scale(scale, scale);

  // 圆角矩形
  const radius = 12;
  ctx.beginPath();
  ctx.moveTo(radius, 0);
  ctx.lineTo(width - radius, 0);
  ctx.quadraticCurveTo(width, 0, width, radius);
  ctx.lineTo(width, height - radius);
  ctx.quadraticCurveTo(width, height, width - radius, height);
  ctx.lineTo(radius, height);
  ctx.quadraticCurveTo(0, height, 0, height - radius);
  ctx.lineTo(0, radius);
  ctx.quadraticCurveTo(0, 0, radius, 0);
  ctx.closePath();

  // 渐变
  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, node.color);
  gradient.addColorStop(1, shadeColor(node.color, -15));
  ctx.fillStyle = gradient;
  ctx.fill();

  // 文字
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 16px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(node.title, width / 2, height / 2 + 1);

  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;

  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthTest: false,
  });
  const sprite = new THREE.Sprite(material);

  const worldHeight = 10;
  const worldWidth = worldHeight * (width / height);
  sprite.scale.set(worldWidth, worldHeight, 1);

  return sprite;
};

const shadeColor = (hex: string, percent: number): string => {
  const num = parseInt(hex.replace('#', ''), 16);
  const r = Math.max(0, Math.min(255, (num >> 16) + percent));
  const g = Math.max(0, Math.min(255, ((num >> 8) & 0x00ff) + percent));
  const b = Math.max(0, Math.min(255, (num & 0x0000ff) + percent));
  return '#' + ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0');
};

// ============================================================
//  初始化
// ============================================================
const initGraph = () => {
  if (!graphContainer.value) return;
  graphLoading.value = true;

  const container = graphContainer.value;
  const width = container.clientWidth;
  const height = container.clientHeight;

  Graph = new ForceGraph3D(container)
    .width(width)
    .height(height)
    .backgroundColor('#fafafa')
    .graphData({ nodes: [], links: [] })
    .nodeThreeObject(createNodeObject)
    .nodeThreeObjectExtend(false)
    .linkColor((link: any) => link.color)
    .linkWidth((link: any) => link.width)
    .linkOpacity(0.6)
    .linkDirectionalArrowLength((link: any) => link.isEmbed ? 0 : 3.5)
    .linkDirectionalArrowRelPos(1)
    .linkDirectionalArrowColor((link: any) => link.color)
    .onNodeClick((node: any) => {
      handleNodeClick(node);
    })
    .onBackgroundClick(() => {
      selectedNode.value = null;
    })
    .d3AlphaDecay(0.02)
    .d3VelocityDecay(0.3)
    .warmupTicks(80)
    .cooldownTicks(120);

  Graph.controls().autoRotate = autoRotate.value;
  Graph.controls().autoRotateSpeed = 0.5;
  Graph.controls().enableDamping = true;
  Graph.controls().dampingFactor = 0.1;

  updateGraphData();
  graphLoading.value = false;
};

const updateGraphData = () => {
  if (!Graph) return;
  Graph.graphData({
    nodes: displayedNodes.value.map(n => ({ ...n })),
    links: displayedEdges.value.map(e => ({ ...e })),
  });
};

// ============================================================
//  节点点击
// ============================================================
const handleNodeClick = (node: any) => {
  const card = allCards.value.find(c => c.id === node.id);
  if (!card) return;
  selectedNode.value = card;

  const distance = 100;
  const distRatio = 1 + distance / Math.hypot(node.x || 0, node.y || 0, node.z || 0);

  Graph.cameraPosition(
    {
      x: (node.x || 0) * distRatio,
      y: (node.y || 0) * distRatio,
      z: (node.z || 0) * distRatio,
    },
    node,
    1000
  );
};

// ============================================================
//  工具栏
// ============================================================
const toggleRotate = () => {
  autoRotate.value = !autoRotate.value;
  if (Graph) Graph.controls().autoRotate = autoRotate.value;
};

const resetCamera = () => {
  if (!Graph) return;
  Graph.cameraPosition({ x: 0, y: 0, z: 400 }, { x: 0, y: 0, z: 0 }, 1000);
};

// ============================================================
//  预览辅助
// ============================================================
const getNodePreview = (cardId: string): string => {
  const detail = store.getCardDetailById(cardId) || detailMap.value[cardId];
  if (!detail) return '';
  return detail.description || '';
};

const getNodeTags = (cardId: string): string[] => {
  const detail = store.getCardDetailById(cardId) || detailMap.value[cardId];
  if (!detail) return [];
  if (Array.isArray(detail.tags)) return detail.tags;
  try {
    return JSON.parse((detail.tags as any) || '[]');
  } catch {
    return [];
  }
};

// ============================================================
//  拉取所有卡片详情（用于嵌入关系）
// ============================================================
const loadAllDetails = async () => {
  const ids = allCards.value.map(c => c.id);
  const needFetch = ids.filter(id => !detailMap.value[id] && !store.getCardDetailById(id));
  if (!needFetch.length) return;

  // 分批拉取，避免并发过高
  const batchSize = 8;
  for (let i = 0; i < needFetch.length; i += batchSize) {
    const batch = needFetch.slice(i, i + batchSize);
    await Promise.all(
      batch.map(async (id) => {
        try {
          const cached = store.getCardDetailById(id);
          if (cached) {
            detailMap.value[id] = cached;
            return;
          }
          const detail = await store.fetchCardDetail(props.projectId, id);
          if (detail) detailMap.value[id] = detail;
        } catch (err) {
          console.warn(`加载卡片详情失败: ${id}`, err);
        }
      })
    );
  }
  // 详情加载完刷新
  updateGraphData();
};

// ============================================================
//  打开详情
// ============================================================
const openCardDetail = (cardId: string) => {
  selectedNode.value = null;
  router.push(`/world/project/${props.projectId}/card/${cardId}`);
};

// ============================================================
//  监听变化
// ============================================================
watch(displayedNodes, () => updateGraphData(), { deep: true });
watch(displayedEdges, () => updateGraphData(), { deep: true });

const handleResize = () => {
  if (!Graph || !graphContainer.value) return;
  Graph.width(graphContainer.value.clientWidth);
  Graph.height(graphContainer.value.clientHeight);
};

// ============================================================
//  生命周期
// ============================================================
onMounted(() => {
  nextTick(() => {
    initGraph();
    // 首屏渲染后异步拉详情，用于渲染嵌入边
    loadAllDetails();
  });
  window.addEventListener('resize', handleResize);
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize);
  if (Graph) {
    try { Graph._destructor(); } catch (e) { /* ignore */ }
    Graph = null;
  }
});

defineExpose({ refresh: () => updateGraphData() });
</script>

<style scoped>
/* ============================================================
   容器：全屏 3D
   ============================================================ */
.relation-graph-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 600px;
  background: #fafafa;
  overflow: hidden;
}

/* ============================================================
   工具栏（浮在图上）
   ============================================================ */
.graph-toolbar {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  z-index: 5;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  pointer-events: none;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
  pointer-events: auto;
}

.toolbar-left {
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(8px);
  padding: 8px 14px;
  border-radius: 8px;
  border: 1px solid #f0f0f0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.graph-title {
  font-size: 14px;
  font-weight: 600;
  color: #1a1a1a;
}
.graph-info {
  font-size: 12px;
  color: #999;
}

.legend {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: #666;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(8px);
  padding: 6px 12px;
  border-radius: 6px;
  border: 1px solid #f0f0f0;
}
.legend-item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.legend-line {
  display: inline-block;
  width: 18px;
  height: 0;
  border-top: 2px solid #94a3b8;
}
.legend-line.dashed {
  border-top: 2px dashed #cbd5e1;
}

.toolbar-btn {
  padding: 6px 14px;
  border: 1px solid #f0f0f0;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(8px);
  border-radius: 6px;
  font-size: 12px;
  color: #666;
  cursor: pointer;
  transition: all 0.15s;
  font-family: inherit;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}
.toolbar-btn:hover {
  background: #fff;
  border-color: #d0d0d0;
  color: #1a1a1a;
}

/* ============================================================
   3D 容器
   ============================================================ */
.graph-container {
  width: 100%;
  height: 100%;
  position: relative;
}
.graph-container :deep(canvas) {
  display: block;
}

.graph-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  color: #ccc;
  font-size: 14px;
}
.graph-empty p { margin: 0; }

/* ============================================================
   节点浮窗
   ============================================================ */
.node-popup {
  position: absolute;
  top: 80px;
  right: 16px;
  width: 280px;
  background: #fff;
  border-radius: 10px;
  border: 1px solid #f0f0f0;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  z-index: 10;
}
.popup-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  border-bottom: 1px solid #f5f5f5;
}
.popup-type {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  font-weight: 500;
  flex-shrink: 0;
}
.popup-title {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: #1a1a1a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.popup-close {
  background: none;
  border: none;
  font-size: 18px;
  color: #ccc;
  cursor: pointer;
  padding: 0 4px;
  line-height: 1;
}
.popup-close:hover { color: #666; }

.popup-body {
  padding: 12px 14px;
}
.popup-desc {
  font-size: 13px;
  line-height: 1.6;
  color: #666;
  margin: 0 0 10px;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.popup-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-bottom: 10px;
}
.popup-tag {
  font-size: 11px;
  color: #6366f1;
  background: #f0f2ff;
  padding: 1px 8px;
  border-radius: 3px;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
}
.popup-btn {
  padding: 6px 14px;
  background: #111;
  color: #fff;
  border: none;
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  font-family: inherit;
  transition: background 0.15s;
}
.popup-btn:hover { background: #333; }

/* ============================================================
   响应式
   ============================================================ */
@media (max-width: 640px) {
  .graph-toolbar {
    flex-direction: column;
    align-items: stretch;
  }
  .toolbar-right {
    flex-wrap: wrap;
  }
  .legend { display: none; }
  .node-popup {
    top: auto;
    bottom: 16px;
    left: 16px;
    right: 16px;
    width: auto;
  }
}
</style>