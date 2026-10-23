import { onMounted, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import ForceGraph3D, { type ForceGraph3DInstance, type LinkObject } from '3d-force-graph'
import type { WorldGraphNode, WorldGraphEdge } from '@/utils/worldGraph'
import { getNodeCameraPosition } from '@/utils/worldGraph'
import { createNodeObject } from '@/utils/worldGraphSprite'

type GraphLink = Omit<WorldGraphEdge, 'source' | 'target'> & LinkObject<WorldGraphNode>
interface GraphControls { autoRotate: boolean; autoRotateSpeed: number; enableDamping: boolean; dampingFactor: number }

/** 组件只提供图数据和选中回调，3D 实例、尺寸监听与释放在此处管理。 */
export function useWorldGraph(container: Ref<HTMLElement | null>,
  data: Ref<{ nodes: WorldGraphNode[]; links: WorldGraphEdge[] }>, onSelect: (id: string) => void) {
  const loading = ref(false)
  const autoRotate = ref(true)
  let graph: ForceGraph3DInstance<WorldGraphNode, GraphLink> | null = null

  function refresh() {
    // 图库会修改坐标与边的端点，传入副本避免污染 Store 派生数据。
    graph?.graphData({ nodes: data.value.nodes.map(node => ({ ...node })), links: data.value.links.map(link => ({ ...link })) })
  }
  function resize() {
    if (graph && container.value) graph.width(container.value.clientWidth).height(container.value.clientHeight)
  }
  function toggleRotate() {
    autoRotate.value = !autoRotate.value
    if (graph) (graph.controls() as GraphControls).autoRotate = autoRotate.value
  }
  const resetCamera = () => graph?.cameraPosition({ x: 0, y: 0, z: 400 }, { x: 0, y: 0, z: 0 }, 1000)

  onMounted(() => {
    if (!container.value) return
    loading.value = true
    try {
      graph = new ForceGraph3D(container.value) as unknown as ForceGraph3DInstance<WorldGraphNode, GraphLink>
      graph.width(container.value.clientWidth).height(container.value.clientHeight)
        .backgroundColor('#fafafa').graphData({ nodes: [], links: [] })
        .nodeThreeObject(createNodeObject).nodeThreeObjectExtend(false)
        .linkColor(link => link.color).linkWidth(link => link.width).linkOpacity(0.6)
        .linkDirectionalArrowLength(link => link.isEmbed ? 0 : 3.5)
        .linkDirectionalArrowRelPos(1).linkDirectionalArrowColor(link => link.color)
        .linkLabel(link => link.label || '')
        .onNodeClick(node => {
          onSelect(node.id)
          const { position, target } = getNodeCameraPosition(node)
          graph?.cameraPosition(position, target, 1000)
        })
        .onBackgroundClick(() => onSelect(''))
        .d3AlphaDecay(0.02).d3VelocityDecay(0.3).warmupTicks(80).cooldownTicks(120)
      Object.assign(graph.controls(), { autoRotate: autoRotate.value, autoRotateSpeed: 0.5, enableDamping: true, dampingFactor: 0.1 })
      refresh()
      window.addEventListener('resize', resize)
    } finally { loading.value = false }
  })
  watch(data, refresh, { deep: true })
  onBeforeUnmount(() => {
    window.removeEventListener('resize', resize)
    try { graph?._destructor() }
    catch (error) { console.warn('释放世界观图谱失败:', error) }
    finally { graph = null }
  })
  return { loading, autoRotate, refresh, toggleRotate, resetCamera }
}
