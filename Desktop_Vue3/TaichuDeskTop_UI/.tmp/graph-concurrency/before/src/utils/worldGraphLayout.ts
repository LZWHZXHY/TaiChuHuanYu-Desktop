import type { WorldGraphNode, WorldGraphEdge } from './worldGraph';
/** 连通分组只计算一次；稳定布局，不在每一帧执行力模拟。 */
export function layoutWorldGraph(nodes: WorldGraphNode[], links: WorldGraphEdge[]) {
  const neighbors = new Map(nodes.map(node => [node.id, new Set<string>()]));
  for (const link of links) {
    neighbors.get(link.source)?.add(link.target);
    neighbors.get(link.target)?.add(link.source);
  }
  const ordered = [...nodes].sort((a, b) => neighbors.get(b.id)!.size - neighbors.get(a.id)!.size || a.id.localeCompare(b.id));
  const visited = new Set<string>(), groups: string[][] = [];
  for (const node of ordered) {
    if (visited.has(node.id))
      continue;
    const group = [node.id];
    visited.add(node.id);
    for (let i = 0; i < group.length; i++)
      for (const id of neighbors.get(group[i]!) || []) {
        if (!visited.has(id) && neighbors.has(id)) {
          visited.add(id);
          group.push(id);
        }
      }
    groups.push(group);
  }
  // 连通条目连续排列；分组不再各占一个矩形，避免小组之间留下整行空洞。
  const order = groups.flat()
  const columns = Math.max(1, Math.ceil(Math.sqrt(nodes.length * 84 / 190 * 2.2)))
  const positions = new Map(order.map((id, index) => {
    const row = Math.floor(index / columns), column = index % columns
    return [id, { x: (row % 2 ? columns - 1 - column : column) * 190, y: row * 84 }]
  }))
  return nodes.map(node => ({ ...node, ...positions.get(node.id)! }));
}

/** 坐标必须是最终显示的世界坐标（包含用户拖拽），边距按屏幕像素计算。 */
export function fitWorldGraph(points: { x: number; y: number }[], width: number, height: number) {
  const valid = points.filter(p => Number.isFinite(p.x) && Number.isFinite(p.y));
  if (!valid.length || width <= 0 || height <= 0) return null;
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  for (const p of valid) { minX = Math.min(minX,p.x); maxX = Math.max(maxX,p.x); minY = Math.min(minY,p.y); maxY = Math.max(maxY,p.y); }
  const zoom = Math.min(1, Math.max(.0001, Math.min(Math.max(1,width - 200) / Math.max(1,maxX - minX), Math.max(1,height - 100) / Math.max(1,maxY - minY))));
  return { zoom, offset: { x: width / 2 - (minX + maxX) / 2 * zoom, y: height / 2 - (minY + maxY) / 2 * zoom } };
}

export function parseGraphPositions(json: string | null | undefined): Record<string, { x: number; y: number }> {
  try {
    const value = JSON.parse(json || '{}')
    if (!value || Array.isArray(value) || typeof value !== 'object') return {}
    return Object.fromEntries(Object.entries(value).filter((entry): entry is [string, { x: number; y: number }] => {
      const p = entry[1] as { x?: number; y?: number } | null
      return Boolean(p && Number.isFinite(p.x) && Number.isFinite(p.y))
    }))
  } catch { return {} }
}
