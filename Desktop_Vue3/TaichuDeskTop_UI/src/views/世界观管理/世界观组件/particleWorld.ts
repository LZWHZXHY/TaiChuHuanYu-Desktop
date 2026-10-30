// particleWorld.ts：世界观「粒子星图」渲染引擎（three.js）。
// · 每个条目是一团发光粒子，形状按类型区分；关系是带「彗尾」粒子流的弧线；
// · 阵型（见 particleLayouts.ts）切换时，每个粒子从当前位置带着湍流飞向新位置；
// · 聚焦一个有封面的条目时，它的粒子会重新排成封面图（粒子画像）；
// · 关系路径：两个条目之间的最短关系链在中间排成一列；
// · 第一次打开时粒子先从四面八方聚成世界名字，再散开组成星图。
// 粒子的插值、旋转、画像、闪烁都在着色器里做；CPU 只在换阵型、换选中时改缓冲区。
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { placeGraphLabels } from '@/utils/worldGraph'
import { SHAPES, VIEW_DIR, applyPath, fib, hexToRgb, layoutFor, rng, shapeOfType, type Formation, type GroupLabel, type Guide, type LayoutCtx, type PWLink, type PWNode, type TimelineMark, type NeuralLayer, type V3, type Wave } from './particleLayouts'

export type { Formation, PWLink, PWNode, TimelineMark } from './particleLayouts'
export interface PWOptions {
  onSelect: (id: string) => void
  /** 聚焦的条目有没有可用的粒子画像（封面图读到了） */
  onPortrait?: (available: boolean) => void
  reducedMotion?: boolean
  /** 第一次显示时粒子先聚成的文字（世界名） */
  introText?: string
  typeLabel?: (type: string) => string
  /** 世界体系里当「恒星」的类型 */
  containers?: string[]
  /** 「脉络」结构的分层 */
  neuralLayers?: NeuralLayer[]
  /** 自定义类型 → 粒子形状（sphere / planet / crystal / burst / cube / helix / cloud / torus / disc） */
  shapes?: Record<string, string>
  /** 鼠标悬停到某个条目（空字符串 = 移开） */
  onHover?: (id: string) => void
}

const MORPH_DELAY = 0.35
const LINK_SEGMENTS = 16
const TRAIL = 4
const ease = (t: number) => t * t * (3 - 2 * t)
const smooth = (m: number, seed: number) => ease(Math.min(1, Math.max(0, (m - seed * MORPH_DELAY) / (1 - MORPH_DELAY))))
const portraitEase = (v: number, seed: number) => ease(Math.min(1, Math.max(0, (v - seed * 0.3) / 0.7)))

// ================= 着色器 =================
// 亮色主题：普通透明度混合，颜色压暗成「墨色」，画在纸色网格上；
// 暗色主题：加法混合发光。画布本身透明，背景交给页面（跟着网站的黑白主题和莱茵风网格走）。

const THEME_GLSL = /* glsl */`
  uniform float uLight;
  vec3 ink(vec3 c) { float m = max(c.r, max(c.g, c.b)); return m > 0.001 ? c / m * 0.58 : vec3(0.35); }
  /** p 是已经乘过透明度的颜色（发光强度） */
  vec4 outPre(vec3 p, float gain) {
    if (uLight > 0.5) return vec4(ink(p), clamp(max(p.r, max(p.g, p.b)) * gain, 0.0, 1.0));
    return vec4(p, max(p.r, max(p.g, p.b)));
  }`

const NODE_VERT = /* glsl */`
  uniform float uMorph; uniform float uTime; uniform float uBloom; uniform float uTurb; uniform float uShimmer;
  uniform float uScale; uniform float uSizeMul; uniform float uPortrait; uniform float uPortraitSize; uniform float uFadeDist; uniform float uPortraitDot;
  uniform vec3 uCamRight; uniform vec3 uCamUp;
  attribute vec3 aFrom; attribute vec3 aTo; attribute vec3 aOffset; attribute vec3 aColor; attribute vec3 aImg; attribute vec3 aImgCol;
  attribute float aRadius; attribute float aSeed; attribute float aSize; attribute float aState; attribute float aSpin;
  varying vec3 vColor; varying float vAlpha; varying float vPP;
  vec3 rotY(vec3 o, float a) { float c = cos(a), s = sin(a); return vec3(c * o.x + s * o.z, o.y, -s * o.x + c * o.z); }
  void main() {
    float t = clamp((uMorph - aSeed * ${MORPH_DELAY.toFixed(2)}) / ${(1 - MORPH_DELAY).toFixed(2)}, 0.0, 1.0);
    float e = t * t * (3.0 - 2.0 * t);
    float r = aRadius * (aState > 1.5 ? 1.0 + uBloom : 1.0);
    vec3 shapeP = aTo + rotY(aOffset, uTime * aSpin) * r;
    float q = clamp((uPortrait - aSeed * 0.3) / 0.7, 0.0, 1.0);
    float pp = aImg.z * q * q * (3.0 - 2.0 * q);
    vec3 imgP = aTo + (uCamRight * aImg.x + uCamUp * aImg.y) * uPortraitSize;
    vec3 p = mix(aFrom, mix(shapeP, imgP, pp), e);
    float mid = sin(e * 3.14159265) + sin(pp * 3.14159265) * 0.4;
    p += vec3(sin(aSeed * 91.7 + uTime * 1.7), cos(aSeed * 47.3 + uTime * 1.3), sin(aSeed * 29.1 + uTime * 1.1)) * mid * uTurb;
    p += vec3(sin(uTime * 0.8 + aSeed * 40.0), cos(uTime * 0.7 + aSeed * 31.0), sin(uTime * 0.9 + aSeed * 17.0)) * 0.35 * uShimmer * (1.0 - pp);
    vec4 mv = modelViewMatrix * vec4(p, 1.0);
    float st = aState < 0.5 ? 0.65 : (aState > 1.5 ? 1.25 : 1.0);
    // 画像里的粒子大小按画像尺寸算，疏密均匀
    gl_PointSize = clamp(mix(aSize * st * uSizeMul, uPortraitDot * (0.75 + aSeed * 0.5), pp) * uScale / max(1.0, -mv.z), 1.0, 56.0);
    gl_Position = projectionMatrix * mv;
    vColor = mix(aColor, aImgCol, pp);
    vPP = pp;
    float twinkle = mix(0.74 + 0.26 * sin(uTime * 2.3 + aSeed * 60.0), 1.0, max(1.0 - uShimmer, pp));
    // 远处的粒子淡一些，有景深
    float fade = clamp(1.4 - (-mv.z) / uFadeDist, 0.28, 1.0);
    vAlpha = (aState < 0.5 ? 0.08 : (aState > 1.5 ? 1.0 : 0.8)) * twinkle * fade;
  }`
/** 条目粒子：亮核 + 柔和光晕；亮色主题下画像粒子保留原图颜色 */
const NODE_FRAG = /* glsl */`
  ${THEME_GLSL}
  varying vec3 vColor; varying float vAlpha; varying float vPP;
  void main() {
    float d = length(gl_PointCoord - 0.5) * 2.0;
    if (d > 1.0) discard;
    float a = exp(-d * d * 9.0) + exp(-d * d * 3.0) * 0.3;
    a *= (1.0 - smoothstep(0.8, 1.0, d)) * vAlpha;
    if (uLight > 0.5) gl_FragColor = vec4(mix(ink(vColor), vColor, vPP), clamp(a * 1.1, 0.0, 1.0));
    else { vec3 p = vColor * a; gl_FragColor = vec4(p, max(p.r, max(p.g, p.b))); }
  }`
/** 其他小光点（关系流、星星） */
const DOT_FRAG = /* glsl */`
  ${THEME_GLSL}
  varying vec3 vColor; varying float vAlpha;
  void main() {
    float d = length(gl_PointCoord - 0.5) * 2.0;
    if (d > 1.0) discard;
    float a = exp(-d * d * 9.0) + exp(-d * d * 3.0) * 0.3;
    a *= 1.0 - smoothstep(0.8, 1.0, d);
    gl_FragColor = outPre(vColor * vAlpha * a, 1.6);
  }`
const FLOW_VERT = /* glsl */`
  uniform float uScale;
  attribute vec3 aColor; attribute float aSize;
  varying vec3 vColor; varying float vAlpha;
  void main() {
    vec4 mv = modelViewMatrix * vec4(position, 1.0);
    gl_PointSize = clamp(aSize * uScale / max(1.0, -mv.z), 1.0, 18.0);
    gl_Position = projectionMatrix * mv;
    vColor = aColor; vAlpha = 1.0;
  }`
const HALO_VERT = /* glsl */`
  uniform float uScale;
  attribute vec3 aColor; attribute float aSize;
  varying vec3 vColor; varying float vAlpha;
  void main() {
    vec4 mv = modelViewMatrix * vec4(position, 1.0);
    gl_PointSize = clamp(aSize * uScale / max(1.0, -mv.z), 2.0, 420.0);
    gl_Position = projectionMatrix * mv;
    vColor = aColor; vAlpha = 1.0;
  }`
const HALO_FRAG = /* glsl */`
  ${THEME_GLSL}
  varying vec3 vColor; varying float vAlpha;
  void main() {
    float d = length(gl_PointCoord - 0.5) * 2.0;
    if (d > 1.0) discard;
    gl_FragColor = outPre(vColor * exp(-d * d * 4.5) * (1.0 - d), 2.2);
  }`
const LINE_VERT = /* glsl */`
  attribute vec3 aColor;
  varying vec3 vColor;
  void main() { vColor = aColor; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`
const LINE_FRAG = /* glsl */`
  ${THEME_GLSL}
  uniform float uAlpha;
  varying vec3 vColor;
  void main() { gl_FragColor = outPre(vColor * uAlpha, 2.0); }`
const NEBULA_VERT = /* glsl */`
  varying vec2 vUv;
  void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`
const NEBULA_FRAG = /* glsl */`
  uniform sampler2D uMap; uniform float uGain;
  varying vec2 vUv;
  void main() { vec3 p = texture2D(uMap, vUv).rgb * uGain; gl_FragColor = vec4(p, max(p.r, max(p.g, p.b))); }`
const STAR_VERT = /* glsl */`
  uniform float uTime; uniform float uScale;
  attribute vec3 aColor; attribute float aSize; attribute float aSeed;
  varying vec3 vColor; varying float vAlpha;
  void main() {
    vec4 mv = modelViewMatrix * vec4(position, 1.0);
    gl_PointSize = clamp(aSize * uScale / max(1.0, -mv.z), 1.0, 6.0);
    gl_Position = projectionMatrix * mv;
    vColor = aColor; vAlpha = 0.55 + 0.45 * sin(uTime * (0.6 + aSeed) + aSeed * 50.0);
  }`

// ================= 主体 =================

interface NodeInfo { node: PWNode; start: number; count: number; radius: number; degree: number; from: V3; to: V3 }
interface FlowSpec { a: string; b: string; label: string; color: V3; count: number; speed: number; phase: number; embed: boolean; mid: V3 }
interface PortraitData { pts: Float32Array; n: number }

export class ParticleWorld {
  private container: HTMLElement
  private opts: PWOptions
  private renderer: THREE.WebGLRenderer
  private scene = new THREE.Scene()
  private camera: THREE.PerspectiveCamera
  private controls: OrbitControls
  private nodeGeo = new THREE.BufferGeometry()
  private nodeMat: THREE.ShaderMaterial
  private nodePoints: THREE.Points
  private lineGeo = new THREE.BufferGeometry()
  private lines: THREE.LineSegments
  private flowGeo = new THREE.BufferGeometry()
  private flowMat: THREE.ShaderMaterial
  private flow: THREE.Points
  private haloGeo = new THREE.BufferGeometry()
  private haloMat: THREE.ShaderMaterial
  private halo: THREE.Points
  private waveGeo = new THREE.BufferGeometry()
  private wavePts: THREE.Points
  private wave: Wave | null = null
  private wavePos = 0
  private guideGeo = new THREE.BufferGeometry()
  private guideMat: THREE.ShaderMaterial
  private guides: THREE.LineSegments
  private starMat: THREE.ShaderMaterial
  private labelLayer: HTMLDivElement
  private labelPool: HTMLButtonElement[] = []
  private groupPool: HTMLDivElement[] = []
  private edgePool: HTMLSpanElement[] = []
  private frame = 0
  private frameCount = 0
  private t0 = performance.now()
  private last = performance.now()
  private resizeObs: ResizeObserver

  private nodes: PWNode[] = []
  private links: PWLink[] = []
  private info = new Map<string, NodeInfo>()
  private degree = new Map<string, number>()
  private neighbors = new Map<string, Set<string>>()
  private formation: Formation = 'system'
  private focusId = ''
  private path: string[] | null = null
  private timeline = new Map<string, TimelineMark>()
  private groups: GroupLabel[] = []
  private signature = ''
  private selected = ''
  private hovered = ''
  private bloom = 0
  private morph = 1
  private morphDur = 2.2
  private intro: 'none' | 'text' | 'hold' = 'none'
  private introAt = 0
  private pending: { nodes: PWNode[]; links: PWLink[] } | null = null
  private flyAnim: { from: THREE.Vector3; to: THREE.Vector3; tFrom: THREE.Vector3; tTo: THREE.Vector3; start: number; dur: number } | null = null
  private layoutRadius = 200
  private down: { x: number; y: number } | null = null
  private disposed = false
  private wantRotate = true
  private flowSpec: FlowSpec[] = []
  private lineSegs = LINK_SEGMENTS
  // 粒子画像
  private portraits = new Map<string, PortraitData | 'loading' | 'failed'>()
  private portraitReady = ''
  private portraitWanted = true
  private portraitV = 0
  private camRight = new THREE.Vector3(1, 0, 0)
  private camUp = new THREE.Vector3(0, 1, 0)
  // 主题
  private uLight = { value: 0 }
  private light = false
  private nebula!: THREE.Mesh
  private stars!: THREE.Points

  constructor(container: HTMLElement, opts: PWOptions) {
    this.container = container
    this.opts = opts
    const w = Math.max(1, container.clientWidth), h = Math.max(1, container.clientHeight)
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' })
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
    this.renderer.setSize(w, h)
    this.renderer.setClearColor(0x000000, 0)
    this.renderer.domElement.className = 'pw-canvas'
    container.appendChild(this.renderer.domElement)
    this.camera = new THREE.PerspectiveCamera(50, w / h, 1, 14000)
    this.camera.position.set(0, 260, 900)
    this.controls = new OrbitControls(this.camera, this.renderer.domElement)
    this.controls.enableDamping = true
    this.controls.dampingFactor = 0.08
    this.controls.autoRotate = !opts.reducedMotion
    this.controls.autoRotateSpeed = 0.35
    this.controls.minDistance = 20
    this.controls.maxDistance = 7000
    this.controls.addEventListener('start', () => { this.flyAnim = null })

    const common = { transparent: true, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending, premultipliedAlpha: true }
    const uLight = this.uLight
    this.nodeMat = new THREE.ShaderMaterial({
      ...common, vertexShader: NODE_VERT, fragmentShader: NODE_FRAG,
      uniforms: { uLight,
        uMorph: { value: 1 }, uTime: { value: 0 }, uBloom: { value: 0 }, uTurb: { value: 0 }, uShimmer: { value: opts.reducedMotion ? 0 : 1 },
        uScale: { value: 1 }, uSizeMul: { value: 1 }, uPortrait: { value: 0 }, uPortraitSize: { value: 60 }, uPortraitDot: { value: 2 }, uFadeDist: { value: 1000 },
        uCamRight: { value: this.camRight }, uCamUp: { value: this.camUp },
      },
    })
    this.nodePoints = new THREE.Points(this.nodeGeo, this.nodeMat)
    this.lines = new THREE.LineSegments(this.lineGeo, new THREE.ShaderMaterial({ ...common, vertexShader: LINE_VERT, fragmentShader: LINE_FRAG, uniforms: { uLight, uAlpha: { value: 1 } } }))
    this.flowMat = new THREE.ShaderMaterial({ ...common, vertexShader: FLOW_VERT, fragmentShader: DOT_FRAG, uniforms: { uLight, uScale: { value: 1 } } })
    this.flow = new THREE.Points(this.flowGeo, this.flowMat)
    this.haloMat = new THREE.ShaderMaterial({ ...common, vertexShader: HALO_VERT, fragmentShader: HALO_FRAG, uniforms: { uLight, uScale: { value: 1 } } })
    this.halo = new THREE.Points(this.haloGeo, this.haloMat)
    this.guideMat = new THREE.ShaderMaterial({ ...common, vertexShader: LINE_VERT, fragmentShader: LINE_FRAG, uniforms: { uLight, uAlpha: { value: 0 } } })
    this.guides = new THREE.LineSegments(this.guideGeo, this.guideMat)
    this.wavePts = new THREE.Points(this.waveGeo, this.flowMat)
    this.starMat = new THREE.ShaderMaterial({ ...common, vertexShader: STAR_VERT, fragmentShader: DOT_FRAG, uniforms: { uLight, uTime: { value: 0 }, uScale: { value: 1 } } })
    // 还没有数据时放一个空点，避免渲染器读不到顶点数
    for (const g of [this.nodeGeo, this.lineGeo, this.flowGeo, this.haloGeo, this.guideGeo, this.waveGeo]) { g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(3), 3)); g.setDrawRange(0, 0) }
    for (const o of [this.nodePoints, this.lines, this.flow, this.halo, this.guides, this.wavePts]) o.frustumCulled = false
    this.nebula = makeNebula()
    this.stars = this.makeStars()
    this.scene.add(this.nebula, this.stars, this.guides, this.halo, this.lines, this.flow, this.wavePts, this.nodePoints)
    this.setTheme(false)

    this.labelLayer = document.createElement('div')
    this.labelLayer.className = 'pw-labels'
    container.appendChild(this.labelLayer)

    const el = this.renderer.domElement
    el.addEventListener('pointerdown', this.onDown)
    el.addEventListener('pointerup', this.onUp)
    el.addEventListener('pointermove', this.onMove)
    el.addEventListener('pointerleave', this.onLeave)
    this.resizeObs = new ResizeObserver(() => this.resize())
    this.resizeObs.observe(container)
    this.updateScale()
    this.frame = requestAnimationFrame(this.tick)
  }

  // ================= 对外接口 =================

  setFormation(f: Formation) {
    if (f === this.formation) return
    this.formation = f
    if (this.intro !== 'none' || !this.nodes.length) return
    this.rebuild(true)
    this.resetCamera('all', true)
  }
  getFormation() { return this.formation }

  /** 跟着网站主题：亮色（纸 + 墨色粒子）/ 暗色（发光粒子 + 星云） */
  setTheme(light: boolean) {
    this.light = light
    this.uLight.value = light ? 1 : 0
    this.scene.traverse(o => {
      const m = (o as THREE.Points).material as THREE.Material | undefined
      if (!(m instanceof THREE.ShaderMaterial)) return
      m.blending = light ? THREE.NormalBlending : THREE.AdditiveBlending
      m.premultipliedAlpha = !light
      m.needsUpdate = true
    })
    this.nebula.visible = !light
    this.stars.visible = !light
    // 画像的取样和主题有关（暗色去掉暗部，亮色去掉亮部）：重新排一次
    if (this.portraitReady) { const d = this.portraits.get(this.portraitReady); if (d && typeof d === 'object') this.applyPortrait(this.portraitReady, d) }
  }

  setAutoRotate(on: boolean) { this.wantRotate = on; if (this.intro === 'none') this.controls.autoRotate = on }

  /** 时间线（编年长河用）：每个条目取最早的一条 */
  setTimeline(items: { cardId: string; date: string; era?: string | null }[]) {
    const m = new Map<string, TimelineMark>()
    for (const it of items) {
      const old = m.get(it.cardId)
      if (!old || it.date.localeCompare(old.date, 'zh-CN', { numeric: true }) < 0) m.set(it.cardId, { date: it.date, era: (it.era ?? '').trim() })
    }
    this.timeline = m
    if (this.formation === 'timeline' && !this.focusId && this.intro === 'none' && this.nodes.length) { this.rebuild(true); this.resetCamera('all', true) }
  }

  setData(nodes: PWNode[], links: PWLink[], focusId: string) {
    const sig = this.sigOf(nodes, links, focusId)
    if (sig === this.signature) return
    this.signature = sig
    this.nodes = nodes
    this.links = links
    this.focusId = nodes.some(n => n.id === focusId) ? focusId : ''
    this.selected = this.focusId
    if (this.path && (this.focusId || this.path.some(id => !nodes.some(n => n.id === id)))) this.path = null
    // 开场动画还没播完：等文字散开后再换
    if (this.intro !== 'none') { this.pending = { nodes, links }; return }
    this.rebuild(true)
    this.resetCamera('all', true)
  }

  /** 关系路径：ids 依次相连；null 取消 */
  showPath(ids: string[] | null) {
    this.path = ids && ids.length > 1 ? ids : null
    this.selected = ''
    if (this.intro !== 'none') this.finishIntro()
    this.rebuild(true)
    this.resetCamera(this.path ? 'path' : 'all', true)
  }

  /** 高亮一个条目（不改变数据）；'' 取消 */
  highlight(id: string) {
    this.selected = id
    this.applyStates()
  }

  // ===== 时间：配合时间线，只「点亮」到某个时刻为止已经出现的条目 =====
  private future = new Set<string>()
  private present = new Set<string>()
  /** order：条目 → 它在时间线上第几个时刻出现；cursor：现在走到第几个时刻（null = 不按时间） */
  setTimeCursor(order: Map<string, number> | null, cursor: number | null) {
    this.future = new Set()
    this.present = new Set()
    if (order && cursor !== null) {
      for (const [id, o] of order) {
        if (o > cursor) this.future.add(id)
        else if (o === cursor) this.present.add(id)
      }
    }
    this.applyStates()
  }

  /** 右侧被面板挡住的宽度（像素）：画面中心往左移，内容不被挡住 */
  private insetRight = 0
  setInsetRight(px: number) { this.insetRight = Math.max(0, px); this.resize() }

  /** 名字标签开关（关掉后只显示选中 / 悬停的） */
  private showNames = true
  setLabels(on: boolean) { this.showNames = on }

  /** 聚焦时显示粒子画像（有封面才有） */
  setPortrait(on: boolean) { this.portraitWanted = on }

  /** 镜头对准全部 / 选中条目和它的邻居 / 关系路径；preferView = 用这种阵型最好看的角度 */
  resetCamera(scope: 'all' | 'selection' | 'path' = 'all', preferView = false) {
    const ids = scope === 'path' && this.path ? this.path
      : scope === 'selection' && this.selected ? [this.selected, ...(this.neighbors.get(this.selected) ?? [])]
        : this.nodes.map(n => n.id)
    const pts = ids.map(id => this.info.get(id)?.to).filter((p): p is V3 => !!p)
    for (const g of scope === 'all' ? this.groups : []) pts.push(g.pos)
    if (!pts.length) return
    const box = new THREE.Box3()
    for (const p of pts) box.expandByPoint(new THREE.Vector3(...p))
    const c = box.getCenter(new THREE.Vector3())
    let R = 30
    for (const id of ids) { const i = this.info.get(id); if (i) R = Math.max(R, c.distanceTo(new THREE.Vector3(...i.to)) + i.radius * (id === this.focusId ? 3.6 : 1.6)) }
    const fov = (this.camera.fov * Math.PI) / 180
    const cw = Math.max(1, this.container.clientWidth), ch = Math.max(1, this.container.clientHeight)
    const visibleAspect = (cw - Math.min(this.insetRight, cw * 0.6)) / ch
    const half = Math.min(fov / 2, Math.atan(Math.tan(fov / 2) * Math.max(visibleAspect, 0.2)))
    const dist = (R / Math.sin(half)) * 1.02
    const dir = this.camera.position.clone().sub(this.controls.target)
    if (preferView || dir.lengthSq() < 1) {
      const v = VIEW_DIR[this.path ? 'path' : this.focusId ? 'focus' : this.formation]
      // 路径和编年长河要正对着看；其他保留现在绕 y 轴转到的方向，只调整俯仰
      const keepYaw = !this.path && this.formation !== 'timeline' && dir.lengthSq() >= 1
      const yaw = keepYaw ? Math.atan2(dir.x, dir.z) : Math.atan2(v[0], v[2])
      const flat = Math.hypot(v[0], v[2])
      dir.set(Math.sin(yaw) * flat, v[1], Math.cos(yaw) * flat)
      if (!keepYaw) this.controls.autoRotate = false
    }
    dir.normalize()
    this.flyTo(c.clone().add(dir.multiplyScalar(dist)), c)
  }

  dispose() {
    this.disposed = true
    cancelAnimationFrame(this.frame)
    this.resizeObs.disconnect()
    const el = this.renderer.domElement
    el.removeEventListener('pointerdown', this.onDown)
    el.removeEventListener('pointerup', this.onUp)
    el.removeEventListener('pointermove', this.onMove)
    el.removeEventListener('pointerleave', this.onLeave)
    this.controls.dispose()
    this.scene.traverse(o => {
      const m = o as THREE.Mesh
      m.geometry?.dispose()
      const mat = m.material as THREE.Material | THREE.Material[] | undefined
      if (Array.isArray(mat)) mat.forEach(x => x.dispose())
      else { ((mat as THREE.ShaderMaterial | undefined)?.uniforms?.uMap?.value as THREE.Texture | undefined)?.dispose(); mat?.dispose() }
    })
    this.renderer.dispose()
    el.remove()
    this.labelLayer.remove()
  }

  // ================= 布局与粒子缓冲区 =================

  private sigOf(nodes: PWNode[], links: PWLink[], focusId: string) {
    return `${focusId}|${nodes.map(n => `${n.id}:${n.type}:${n.title}:${n.cover ?? ''}`).join(',')}|${links.map(l => `${l.source}>${l.target}:${l.isEmbed ? 1 : 0}:${l.color}:${l.label}`).join(',')}`
  }

  private computeDegrees() {
    this.degree = new Map(this.nodes.map(n => [n.id, 0]))
    this.neighbors = new Map(this.nodes.map(n => [n.id, new Set<string>()]))
    for (const l of this.links) {
      if (!this.degree.has(l.source) || !this.degree.has(l.target)) continue
      this.degree.set(l.source, (this.degree.get(l.source) ?? 0) + 1)
      this.degree.set(l.target, (this.degree.get(l.target) ?? 0) + 1)
      this.neighbors.get(l.source)?.add(l.target)
      this.neighbors.get(l.target)?.add(l.source)
    }
  }

  /** 当前每个粒子的实际位置（不含湍流、闪烁），换阵型时作为新的起点 */
  private currentPositions(): Float32Array | null {
    const g = this.nodeGeo
    const from = g.getAttribute('aFrom') as THREE.BufferAttribute | undefined
    if (!from) return null
    const to = g.getAttribute('aTo') as THREE.BufferAttribute
    const off = g.getAttribute('aOffset') as THREE.BufferAttribute
    const img = g.getAttribute('aImg') as THREE.BufferAttribute
    const rad = g.getAttribute('aRadius') as THREE.BufferAttribute
    const seed = g.getAttribute('aSeed') as THREE.BufferAttribute
    const spin = g.getAttribute('aSpin') as THREE.BufferAttribute
    const state = g.getAttribute('aState') as THREE.BufferAttribute
    const time = this.nodeMat.uniforms.uTime!.value as number
    const psize = this.nodeMat.uniforms.uPortraitSize!.value as number
    const R = this.camRight, U = this.camUp
    const n = from.count
    const out = new Float32Array(n * 3)
    for (let i = 0; i < n; i++) {
      const sd = seed.getX(i)
      const e = smooth(this.morph, sd)
      const r = rad.getX(i) * (state.getX(i) > 1.5 ? 1 + this.bloom : 1)
      const a = time * spin.getX(i), c = Math.cos(a), s = Math.sin(a)
      const ox = off.getX(i), oy = off.getY(i), oz = off.getZ(i)
      let tx = to.getX(i) + (c * ox + s * oz) * r, ty = to.getY(i) + oy * r, tz = to.getZ(i) + (-s * ox + c * oz) * r
      const pw = img.getZ(i) * portraitEase(this.portraitV, sd)
      if (pw > 0) {
        const ix = img.getX(i) * psize, iy = img.getY(i) * psize
        tx += (to.getX(i) + R.x * ix + U.x * iy - tx) * pw
        ty += (to.getY(i) + R.y * ix + U.y * iy - ty) * pw
        tz += (to.getZ(i) + R.z * ix + U.z * iy - tz) * pw
      }
      out[i * 3] = from.getX(i) + (tx - from.getX(i)) * e
      out[i * 3 + 1] = from.getY(i) + (ty - from.getY(i)) * e
      out[i * 3 + 2] = from.getZ(i) + (tz - from.getZ(i)) * e
    }
    return out
  }

  /** 按当前数据和阵型重新生成粒子，从当前位置飞到新位置 */
  private rebuild(animate: boolean, introText?: string) {
    const prevPos = this.currentPositions()
    const prevInfo = this.info
    this.computeDegrees()
    const ctx: LayoutCtx = {
      nodes: this.nodes, links: this.links, degree: this.degree, neighbors: this.neighbors, focusId: this.focusId,
      timeline: this.timeline, typeLabel: this.opts.typeLabel ?? (t => t),
      containers: this.opts.containers ? new Set(this.opts.containers) : undefined, neuralLayers: this.opts.neuralLayers,
    }
    let layout = layoutFor(this.formation, ctx)
    if (this.path && !this.focusId) layout = applyPath(layout.anchors, this.path)
    const anchors = layout.anchors
    this.groups = layout.groups
    let R = 60
    for (const p of anchors.values()) R = Math.max(R, Math.hypot(p[0], p[1], p[2]))
    this.layoutRadius = R

    // 分配粒子：关系越多粒子越多；聚焦且有封面的条目要排成画像，给足粒子
    const n = this.nodes.length
    const budget = Math.min(52000, Math.max(4000, n * 220))
    const weights = this.nodes.map(node => 1 + Math.sqrt(this.degree.get(node.id) ?? 0) * 0.9 + (node.id === this.focusId ? 3 : 0))
    const sumW = weights.reduce((a, b) => a + b, 0) || 1
    const counts = weights.map((w, i) => {
      const node = this.nodes[i]!
      const c = Math.max(n > 600 ? 18 : 36, Math.min(1400, Math.round((budget * w) / sumW)))
      return node.id === this.focusId && node.cover ? Math.max(c, 9000) : c
    })
    const total = counts.reduce((a, b) => a + b, 0)
    // 星团大小跟着条目之间的平均间距走，疏密不同的阵型看起来都饱满
    const spacing = (R / Math.max(2, Math.sqrt(n))) * (this.focusId ? 1 : 1.4)
    const unit = Math.max(6, Math.min(40, spacing * 0.22))

    const aFrom = new Float32Array(total * 3), aTo = new Float32Array(total * 3), aOffset = new Float32Array(total * 3), aColor = new Float32Array(total * 3)
    const aImg = new Float32Array(total * 3), aImgCol = new Float32Array(total * 3)
    const aRadius = new Float32Array(total), aSeed = new Float32Array(total), aSize = new Float32Array(total), aState = new Float32Array(total), aSpin = new Float32Array(total)
    const info = new Map<string, NodeInfo>()
    let k = 0
    const far = (): V3 => { const r = Math.random, p = fib(Math.floor(r() * 997), 997), d = R * (2.2 + r() * 1.8); return [p[0] * d, p[1] * d, p[2] * d] }
    this.nodes.forEach((node, ni) => {
      const count = counts[ni]!
      const deg = this.degree.get(node.id) ?? 0
      const radius = unit * (0.75 + 0.35 * Math.sqrt(deg + 1)) * (node.id === this.focusId ? 1.8 : 1)
      const to = anchors.get(node.id) ?? [0, 0, 0]
      const shape = SHAPES[this.opts.shapes?.[node.type] ?? shapeOfType(node.type)] ?? SHAPES.sphere!
      const r = rng(node.id + ':p')
      const base = hexToRgb(node.color)
      const spin = (0.12 + r() * 0.3) * (r() < 0.5 ? -1 : 1)
      // 粒子多的星团单个粒子暗一点，避免叠加后发白
      const dim0 = Math.max(0.42, Math.min(1, 1.5 / Math.sqrt(count / 120)))
      const prev = prevInfo.get(node.id)
      for (let j = 0; j < count; j++, k++) {
        const o = shape(r, j, count)
        aOffset.set(o, k * 3)
        aTo.set(to, k * 3)
        aRadius[k] = introText ? 0 : radius
        aSeed[k] = r()
        aSpin[k] = spin
        aSize[k] = radius * 0.16 * (0.6 + r() * 0.8)
        aState[k] = 1
        // 颜色：在类型色和白色之间轻微变化，像星星一样有亮有暗
        // 越靠近星团中心越白、越亮，外圈保留类型色
        const core = Math.max(0, 1 - Math.hypot(o[0], o[1], o[2]))
        const wht = Math.max(Math.pow(r(), 3) * 0.55, core * core * 0.6), dim = (0.75 + r() * 0.35 + core * 0.25) * dim0
        aColor[k * 3] = (base[0] + (1 - base[0]) * wht) * dim
        aColor[k * 3 + 1] = (base[1] + (1 - base[1]) * wht) * dim
        aColor[k * 3 + 2] = (base[2] + (1 - base[2]) * wht) * dim
        // 起点：原来就有的条目从它现在的粒子位置出发，新条目从远处飞进来
        if (prev && prevPos && animate) {
          const src = prev.start + (j % prev.count)
          aFrom[k * 3] = prevPos[src * 3]!; aFrom[k * 3 + 1] = prevPos[src * 3 + 1]!; aFrom[k * 3 + 2] = prevPos[src * 3 + 2]!
        } else if (animate) aFrom.set(far(), k * 3)
        else { aFrom[k * 3] = to[0] + o[0] * radius; aFrom[k * 3 + 1] = to[1] + o[1] * radius; aFrom[k * 3 + 2] = to[2] + o[2] * radius }
      }
      const prevAnchor = prev ? this.anchorNow(prev) : null
      info.set(node.id, { node, start: k - count, count, radius, degree: deg, from: prevAnchor ?? far(), to })
    })

    // 开场：先聚成世界名字
    if (introText) {
      const pts = textPoints(introText, total, R * 2.4)
      for (let i = 0; i < total; i++) aTo.set(pts[i]!, i * 3)
    }

    // 换一个新的几何体（旧的连同显存一起释放）
    this.nodeGeo.dispose()
    const g = this.nodeGeo = new THREE.BufferGeometry()
    this.nodePoints.geometry = g
    const set = (name: string, arr: Float32Array, size: number) => g.setAttribute(name, new THREE.BufferAttribute(arr, size))
    set('position', aTo, 3) // 只为了让 three 知道点数
    set('aFrom', aFrom, 3); set('aTo', aTo, 3); set('aOffset', aOffset, 3); set('aColor', aColor, 3); set('aImg', aImg, 3); set('aImgCol', aImgCol, 3)
    set('aRadius', aRadius, 1); set('aSeed', aSeed, 1); set('aSize', aSize, 1); set('aState', aState, 1); set('aSpin', aSpin, 1)
    g.setDrawRange(0, total)
    this.info = info
    const u = this.nodeMat.uniforms
    u.uTurb!.value = this.opts.reducedMotion ? 0 : Math.min(60, R * 0.12)
    u.uSizeMul!.value = introText ? 2.4 : 1
    const focus = this.focusId ? info.get(this.focusId) : null
    // 画像不要盖住周围的条目：最多到最近邻居距离的一半
    let nearest = Infinity
    if (focus) for (const [id, i] of info) if (id !== this.focusId) nearest = Math.min(nearest, Math.hypot(i.to[0] - focus.to[0], i.to[1] - focus.to[1], i.to[2] - focus.to[2]))
    u.uPortraitSize!.value = focus ? Math.min(focus.radius * 3.4, nearest * 0.5) : 60
    u.uPortraitDot!.value = (u.uPortraitSize!.value as number) * 0.034
    this.portraitV = 0
    this.portraitReady = ''
    this.opts.onPortrait?.(false)
    this.morph = animate ? 0 : 1
    this.morphDur = this.opts.reducedMotion ? 0.35 : this.intro === 'text' ? 1.9 : 2.3
    this.buildLinks()
    this.buildHalos()
    this.buildGuides(introText ? [] : layout.guides)
    this.buildWave(introText || this.path || this.focusId ? null : layout.wave ?? null)
    this.applyStates()
    if (this.focusId && !introText) this.ensurePortrait(this.focusId)
  }

  /** 某个条目现在的中心（随变形进度从旧位置移向新位置） */
  private anchorNow(i: NodeInfo): V3 {
    const e = smooth(this.morph, 0.5)
    return [i.from[0] + (i.to[0] - i.from[0]) * e, i.from[1] + (i.to[1] - i.from[1]) * e, i.from[2] + (i.to[2] - i.from[2]) * e]
  }

  // ================= 参考线（轨道、树枝、长河、星座连线） =================

  private buildGuides(list: Guide[]) {
    this.guideGeo.dispose()
    this.guideGeo = new THREE.BufferGeometry()
    this.guides.geometry = this.guideGeo
    const pos = new Float32Array(Math.max(1, list.length) * 6), col = new Float32Array(Math.max(1, list.length) * 6)
    list.forEach((s, i) => {
      pos.set(s.a, i * 6); pos.set(s.b, i * 6 + 3)
      for (const o of [0, 3]) { col[i * 6 + o] = s.color[0] * s.alpha; col[i * 6 + o + 1] = s.color[1] * s.alpha; col[i * 6 + o + 2] = s.color[2] * s.alpha }
    })
    this.guideGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3))
    this.guideGeo.setAttribute('aColor', new THREE.BufferAttribute(col, 3))
    this.guideGeo.setDrawRange(0, list.length * 2)
  }

  // ================= 「脉络」的光脉冲 =================

  private buildWave(w: Wave | null) {
    this.wave = w
    this.waveGeo.dispose()
    this.waveGeo = new THREE.BufferGeometry()
    this.wavePts.geometry = this.waveGeo
    const n = Math.max(1, (w?.segs.length ?? 0) * TRAIL)
    this.waveGeo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(n * 3), 3).setUsage(THREE.DynamicDrawUsage))
    this.waveGeo.setAttribute('aColor', new THREE.BufferAttribute(new Float32Array(n * 3), 3).setUsage(THREE.DynamicDrawUsage))
    const sizes = new Float32Array(n)
    const base = Math.max(3, this.layoutRadius / 90)
    for (let i = 0; i < n; i++) sizes[i] = base * (1 - (i % TRAIL) * 0.2)
    this.waveGeo.setAttribute('aSize', new THREE.BufferAttribute(sizes, 1))
    this.waveGeo.setDrawRange(0, w ? n : 0)
  }

  /** 一道波从第一层扫到最后一层：每段连接上一颗带尾巴的光点 */
  private updateWave(time: number, fade: number) {
    const w = this.wave
    if (!w) return
    const span = w.layers + 0.6
    this.wavePos = (time * 0.42) % span - 0.3
    const P = (this.waveGeo.getAttribute('position') as THREE.BufferAttribute).array as Float32Array
    const C = (this.waveGeo.getAttribute('aColor') as THREE.BufferAttribute).array as Float32Array
    let k = 0
    for (const s of w.segs) {
      const t0 = this.wavePos - s.layer
      for (let tr = 0; tr < TRAIL; tr++, k++) {
        const t = t0 - tr * 0.045
        const on = t >= 0 && t <= 1 ? Math.sin(t * Math.PI) : 0
        const e = Math.min(1, Math.max(0, t))
        P[k * 3] = s.a[0] + (s.b[0] - s.a[0]) * e; P[k * 3 + 1] = s.a[1] + (s.b[1] - s.a[1]) * e; P[k * 3 + 2] = s.a[2] + (s.b[2] - s.a[2]) * e
        const g = on * fade * (1 - tr * 0.24) * (this.selected ? 0.35 : 1)
        // 从第一层的青色渐变到最后一层的粉色
        const mix = (s.layer + e) / Math.max(1, w.layers - 1)
        C[k * 3] = (0.45 + 0.5 * mix) * g; C[k * 3 + 1] = (0.95 - 0.45 * mix) * g; C[k * 3 + 2] = (0.9 - 0.1 * mix) * g
      }
    }
    ;(this.waveGeo.getAttribute('position') as THREE.BufferAttribute).needsUpdate = true
    ;(this.waveGeo.getAttribute('aColor') as THREE.BufferAttribute).needsUpdate = true
  }

  // ================= 光晕：每个星团后面一团柔光 =================

  private buildHalos() {
    this.haloGeo.dispose()
    this.haloGeo = new THREE.BufferGeometry()
    this.halo.geometry = this.haloGeo
    const n = Math.max(1, this.info.size)
    this.haloGeo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(n * 3), 3).setUsage(THREE.DynamicDrawUsage))
    this.haloGeo.setAttribute('aColor', new THREE.BufferAttribute(new Float32Array(n * 3), 3).setUsage(THREE.DynamicDrawUsage))
    this.haloGeo.setAttribute('aSize', new THREE.BufferAttribute(new Float32Array(n), 1).setUsage(THREE.DynamicDrawUsage))
    this.haloGeo.setDrawRange(0, this.info.size)
  }

  private updateHalos(time: number) {
    const P = (this.haloGeo.getAttribute('position') as THREE.BufferAttribute).array as Float32Array
    const C = (this.haloGeo.getAttribute('aColor') as THREE.BufferAttribute).array as Float32Array
    const S = (this.haloGeo.getAttribute('aSize') as THREE.BufferAttribute).array as Float32Array
    const sel = this.selected, near = sel ? this.neighbors.get(sel) : null
    const inPath = this.path ? new Set(this.path) : null
    const appear = this.intro !== 'none' ? 0 : Math.min(1, this.morph * 1.4)
    let k = 0
    for (const [id, i] of this.info) {
      const p = this.anchorNow(i)
      P[k * 3] = p[0]; P[k * 3 + 1] = p[1]; P[k * 3 + 2] = p[2]
      const c = hexToRgb(i.node.color)
      let s = 0.16
      if (inPath) s = inPath.has(id) ? 0.3 : 0.02
      else if (sel) s = id === sel ? 0.34 : near?.has(id) ? 0.16 : 0.025
      if (this.future.has(id)) s *= 0.12
      else if (this.present.has(id)) s = Math.max(s, 0.34)
      if (id === this.hovered) s = Math.max(s, 0.3)
      let pulse = id === sel || id === this.hovered || this.present.has(id) ? 1 + 0.12 * Math.sin(time * 3) : 1
      // 脉络：光脉冲经过这一层时，这一层的条目亮一下
      const layer = this.wave?.nodeLayer.get(id)
      if (layer !== undefined && !sel) {
        const f = Math.exp(-Math.pow(this.wavePos - layer, 2) * 14)
        s += 0.32 * f
        pulse *= 1 + 0.35 * f
      }
      const port = id === this.focusId ? 1 + this.portraitV * 0.9 : 1
      s *= appear * (id === this.focusId && this.portraitV > 0 ? 1 - this.portraitV * 0.6 : 1)
      C[k * 3] = c[0] * s; C[k * 3 + 1] = c[1] * s; C[k * 3 + 2] = c[2] * s
      S[k] = i.radius * 3.2 * pulse * port
      k++
    }
    for (const name of ['position', 'aColor', 'aSize']) (this.haloGeo.getAttribute(name) as THREE.BufferAttribute).needsUpdate = true
  }

  // ================= 关系：弧线 + 带彗尾的流动粒子 =================

  private buildLinks() {
    const dense = this.links.length > 1500
    this.flowSpec = this.links.filter(l => this.info.has(l.source) && this.info.has(l.target)).map(l => {
      const r = rng(l.source + '>' + l.target)
      return { a: l.source, b: l.target, label: l.label, color: hexToRgb(l.color), count: l.isEmbed ? 1 : (dense ? 1 : 3), speed: 0.05 + r() * 0.07, phase: r(), embed: l.isEmbed, mid: [0, 0, 0] as V3 }
    })
    const segs = dense ? 8 : LINK_SEGMENTS
    this.lineGeo.dispose(); this.flowGeo.dispose()
    this.lineGeo = new THREE.BufferGeometry(); this.flowGeo = new THREE.BufferGeometry()
    this.lines.geometry = this.lineGeo; this.flow.geometry = this.flowGeo
    const linePos = new Float32Array(Math.max(1, this.flowSpec.length * segs * 2) * 3)
    this.lineGeo.setAttribute('position', new THREE.BufferAttribute(linePos, 3).setUsage(THREE.DynamicDrawUsage))
    this.lineGeo.setAttribute('aColor', new THREE.BufferAttribute(new Float32Array(linePos.length), 3).setUsage(THREE.DynamicDrawUsage))
    const flowCount = Math.max(1, this.flowSpec.reduce((s, f) => s + f.count, 0) * TRAIL)
    this.flowGeo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(flowCount * 3), 3).setUsage(THREE.DynamicDrawUsage))
    this.flowGeo.setAttribute('aColor', new THREE.BufferAttribute(new Float32Array(flowCount * 3), 3).setUsage(THREE.DynamicDrawUsage))
    // 彗尾：一颗亮头 + 越来越小的尾巴
    const sizes = new Float32Array(flowCount)
    const base = Math.max(2.4, this.layoutRadius / 110)
    for (let i = 0; i < flowCount; i++) sizes[i] = base * (1 - (i % TRAIL) * 0.2)
    this.flowGeo.setAttribute('aSize', new THREE.BufferAttribute(sizes, 1))
    this.lineSegs = segs
  }

  /** 每帧更新弧线和流动粒子的位置（跟着星团中心走） */
  private updateLinks(time: number) {
    const lp = this.lineGeo.getAttribute('position') as THREE.BufferAttribute
    const lc = this.lineGeo.getAttribute('aColor') as THREE.BufferAttribute | undefined
    if (!lc) return
    const fp = this.flowGeo.getAttribute('position') as THREE.BufferAttribute
    const fc = this.flowGeo.getAttribute('aColor') as THREE.BufferAttribute
    const L = lp.array as Float32Array, LC = lc.array as Float32Array, F = fp.array as Float32Array, FC = fc.array as Float32Array
    const sel = this.selected || this.hovered
    const pathPairs = this.path ? new Set(this.path.slice(1).flatMap((id, i) => [`${this.path![i]}>${id}`, `${id}>${this.path![i]}`])) : null
    const centers = new Map<string, V3>()
    for (const [id, i] of this.info) centers.set(id, this.anchorNow(i))
    // 开场文字阶段不画关系
    const linkFade = this.intro !== 'none' ? 0 : Math.min(1, Math.max(0, (this.morph - 0.55) / 0.45))
    let li = 0, fi = 0
    const segs = this.lineSegs
    const bez = (a: V3, c: V3, b: V3, t: number, o: Float32Array | V3, at: number) => {
      const u = 1 - t
      o[at] = u * u * a[0] + 2 * u * t * c[0] + t * t * b[0]
      o[at + 1] = u * u * a[1] + 2 * u * t * c[1] + t * t * b[1]
      o[at + 2] = u * u * a[2] + 2 * u * t * c[2] + t * t * b[2]
    }
    for (const f of this.flowSpec) {
      const a = centers.get(f.a)!, b = centers.get(f.b)!
      // 弧线控制点：从中点往外拱一点
      const mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2, mz = (a[2] + b[2]) / 2
      const len = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2])
      const ml = Math.hypot(mx, my, mz) || 1
      const bulge = pathPairs ? len * 0.12 : len * 0.22
      const c: V3 = [mx + (mx / ml) * bulge, my + (my / ml) * bulge + len * 0.08, mz + (mz / ml) * bulge]
      bez(a, c, b, 0.5, f.mid, 0)
      const onPath = !!pathPairs?.has(`${f.a}>${f.b}`)
      const related = pathPairs ? onPath : !sel || f.a === sel || f.b === sel
      const tf = this.future.has(f.a) || this.future.has(f.b) ? 0.05 : 1
      const alpha = tf * linkFade * (pathPairs ? (onPath ? 1 : 0.02) : sel ? (related ? 0.85 : 0.04) : (f.embed ? 0.14 : 0.26))
      for (let s = 0; s < segs; s++) {
        bez(a, c, b, s / segs, L, li * 3)
        bez(a, c, b, (s + 1) / segs, L, li * 3 + 3)
        // 越靠近目标越亮，看得出方向
        const k0 = alpha * (0.3 + 0.7 * (s / segs)), k1 = alpha * (0.3 + 0.7 * ((s + 1) / segs))
        LC[li * 3] = f.color[0] * k0; LC[li * 3 + 1] = f.color[1] * k0; LC[li * 3 + 2] = f.color[2] * k0
        LC[li * 3 + 3] = f.color[0] * k1; LC[li * 3 + 4] = f.color[1] * k1; LC[li * 3 + 5] = f.color[2] * k1
        li += 2
      }
      const fa = tf * linkFade * (pathPairs ? (onPath ? 1.2 : 0.02) : sel ? (related ? 1 : 0.03) : (f.embed ? 0.4 : 0.75))
      const speed = onPath ? f.speed * 2 : f.speed
      for (let j = 0; j < f.count; j++) {
        const head = (f.phase + j / f.count + time * speed) % 1
        for (let tr = 0; tr < TRAIL; tr++, fi++) {
          const t = Math.max(0, head - tr * 0.016)
          bez(a, c, b, t, F, fi * 3)
          const glow = fa * Math.sin(t * Math.PI) * (1 - tr * 0.24)
          FC[fi * 3] = (f.color[0] * 0.6 + 0.4) * glow; FC[fi * 3 + 1] = (f.color[1] * 0.6 + 0.4) * glow; FC[fi * 3 + 2] = (f.color[2] * 0.6 + 0.4) * glow
        }
      }
    }
    lp.needsUpdate = true; lc.needsUpdate = true; fp.needsUpdate = true; fc.needsUpdate = true
    this.lineGeo.setDrawRange(0, li)
    this.flowGeo.setDrawRange(0, fi)
  }

  // ================= 选中 / 悬停 =================

  private applyStates() {
    const st = this.nodeGeo.getAttribute('aState') as THREE.BufferAttribute | undefined
    if (!st) return
    const arr = st.array as Float32Array
    const sel = this.selected, hov = this.hovered
    const near = sel ? this.neighbors.get(sel) : null
    const inPath = this.path ? new Set(this.path) : null
    for (const [id, i] of this.info) {
      let v = 1
      if (inPath) v = inPath.has(id) ? 2 : 0
      else if (sel) v = id === sel ? 2 : near?.has(id) ? 1 : 0
      // 时间：还没发生的变成虚影，正在发生的点亮
      if (this.future.has(id)) v = 0
      else if (this.present.has(id) && !inPath && !sel) v = 2
      if (hov && id === hov) v = 2
      arr.fill(v, i.start, i.start + i.count)
    }
    st.needsUpdate = true
  }

  /** 屏幕坐标下最近的条目 */
  private pick(x: number, y: number): string {
    const w = this.container.clientWidth, h = this.container.clientHeight
    const focal = h / 2 / Math.tan((this.camera.fov * Math.PI) / 360)
    const v = new THREE.Vector3()
    let best = '', bestD = Infinity
    for (const [id, i] of this.info) {
      const p = this.anchorNow(i)
      v.set(p[0], p[1], p[2])
      const depth = v.clone().applyMatrix4(this.camera.matrixWorldInverse).z
      if (depth > -1) continue
      v.project(this.camera)
      const sx = (v.x * 0.5 + 0.5) * w, sy = (-v.y * 0.5 + 0.5) * h
      const rw = id === this.focusId && this.portraitV > 0.5 ? (this.nodeMat.uniforms.uPortraitSize!.value as number) : i.radius * 1.1
      const rpx = Math.max(10, (rw * focal) / -depth)
      const d = Math.hypot(sx - x, sy - y)
      if (d < rpx && d - rpx < bestD) { bestD = d - rpx; best = id }
    }
    return best
  }

  private onDown = (e: PointerEvent) => {
    this.down = { x: e.clientX, y: e.clientY }
    // 开场动画中点一下直接跳过（这一下不算点选条目）
    if (this.intro !== 'none') { this.finishIntro(); this.down = null }
  }
  private onUp = (e: PointerEvent) => {
    const d = this.down
    this.down = null
    if (!d || Math.hypot(e.clientX - d.x, e.clientY - d.y) > 5 || e.button !== 0) return
    const r = this.container.getBoundingClientRect()
    this.opts.onSelect(this.pick(e.clientX - r.left, e.clientY - r.top))
  }
  private onMove = (e: PointerEvent) => {
    if (this.down || this.intro !== 'none' || e.buttons) return
    const r = this.container.getBoundingClientRect()
    const id = this.pick(e.clientX - r.left, e.clientY - r.top)
    if (id !== this.hovered) {
      this.hovered = id
      this.opts.onHover?.(id)
      this.renderer.domElement.style.cursor = id ? 'pointer' : ''
      this.applyStates()
    }
  }
  private onLeave = () => { if (this.hovered) { this.hovered = ''; this.applyStates() } }

  // ================= 粒子画像：聚焦条目的粒子排成它的封面 =================

  private ensurePortrait(id: string) {
    const url = this.info.get(id)?.node.cover
    if (!url) return
    const cached = this.portraits.get(id)
    if (cached === 'loading' || cached === 'failed') return
    if (cached) { this.applyPortrait(id, cached); return }
    this.portraits.set(id, 'loading')
    const img = new Image()
    img.crossOrigin = 'anonymous'
    img.decoding = 'async'
    img.onload = () => {
      try {
        const data = samplePortrait(img)
        if (!data) { this.portraits.set(id, 'failed'); return }
        this.portraits.set(id, data)
        if (this.focusId === id && !this.disposed) this.applyPortrait(id, data)
      } catch { this.portraits.set(id, 'failed') } // 图片不允许跨域读取时只能放弃画像
    }
    img.onerror = () => this.portraits.set(id, 'failed')
    img.src = url
  }

  private applyPortrait(id: string, data: PortraitData) {
    const i = this.info.get(id)
    if (!i) return
    const img = this.nodeGeo.getAttribute('aImg') as THREE.BufferAttribute
    const col = this.nodeGeo.getAttribute('aImgCol') as THREE.BufferAttribute
    const A = img.array as Float32Array, C = col.array as Float32Array
    const r = rng(id + ':img')
    // 暗色主题里太暗的像素看不见、亮色主题里太亮的像素看不见：只用看得见的像素
    const idx: number[] = []
    for (let p = 0; p < data.n; p++) { const lum = data.pts[p * 6 + 5]!; if (this.light ? lum < 0.93 : lum >= 0.07) idx.push(p) }
    if (idx.length < 40) return
    // 暗色主题是叠加发光：每个像素平均分到的粒子越多，单个粒子越暗，整体亮度接近原图
    const overlap = i.count * 0.00029 * (idx.length / data.n + 0.25)
    const k = this.light ? 1 : Math.max(0.3, Math.min(1, 2.2 / Math.max(1, overlap)))
    for (let j = 0; j < i.count; j++) {
      const p = idx[Math.floor(r() * idx.length)]! * 6, q = (i.start + j) * 3
      A[q] = data.pts[p]! + (r() - 0.5) * 0.012
      A[q + 1] = data.pts[p + 1]! + (r() - 0.5) * 0.012
      A[q + 2] = 1
      C[q] = data.pts[p + 2]! * k; C[q + 1] = data.pts[p + 3]! * k; C[q + 2] = data.pts[p + 4]! * k
    }
    img.needsUpdate = true
    col.needsUpdate = true
    this.portraitReady = id
    this.opts.onPortrait?.(true)
  }

  // ================= 镜头 =================

  private flyTo(pos: THREE.Vector3, target: THREE.Vector3, dur = 1.1) {
    if (this.opts.reducedMotion) { this.camera.position.copy(pos); this.controls.target.copy(target); return }
    this.flyAnim = { from: this.camera.position.clone(), to: pos, tFrom: this.controls.target.clone(), tTo: target, start: (performance.now() - this.t0) / 1000, dur }
  }

  // ================= 开场：粒子聚成世界名字 =================

  /** 第一次显示数据。有世界名时先聚成文字 */
  start(nodes: PWNode[], links: PWLink[], focusId: string) {
    this.nodes = nodes
    this.links = links
    this.focusId = nodes.some(n => n.id === focusId) ? focusId : ''
    this.selected = this.focusId
    this.signature = this.sigOf(nodes, links, focusId)
    const text = (this.opts.introText ?? '').trim()
    if (text && nodes.length && !this.opts.reducedMotion && !this.focusId) {
      this.intro = 'text'
      this.rebuild(true, text.slice(0, 14))
      this.camera.position.set(0, 0, this.layoutRadius * 4.2)
      this.controls.target.set(0, 0, 0)
      this.controls.autoRotate = false
    } else {
      this.rebuild(true)
      this.resetCamera('all', true)
    }
  }

  private finishIntro() {
    if (this.intro === 'none') return
    this.intro = 'none'
    const p = this.pending
    this.pending = null
    if (p) { this.nodes = p.nodes; this.links = p.links; this.focusId = p.nodes.some(n => n.id === this.focusId) ? this.focusId : '' }
    this.rebuild(true)
    this.controls.autoRotate = this.wantRotate
    this.resetCamera('all', true)
  }

  // ================= 每一帧 =================

  private tick = () => {
    if (this.disposed) return
    this.frame = requestAnimationFrame(this.tick)
    this.frameCount++
    const nowMs = performance.now()
    const dt = Math.min(0.05, (nowMs - this.last) / 1000)
    this.last = nowMs
    const time = (nowMs - this.t0) / 1000
    if (this.morph < 1) this.morph = Math.min(1, this.morph + dt / this.morphDur)
    if (this.intro === 'text' && this.morph >= 1) { this.intro = 'hold'; this.introAt = time }
    if (this.intro === 'hold' && time - this.introAt > 1.1) this.finishIntro()
    // 选中的条目「绽开」
    const bloomTarget = this.selected && !this.focusId ? 0.55 : this.selected ? 0.35 : 0
    this.bloom += (bloomTarget - this.bloom) * Math.min(1, dt * 4)
    // 粒子画像：聚焦的变形基本完成后再展开
    const pTarget = this.portraitReady && this.portraitReady === this.focusId && this.portraitWanted && this.morph > 0.85 ? 1 : 0
    if (this.portraitV !== pTarget) this.portraitV = pTarget > this.portraitV ? Math.min(1, this.portraitV + dt / (this.opts.reducedMotion ? 0.3 : 1.8)) : Math.max(0, this.portraitV - dt / 1.2)
    if (this.flyAnim) {
      const f = this.flyAnim
      const t = Math.min(1, (time - f.start) / f.dur), e = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2
      this.camera.position.lerpVectors(f.from, f.to, e)
      this.controls.target.lerpVectors(f.tFrom, f.tTo, e)
      if (t >= 1) this.flyAnim = null
    }
    this.controls.update()
    this.camera.updateMatrixWorld()
    this.camRight.setFromMatrixColumn(this.camera.matrixWorld, 0)
    this.camUp.setFromMatrixColumn(this.camera.matrixWorld, 1)
    const u = this.nodeMat.uniforms
    u.uMorph!.value = this.morph
    u.uTime!.value = time
    u.uBloom!.value = this.bloom
    u.uPortrait!.value = this.portraitV
    u.uFadeDist!.value = Math.max(200, this.camera.position.distanceTo(this.controls.target) * 1.15)
    this.starMat.uniforms.uTime!.value = time
    this.guideMat.uniforms.uAlpha!.value = (this.intro !== 'none' ? 0 : ease(Math.min(1, Math.max(0, (this.morph - 0.55) / 0.45)))) * (this.selected || this.path ? 0.3 : 1)
    this.updateLinks(time)
    this.updateWave(time, this.guideMat.uniforms.uAlpha!.value as number)
    this.updateHalos(time)
    this.renderer.render(this.scene, this.camera)
    if ((this.frameCount & 1) === 0) this.updateLabels()
  }

  // ================= 标签：条目名、分组标题、关系名 =================

  private project(p: V3, w: number, h: number) {
    const v = new THREE.Vector3(p[0], p[1], p[2])
    const depth = v.clone().applyMatrix4(this.camera.matrixWorldInverse).z
    if (depth > -1) return null
    v.project(this.camera)
    return { x: (v.x * 0.5 + 0.5) * w, y: (-v.y * 0.5 + 0.5) * h, z: -depth }
  }

  private updateLabels() {
    const w = this.container.clientWidth, h = this.container.clientHeight
    const show = this.intro === 'none' && this.morph > 0.7
    const typeLabel = this.opts.typeLabel ?? ((t: string) => t)
    const pts: { id: string; x: number; y: number; z: number; width: number; priority?: boolean }[] = []
    if (show) {
      const sel = this.selected, near = sel ? this.neighbors.get(sel) : null
      const inPath = this.path ? new Set(this.path) : null
      const ranked = [...this.info.values()].sort((a, b) => b.degree - a.degree)
      const top = new Set(ranked.slice(0, Math.min(40, Math.max(8, Math.round(ranked.length * 0.25)))).map(i => i.node.id))
      for (const i of ranked) {
        const id = i.node.id
        const important = inPath ? inPath.has(id) : id === sel || id === this.hovered || this.present.has(id) || (this.showNames && !this.future.has(id) && (!!near?.has(id) || (!sel && top.has(id))))
        if (!important && id !== this.hovered) continue
        const p = this.anchorNow(i)
        const lift = id === this.focusId && this.portraitV > 0.3 ? (this.nodeMat.uniforms.uPortraitSize!.value as number) * 1.08 : i.radius * 1.15
        const s = this.project([p[0], p[1] + lift, p[2]], w, h)
        if (!s) continue
        const big = id === sel || id === this.hovered || !!inPath?.has(id) || this.present.has(id)
        pts.push({ id, x: s.x, y: s.y + 30, z: s.z, width: Math.min(260, i.node.title.length * 13 + 26 + (big ? 80 : 0)), priority: big })
      }
    }
    const boxes = placeGraphLabels(pts, w, h)
    while (this.labelPool.length < boxes.length) {
      const b = document.createElement('button')
      b.type = 'button'
      b.className = 'pw-label'
      b.addEventListener('click', () => { const id = b.dataset.id; if (id) this.opts.onSelect(id) })
      this.labelLayer.appendChild(b)
      this.labelPool.push(b)
    }
    this.labelPool.forEach((b, k) => {
      const box = boxes[k]
      if (!box) { b.style.display = 'none'; return }
      const i = this.info.get(box.id)!
      const big = !!box.priority
      const key = `${box.id}|${big ? 1 : 0}`
      if (b.dataset.key !== key) {
        b.dataset.key = key
        b.dataset.id = box.id
        b.style.borderLeftColor = i.node.color
        b.replaceChildren(document.createTextNode(i.node.title))
        if (big) {
          const sub = document.createElement('small')
          sub.textContent = `${typeLabel(i.node.type)} · ${i.degree} 条关系`
          b.appendChild(sub)
        }
      }
      b.style.display = ''
      b.classList.toggle('on', big)
      b.style.transform = `translate(${Math.round(box.left)}px, ${Math.round(box.top)}px)`
      b.style.maxWidth = `${box.width}px`
    })
    this.updateGroupLabels(show, w, h)
    this.updateEdgeLabels(show, w, h)
  }

  private updateGroupLabels(show: boolean, w: number, h: number) {
    const list = show && !this.path && !this.focusId ? this.groups : []
    while (this.groupPool.length < list.length) {
      const d = document.createElement('div')
      d.className = 'pw-group'
      this.labelLayer.appendChild(d)
      this.groupPool.push(d)
    }
    this.groupPool.forEach((d, k) => {
      const g = list[k]
      const s = g ? this.project(g.pos, w, h) : null
      if (!g || !s || s.x < -100 || s.x > w + 100 || s.y < -40 || s.y > h + 40) { d.style.display = 'none'; return }
      const key = `${g.text}|${g.sub ?? ''}|${g.color}`
      if (d.dataset.key !== key) {
        d.dataset.key = key
        d.style.setProperty('--g', g.color)
        const t = document.createElement('b'); t.textContent = g.text
        d.replaceChildren(t)
        if (g.sub) { const sub = document.createElement('small'); sub.textContent = g.sub; d.appendChild(sub) }
      }
      d.style.display = ''
      d.classList.toggle('below', !!g.below)
      d.style.transform = `translate(${Math.round(s.x)}px, ${Math.round(s.y)}px) translate(-50%, ${g.below ? '0' : '-100%'})`
      d.style.opacity = String(Math.max(0.35, Math.min(1, 1.6 - s.z / (this.camera.position.distanceTo(this.controls.target) * 1.4))))
    })
  }

  /** 关系名：聚焦时显示和它相连的关系，路径模式显示路径上的关系 */
  private updateEdgeLabels(show: boolean, w: number, h: number) {
    const list: FlowSpec[] = []
    if (show) {
      if (this.path) {
        for (let i = 1; i < this.path.length; i++) {
          const a = this.path[i - 1], b = this.path[i]
          const f = this.flowSpec.find(x => (x.a === a && x.b === b) || (x.a === b && x.b === a))
          if (f) list.push(f)
        }
      } else if (this.selected) {
        for (const f of this.flowSpec) if ((f.a === this.selected || f.b === this.selected) && list.length < 24) list.push(f)
      }
    }
    while (this.edgePool.length < list.length) {
      const s = document.createElement('span')
      s.className = 'pw-edge'
      this.labelLayer.appendChild(s)
      this.edgePool.push(s)
    }
    this.edgePool.forEach((el, k) => {
      const f = list[k]
      const p = f ? this.project(f.mid, w, h) : null
      if (!f || !p || !f.label) { el.style.display = 'none'; return }
      if (el.textContent !== f.label) el.textContent = f.label
      el.style.setProperty('--e', `rgb(${f.color.map(c => Math.round(c * 255)).join(',')})`)
      el.style.display = ''
      el.style.transform = `translate(${Math.round(p.x)}px, ${Math.round(p.y)}px) translate(-50%, -50%)`
    })
  }

  // ================= 其他 =================

  private makeStars() {
    const n = 2600
    const pos = new Float32Array(n * 3), col = new Float32Array(n * 3), size = new Float32Array(n), seed = new Float32Array(n)
    const r = rng('stars')
    for (let i = 0; i < n; i++) {
      const p = fib(i, n), d = 2600 + r() * 3000
      pos[i * 3] = p[0] * d; pos[i * 3 + 1] = p[1] * d; pos[i * 3 + 2] = p[2] * d
      const warm = r()
      col[i * 3] = 0.55 + warm * 0.35; col[i * 3 + 1] = 0.62 + r() * 0.2; col[i * 3 + 2] = 0.9 - warm * 0.3
      size[i] = 3 + r() * 9; seed[i] = r()
    }
    const g = new THREE.BufferGeometry()
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3))
    g.setAttribute('aColor', new THREE.BufferAttribute(col, 3))
    g.setAttribute('aSize', new THREE.BufferAttribute(size, 1))
    g.setAttribute('aSeed', new THREE.BufferAttribute(seed, 1))
    const p = new THREE.Points(g, this.starMat)
    p.frustumCulled = false
    return p
  }

  private updateScale() {
    const h = Math.max(1, this.container.clientHeight)
    const focal = (h / 2 / Math.tan((this.camera.fov * Math.PI) / 360)) * this.renderer.getPixelRatio()
    for (const m of [this.nodeMat, this.flowMat, this.starMat, this.haloMat]) m.uniforms.uScale!.value = focal
  }

  resize() {
    const w = Math.max(1, this.container.clientWidth), h = Math.max(1, this.container.clientHeight)
    this.renderer.setSize(w, h)
    const inset = Math.min(this.insetRight, w * 0.6)
    if (inset > 0) {
      // 画一个更宽的视野，只显示其中靠右的一块：视野中心就落在没被挡住的左边区域中间
      this.camera.aspect = (w + inset) / h
      this.camera.setViewOffset(w + inset, h, inset, 0, w, h)
    } else {
      this.camera.aspect = w / h
      this.camera.clearViewOffset()
    }
    this.camera.updateProjectionMatrix()
    this.updateScale()
  }
}

/** 星云天穹：一个从里面看的大球，贴上程序画的星云（随镜头转动，有身临其境感） */
function makeNebula() {
  const W = 1024, H = 512
  const c = document.createElement('canvas')
  c.width = W; c.height = H
  const x = c.getContext('2d')!
  // 底色是黑的：加法混合时不增加任何颜色，只有星云和星点会叠到页面背景上
  x.fillStyle = '#000000'
  x.fillRect(0, 0, W, H)
  const r = rng('nebula')
  // 银河带
  const band = x.createLinearGradient(0, H * 0.3, 0, H * 0.7)
  band.addColorStop(0, 'rgba(40,70,110,0)'); band.addColorStop(0.5, 'rgba(60,95,140,0.10)'); band.addColorStop(1, 'rgba(40,70,110,0)')
  x.fillStyle = band
  x.fillRect(0, 0, W, H)
  const palette = [[45, 212, 191], [99, 102, 241], [59, 130, 246], [139, 92, 246], [236, 72, 153], [14, 165, 233]]
  x.globalCompositeOperation = 'lighter'
  for (let i = 0; i < 26; i++) {
    const col = palette[Math.floor(r() * palette.length)]!
    const cx = r() * W, cy = H * (0.5 + (r() - 0.5) * (r() < 0.7 ? 0.35 : 0.8)), rad = 50 + r() * 190, a = 0.035 + r() * 0.07
    for (const dx of [-W, 0, W]) {
      const g = x.createRadialGradient(cx + dx, cy, 0, cx + dx, cy, rad)
      g.addColorStop(0, `rgba(${col[0]},${col[1]},${col[2]},${a})`)
      g.addColorStop(0.5, `rgba(${col[0]},${col[1]},${col[2]},${a * 0.4})`)
      g.addColorStop(1, `rgba(${col[0]},${col[1]},${col[2]},0)`)
      x.fillStyle = g
      x.fillRect(cx + dx - rad, cy - rad, rad * 2, rad * 2)
    }
  }
  // 远处的微小星点
  for (let i = 0; i < 2200; i++) {
    const b = Math.floor(120 + r() * 135)
    x.fillStyle = `rgba(${b},${b + 10 > 255 ? 255 : b + 10},255,${0.15 + r() * 0.5})`
    x.fillRect(r() * W, H * (0.5 + (r() - 0.5) * (r() < 0.6 ? 0.5 : 1)), r() < 0.08 ? 1.6 : 0.8, r() < 0.08 ? 1.6 : 0.8)
  }
  const tex = new THREE.CanvasTexture(c)
  tex.colorSpace = THREE.SRGBColorSpace
  const sphere = new THREE.Mesh(new THREE.SphereGeometry(9000, 48, 24), new THREE.ShaderMaterial({
    vertexShader: NEBULA_VERT, fragmentShader: NEBULA_FRAG, uniforms: { uMap: { value: tex }, uGain: { value: 1 } },
    side: THREE.BackSide, transparent: true, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending, premultipliedAlpha: true,
  }))
  sphere.renderOrder = -1
  sphere.rotation.z = 0.35
  return sphere
}

/** 封面图取样：缩到 180 以内，去掉透明像素，得到 x, y, r, g, b, 亮度 */
function samplePortrait(img: HTMLImageElement): PortraitData | null {
  const w0 = img.naturalWidth, h0 = img.naturalHeight
  if (!w0 || !h0) return null
  const s = 180 / Math.max(w0, h0)
  const w = Math.max(1, Math.round(w0 * Math.min(1, s))), h = Math.max(1, Math.round(h0 * Math.min(1, s)))
  const c = document.createElement('canvas')
  c.width = w; c.height = h
  const x = c.getContext('2d', { willReadFrequently: true })!
  x.drawImage(img, 0, 0, w, h)
  const d = x.getImageData(0, 0, w, h).data // 跨域不允许时这里会抛错
  const out: number[] = []
  const half = Math.max(w, h) / 2
  for (let py = 0; py < h; py++) for (let px = 0; px < w; px++) {
    const q = (py * w + px) * 4
    if (d[q + 3]! < 60) continue
    const r = d[q]! / 255, g = d[q + 1]! / 255, b = d[q + 2]! / 255
    const lum = 0.299 * r + 0.587 * g + 0.114 * b
    out.push((px - w / 2) / half, -(py - h / 2) / half, r, g, b, lum)
  }
  if (out.length < 6 * 60) return null
  return { pts: new Float32Array(out), n: out.length / 6 }
}

/** 把文字画到画布上取样，得到 count 个点（三维，文字在 xy 平面上，宽度约 width） */
function textPoints(text: string, count: number, width: number): V3[] {
  const c = document.createElement('canvas')
  const fs = 120
  const ctx = c.getContext('2d', { willReadFrequently: true })!
  ctx.font = `900 ${fs}px "Noto Sans SC", "PingFang SC", "Microsoft YaHei", sans-serif`
  const tw = Math.ceil(ctx.measureText(text).width) + 20
  c.width = tw; c.height = fs + 40
  ctx.font = `900 ${fs}px "Noto Sans SC", "PingFang SC", "Microsoft YaHei", sans-serif`
  ctx.fillStyle = '#fff'
  ctx.textBaseline = 'middle'
  ctx.fillText(text, 10, c.height / 2)
  const data = ctx.getImageData(0, 0, c.width, c.height).data
  const filled: number[] = []
  for (let y = 0; y < c.height; y += 2) for (let x = 0; x < c.width; x += 2) if (data[(y * c.width + x) * 4 + 3]! > 128) filled.push(x, y)
  const scale = width / Math.max(1, c.width)
  const r = rng(text)
  const out: V3[] = []
  const m = filled.length / 2
  for (let i = 0; i < count; i++) {
    if (!m) { out.push([0, 0, 0]); continue }
    const k = Math.floor(r() * m)
    const x = filled[k * 2]! + r() * 2, y = filled[k * 2 + 1]! + r() * 2
    out.push([(x - c.width / 2) * scale, -(y - c.height / 2) * scale, (r() - 0.5) * 6])
  }
  return out
}
