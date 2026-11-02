// 粒子变形用的图形。每个图形只负责在透明画布上用白色画出轮廓/实心形状，
// ParticleMorph 会对画出来的像素取样，让粒子聚成这个形状。

export type ShapeDraw = (ctx: CanvasRenderingContext2D, w: number, h: number) => void

export type ParticleShape =
  | { kind: 'image'; src: string; scale?: number }
  | { kind: 'text'; text: string; scale?: number; weight?: number; mono?: boolean }
  | { kind: 'draw'; draw: ShapeDraw }

const center = (w: number, h: number) => ({ cx: w / 2, cy: h / 2, r: Math.min(w, h) / 2 })

/** 观测靶心：同心圆 + 十字准线 + 刻度。 */
export const targetShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'
  ctx.lineWidth = Math.max(2, r * 0.035)
  for (const k of [0.82, 0.56, 0.3]) { ctx.beginPath(); ctx.arc(cx, cy, r * k, 0, Math.PI * 2); ctx.stroke() }
  ctx.beginPath()
  ctx.moveTo(cx - r * 0.95, cy); ctx.lineTo(cx - r * 0.38, cy)
  ctx.moveTo(cx + r * 0.38, cy); ctx.lineTo(cx + r * 0.95, cy)
  ctx.moveTo(cx, cy - r * 0.95); ctx.lineTo(cx, cy - r * 0.38)
  ctx.moveTo(cx, cy + r * 0.38); ctx.lineTo(cx, cy + r * 0.95)
  ctx.stroke()
  for (let i = 0; i < 24; i++) {
    const a = (i / 24) * Math.PI * 2, inner = i % 6 === 0 ? 0.86 : 0.9
    ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * r * inner, cy + Math.sin(a) * r * inner)
    ctx.lineTo(cx + Math.cos(a) * r * 0.96, cy + Math.sin(a) * r * 0.96); ctx.stroke()
  }
  ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(cx, cy, r * 0.07, 0, Math.PI * 2); ctx.fill()
}

/** 双层菱形徽记。 */
export const diamondShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.05)
  const rhombus = (s: number) => { ctx.beginPath(); ctx.moveTo(cx, cy - r * s); ctx.lineTo(cx + r * s * 0.78, cy); ctx.lineTo(cx, cy + r * s); ctx.lineTo(cx - r * s * 0.78, cy); ctx.closePath() }
  rhombus(0.92); ctx.stroke()
  rhombus(0.58); ctx.stroke()
  ctx.fillStyle = '#fff'; rhombus(0.24); ctx.fill()
}

/** 六边形框体，与首页标识外框同一几何语言。 */
export const hexShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.045)
  const hex = (s: number) => { ctx.beginPath(); for (let i = 0; i < 6; i++) { const a = Math.PI / 6 + (i * Math.PI) / 3; const x = cx + Math.cos(a) * r * s, y = cy + Math.sin(a) * r * s; i ? ctx.lineTo(x, y) : ctx.moveTo(x, y) } ctx.closePath() }
  hex(0.92); ctx.stroke(); hex(0.64); ctx.stroke()
  ctx.beginPath(); ctx.moveTo(cx, cy - r * 0.64); ctx.lineTo(cx, cy + r * 0.64)
  ctx.moveTo(cx - r * 0.55, cy - r * 0.32); ctx.lineTo(cx + r * 0.55, cy + r * 0.32)
  ctx.moveTo(cx + r * 0.55, cy - r * 0.32); ctx.lineTo(cx - r * 0.55, cy + r * 0.32); ctx.stroke()
}

/** 奖杯（年度最佳）。 */
export const trophyShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const s = r * 0.9
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, s * 0.07)
  // 杯身
  ctx.beginPath()
  ctx.moveTo(cx - s * 0.5, cy - s * 0.72); ctx.lineTo(cx + s * 0.5, cy - s * 0.72)
  ctx.quadraticCurveTo(cx + s * 0.5, cy + s * 0.05, cx, cy + s * 0.18)
  ctx.quadraticCurveTo(cx - s * 0.5, cy + s * 0.05, cx - s * 0.5, cy - s * 0.72)
  ctx.fill()
  // 把手
  ctx.beginPath(); ctx.arc(cx - s * 0.5, cy - s * 0.42, s * 0.22, Math.PI * 0.5, Math.PI * 1.5); ctx.stroke()
  ctx.beginPath(); ctx.arc(cx + s * 0.5, cy - s * 0.42, s * 0.22, -Math.PI * 0.5, Math.PI * 0.5); ctx.stroke()
  // 杯脚与底座
  ctx.fillRect(cx - s * 0.07, cy + s * 0.16, s * 0.14, s * 0.32)
  ctx.fillRect(cx - s * 0.32, cy + s * 0.48, s * 0.64, s * 0.1)
  ctx.fillRect(cx - s * 0.42, cy + s * 0.62, s * 0.84, s * 0.12)
}

/** 五角星。 */
export const starShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'; ctx.beginPath()
  for (let i = 0; i < 10; i++) {
    const a = -Math.PI / 2 + (i * Math.PI) / 5, rr = i % 2 ? r * 0.38 : r * 0.9
    const x = cx + Math.cos(a) * rr, y = cy + Math.sin(a) * rr
    i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)
  }
  ctx.closePath(); ctx.fill()
}

/** 画框：外框 + 山峦 + 太阳（画廊）。 */
export const frameShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const fw = r * 1.7, fh = r * 1.3, x = cx - fw / 2, y = cy - fh / 2
  ctx.strokeStyle = '#fff'; ctx.fillStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.06)
  ctx.strokeRect(x, y, fw, fh)
  ctx.beginPath(); ctx.arc(x + fw * 0.72, y + fh * 0.3, r * 0.13, 0, Math.PI * 2); ctx.fill()
  ctx.beginPath()
  ctx.moveTo(x + fw * 0.06, y + fh * 0.92); ctx.lineTo(x + fw * 0.36, y + fh * 0.4); ctx.lineTo(x + fw * 0.56, y + fh * 0.72)
  ctx.lineTo(x + fw * 0.68, y + fh * 0.56); ctx.lineTo(x + fw * 0.94, y + fh * 0.92); ctx.closePath(); ctx.fill()
}

/** 对话气泡 + 三个点（论坛）。 */
export const bubbleShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const bw = r * 1.7, bh = r * 1.1, x = cx - bw / 2, y = cy - bh / 2 - r * 0.15
  ctx.strokeStyle = '#fff'; ctx.fillStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.07)
  ctx.beginPath()
  ctx.moveTo(x, y); ctx.lineTo(x + bw, y); ctx.lineTo(x + bw, y + bh)
  ctx.lineTo(x + bw * 0.42, y + bh); ctx.lineTo(x + bw * 0.22, y + bh + r * 0.38); ctx.lineTo(x + bw * 0.26, y + bh)
  ctx.lineTo(x, y + bh); ctx.closePath(); ctx.stroke()
  for (const k of [0.28, 0.5, 0.72]) { ctx.beginPath(); ctx.arc(x + bw * k, y + bh / 2, r * 0.1, 0, Math.PI * 2); ctx.fill() }
}

/** 火柴人出拳姿势（柴圈）。 */
export const stickmanShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'; ctx.fillStyle = '#fff'; ctx.lineWidth = Math.max(3, r * 0.1); ctx.lineCap = 'round'
  const hx = cx - r * 0.05, hy = cy - r * 0.62
  ctx.beginPath(); ctx.arc(hx, hy, r * 0.2, 0, Math.PI * 2); ctx.fill()
  const neck = { x: hx + r * 0.04, y: hy + r * 0.22 }, hip = { x: cx - r * 0.08, y: cy + r * 0.22 }
  ctx.beginPath()
  ctx.moveTo(neck.x, neck.y); ctx.lineTo(hip.x, hip.y)
  ctx.moveTo(neck.x, neck.y + r * 0.12); ctx.lineTo(cx + r * 0.36, cy - r * 0.2); ctx.lineTo(cx + r * 0.8, cy - r * 0.3)   // 出拳
  ctx.moveTo(neck.x, neck.y + r * 0.12); ctx.lineTo(cx - r * 0.42, cy - r * 0.04); ctx.lineTo(cx - r * 0.3, cy - r * 0.32) // 护身
  ctx.moveTo(hip.x, hip.y); ctx.lineTo(cx + r * 0.3, cy + r * 0.5); ctx.lineTo(cx + r * 0.42, cy + r * 0.88)              // 前腿
  ctx.moveTo(hip.x, hip.y); ctx.lineTo(cx - r * 0.42, cy + r * 0.52); ctx.lineTo(cx - r * 0.76, cy + r * 0.74)             // 后腿
  ctx.stroke()
}

/** 地球：圆 + 经纬线（世界观）。 */
export const globeShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const R = r * 0.86
  ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.045)
  ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI * 2); ctx.stroke()
  for (const k of [0.38, 0.75]) { ctx.beginPath(); ctx.ellipse(cx, cy, R * k, R, 0, 0, Math.PI * 2); ctx.stroke() }
  for (const k of [-0.5, 0, 0.5]) { const yy = cy + R * k, half = Math.sqrt(R * R - (R * k) ** 2); ctx.beginPath(); ctx.moveTo(cx - half, yy); ctx.lineTo(cx + half, yy); ctx.stroke() }
  ctx.beginPath(); ctx.ellipse(cx, cy, R * 1.25, R * 0.3, -0.35, 0, Math.PI * 2); ctx.stroke()
}

/** 等轴方块，顶面带草皮纹（我的世界）。 */
export const cubeShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const s = r * 0.8, dx = s * Math.cos(Math.PI / 6), dy = s * 0.5
  const top = { x: cx, y: cy - s }, left = { x: cx - dx, y: cy - s + dy }, right = { x: cx + dx, y: cy - s + dy }, mid = { x: cx, y: cy - s + 2 * dy }
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.05)
  // 顶面实心
  ctx.beginPath(); ctx.moveTo(top.x, top.y); ctx.lineTo(right.x, right.y); ctx.lineTo(mid.x, mid.y); ctx.lineTo(left.x, left.y); ctx.closePath(); ctx.fill()
  // 侧面轮廓
  ctx.beginPath()
  ctx.moveTo(left.x, left.y); ctx.lineTo(left.x, left.y + s); ctx.lineTo(mid.x, mid.y + s); ctx.lineTo(right.x, right.y + s); ctx.lineTo(right.x, right.y)
  ctx.moveTo(mid.x, mid.y); ctx.lineTo(mid.x, mid.y + s)
  ctx.stroke()
  // 侧面像素格
  for (let i = 1; i < 4; i++) {
    const t = i / 4
    ctx.beginPath()
    ctx.moveTo(left.x + (mid.x - left.x) * t, left.y + (mid.y - left.y) * t + s * 0.18); ctx.lineTo(left.x + (mid.x - left.x) * t, left.y + (mid.y - left.y) * t + s * 0.4)
    ctx.moveTo(mid.x + (right.x - mid.x) * t, mid.y + (right.y - mid.y) * t + s * 0.18); ctx.lineTo(mid.x + (right.x - mid.x) * t, mid.y + (right.y - mid.y) * t + s * 0.4)
    ctx.stroke()
  }
}

/** 打开的书（知识库）。 */
export const bookShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.06)
  const pw = r * 0.82, ph = r * 1.1, top = cy - ph / 2
  for (const dir of [-1, 1]) {
    ctx.beginPath()
    ctx.moveTo(cx, top + r * 0.1); ctx.quadraticCurveTo(cx + dir * pw * 0.5, top - r * 0.05, cx + dir * pw, top)
    ctx.lineTo(cx + dir * pw, top + ph); ctx.quadraticCurveTo(cx + dir * pw * 0.5, top + ph - r * 0.15, cx, top + ph + r * 0.1)
    ctx.closePath(); ctx.stroke()
    for (const k of [0.3, 0.5, 0.7]) { ctx.beginPath(); ctx.moveTo(cx + dir * pw * 0.2, top + ph * k); ctx.lineTo(cx + dir * pw * 0.8, top + ph * k - r * 0.04); ctx.stroke() }
  }
}

/** 心形（赞助墙）。 */
export const heartShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const s = r * 0.85
  ctx.fillStyle = '#fff'; ctx.beginPath()
  ctx.moveTo(cx, cy + s * 0.8)
  ctx.bezierCurveTo(cx - s * 1.3, cy - s * 0.1, cx - s * 0.55, cy - s * 0.95, cx, cy - s * 0.38)
  ctx.bezierCurveTo(cx + s * 0.55, cy - s * 0.95, cx + s * 1.3, cy - s * 0.1, cx, cy + s * 0.8)
  ctx.fill()
}

/** 碗与热气（必吃榜）。 */
export const bowlShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.07); ctx.lineCap = 'round'
  ctx.beginPath(); ctx.moveTo(cx - r * 0.85, cy + r * 0.05); ctx.lineTo(cx + r * 0.85, cy + r * 0.05)
  ctx.arc(cx, cy + r * 0.05, r * 0.85, 0, Math.PI); ctx.fill()
  ctx.fillRect(cx - r * 0.3, cy + r * 0.86, r * 0.6, r * 0.1)
  for (const k of [-0.4, 0, 0.4]) {
    ctx.beginPath(); ctx.moveTo(cx + r * k, cy - r * 0.15)
    ctx.bezierCurveTo(cx + r * (k + 0.15), cy - r * 0.35, cx + r * (k - 0.15), cy - r * 0.55, cx + r * k, cy - r * 0.8); ctx.stroke()
  }
}

/** 齿轮（开发进度）。 */
export const gearShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'; ctx.beginPath()
  const teeth = 10
  for (let i = 0; i < teeth * 2; i++) {
    const a = (i / (teeth * 2)) * Math.PI * 2
    const rr = i % 2 === 0 ? r * 0.88 : r * 0.68
    const a1 = a - Math.PI / (teeth * 2) * 0.6, a2 = a + Math.PI / (teeth * 2) * 0.6
    ctx.lineTo(cx + Math.cos(a1) * rr, cy + Math.sin(a1) * rr); ctx.lineTo(cx + Math.cos(a2) * rr, cy + Math.sin(a2) * rr)
  }
  ctx.closePath(); ctx.fill()
  ctx.globalCompositeOperation = 'destination-out'
  ctx.beginPath(); ctx.arc(cx, cy, r * 0.3, 0, Math.PI * 2); ctx.fill()
  ctx.globalCompositeOperation = 'source-over'
}

/** 预设：各板块页头使用的粒子图形组合。 */
export const SECTION_PARTICLES: Record<string, ParticleShape[]> = {
  gallery: [{ kind: 'draw', draw: frameShape }, { kind: 'draw', draw: starShape }, { kind: 'text', text: '画', scale: 0.7 }],
  forum: [{ kind: 'draw', draw: bubbleShape }, { kind: 'text', text: '论坛', scale: 0.5 }],
  chai: [{ kind: 'draw', draw: stickmanShape }, { kind: 'text', text: '柴', scale: 0.7 }],
  world: [{ kind: 'draw', draw: globeShape }, { kind: 'draw', draw: hexShape }],
  games: [{ kind: 'draw', draw: cubeShape }, { kind: 'text', text: '游', scale: 0.7 }, { kind: 'draw', draw: targetShape }],
  minecraft: [{ kind: 'draw', draw: cubeShape }, { kind: 'text', text: 'MC', mono: true, scale: 0.5 }],
  honor: [{ kind: 'draw', draw: trophyShape }, { kind: 'draw', draw: starShape }],
  knowledge: [{ kind: 'draw', draw: bookShape }, { kind: 'draw', draw: targetShape }],
  sponsors: [{ kind: 'draw', draw: heartShape }, { kind: 'draw', draw: starShape }],
  food: [{ kind: 'draw', draw: bowlShape }, { kind: 'draw', draw: heartShape }],
  dev: [{ kind: 'draw', draw: gearShape }, { kind: 'draw', draw: hexShape }],
  collab: [{ kind: 'draw', draw: diamondShape }, { kind: 'draw', draw: hexShape }],
  awards: [{ kind: 'draw', draw: trophyShape }, { kind: 'draw', draw: cubeShape }, { kind: 'draw', draw: starShape }],
}

/** 加载中：几何图形快速轮换。 */
SECTION_PARTICLES.loading = [{ kind: 'draw', draw: hexShape }, { kind: 'draw', draw: diamondShape }, { kind: 'draw', draw: targetShape }]
/** 出错：靶心。 */
SECTION_PARTICLES.error = [{ kind: 'draw', draw: targetShape }, { kind: 'text', text: '!', scale: 0.8 }]
/** 没有指定板块时的空状态。 */
SECTION_PARTICLES.empty = [{ kind: 'draw', draw: diamondShape }, { kind: 'draw', draw: hexShape }]
/** 聊天室：对话气泡与 AI 字样。 */
SECTION_PARTICLES.chat = [{ kind: 'draw', draw: bubbleShape }, { kind: 'text', text: 'AI', mono: true, scale: 0.5 }, { kind: 'text', text: '聊', scale: 0.7 }]

// ===== 世界观：条目类型对应的图形 =====

/** 定位针（地点）。 */
export const pinShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  const R = r * 0.5, top = cy - r * 0.36
  ctx.fillStyle = '#fff'
  ctx.beginPath(); ctx.arc(cx, top, R, Math.PI * 0.82, Math.PI * 0.18); ctx.lineTo(cx, cy + r * 0.9); ctx.closePath(); ctx.fill()
  ctx.globalCompositeOperation = 'destination-out'
  ctx.beginPath(); ctx.arc(cx, top, R * 0.42, 0, Math.PI * 2); ctx.fill()
  ctx.globalCompositeOperation = 'source-over'
}

/** 旗帜（势力 / 组织）。 */
export const flagShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(3, r * 0.08)
  const px = cx - r * 0.55
  ctx.beginPath(); ctx.moveTo(px, cy - r * 0.9); ctx.lineTo(px, cy + r * 0.9); ctx.stroke()
  ctx.beginPath(); ctx.moveTo(px, cy - r * 0.82); ctx.lineTo(cx + r * 0.75, cy - r * 0.6); ctx.lineTo(cx + r * 0.35, cy - r * 0.32)
  ctx.lineTo(cx + r * 0.75, cy - r * 0.04); ctx.lineTo(px, cy + r * 0.1); ctx.closePath(); ctx.fill()
}

/** 剑（物品）。 */
export const swordShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.save(); ctx.translate(cx, cy); ctx.rotate(-Math.PI / 4)
  ctx.fillStyle = '#fff'
  ctx.beginPath(); ctx.moveTo(0, -r * 0.95); ctx.lineTo(r * 0.11, -r * 0.78); ctx.lineTo(r * 0.11, r * 0.36); ctx.lineTo(-r * 0.11, r * 0.36); ctx.lineTo(-r * 0.11, -r * 0.78); ctx.closePath(); ctx.fill()
  ctx.fillRect(-r * 0.36, r * 0.36, r * 0.72, r * 0.1)
  ctx.fillRect(-r * 0.06, r * 0.46, r * 0.12, r * 0.34)
  ctx.beginPath(); ctx.arc(0, r * 0.86, r * 0.1, 0, Math.PI * 2); ctx.fill()
  ctx.restore()
}

/** 闪电（技能）。 */
export const boltShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'
  ctx.beginPath(); ctx.moveTo(cx + r * 0.2, cy - r * 0.95); ctx.lineTo(cx - r * 0.5, cy + r * 0.12); ctx.lineTo(cx - r * 0.02, cy + r * 0.12)
  ctx.lineTo(cx - r * 0.22, cy + r * 0.95); ctx.lineTo(cx + r * 0.52, cy - r * 0.16); ctx.lineTo(cx + r * 0.04, cy - r * 0.16); ctx.closePath(); ctx.fill()
}

/** 云和雨线（气候）。 */
export const cloudShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.07); ctx.lineCap = 'round'
  const y = cy - r * 0.15
  for (const [dx, dy, s] of [[-0.42, 0.08, 0.32], [-0.06, -0.14, 0.42], [0.38, 0.06, 0.32]] as const) { ctx.beginPath(); ctx.arc(cx + r * dx, y + r * dy, r * s, 0, Math.PI * 2); ctx.fill() }
  ctx.fillRect(cx - r * 0.74, y + r * 0.06, r * 1.44, r * 0.32)
  for (const dx of [-0.42, -0.08, 0.26]) { ctx.beginPath(); ctx.moveTo(cx + r * dx, cy + r * 0.45); ctx.lineTo(cx + r * (dx - 0.12), cy + r * 0.85); ctx.stroke() }
}

/** 叶子（物种 / 生物）。 */
export const leafShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'
  ctx.beginPath(); ctx.moveTo(cx - r * 0.7, cy + r * 0.7)
  ctx.quadraticCurveTo(cx - r * 0.8, cy - r * 0.75, cx + r * 0.78, cy - r * 0.78)
  ctx.quadraticCurveTo(cx + r * 0.75, cy + r * 0.8, cx - r * 0.7, cy + r * 0.7); ctx.fill()
  ctx.globalCompositeOperation = 'destination-out'; ctx.lineWidth = Math.max(2, r * 0.06); ctx.strokeStyle = '#000'
  ctx.beginPath(); ctx.moveTo(cx - r * 0.55, cy + r * 0.55); ctx.lineTo(cx + r * 0.62, cy - r * 0.62); ctx.stroke()
  ctx.globalCompositeOperation = 'source-over'
}

/** 沙漏（事件）。 */
export const hourglassShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.strokeStyle = '#fff'; ctx.fillStyle = '#fff'; ctx.lineWidth = Math.max(2, r * 0.07)
  const x0 = cx - r * 0.5, x1 = cx + r * 0.5, y0 = cy - r * 0.8, y1 = cy + r * 0.8
  ctx.fillRect(x0 - r * 0.12, y0 - r * 0.1, r * 1.24, r * 0.1); ctx.fillRect(x0 - r * 0.12, y1, r * 1.24, r * 0.1)
  ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y0); ctx.lineTo(cx + r * 0.06, cy); ctx.lineTo(x1, y1); ctx.lineTo(x0, y1); ctx.lineTo(cx - r * 0.06, cy); ctx.closePath(); ctx.stroke()
  ctx.beginPath(); ctx.moveTo(cx - r * 0.3, y1 - r * 0.02); ctx.lineTo(cx, y1 - r * 0.38); ctx.lineTo(cx + r * 0.3, y1 - r * 0.02); ctx.closePath(); ctx.fill()
}

/** 头像轮廓（角色）。 */
export const personShape: ShapeDraw = (ctx, w, h) => {
  const { cx, cy, r } = center(w, h)
  ctx.fillStyle = '#fff'
  ctx.beginPath(); ctx.arc(cx, cy - r * 0.38, r * 0.34, 0, Math.PI * 2); ctx.fill()
  ctx.beginPath(); ctx.moveTo(cx - r * 0.78, cy + r * 0.9); ctx.quadraticCurveTo(cx - r * 0.74, cy + r * 0.08, cx, cy + r * 0.06)
  ctx.quadraticCurveTo(cx + r * 0.74, cy + r * 0.08, cx + r * 0.78, cy + r * 0.9); ctx.closePath(); ctx.fill()
}

/** 世界观条目类型 → 粒子图形（没列到的类型用六边形）。 */
export const CARD_TYPE_SHAPES: Record<string, ShapeDraw> = {
  character: personShape, location: pinShape, item: swordShape, event: hourglassShape,
  faction: flagShape, organization: flagShape, species: leafShape, creature: leafShape,
  occupation: gearShape, skill: boltShape, climate: cloudShape, concept: diamondShape,
}
export const cardTypeShape = (type?: string | null): ShapeDraw => CARD_TYPE_SHAPES[type ?? ''] ?? hexShape

/** 名字取第一个字（跳过空格和符号），给粒子拼字用。 */
export function firstGlyph(name?: string | null): string {
  const chars = [...(name ?? '').trim()]
  return chars.find(c => /[\p{L}\p{N}]/u.test(c)) ?? chars[0] ?? '界'
}
