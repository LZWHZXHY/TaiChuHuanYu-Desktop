import { computed, reactive, watch } from 'vue'
import { useUserStore } from '@/stores/user'
import { appearanceApi, type AppearanceView, type BgFocus, type BgKind, type BgMedia, type BgPreset, type BgSource, type SponsorInfo } from '@/api/appearance'

export type { BgFocus, BgKind, BgMedia, BgPreset, BgSource, SponsorInfo }

/**
 * 网站背景（只有自己能看到）。
 * default 站点默认的纯色坐标纸 / preset 官方背景（所有人可选）/ custom 自己上传的图片或视频（赞助特权）。
 * 选了背景后它替换掉纯色底色，网格、粒子、氛围线条照常叠在上面。
 * 设置存在服务器（换设备也在），本地缓存一份「实际显示的背景」，刷新时立即显示、不闪回纯色。
 * 另外：访问别人的个人主页时，主页主人设置的「整页背景」会临时盖在上面（setVisit），离开主页自动恢复。
 */
export interface SiteBackground {
  source: BgSource
  presetId: number | null
  /** 自己上传的素材（没在用时也保留） */
  custom: BgMedia | null
  /** 遮罩强度 0–90（%）：越高背景越淡、文字越清楚 */
  veil: number
  /** 模糊 0–20（px） */
  blur: number
  /** 是否保留坐标纸网格 */
  grid: boolean
  focus: BgFocus
  /** 左侧导航栏不透明度 30–100（%）：越小越透、越能看到背景 */
  nav: number
}

export const CUSTOM_FEATURE = 'custom-background'
export const DEFAULT_BACKGROUND: SiteBackground = { source: 'default', presetId: null, custom: null, veil: 55, blur: 0, grid: true, focus: 'center', nav: 80 }
export const FOCUS_POSITION: Record<BgFocus, string> = { center: 'center', top: 'center top', bottom: 'center bottom', left: 'left center', right: 'right center' }

const CACHE_KEY = 'taichu.siteBackground'

const state = reactive({
  saved: { ...DEFAULT_BACKGROUND } as SiteBackground,
  /** 设置页正在调整、还没保存时的预览 */
  preview: null as SiteBackground | null,
  /** 正在看的个人主页的整页背景（优先于自己的网站背景，自己的预览除外） */
  visit: null as { media: BgMedia; look: SiteBackground } | null,
  owner: '' as string,
  presets: [] as BgPreset[],
  presetsLoaded: false,
  sponsor: null as SponsorInfo | null,
  customLocked: false,
  /** 服务器数据回来之前，先用缓存里的「实际显示的背景」 */
  cachedMedia: null as BgMedia | null,
  remoteLoaded: false,
})

const isHttps = (v: unknown): v is string => typeof v === 'string' && /^https:\/\//i.test(v)

function normalizeMedia(raw: Partial<BgMedia> | null | undefined): BgMedia | null {
  if (!raw || !isHttps(raw.url)) return null
  const kind: BgKind = raw.kind === 'video' ? 'video' : 'image'
  return { url: raw.url, kind, poster: kind === 'video' && isHttps(raw.poster) ? raw.poster : null }
}

export function normalize(raw: Partial<SiteBackground> | null | undefined): SiteBackground {
  const v = { ...DEFAULT_BACKGROUND, ...(raw ?? {}) }
  const focus = (Object.keys(FOCUS_POSITION) as BgFocus[]).includes(v.focus) ? v.focus : 'center'
  const source: BgSource = v.source === 'preset' || v.source === 'custom' ? v.source : 'default'
  const presetId = typeof v.presetId === 'number' ? v.presetId : null
  return {
    source: source === 'preset' && presetId == null ? 'default' : source,
    presetId: source === 'preset' ? presetId : null,
    custom: normalizeMedia(v.custom),
    veil: Math.min(90, Math.max(0, Math.round(Number(v.veil) || 0))),
    blur: Math.min(20, Math.max(0, Math.round(Number(v.blur) || 0))),
    grid: v.grid !== false,
    focus,
    nav: Math.min(100, Math.max(30, Math.round(Number(v.nav) || 80))),
  }
}

export function canUseCustom(sponsor: SponsorInfo | null) {
  return Boolean(sponsor?.features?.[CUSTOM_FEATURE]?.unlocked)
}

/** 这套设置实际要显示的素材；null 表示默认坐标纸 */
export function resolveMedia(bg: SiteBackground, presets: BgPreset[], sponsor: SponsorInfo | null): BgMedia | null {
  if (bg.source === 'preset') {
    const p = presets.find(item => item.id === bg.presetId)
    return p ? { url: p.url, kind: p.kind, poster: p.poster ?? null } : null
  }
  if (bg.source === 'custom') return canUseCustom(sponsor) ? bg.custom : null
  return null
}

function readCache(owner: string): { value: SiteBackground; media: BgMedia | null } | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as { owner?: string; value?: Partial<SiteBackground>; media?: Partial<BgMedia> }
    return parsed.owner === owner ? { value: normalize(parsed.value), media: normalizeMedia(parsed.media) } : null
  } catch { return null }
}
function writeCache() {
  try {
    const media = state.remoteLoaded && state.presetsLoaded ? resolveMedia(state.saved, state.presets, state.sponsor) : state.cachedMedia
    if (state.remoteLoaded && state.presetsLoaded) state.cachedMedia = media
    if (!state.owner || !media) localStorage.removeItem(CACHE_KEY)
    else localStorage.setItem(CACHE_KEY, JSON.stringify({ owner: state.owner, value: state.saved, media }))
  } catch { /* 隐私模式等情况下忽略 */ }
}

function applyView(view: AppearanceView) {
  state.saved = normalize(view)
  state.sponsor = view.sponsor ?? null
  state.customLocked = Boolean(view.customLocked)
  state.remoteLoaded = true
  writeCache()
}

let loadVersion = 0
async function loadFor(owner: string) {
  const version = ++loadVersion
  state.owner = owner
  state.preview = null
  state.sponsor = null
  state.customLocked = false
  state.remoteLoaded = false
  if (!owner) {
    state.saved = { ...DEFAULT_BACKGROUND }
    state.cachedMedia = null
    return
  }
  const cached = readCache(owner)
  state.saved = cached?.value ?? { ...DEFAULT_BACKGROUND }
  state.cachedMedia = cached?.media ?? null
  try {
    const view = await appearanceApi.get()
    if (version === loadVersion && view) applyView(view)
  } catch { /* 后端未部署或网络失败时沿用缓存 */ }
}

export async function loadPresets(force = false) {
  if (state.presetsLoaded && !force) return state.presets
  try {
    const list = await appearanceApi.presets()
    state.presets = (Array.isArray(list) ? list : []).flatMap(p => {
      const m = normalizeMedia(p)
      return m ? [{ ...p, ...m }] : []
    })
    state.presetsLoaded = true
    writeCache()
  } catch { /* 没有官方背景时为空 */ }
  return state.presets
}

let started = false
/** 在 App.vue 里调用一次：跟随登录状态加载 / 清除背景 */
export function setupSiteBackground() {
  if (started) return
  started = true
  const user = useUserStore()
  watch(() => (user.isLoggedIn ? String(user.userId || '') : ''), owner => {
    void loadFor(owner)
    if (owner) void loadPresets()
  }, { immediate: true })
  // 导航栏不透明度：写成全站 CSS 变量（OperationTerminal 读取），调整时实时预览
  watch(() => (state.preview ?? state.saved).nav, nav => {
    if (typeof document !== 'undefined') document.documentElement.style.setProperty('--nav-opacity', String((nav ?? 80) / 100))
  }, { immediate: true })
}

export function useSiteBackground() {
  /** 当前实际显示的素材（有预览时显示预览；服务器数据回来前用缓存） */
  const media = computed<BgMedia | null>(() => {
    if (state.preview) return resolveMedia(state.preview, state.presets, state.sponsor)
    if (state.visit) return state.visit.media
    if (!state.remoteLoaded || (state.saved.source === 'preset' && !state.presetsLoaded)) return state.cachedMedia
    return resolveMedia(state.saved, state.presets, state.sponsor)
  })
  const active = computed(() => state.preview ?? state.visit?.look ?? state.saved)
  const style = computed(() => {
    const m = media.value
    if (!m) return null
    const bg = active.value
    const common = { filter: bg.blur ? `blur(${bg.blur}px)` : undefined, inset: bg.blur ? `-${bg.blur * 2}px` : '0' }
    return {
      kind: m.kind,
      src: m.url,
      poster: m.poster ?? undefined,
      image: { ...common, backgroundImage: `url(${JSON.stringify(m.url)})`, backgroundPosition: FOCUS_POSITION[bg.focus] },
      video: { ...common, objectPosition: FOCUS_POSITION[bg.focus], width: bg.blur ? `calc(100% + ${bg.blur * 4}px)` : '100%', height: bg.blur ? `calc(100% + ${bg.blur * 4}px)` : '100%' },
      veil: { '--bg-veil': `${bg.veil}%` } as Record<string, string>,
      grid: bg.grid,
    }
  })
  return {
    state,
    active,
    media,
    style,
    canCustom: computed(() => canUseCustom(state.sponsor)),
    loadPresets,
    setPreview: (value: SiteBackground | null) => { state.preview = value ? normalize(value) : null },
    /** 个人主页的整页背景：传 null 恢复自己的网站背景 */
    setVisit: (value: { url: string; kind: BgKind; poster: string | null; veil: number; blur: number; focus: BgFocus } | null) => {
      const media = value ? normalizeMedia(value) : null
      state.visit = media && value
        ? { media, look: normalize({ source: 'custom', custom: media, veil: value.veil, blur: value.blur, focus: value.focus, grid: false, nav: state.saved.nav }) }
        : null
    },
    async save(value: SiteBackground) {
      const v = normalize(value)
      const view = await appearanceApi.save({
        source: v.source, presetId: v.presetId, url: v.custom?.url ?? null, kind: v.custom?.kind ?? 'image', poster: v.custom?.poster ?? null,
        veil: v.veil, blur: v.blur, grid: v.grid, focus: v.focus, nav: v.nav,
      })
      applyView(view)
      state.preview = null
      return { ...state.saved }
    },
    async reset() {
      const view = await appearanceApi.reset()
      applyView(view)
      state.preview = null
    },
    /** 绑定爱发电订单后刷新特权状态 */
    setSponsor(info: SponsorInfo) {
      state.sponsor = info
      state.customLocked = state.saved.source === 'custom' && !canUseCustom(info)
      writeCache()
    },
  }
}
