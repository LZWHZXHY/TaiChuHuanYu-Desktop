import { onMounted, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import ForceGraph3D, { type ForceGraph3DInstance, type LinkObject } from '3d-force-graph'
import type { WorldGraphNode, WorldGraphEdge } from '@/utils/worldGraph'
import { getNodeCameraPosition, getGraphCameraPosition } from '@/utils/worldGraph'
import { createGraphLabels } from '@/utils/worldGraphSprite'

type GraphLink = Omit<WorldGraphEdge, 'source' | 'target'> & LinkObject<WorldGraphNode>
interface GraphControls {
  autoRotate: boolean; autoRotateSpeed: number; enableDamping: boolean; dampingFactor: number
  addEventListener(event: 'start', listener: () => void): void
}

/** 组件只提供图数据和选中回调，3D 实例、尺寸监听与释放在此处管理。 */
export function useWorldGraph(container: Ref<HTMLElement | null>,
  data: Ref<{ nodes: WorldGraphNode[]; links: WorldGraphEdge[] }>, onSelect: (id: string) => void) {
  const loading = ref(false)
  const autoRotate = ref(false)
  let graph: ForceGraph3DInstance<WorldGraphNode, GraphLink> | null = null
  let resizeObserver: ResizeObserver | undefined
  let themeObserver: MutationObserver | undefined
  let needsInitialFit = true
  let firstFrame = true
  let refreshTimer: ReturnType<typeof setTimeout> | undefined
  let signature = ''
  let selectedId = ''
  const labels = createGraphLabels(selectNode)

  function selectNode(id: string) {
    selectedId = id; labels.setActive(id); needsInitialFit = false
    const node = graph?.graphData().nodes.find(node => node.id === id)
    onSelect(id)
    if (node) {
      const { position, target } = getNodeCameraPosition(node)
      graph?.cameraPosition(position, target, 500)
    }
  }

  function syncTheme() {
    if (container.value) graph?.backgroundColor(getComputedStyle(container.value).getPropertyValue('--bg-main').trim())
  }

  function refresh() {
    if (!graph) return
    const next = JSON.stringify(data.value)
    if (next === signature) return
    signature = next
    // 保留图库拥有的坐标和速度，详情陆续载入时不把已有节点重新打散。
    const existing = new Map(graph.graphData().nodes.map(node => [node.id, node]))
    const nodes = data.value.nodes.map(node => Object.assign(existing.get(node.id) || {}, node))
    labels.setNodes(nodes)
    graph.graphData({ nodes, links: data.value.links.map(link => ({ ...link })) })
    // 密集全景使用细线；聚焦后恢复粗细和方向，减少数千个圆柱/箭头的绘制。
    const dense = nodes.length > 250
    graph.linkWidth(link => dense ? 0 : link.width)
      .linkDirectionalArrowLength(link => dense || link.isEmbed ? 0 : 3.5)
    const changedNodes = nodes.length !== existing.size || nodes.some(node => !existing.has(node.id))
    // 筛选/聚焦会重新计算中心，等首帧新坐标产生后再取景，避免结果落在镜头外。
    if (changedNodes) { firstFrame = true; needsInitialFit = true }
  }
  function resize() {
    if (graph && container.value) graph.width(container.value.clientWidth).height(container.value.clientHeight)
  }
  function toggleRotate() {
    autoRotate.value = !autoRotate.value
    if (graph) (graph.controls() as GraphControls).autoRotate = autoRotate.value
  }
  function resetCamera() {
    if (!graph || !container.value) return
    const { position, target } = getGraphCameraPosition(graph.graphData().nodes,
      container.value.clientWidth / Math.max(container.value.clientHeight, 1), 50)
    graph.cameraPosition(position, target, 500)
  }

  onMounted(() => {
    if (!container.value) return
    loading.value = true
    try {
      graph = new ForceGraph3D(container.value, { extraRenderers: [labels] }) as unknown as ForceGraph3DInstance<WorldGraphNode, GraphLink>
      graph.width(container.value.clientWidth).height(container.value.clientHeight)
        .graphData({ nodes: [], links: [] })
        .nodeColor(node => node.color).nodeRelSize(5).nodeResolution(6).showNavInfo(false)
        .nodeLabel(node => { const label = document.createElement('span'); label.textContent = node.title; return label })
        .linkColor(link => link.color).linkOpacity(0.6).linkResolution(3).linkDirectionalArrowResolution(3)
        .linkDirectionalArrowRelPos(1).linkDirectionalArrowColor(link => link.color)
        .linkLabel(link => {
          const label = document.createElement('span')
          label.textContent = link.label
          return label
        })
        .onNodeClick(node => selectNode(node.id))
        .onNodeHover(node => labels.setActive(node?.id || selectedId))
        .onBackgroundClick(() => selectNode(''))
        .onEngineTick(() => {
          if (firstFrame && data.value.nodes.length) { firstFrame = false; resetCamera() }
        })
        .onEngineStop(() => {
          if (needsInitialFit && data.value.nodes.length) { needsInitialFit = false; resetCamera() }
        })
        .d3AlphaDecay(0.04).d3VelocityDecay(0.3).warmupTicks(0).cooldownTicks(120)
      graph.d3Force('charge')?.strength(-100)
      graph.d3Force('link')?.distance(90)
      graph.renderer().setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
      Object.assign(graph.controls(), { autoRotate: autoRotate.value, autoRotateSpeed: 0.5, enableDamping: true, dampingFactor: 0.1 })
      ;(graph.controls() as GraphControls).addEventListener('start', () => { needsInitialFit = false })
      refresh()
      syncTheme()
      resizeObserver = new ResizeObserver(resize)
      resizeObserver.observe(container.value)
      themeObserver = new MutationObserver(syncTheme)
      themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
    } finally { loading.value = false }
  })
  watch(data, () => { clearTimeout(refreshTimer); refreshTimer = setTimeout(refresh, 80) })
  onBeforeUnmount(() => {
    resizeObserver?.disconnect()
    themeObserver?.disconnect()
    clearTimeout(refreshTimer)
    try { graph?._destructor() }
    catch (error) { console.warn('释放世界观图谱失败:', error) }
    finally { graph = null; labels.dispose() }
  })
  return { loading, autoRotate, refresh, toggleRotate, resetCamera }
}
