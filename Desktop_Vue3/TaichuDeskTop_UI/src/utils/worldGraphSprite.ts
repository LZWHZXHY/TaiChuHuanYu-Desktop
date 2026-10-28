import { Vector3, type Camera, type Scene } from 'three'
import { placeGraphLabels, type WorldGraphNode } from './worldGraph'

/** 屏幕字号独立于 3D 缩放，只为当前可见且不重叠的名称保留 DOM。 */
export function createGraphLabels(onSelect: (id: string) => void) {
  const domElement = document.createElement('div')
  domElement.className = 'world-graph-labels'
  Object.assign(domElement.style, { position: 'absolute', inset: '0', pointerEvents: 'none', overflow: 'hidden' })
  const measure = document.createElement('canvas').getContext('2d')!
  measure.font = '600 14px sans-serif'
  const entries = new Map<string, { node: WorldGraphNode; width: number }>()
  const buttons = new Map<string, HTMLButtonElement>()
  const position = new Vector3()
  let width = 1, height = 1, activeId = ''

  return {
    domElement,
    setSize(w: number, h: number) { width = w; height = h },
    setActive(id: string) { activeId = id },
    setNodes(nodes: WorldGraphNode[]) {
      const ids = new Set(nodes.map(node => node.id))
      for (const id of entries.keys()) if (!ids.has(id)) entries.delete(id)
      for (const node of nodes) entries.set(node.id, { node, width: Math.min(176, Math.max(72, measure.measureText(node.title).width + 24)) })
    },
    render(_scene: Scene, camera: Camera) {
      const focusedId = [...buttons].find(([, button]) => button === document.activeElement)?.[0]
      const points = []
      for (const [id, entry] of entries) {
        const node = entry.node
        position.set(node.x || 0, node.y || 0, node.z || 0).project(camera)
        if (position.z < -1 || position.z > 1) continue
        points.push({ id, x: (position.x + 1) * width / 2, y: (1 - position.y) * height / 2,
          z: position.z, width: entry.width, priority: id === focusedId || id === activeId })
      }
      const visible = placeGraphLabels(points, width, height)
      const ids = new Set(visible.map(point => point.id))
      for (const [id, button] of buttons) if (!ids.has(id)) { button.remove(); buttons.delete(id) }
      for (const point of visible) {
        const node = entries.get(point.id)!.node
        let button = buttons.get(point.id)
        if (!button) {
          button = document.createElement('button')
          button.type = 'button'; button.className = 'world-graph-label'
          button.addEventListener('pointerdown', event => event.stopPropagation())
          button.addEventListener('click', event => { event.stopPropagation(); onSelect(point.id) })
          buttons.set(point.id, button); domElement.appendChild(button)
        }
        if (button.textContent !== node.title) { button.textContent = node.title; button.title = node.title }
        button.style.width = `${point.width}px`
        button.style.borderColor = node.color
        button.style.transform = `translate(${point.left}px, ${point.top}px)`
      }
    },
    dispose() { domElement.remove(); buttons.clear(); entries.clear() },
  }
}
