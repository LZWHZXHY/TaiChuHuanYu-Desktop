<template>
  <div class="doc-view" :class="'mode-' + mode">
    <article class="doc">
      <!-- 顶部：名称、类型、别名、属性、标签在左，封面在右（像 vvd） -->
      <header class="doc-hero" :class="{ 'has-cover': card.coverImage, 'has-sigil': showSigil }" :style="{ '--type-c': typeColor }">
        <div class="hero-main studio-heading">
          <h1 class="doc-title">{{ card.title }}<span v-if="showBlame && notes.title" class="blame" :title="notes.title.title">{{ notes.title.text }}</span></h1>
          <div class="doc-meta">
            <span class="doc-type"><i aria-hidden="true">{{ typeIcon }}</i>{{ getTypeLabel(card.type) }}</span>
            <span class="dot">·</span>
            <span>更新于 {{ formatDate(card.updatedAt) }}</span>
          </div>
          <!-- 来自柴圈 OC《xxx》/ 已发布成柴圈 OC《xxx》（只在条目页显示，弹窗和嵌入里不显示） -->
          <CardOcBadge v-if="isPage" :card-id="card.id" />
          <div v-if="aliases.length" class="hero-line">
            <span class="hl-label" aria-hidden="true">⌘</span><span class="hl-name">别名</span>
            <span v-for="a in aliases" :key="a" class="alias">{{ a }}</span>
            <span v-if="showBlame && notes.aliases" class="blame" :title="notes.aliases.title">{{ notes.aliases.text }}</span>
          </div>
          <!-- 属性：两列紧凑排列 -->
          <dl v-if="props_.length" class="prop-grid">
            <div v-for="p in props_" :key="p.key" class="prop">
              <dt>{{ p.label }}</dt>
              <dd>
                <button v-if="p.cardId" type="button" class="content-ref" @click="$emit('select', p.cardId)">{{ p.value }}</button>
                <span v-else>{{ p.value }}</span>
                <span v-if="showBlame && notes[p.blameKey]" class="blame" :title="notes[p.blameKey].title">{{ notes[p.blameKey].text }}</span>
              </dd>
            </div>
          </dl>
          <div v-if="cardTags.length" class="hero-line">
            <span class="hl-label" aria-hidden="true">#</span><span class="hl-name">标签</span>
            <span v-for="tag in cardTags" :key="tag" class="tag">#{{ tag }}</span>
            <span v-if="showBlame && notes.tags" class="blame" :title="notes.tags.title">{{ notes.tags.text }}</span>
          </div>
          <!-- 谁改过这个条目 -->
          <div v-if="isPage && blame && blame.totalEdits" class="doc-credit">
            <component :is="canViewHistory ? 'button' : 'span'" :type="canViewHistory ? 'button' : undefined" class="credit-sum" :class="{ link: canViewHistory }"
              :title="(canViewHistory ? '点击查看这个条目的全部修改记录' : '') + (blame.trackedFromStart ? '' : '\n（只统计开始记录修改之后的改动）')"
              @click="canViewHistory && $emit('history')">
              <b>{{ blame.editors.length }}</b> 人编辑过 · 共 <b>{{ blame.totalEdits }}</b> 次修改
            </component>
            <span v-if="blame.createdBy" class="credit-item">由 {{ blame.createdBy }} 创建</span>
            <span class="credit-item editors">
              <span v-for="e in blame.editors.slice(0, 6)" :key="e.userId" :title="`${e.username ?? '已注销用户'}：改过 ${e.count} 次，最近一次 ${fullTime(e.lastAt)}`">{{ e.username ?? '已注销用户' }}<i>×{{ e.count }}</i></span>
              <span v-if="blame.editors.length > 6">等</span>
            </span>
            <button type="button" class="credit-toggle" :aria-pressed="showBlame" @click="showBlame = !showBlame">{{ showBlame ? '隐藏每项修改人' : '显示每项修改人' }}</button>
          </div>
          <div v-if="isPage && showBlame && headNotes.length" class="doc-credit head-notes">
            <span v-for="n in headNotes" :key="n.key" class="blame" :title="n.title"><em>{{ n.label }}</em>{{ n.text }}</span>
          </div>
        </div>
        <!-- 没有封面时右边放粒子徽记：条目类型的图形 ↔ 名字第一个字 -->
        <div v-if="showSigil" class="hero-sigil" aria-hidden="true">
          <ParticleMorph :shapes="sigilShapes" :interval="4400" :density="18" :fill="0.82" :max-particles="1500" :colors="[typeColor, '--signal-mint', '--accent-color']" interactive="parent" />
          <span class="sigil-code">{{ (card.type || 'card').toUpperCase() }}</span>
        </div>
        <div v-if="card.coverImage" class="hero-cover">
          <img :src="coverUrl" :alt="card.title" @click="openPreview([coverUrl])" />
        </div>
      </header>

      <!-- 区块：每行 1～2 块（左右并排） -->
      <div v-for="row in rows" :key="row.id" class="doc-row" :class="{ split: row.blocks.length > 1 }">
        <section v-for="block in row.blocks" :key="block.id" class="doc-section doc-block" :class="'k-' + block.kind">
          <h2 v-if="!block.hideTitle" class="section-title">
            {{ blockTitle(block) }}
            <span v-if="block.kind === 'embeds'" class="section-count">{{ embeddedCards.length }}</span>
            <span v-if="block.hidden" class="private-tag" title="对访客隐藏：只有创建者和协作者能看到">仅成员可见</span>
            <span v-if="showBlame && notes[blameKeyOf(block)]" class="blame" :title="notes[blameKeyOf(block)].title">{{ notes[blameKeyOf(block)].text }}</span>
          </h2>
          <!-- 正文 / 文本块 -->
          <!-- 正文 / 文本块：引用有两种：只显示名字（点了在右边看），或者单独一行显示全部内容 -->
          <template v-if="block.kind === 'description' || block.kind === 'text'">
            <template v-for="(seg, si) in splitEmbeds((block.kind === 'description' ? card.description : block.text) ?? '')" :key="si">
              <div v-if="seg.type === 'text'" class="prose" v-html="renderText(seg.text)" @click="onProseClick"></div>
              <CardRefEmbed v-else :card-id="seg.id" :project-id="projectId" :trail="[...(trail ?? []), card.id]" @peek="peek" @select="id => $emit('select', id)" />
            </template>
          </template>
          <!-- 图片 -->
          <div v-else-if="block.kind === 'media'" class="media" :class="{ single: (block.images?.length ?? 0) === 1 }">
            <img v-for="(img, idx) in block.images" :key="img + idx" :src="thumb(img)" :alt="`${blockTitle(block)} ${idx + 1}`" loading="lazy" @click="openPreview(block.images!, idx)" />
          </div>
          <div v-else-if="block.kind === 'gallery'" class="gallery">
            <img v-for="(img, idx) in card.galleryImages" :key="idx" :src="img" :alt="`图 ${idx + 1}`" loading="lazy" @click="openPreview(galleryImages, idx)" />
          </div>
          <!-- 地图 -->
          <MapBlock v-else-if="block.kind === 'map'" :image="block.image" :pins="block.pins" :project-id="projectId"
            :card-id="card.id" :block-id="block.id" :root-label="block.title?.trim() || card.title" @peek="peek" />
          <!-- 画布：点条目卡片在右边弹出，点图片看大图 -->
          <CanvasBlock v-else-if="block.kind === 'canvas'" :items="block.items" :links="block.links" :height="block.height" :project-id="projectId"
            @peek="peek" @preview="(urls, i) => openPreview(urls, i)" />
          <!-- 关系树：点条目节点在右边弹出 -->
          <TreeBlock v-else-if="block.kind === 'tree'" :nodes="block.nodes" :project-id="projectId" @peek="peek" />
          <!-- 能力面板（雷达图）：区块标题栏已经有标题，图里不再重复 -->
          <StatPanelChart v-else-if="block.kind === 'statpanel' && block.panel" :panel="block.panel" hide-title />
          <!-- 这个页面不认识的区块（新版网站加的）：提示刷新 -->
          <p v-else-if="block.kind === 'unknown'" class="empty-note">这里有一块新版网站才能显示的内容，刷新页面就能看到。</p>
          <!-- 参考来源 -->
          <SourcesBlock v-else-if="block.kind === 'sources'" :sources="block.sources" />
          <!-- 时间线 -->
          <TimelineAxis v-else-if="block.kind === 'timeline'" :events="card.timelineEvents" :project-id="projectId" @select="peek" />
          <!-- 引用设定 -->
          <div v-else-if="block.kind === 'embeds'" class="embedded-list">
            <EmbeddedCard v-for="item in embeddedCards" :key="item.id" :card="item"
              :context-label="card.contentBlocks?.find(b => b.cardId === item.id)?.contextLabel"
              :project-id="projectId" clickable @click="$emit('select', item.id)" />
          </div>
          <!-- 设定关系 -->
          <div v-else-if="block.kind === 'relations'" class="backlink-list chips">
            <button v-for="r in card.outRelations" :key="r.id" class="backlink-item" :style="{ '--tc': colorOfType(r.targetCardType) }" @click="$emit('select', r.targetCardId)">
              <span class="backlink-type">{{ r.relationType || '关联' }}</span>
              <span class="backlink-title">{{ r.targetCardTitle || store.getCardById(r.targetCardId, projectId)?.title || '已删除的条目' }}</span>
              <span class="backlink-arrow">→</span>
            </button>
          </div>
        </section>
      </div>
      <p v-if="!rows.length" class="empty-note">暂无描述</p>

      <!-- 被引用 -->
      <section v-if="isPage && backlinks.length" class="doc-section">
        <h2 class="section-title">
          被引用
          <span class="section-count">{{ backlinks.length }}</span>
        </h2>
        <div class="backlink-list">
          <button
            v-for="bk in backlinks"
            :key="bk.id"
            class="backlink-item"
            @click="$emit('select', bk.id)"
          >
            <span class="backlink-type">{{ getTypeLabel(bk.type) }}</span>
            <span class="backlink-title">{{ bk.title }}</span>
            <span class="backlink-arrow">→</span>
          </button>
        </div>
      </section>
      <!-- 编年史里提到它的事 -->
      <CardChronicleEvents v-if="isPage" :project-id="projectId" :card-id="card.id" />
    </article>
    <!-- 点引用名字：右边弹出那个条目 -->
    <CardPeek v-if="isPage && peekId" :card-id="peekId" :project-id="projectId" @close="peekId = null" @select="id => { peekId = null; $emit('select', id) }" @pane="id => { peekId = null; $emit('pane', id) }" />
    <el-image-viewer v-if="previewVisible" :url-list="previewImages" :initial-index="previewIndex" @close="closePreview" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { cardTypeShape, firstGlyph, type ParticleShape } from '@/utils/particleShapes'
import { CardTypeMeta } from '../card_type'
import { useWorldStore } from '@/stores/world'
import { cardSchemas } from '../card-schemas'
import type { CardType } from '../card_type'
import { ElImageViewer } from 'element-plus'
import type { CardDetail, WorldCardBlame } from '@/api/types/world'
import { worldApi } from '@/api/worldApi'
import EmbeddedCard from './EmbeddedCard.vue'
import TimelineAxis from './TimelineAxis.vue'
import MapBlock from './MapBlock.vue'
import SourcesBlock from './SourcesBlock.vue'
import CanvasBlock from './CanvasBlock.vue'
import TreeBlock from './TreeBlock.vue'
import StatPanelChart from '@/components/StatPanelChart.vue'
import CardChronicleEvents from './CardChronicleEvents.vue'
import CardRefEmbed from './CardRefEmbed.vue'
import CardPeek from './CardPeek.vue'
import CardOcBadge from './CardOcBadge.vue'
import { splitEmbeds } from '@/utils/worldEditorText'
import { blockTitle, resolveLayout, type LayoutBlock } from '@/utils/worldLayout'
import { renderCardDescription } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import { useCardPresentation } from '@/composables/世界观管理/useCardPresentation'
import { useCardPreload } from '@/composables/世界观管理/useCardPreload'
import { useCardBacklinks } from '@/composables/世界观管理/useCardBacklinks'
import { useImagePreview } from '@/composables/世界观管理/useImagePreview'
const props = withDefaults(defineProps<{
  card: CardDetail
  projectId: string
  canViewHistory?: boolean
  /** page = 条目页；peek = 右边弹出的详情；embed = 嵌在别的条目正文里 */
  mode?: 'page' | 'peek' | 'embed'
  /** 嵌入时外层已经展开过的条目（防止互相引用无限套娃） */
  trail?: string[]
}>(), { mode: 'page', trail: () => [] })
const isPage = computed(() => props.mode === 'page')
const store = useWorldStore()
const visibleAttributes = computed(() => (props.card.attributes || []).filter(attr => String(attr.value ?? '').trim()))
const aliases = computed(() => (props.card.aliases || []).filter(a => String(a).trim()))
// 顶部属性：类型基本信息 + 自定义属性
const props_ = computed(() => [
  ...contentFields.value.map(f => ({ key: 'f:' + f.key, label: f.label, value: f.value, cardId: f.cardId, blameKey: 'field:' + f.key })),
  ...visibleAttributes.value.map(a => ({ key: 'a:' + a.key, label: a.key, value: String(a.value), cardId: null as string | null, blameKey: 'attr:' + a.key.trim() })),
])

// 区块：只显示有内容的
const layout = computed(() => resolveLayout(props.card.layout, props.card))
function hasBlockContent(block: LayoutBlock) {
  switch (block.kind) {
    case 'description': return Boolean(props.card.description?.trim())
    case 'text': return Boolean(block.text?.trim())
    case 'media': return Boolean(block.images?.length)
    case 'map': return Boolean(block.image)
    case 'sources': return Boolean(block.sources?.some(s => s.title?.trim() || s.url?.trim()))
    case 'canvas': return Boolean(block.items?.length)
    case 'tree': return Boolean(block.nodes?.length)
    case 'statpanel': return Boolean(block.panel?.dims.length)
    case 'unknown': return true
    case 'gallery': return Boolean(props.card.galleryImages?.length)
    case 'timeline': return Boolean(props.card.timelineEvents?.length)
    case 'embeds': return embeddedCards.value.length > 0
    case 'relations': return Boolean(props.card.outRelations?.length)
  }
  return false
}
const rows = computed(() => layout.value.rows
  .map(row => ({ id: row.id, blocks: row.blocks.filter(hasBlockContent) }))
  .filter(row => row.blocks.length))
const renderText = (text?: string) => renderCardDescription(text ?? '', id => store.getCardById(id, props.projectId), 'document')
const thumb = (url: string) => withThumbnail(url, 1000)
const BLAME_KEYS: Record<string, string> = { description: 'description', gallery: 'gallery', timeline: 'timeline', embeds: 'blocks', relations: 'relations' }
const blameKeyOf = (block: LayoutBlock) => BLAME_KEYS[block.kind] ?? 'block:' + block.id
const contentFields = computed(() => {
  let content: Record<string, unknown>
  try { content = JSON.parse(props.card.content || '{}') } catch { return [] }
  if (!content || typeof content !== 'object' || Array.isArray(content)) return []
  return Object.entries(content).filter(([, value]) => value != null && String(value).trim() !== '').map(([key, value]) => {
    const field = cardSchemas[props.card.type as CardType]?.find(field => field.key === key)
    const cardId = field?.type === 'card-ref' && typeof value === 'string' ? value : null
    const text = cardId ? store.getCardById(cardId, props.projectId)?.title || '查看关联条目'
      : Array.isArray(value) ? value.join('、') : typeof value === 'object' ? JSON.stringify(value) : String(value)
    return { key, label: field?.label || key, value: text, cardId }
  })
})
const emit = defineEmits<{ select: [id: string]; history: []; peek: [id: string]; pane: [id: string] }>()
// 点引用名字：条目页自己在右边弹出；弹出层 / 嵌入里的再往外交给条目页
const peekId = ref<string | null>(null)
function peek(id: string) {
  if (isPage.value) peekId.value = id
  else emit('peek', id)
}
watch(() => props.card.id, () => { peekId.value = null })
const { embeddedIds, embeddedCards, cardTags, galleryImages, coverUrl } =
  useCardPresentation(() => props.card, () => props.projectId, 'document')
useCardPreload(() => props.projectId, embeddedIds)
const colorOfType = (type?: string | null) => CardTypeMeta[type as CardType]?.color ?? 'var(--accent-color)'
const typeColor = computed(() => colorOfType(props.card.type))
const typeIcon = computed(() => CardTypeMeta[props.card.type as CardType]?.icon ?? '◇')
const showSigil = computed(() => isPage.value && !props.card.coverImage)
/** 只在条目换了（类型 / 名字）时才换一组图形，避免每次重绘都重新聚拢 */
const sigilShapes = computed<ParticleShape[]>(() => [
  { kind: 'draw', draw: cardTypeShape(props.card.type) },
  { kind: 'text', text: firstGlyph(props.card.title), scale: 0.66 },
])
const { backlinks } = useCardBacklinks(() => props.projectId, () => isPage.value ? props.card.id : '')
const { visible: previewVisible, images: previewImages, index: previewIndex, open: openPreview, close: closePreview } = useImagePreview()
const formatDate = (value: string) => value ? new Date(value).toLocaleDateString('zh-CN') : ''

// ---------- 每一项是谁、什么时候改的 ----------
const BLAME_KEY = 'taichu.worldShowBlame'
const blame = ref<WorldCardBlame | null>(null)
const showBlame = ref(true)
try { showBlame.value = localStorage.getItem(BLAME_KEY) !== '0' } catch { /* 浏览器不让存就默认显示 */ }
watch(showBlame, v => { try { localStorage.setItem(BLAME_KEY, v ? '1' : '0') } catch { /* 忽略 */ } })

let blameVersion = 0
watch(() => [props.projectId, props.card.id, props.card.updatedAt] as const, async ([pid, cid]) => {
  const version = ++blameVersion
  if (!isPage.value) return
  try {
    const data = await worldApi.getCardBlame(pid, cid)
    if (version === blameVersion) blame.value = data
  } catch { if (version === blameVersion) blame.value = null }
}, { immediate: true })

const toDate = (v: string) => new Date(v.endsWith('Z') || /[+-]\d\d:\d\d$/.test(v) ? v : v + 'Z')
const fullTime = (v: string) => toDate(v).toLocaleString('zh-CN', { hour12: false })
function relTime(v: string) {
  const d = toDate(v)
  const diff = Date.now() - d.getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  if (diff < 30 * 86400000) return `${Math.floor(diff / 86400000)} 天前`
  return d.toLocaleDateString('zh-CN')
}
interface Note { text: string; title: string }
const notes = computed<Record<string, Note>>(() => {
  const out: Record<string, Note> = {}
  for (const [key, f] of Object.entries(blame.value?.fields ?? {})) {
    const who = f.username ?? '已注销用户'
    out[key] = {
      text: `${who} · ${relTime(f.at)}${f.count > 1 ? ` · ${f.count} 次` : ''}`,
      title: `最后由 ${who} 于 ${fullTime(f.at)} 修改\n一共改过 ${f.count} 次，${f.editors} 人改过`,
    }
  }
  return out
})
// 标题、类型以及正文里不单独显示的内容（别名、封面、时间线、关联）放在标题下面
const HEAD_FIELDS: [string, string][] = [['type', '类型'], ['cover', '封面'], ['layout', '区块排列']]
const headNotes = computed(() => HEAD_FIELDS.filter(([k]) => notes.value[k]).map(([key, label]) => ({ key, label, ...notes.value[key] })))

function onProseClick(event: MouseEvent) {
  const target = (event.target as HTMLElement).closest('[data-card-id]') as HTMLElement | null
  const id = target?.getAttribute('data-card-id')
  if (id) peek(id)
}
</script>

<style scoped>
.content-ref { border: 0; padding: 0; text-align: left; background: none; color: var(--accent-color); cursor: pointer; font: inherit; }
.attr-value { white-space: pre-wrap; overflow-wrap: anywhere; }
/* 修改人标注：淡淡的小字，不抢正文 */
.doc-credit { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 12px; margin-top: 10px; font-size: 12px; color: var(--text-secondary); }
.doc-credit b { color: var(--text-primary); font-weight: 600; }
.credit-sum { border: 0; padding: 0; background: none; color: inherit; font: inherit; }
.credit-sum.link { cursor: pointer; border-bottom: 1px dashed currentColor; }
.credit-sum.link:hover { color: var(--accent-color); }
.credit-item.editors { display: inline-flex; flex-wrap: wrap; gap: 4px 10px; }
.credit-item.editors i { font-style: normal; margin-left: 2px; opacity: .7; }
.credit-toggle { margin-left: auto; border: 1px solid var(--border-line); padding: 1px 8px; background: none; color: var(--text-secondary); font: inherit; cursor: pointer; }
.credit-toggle:hover { color: var(--accent-color); border-color: var(--accent-color); }
.head-notes { margin-top: 6px; }
.blame { font-size: 11px; font-weight: 400; letter-spacing: 0; text-transform: none; color: var(--text-secondary); opacity: .8; white-space: nowrap; cursor: help; }
.blame em { font-style: normal; margin-right: 4px; padding: 0 4px; border: 1px solid var(--border-line); }
.section-title .blame { margin-left: auto; }
.sec-blame { display: flex; justify-content: flex-end; margin-bottom: 6px; }
.attr-item .blame { align-self: center; }
* { box-sizing: border-box; }
.doc-view {
  padding: clamp(24px, 4vw, 48px) clamp(20px, 4vw, 40px) 32px;
}
.doc {
  max-width: 960px;
  margin: 0 auto;
}
/* 右边弹出 / 嵌在正文里：更紧凑 */
.doc-view.mode-peek, .doc-view.mode-embed { padding: 16px 18px 20px; }
.doc-view.mode-embed .doc, .doc-view.mode-peek .doc { max-width: none; }
.doc-view.mode-peek .doc-hero, .doc-view.mode-embed .doc-hero { margin-bottom: 18px; padding: 14px 16px; grid-template-columns: minmax(0, 1fr); border-top-width: 2px; }
.doc-view.mode-embed .doc-hero.has-cover { grid-template-columns: minmax(0, 1fr) 120px; }
/* 嵌入时名字和类型已经在外框标题栏上了，里面不再重复，也不再套一层框 */
.doc-view.mode-embed .doc-hero { border: 0; background: none; padding: 0; margin-bottom: 14px; }
.doc-view.mode-embed .doc-title, .doc-view.mode-embed .doc-meta { display: none; }
.doc-view.mode-embed .hero-main:not(:has(> :not(.doc-title):not(.doc-meta))) { display: none; }
.doc-view.mode-peek .doc-hero.has-cover .hero-cover { order: -1; max-width: 260px; }
.doc-view.mode-peek .doc-title, .doc-view.mode-embed .doc-title { font-size: 20px; }
.doc-view.mode-peek .doc-section, .doc-view.mode-embed .doc-section { margin-bottom: 20px; }
.doc-view.mode-peek .doc-row.split { grid-template-columns: minmax(0, 1fr); }
.doc-view.mode-peek .prop-grid { grid-template-columns: minmax(0, 1fr); }
.doc-view.mode-embed .prose, .doc-view.mode-peek .prose { font-size: 14px; line-height: 1.8; }
/* 顶部：左边名称属性，右边封面 */
.doc-hero { display: grid; grid-template-columns: minmax(0, 1fr); gap: 24px; margin-bottom: 36px; padding: 24px 28px; border: 1px solid var(--surface-edge, var(--border-line)); border-top: 3px solid var(--surface-color, var(--accent-color)); background: linear-gradient(120deg,var(--paper-card),var(--surface-wash,var(--bg-sub))); }
.doc-hero.has-cover { grid-template-columns: minmax(0, 1fr) minmax(160px, 260px); }
.hero-main { min-width: 0; display: flex; flex-direction: column; gap: 12px; }
.hero-cover { align-self: start; overflow: hidden; background: var(--bg-sub); cursor: zoom-in; }
.hero-cover img { width: 100%; display: block; max-height: 340px; object-fit: cover; }
.hero-line { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; font-size: 13px; }
.hl-label { width: 16px; text-align: center; color: var(--text-secondary); }
.hl-name { color: var(--text-secondary); margin-right: 4px; }
.alias { padding: 1px 8px; border: 1px solid var(--border-line); color: var(--text-primary); }
.prop-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px 28px; margin: 4px 0; }
.prop { display: grid; grid-template-columns: minmax(64px, auto) minmax(0, 1fr); gap: 12px; align-items: baseline; font-size: 14px; line-height: 1.6; padding-bottom: 6px; border-bottom: 1px dashed var(--border-line); }
.prop dt { color: var(--text-secondary); font-size: 13px; }
.prop dd { margin: 0; min-width: 0; color: var(--text-primary); white-space: pre-wrap; overflow-wrap: anywhere; }
.prop dd .blame { display: block; }
/* 区块行：两块时左右并排 */
.doc-row { display: grid; grid-template-columns: minmax(0, 1fr); gap: 32px; }
.doc-row.split { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.doc-block { min-width: 0; }
.private-tag { font-size: 11px; font-weight: 400; letter-spacing: 0; padding: 0 6px; border: 1px dashed var(--border-line); color: var(--text-secondary); }
.media { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; }
.media.single { grid-template-columns: minmax(0, 1fr); }
.media img { width: 100%; display: block; object-fit: cover; aspect-ratio: 4 / 3; cursor: zoom-in; background: var(--bg-sub); }
.media.single img { aspect-ratio: auto; max-height: 520px; object-fit: contain; }
.doc-title {
  font-size: clamp(24px, 3vw, 32px);
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 10px;
  letter-spacing: -0.5px;
  line-height: 1.25;
  overflow-wrap: anywhere;
}
.doc-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}
.doc-meta .dot { color: var(--text-secondary); }
.doc-type {
  display: inline-flex; align-items: center; gap: 5px; padding: 1px 8px 1px 6px;
  color: var(--type-c, var(--accent-color)); font-weight: 600; font-size: 12px;
  background: color-mix(in srgb, var(--type-c, var(--accent-color)) 10%, transparent); border-left: 2px solid var(--type-c, var(--accent-color));
}
.doc-type i { font-style: normal; font-size: 12px; }
.doc-hero { position: relative; border-top-color: var(--type-c, var(--surface-color, var(--accent-color))) !important; }
.doc-hero::before { content: ''; position: absolute; right: -1px; top: -3px; width: 14px; height: 14px; background: linear-gradient(225deg, var(--type-c, var(--accent-color)) 50%, transparent 50%); pointer-events: none; }
.doc-hero.has-sigil { grid-template-columns: minmax(0, 1fr) clamp(150px, 22%, 220px); }
.hero-sigil { position: relative; align-self: stretch; min-height: 150px; border-left: 1px dashed var(--border-line);
  background: radial-gradient(circle at 50% 50%, color-mix(in srgb, var(--type-c, var(--signal-mint)) 12%, transparent), transparent 70%); }
.hero-sigil :deep(canvas) { position: absolute; inset: 0; width: 100%; height: 100%; }
.sigil-code { position: absolute; right: 6px; bottom: 4px; font: 9px/1 var(--font-mono, monospace); letter-spacing: .14em; color: var(--text-secondary); opacity: .7; }
.doc-cover {
  margin: 16px;
  border-radius: 0;
  overflow: hidden;
  cursor: zoom-in;
  background: var(--bg-sub);
}
.doc-cover img {
  width: 100%;
  height: auto;
  max-height: 240px;
  object-fit: contain;
  display: block;
  transition: opacity 0.15s;
}
.doc-cover:hover img { opacity: 0.95; }
.doc-section {
  margin-bottom: 40px;
}
.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 1px;
  margin: 0 0 14px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--surface-edge, var(--border-line));
  border-left: 3px solid var(--surface-color, var(--accent-color));
  padding-left: 12px;
}
.section-count {
  font-size: 11px;
  color: var(--text-secondary);
  font-weight: 400;
  letter-spacing: 0;
  text-transform: none;
}
.prose {
  font-size: 16px;
  line-height: 1.95;
  color: var(--text-primary);
  white-space: pre-wrap;
  word-break: break-word;
}
.prose :deep(.inline-ref) {
  display: inline-block;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 8px;
  border-radius: 0;
  font-size: 13px;
  cursor: pointer;
  margin: 0 2px;
  font-weight: 500;
}
.prose :deep(.inline-ref:hover) {
  background: var(--accent-soft);
}
.prose :deep(.inline-ref.broken) {
  background: var(--danger-soft);
  color: var(--danger);
}
.empty-note {
  font-size: 13px;
  color: var(--text-secondary);
  font-style: italic;
  margin: 0;
  padding: 4px 0;
}
.gallery {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
}
.gallery img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 0;
  cursor: zoom-in;
  background: var(--bg-sub);
  transition: opacity 0.15s;
}
.gallery img:hover { opacity: 0.9; }
.attr-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.attr-item {
  display: grid;
  grid-template-columns: 120px 1fr auto;
  gap: 24px;
  font-size: 14px;
  line-height: 1.6;
}
.attr-key {
  color: var(--text-secondary);
}
.attr-value {
  color: var(--text-primary);
  word-break: break-word;
  white-space: pre-wrap;
}
.embedded-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag {
  font-size: 13px;
  color: var(--accent-color);
  cursor: default;
  transition: opacity 0.15s;
}
.tag:hover { opacity: 0.7; }

.backlink-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
/* 设定关系区块：小卡片排成一行，颜色跟着目标条目类型 */
.backlink-list.chips { flex-direction: row; flex-wrap: wrap; gap: 6px; }
.backlink-list.chips .backlink-item { --tc: var(--accent-color); flex: 0 1 auto; max-width: 100%; padding: 4px 10px 4px 8px; border: 1px solid var(--border-line); border-left: 3px solid var(--tc); background: var(--paper-card); font-size: 13px; transition: border-color .15s, translate .15s, box-shadow .15s; }
.backlink-list.chips .backlink-item:hover { background: var(--paper-card); border-color: var(--tc); translate: 0 -1px; box-shadow: 2px 2px 0 color-mix(in srgb, var(--tc) 22%, transparent); }
.backlink-list.chips .backlink-title { flex: 0 1 auto; max-width: 12em; font-weight: 600; }
.backlink-list.chips .backlink-type { order: 2; font-family: var(--font-mono, monospace); }
.backlink-list.chips .backlink-arrow { display: none; }
.backlink-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  border: none;
  background: none;
  border-radius: 0;
  cursor: pointer;
  font-size: 14px;
  color: var(--text-primary);
  text-align: left;
  transition: background 0.15s;
}
.backlink-item:hover {
  background: var(--bg-sub);
}
.backlink-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 0 8px;
  border-radius: 0;
  flex-shrink: 0;
}
.backlink-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.backlink-arrow {
  color: var(--text-secondary);
}
@media (max-width: 820px) {
  .doc-view { padding: 24px 20px 80px; }
  .doc-title { font-size: 24px; }
  .attr-item {
    grid-template-columns: 1fr;
    gap: 2px;
  }
  .attr-item .blame { justify-self: start; }
  .credit-toggle { margin-left: 0; }
  .attr-key {
    font-size: 12px;
    color: var(--text-secondary);
  }
}
@media (max-width: 760px) { .doc-row.split { grid-template-columns: minmax(0, 1fr); } .prop-grid { grid-template-columns: minmax(0, 1fr); } }
@media(max-width:640px) { .doc-hero.has-sigil { grid-template-columns: minmax(0,1fr); }.hero-sigil { display: none; }.doc-hero.has-cover { grid-template-columns: minmax(0,1fr); }.hero-cover { order: -1; max-width: 320px; }.doc-hero { padding: 20px 18px; }.doc-meta { flex-wrap: wrap; } }
</style>
