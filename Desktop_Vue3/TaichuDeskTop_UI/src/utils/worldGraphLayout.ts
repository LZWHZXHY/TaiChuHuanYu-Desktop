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
  const rowWidth = Math.max(700, Math.sqrt(nodes.length) * 110);
  let left = 0, top = 0, rowHeight = 0;
  for (const group of groups) {
    const radius = Math.max(70, Math.sqrt(group.length) * 36), size = radius * 2 + 90;
    if (left && left + size > rowWidth) {
      left = 0;
      top += rowHeight;
      rowHeight = 0;
    }
    group.forEach((id, i) => {
      const r = i ? Math.sqrt(i) * 36 : 0, angle = i * 2.399963229728653;
      positions.set(id, { x: left + radius + r * Math.cos(angle), y: top + radius + r * Math.sin(angle) });
    });
    left += size;
    rowHeight = Math.max(rowHeight, size);
  }
  return nodes.map(node => ({ ...node, ...positions.get(node.id)! }));
}
