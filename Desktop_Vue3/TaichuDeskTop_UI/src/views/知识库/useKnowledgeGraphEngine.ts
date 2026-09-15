import { shallowRef } from 'vue'
import type { Ref } from 'vue'
import * as THREE_NS from 'three'
import ForceGraph3D from '3d-force-graph'
import SpriteText from 'three-spritetext'
import type { GraphNode, GraphLink, GraphDataResponse } from './types'
import { useKnowledgeGraphData } from './useKnowledgeGraphData'
import request from '@/utils/request'

const THREE: any = THREE_NS

export interface EngineEmits {
  onNodeClick: (node: GraphNode) => void
}

export function useKnowledgeGraphEngine(containerRef: Ref<HTMLElement | null>, emits: EngineEmits) {
  const graph = shallowRef<any>(null)
  let animationFrame = 0
  const { getTheme, getNodeSize, hexToRgba, escapeHtml } = useKnowledgeGraphData()

  const createNodeObject = (node: GraphNode, showLabelsInitial: boolean) => {
    const theme = getTheme(node.type)
    const group = new THREE.Group()
    const radius = Math.max(2.6, node.val * 0.48)

    const coreGeometry = new THREE.SphereGeometry(radius, 24, 24)
    const coreMaterial = new THREE.MeshStandardMaterial({
      color: theme.color, emissive: theme.emissive, emissiveIntensity: 1.8,
      roughness: 0.25, metalness: 0.28, transparent: true, opacity: 0.95
    })
    const core = new THREE.Mesh(coreGeometry, coreMaterial)

    const glowGeometry = new THREE.SphereGeometry(radius * 1.55, 20, 20)
    const glowMaterial = new THREE.MeshBasicMaterial({
      color: theme.color, transparent: true, opacity: 0.12,
      depthWrite: false, blending: THREE.AdditiveBlending
    })
    const glow = new THREE.Mesh(glowGeometry, glowMaterial)

    const ringGeometry = new THREE.TorusGeometry(radius * 1.22, Math.max(0.2, radius * 0.055), 8, 32)
    const ringMaterial = new THREE.MeshBasicMaterial({
      color: theme.color, transparent: true, opacity: 0.75,
      depthWrite: false, blending: THREE.AdditiveBlending
    })
    const ring = new THREE.Mesh(ringGeometry, ringMaterial)
    ring.rotation.x = Math.PI / 2

    const ring2Geometry = new THREE.TorusGeometry(radius * 1.45, Math.max(0.1, radius * 0.025), 6, 32)
    const ring2Material = new THREE.MeshBasicMaterial({
      color: theme.color, transparent: true, opacity: 0.28,
      depthWrite: false, blending: THREE.AdditiveBlending
    })
    const ring2 = new THREE.Mesh(ring2Geometry, ring2Material)
    ring2.rotation.x = Math.PI / 2
    ring2.rotation.z = Math.PI / 4

    const pointGeometry = new THREE.SphereGeometry(radius * 0.18, 12, 12)
    const pointMaterial = new THREE.MeshBasicMaterial({ color: '#ffffff', transparent: true, opacity: 0.9 })
    const point = new THREE.Mesh(pointGeometry, pointMaterial)

    const label: any = new (SpriteText as any)(node.name || '未知节点')
    label.color = theme.label
    label.textHeight = 4.2
    label.strokeWidth = 1.6
    label.strokeColor = 'rgba(2, 6, 23, 0.95)'
    label.backgroundColor = 'rgba(2, 6, 23, 0.76)'
    label.padding = 1.8
    label.borderRadius = 2
    label.visible = showLabelsInitial

    if (label.position) label.position.y = radius + 6
    if (label.material) {
      label.material.depthWrite = false
      label.material.depthTest = false
      label.material.transparent = true
    }
    label.renderOrder = 10

    const typeLabel: any = new (SpriteText as any)(theme.typeLabel)
    typeLabel.color = theme.color
    typeLabel.textHeight = 1.7
    typeLabel.strokeWidth = 1
    typeLabel.strokeColor = 'rgba(2, 6, 23, 0.9)'
    typeLabel.visible = showLabelsInitial

    if (typeLabel.position) typeLabel.position.y = radius + 2.6
    if (typeLabel.material) {
      typeLabel.material.depthWrite = false
      typeLabel.material.depthTest = false
      typeLabel.material.transparent = true
      typeLabel.material.opacity = 0.85
    }

    group.add(glow, ring2, ring, core, point, typeLabel, label)
    group.userData = { node, core, glow, ring, ring2, label, typeLabel }
    node.__threeObject = group
    return group
  }

  const flyCameraToNode = (node: GraphNode, lookAtNode = true, duration = 1200) => {
    if (!graph.value || node.x === undefined || node.y === undefined || node.z === undefined) return
    const distance = Math.max(120, node.val * 14)
    const length = Math.sqrt(node.x * node.x + node.y * node.y + node.z * node.z) || 1
    const ratio = 1 + distance / length
    graph.value.cameraPosition(
      { x: node.x * ratio, y: node.y * ratio, z: node.z * ratio },
      lookAtNode ? node : undefined,
      duration
    )
  }

  const toggleLabelsVisibility = (show: boolean) => {
    if (!graph.value) return
    const { nodes } = graph.value.graphData()
    nodes.forEach((node: any) => {
      const object = node.__threeObject
      if (object && object.userData) {
        if (object.userData.label) object.userData.label.visible = show
        if (object.userData.typeLabel) object.userData.typeLabel.visible = show
      }
    })
  }

  // 🌟 核心修复：升级滑块控制逻辑，三维力场同步调节
  const updateRepulsion = (strength: number) => {
    if (!graph.value) return

    // 1. 调整全局排斥力
    const charge = graph.value.d3Force('charge')
    if (charge) {
      charge.strength(-strength) 
    }

    // 2. 调整连线距离
    const linkForce = graph.value.d3Force('link')
    if (linkForce) {
      const distance = 30 + (strength / 800) * 120
      linkForce.distance(distance)
    }

    // 3. 调整中心黑洞引力 (让散开的游离节点在斥力降低时被拉回中心)
    const centerForce = graph.value.d3Force('center')
    if (centerForce) {
      const centerPull = 0.2 - (strength / 800) * 0.15 
      centerForce.strength(centerPull)
    }

    // 重新加热引擎使其平滑过渡
    graph.value.d3AlphaDecay(0.02)
    graph.value.d3ReheatSimulation()
  }

  const animateNodes = () => {
    if (graph.value) {
      const scene = graph.value.scene?.()
      if (scene) {
        scene.traverse((object: any) => {
          if (!object.userData || !object.userData.node) return
          const { ring, ring2, glow, node } = object.userData
          if (ring) ring.rotation.z += 0.0025
          if (ring2) ring2.rotation.z -= 0.0015
          if (glow) {
            const phase = node.id.length * 0.5
            const scale = 1 + Math.sin(performance.now() * 0.002 + phase) * 0.025
            glow.scale.setScalar(scale)
          }
        })
      }
    }
    animationFrame = requestAnimationFrame(animateNodes)
  }

  const initEngine = (rawNodes: any[], rawEdges: any[], showLabelsInitial: boolean) => {
    if (!containerRef.value) return

    const graphNodes: GraphNode[] = rawNodes.map(n => ({
      id: n.id,
      name: n.title || '未知节点',
      type: n.type || 'default',
      val: getNodeSize(n.size),
      color: getTheme(n.type).color,
      coverImage: n.coverImage,
      description: '', // 🌟 已清除假数据，等待点击时动态拉取
      _nasPath: n._nasPath
    }))

    const graphLinks: GraphLink[] = rawEdges.map(e => ({
      source: e.source,
      target: e.target,
      name: e.relationType || ''
    }))

    graph.value = (ForceGraph3D as any)()(containerRef.value)
      .graphData({ nodes: graphNodes, links: graphLinks })
      .backgroundColor('#020617')
      .nodeThreeObject((node: GraphNode) => createNodeObject(node, showLabelsInitial))
      .nodeLabel((node: GraphNode) => `
        <div class="graph-node-tooltip">
          <div class="tooltip-title">${escapeHtml(node.name)}</div>
          <div class="tooltip-type">${escapeHtml(node.type)}</div>
        </div>
      `)
      .linkColor((link: GraphLink) => {
        const source = typeof link.source === 'object' ? link.source : null
        return source ? hexToRgba(getTheme(source.type).color, 0.27) : 'rgba(148, 163, 184, 0.22)'
      })
      .linkOpacity(0.72)
      .linkWidth(() => 0.7)
      .linkDirectionalParticles(2)
      .linkDirectionalParticleWidth(1.3)
      .linkDirectionalParticleSpeed(0.004)
      .linkLabel((link: GraphLink) => link.name ? `<div class="relation-tooltip">${escapeHtml(link.name)}</div>` : '')
      .onNodeHover((node: GraphNode | null) => {
        document.body.style.cursor = node ? 'pointer' : 'default'
        if (!node) return
        const object = node.__threeObject
        if (!object) return
        if (object.userData?.core) {
          object.userData.core.material.emissiveIntensity = 3.0
          object.userData.core.scale.setScalar(1.18)
        }
        if (object.userData?.glow) {
          object.userData.glow.material.opacity = 0.22
          object.userData.glow.scale.setScalar(1.18)
        }
      })
      .onNodeClick((node: GraphNode) => emits.onNodeClick(node))

    // 🌟 保留你提供的高聚拢默认参数
    const charge = graph.value.d3Force('charge')
    if (charge) charge.strength(-50)
    const linkForce = graph.value.d3Force('link')
    if (linkForce) {
      linkForce.distance(35)
      linkForce.strength(0.5)
    }
    const centerForce = graph.value.d3Force('center')
    if (centerForce) centerForce.strength(0.18)

    const renderer = graph.value.renderer?.()
    if (renderer) {
      if (renderer.setPixelRatio) renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
      if ('outputColorSpace' in renderer && THREE.SRGBColorSpace) renderer.outputColorSpace = THREE.SRGBColorSpace
      if ('toneMapping' in renderer) renderer.toneMapping = THREE.ACESFilmicToneMapping
      if ('toneMappingExposure' in renderer) renderer.toneMappingExposure = 1.15
    }

    const scene = graph.value.scene?.()
    if (scene) {
      scene.add(new THREE.AmbientLight('#ffffff', 0.8))
      const mainLight = new THREE.PointLight('#ffffff', 2.3, 1200)
      mainLight.position.set(0, 150, 250)
      scene.add(mainLight)
      const cyanLight = new THREE.PointLight('#38bdf8', 1.5, 900)
      cyanLight.position.set(-250, 80, -180)
      scene.add(cyanLight)
      const purpleLight = new THREE.PointLight('#a855f7', 1.3, 850)
      purpleLight.position.set(260, -100, 160)
      scene.add(purpleLight)
    }

    graph.value.cameraPosition({ x: 0, y: 0, z: 420 })
    animateNodes()
  }

  const handleResize = () => {
    if (!graph.value || !containerRef.value) return
    graph.value.width(containerRef.value.clientWidth)
    graph.value.height(containerRef.value.clientHeight)
  }

  const destroyEngine = () => {
    cancelAnimationFrame(animationFrame)
    if (graph.value) {
      try { graph.value._destructor() } 
      catch (error) { console.warn('3D Graph 销毁失败：', error) }
      graph.value = null
    }
  }
  const updateVisibility = (activeTypes: string[]) => {
    if (!graph.value) return
    
    // 如果数组为空，代表“不限制”，全部显示
    const isAllVisible = activeTypes.length === 0

    graph.value.nodeVisibility((node: GraphNode) => {
      if (isAllVisible) return true
      return activeTypes.includes(node.type)
    })

    graph.value.linkVisibility((link: GraphLink) => {
      if (isAllVisible) return true
      const sourceNode = typeof link.source === 'object' ? link.source : null
      const targetNode = typeof link.target === 'object' ? link.target : null
      
      // 只有当连线的两端节点都在被选中的类型中时，才显示该连线
      if (sourceNode && targetNode) {
        return activeTypes.includes(sourceNode.type) && activeTypes.includes(targetNode.type)
      }
      return false
    })
  }



  return {
    graph,
    initEngine,
    flyCameraToNode,
    toggleLabelsVisibility,
    handleResize,
    destroyEngine,
    updateRepulsion,
    updateVisibility // <--- 导出新方法
  }
}