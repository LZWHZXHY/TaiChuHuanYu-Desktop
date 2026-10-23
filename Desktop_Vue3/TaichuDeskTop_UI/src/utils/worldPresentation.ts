import { CardTypeMeta, type CardType, type ContentBlock } from '@/views/世界观管理/card_type'
import type { CardDetail, CardSummary } from '@/api/types/world'
import { toCdnUrl } from '@/utils/imageThumb'

export const cardTypeOrder = Object.keys(CardTypeMeta) as CardType[]
export const getCardTypeLabel = (type: string) => CardTypeMeta[type as CardType]?.label || type

export function parseStringArray(value: unknown): string[] {
  try {
    const parsed: unknown = typeof value === 'string' ? JSON.parse(value) : value
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string') : []
  } catch { return [] }
}

/** 封面兼容旧的 JSON 数组和直接存储的 URL，并统一走 CDN。 */
export function parseCoverImage(value?: string | null): string {
  if (!value) return ''
  const url = (() => { try { return parseStringArray(JSON.parse(value))[0] ?? '' } catch { return value } })()
  return toCdnUrl(url)
}

export function getEmbeddedCardIds(card?: Pick<CardDetail, 'contentBlocks' | 'description'> | null, includeDescription = false): string[] {
  const ids = new Set(card?.contentBlocks.map(block => block.cardId).filter(Boolean) ?? [])
  if (includeDescription) {
    for (const match of (card?.description ?? '').matchAll(/\{CARD:([^}]+)\}/g)) ids.add(match[1]!)
  }
  return [...ids]
}

export function getNestedBlocks(blocks: ContentBlock[], currentId: string | undefined,
  ancestors: string[], depth: number, maxDepth: number) {
  if (depth >= maxDepth - 1) return []
  return blocks.filter(block => block.cardId && block.cardId !== currentId && !ancestors.includes(block.cardId))
    .map(block => ({ ...block, blockId: block.id || `${block.cardId}-${block.order}` }))
}

export function groupWorldCards(cards: CardSummary[], search = '') {
  const query = search.trim().toLowerCase()
  const groups = new Map<string, CardSummary[]>(cardTypeOrder.map(type => [type, []]))
  for (const card of cards) {
    if (!query || card.title.toLowerCase().includes(query)) groups.get(card.type)?.push(card)
  }
  return [...groups].filter(([, cards]) => cards.length)
    .map(([type, cards]) => ({ type, label: getCardTypeLabel(type), cards }))
}

const escapeHtml = (text: string) => text.replace(/[&<>"']/g, char =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]!)

export function renderCardDescription(description: string, findCard: (id: string) => CardSummary | undefined,
  variant: 'document' | 'detail') {
  // 先整体转义正文，防止描述中的 HTML 被 v-html 当作标签执行（XSS），
  // 再替换 {CARD:id} token 为安全的可点击引用。token 不含 HTML 特殊字符，转义后仍可匹配。
  return escapeHtml(description).replace(/\{CARD:([^}]+)\}/g, (_, id: string) => {
    const card = findCard(id)
    if (!card) return '<span class="inline-ref broken">⚠️ 已删除的卡片</span>'
    const label = escapeHtml(getCardTypeLabel(card.type))
    const title = escapeHtml(card.title)
    const sep = variant === 'document' ? ' ' : ': '
    return `<a class="inline-ref" data-card-id="${escapeHtml(id)}">@${label}${sep}${title}</a>`
  })
}

export function formatWorldUpdatedAt(value: string, now = Date.now()) {
  if (!value) return ''
  const date = new Date(value)
  const seconds = Math.floor((now - date.getTime()) / 1000)
  if (seconds < 60) return '刚刚'
  if (seconds < 3600) return Math.floor(seconds / 60) + ' 分钟前'
  if (seconds < 86400) return Math.floor(seconds / 3600) + ' 小时前'
  if (seconds < 604800) return Math.floor(seconds / 86400) + ' 天前'
  return date.toLocaleDateString('zh-CN')
}
