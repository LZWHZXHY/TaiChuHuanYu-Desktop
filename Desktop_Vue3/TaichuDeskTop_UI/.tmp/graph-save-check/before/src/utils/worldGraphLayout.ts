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
  const positions = new Map<string, {
    x: number;
    y: number;
  }>();
  const cellWidth = 190, cellHeight = 84, gap = 28;
  const rowWidth = Math.max(cellWidth * 3, Math.sqrt(nodes.length * cellWidth * cellHeight) * 1.4);
  let left = 0, top = 0, rowHeight = 0;
  for (const group of groups) {
    const columns = Math.min(group.length, Math.max(1, Math.ceil(Math.sqrt(group.length / 2))));
    const rows = Math.ceil(group.length / columns);
    const groupWidth = columns * cellWidth, groupHeight = rows * cellHeight;
    if (left && left + groupWidth > rowWidth) { left = 0; top += rowHeight + gap; rowHeight = 0; }
    group.forEach((id, i) => positions.set(id, { x: left + (i % columns) * cellWidth + cellWidth / 2, y: top + Math.floor(i / columns) * cellHeight + cellHeight / 2 }));
    left += groupWidth + gap;
    rowHeight = Math.max(rowHeight, groupHeight);
  }
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
