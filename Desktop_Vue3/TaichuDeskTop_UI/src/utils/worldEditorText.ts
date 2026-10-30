import type { JSONContent } from '@tiptap/core'
import type { CardSummary } from '@/api/types/world'
import { getCardTypeLabel } from './worldPresentation'

// 名称只用于编辑器显示，持久化仍使用原有引用标记，重名或改名不会改变引用目标。
export function worldReferenceLabel(id: string, cards: CardSummary[]) {
  const card = cards.find(card => card.id === id)
  return card ? `@${getCardTypeLabel(card.type)} ${card.title}` : '@不可用的条目'
}

/**
 * 引用有两种显示方式：
 * - 只显示名字：正文里的 {CARD:id}，点了在右边弹出那个条目的详细内容
 * - 显示全部内容：单独一行的 !{CARD:id}（和 Obsidian 的 ![[笔记]] 一样），阅读时直接把那个条目整个展开
 * 旧代码只认 {CARD:id}，看到 !{CARD:id} 也照样当普通引用，不会坏。
 */
export const EMBED_LINE = /^\s*!\{CARD:([^}]+)\}\s*$/

export type TextSegment = { type: 'text'; text: string } | { type: 'embed'; id: string }
/** 把正文拆成「普通文字」和「嵌入全文的条目」几段（阅读页用） */
export function splitEmbeds(text: string): TextSegment[] {
  const out: TextSegment[] = []
  let buffer: string[] = []
  const flush = () => { if (buffer.length) { out.push({ type: 'text', text: buffer.join('\n') }); buffer = [] } }
  for (const line of (text || '').split(/\r?\n/)) {
    const m = line.match(EMBED_LINE)
    if (m) { flush(); out.push({ type: 'embed', id: m[1]! }) }
    else buffer.push(line)
  }
  flush()
  // 嵌入前后的空行不用单独显示
  return out.filter(seg => seg.type === 'embed' || seg.text.trim() || out.length === 1)
}

export function parseWorldEditorText(text: string, cards: CardSummary[]): JSONContent {
  return { type: 'doc', content: text.split(/\r?\n/).map(line => {
    const embed = line.match(EMBED_LINE)
    if (embed) return { type: 'paragraph', content: [{ type: 'worldReference', attrs: { id: embed[1], label: worldReferenceLabel(embed[1]!, cards), mode: 'embed' } }] }
    const content: JSONContent[] = []
    let offset = 0
    for (const match of line.matchAll(/\{CARD:([^}]+)\}/g)) {
      if (match.index > offset) content.push({ type: 'text', text: line.slice(offset, match.index) })
      content.push({ type: 'worldReference', attrs: { id: match[1], label: worldReferenceLabel(match[1]!, cards) } })
      offset = match.index + match[0].length
    }
    if (offset < line.length) content.push({ type: 'text', text: line.slice(offset) })
    return { type: 'paragraph', content }
  }) }
}

export function serializeWorldEditorText(doc: JSONContent): string {
  const inline = (node: JSONContent): string => {
    if (node.type === 'text') return node.text ?? ''
    // 嵌入全文必须单独占一行：和文字挤在同一段里时，前后各换一行
    if (node.type === 'worldReference') return node.attrs?.mode === 'embed' ? `\n!{CARD:${node.attrs?.id}}\n` : `{CARD:${node.attrs?.id}}`
    if (node.type === 'hardBreak') return '\n'
    return (node.content ?? []).map(inline).join('')
  }
  return (doc.content ?? []).map(node => inline(node).replace(/^\n(?=!\{CARD:)/, '').replace(/(!\{CARD:[^}]+\})\n$/, '$1')).join('\n')
}
