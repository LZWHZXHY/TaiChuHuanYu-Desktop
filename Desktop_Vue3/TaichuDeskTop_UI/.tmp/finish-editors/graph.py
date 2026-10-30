exec(open('.tmp/finish-editors/update.py',encoding='utf-8').read().split('# 普通详情')[0])
for name in ['LoginForm','RegisterForm','ForgotForm']:
    p=f'src/views/身份认证/{name}.vue';s=(root/p).read_text(encoding='utf-8')
    s=re.sub(r'/ aria-label="([^"]+)">',r'aria-label="\1" />',s)
    for a,b in {'aria-label="密钥"':'aria-label="密码"','aria-label="新设密钥"':'aria-label="新密码"','aria-label="校验代码"':'aria-label="邮箱验证码"','aria-label="验证代码"':'aria-label="邮箱验证码"'}.items():s=s.replace(a,b)
    write(p,s)

p='src/utils/worldGraphLayout.ts';s=(root/p).read_text(encoding='utf-8-sig')
# 使用节点卡片的宽高作为布局单位，消除孤立节点原有的大片留白。
start=s.index('  const rowWidth =');end=s.index('  return nodes.map',start)
s=s[:start]+'''  const cellWidth = 190, cellHeight = 84, gap = 28;
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
'''+s[end:]
s+='''
/** 坐标必须是最终显示的世界坐标（包含用户拖拽），边距按屏幕像素计算。 */
export function fitWorldGraph(points: { x: number; y: number }[], width: number, height: number) {
  const valid = points.filter(p => Number.isFinite(p.x) && Number.isFinite(p.y));
  if (!valid.length || width <= 0 || height <= 0) return null;
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  for (const p of valid) { minX = Math.min(minX,p.x); maxX = Math.max(maxX,p.x); minY = Math.min(minY,p.y); maxY = Math.max(maxY,p.y); }
  const zoom = Math.min(1, Math.max(.0001, Math.min(Math.max(1,width - 200) / Math.max(1,maxX - minX), Math.max(1,height - 100) / Math.max(1,maxY - minY))));
  return { zoom, offset: { x: width / 2 - (minX + maxX) / 2 * zoom, y: height / 2 - (minY + maxY) / 2 * zoom } };
}
'''
write(p,s)
p='src/views/世界观管理/世界观组件/FlatWorldGraph.vue';s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('layoutWorldGraph }','layoutWorldGraph, fitWorldGraph }')
s=s.replace('  positions?:', '  layoutData?: { nodes: WorldGraphNode[]; links: WorldGraphEdge[] };\n  focusId?: string;\n  positions?:',1)
s=s.replace('const nodes = computed(() => layoutWorldGraph(props.data.nodes, props.data.links));','''const layout = computed(() => layoutWorldGraph((props.layoutData || props.data).nodes, (props.layoutData || props.data).links));
const nodes = computed(() => { const visible = new Set(props.data.nodes.map(n => n.id)); return layout.value.filter(n => visible.has(n.id)); });''')
s=s.replace('  pinned.value = new Map(Object.entries(p));', '  pinned.value = new Map(Object.entries(p).filter(([,v]) => Number.isFinite(v.x) && Number.isFinite(v.y)));\n  // 下一帧已获得画布尺寸后重新按持久化位置取景。\n  requestAnimationFrame(() => resetCamera());')
start=s.index('const screen = computed(');end=s.index('const edgeOffsets',start)
s=s[:start]+'''const worldNodes = computed(() => nodes.value.map(node => ({ ...node, ...(pinned.value.get(node.id) || {}) })));
const screen = computed(() => worldNodes.value.map(node => ({ ...node, x: node.x * zoom.value + offset.value.x, y: node.y * zoom.value + offset.value.y })));
'''+s[end:]
s=s.replace("if (zoom.value < 0.35) return [];", "if (zoom.value < 0.2 && !props.focusId) return [];")
s=s.replace('({ ...node, z: 0, width:', '({ ...node, z: 0, priority: node.id === props.focusId || selected.value.has(node.id), width:')
start=s.index('function resetCamera()');end=s.index('function scale(',start)
s=s[:start]+'''function resetCamera() {
  const focused = props.focusId ? worldNodes.value.filter(node => node.id === props.focusId) : worldNodes.value.filter(node => selected.value.has(node.id));
  const fit = fitWorldGraph(focused.length ? focused : worldNodes.value, width.value, height.value);
  if (fit) { zoom.value = fit.zoom; offset.value = fit.offset; }
}
'''+s[end:]
s=s.replace('Math.max(.03, Math.min(4, zoom.value * factor))','Math.max(.0001, Math.min(4, zoom.value * factor))')
s=s.replace("@pointerdown=\"labelDown($event, label)\"", "@pointerdown=\"labelDown($event, label)\" @keydown.enter.prevent=\"emit('select', label.id)\" @keydown.space.prevent=\"emit('select', label.id)\"")
s=s.replace("let lastNodeKey = '';", "watch(() => props.focusId, () => { selected.value = new Set(); resetCamera(); }, { flush: 'post' });\nlet lastNodeKey = '';")
s=s.replace('  lastNodeKey = key;\n  resetCamera();','  lastNodeKey = key;\n  const visible = new Set(props.data.nodes.map(n => n.id));\n  selected.value = new Set([...selected.value].filter(id => visible.has(id)));\n  resetCamera();')
s=s.replace('onBeforeUnmount(() => { observer?', 'onBeforeUnmount(() => { detachWindow(); observer?')
write(p,s)
p='src/views/世界观管理/世界观组件/RelationGraph.vue';s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace(':data="filteredData"', ':data="filteredData" :layout-data="graphData" :focus-id="selectedNode?.id"')
s=s.replace('@click="selectedNode = null"','@click="showAll"')
s=s.replace('拖动画布，滚轮缩放；点击条目查看相关内容。','拖动画布，滚轮缩放；点击条目聚焦，返回全部关系后可适应全部节点。')
write(p,s)
print('Updated camera bounds, stable filtered layout, and auth labels')
