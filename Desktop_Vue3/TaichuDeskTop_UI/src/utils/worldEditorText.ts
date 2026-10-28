import type { JSONContent } from '@tiptap/core'
import type { CardSummary } from '@/api/types/world'
import { getCardTypeLabel } from './worldPresentation'

// 名称只用于编辑器显示，持久化仍使用原有引用标记，重名或改名不会改变引用目标。
export function worldReferenceLabel(id: string, cards: CardSummary[]) {
  const card = cards.find(card => card.id === id)
  return card ? `@${getCardTypeLabel(card.type)} ${card.title}` : '@不可用的条目'
}

export function parseWorldEditorText(text: string, cards: CardSummary[]): JSONContent {
  return { type: 'doc', content: text.split(/\r?\n/).map(line => {
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
    if (node.type === 'worldReference') return `{CARD:${node.attrs?.id}}`
    if (node.type === 'hardBreak') return '\n'
    return (node.content ?? []).map(inline).join('')
  }
  return (doc.content ?? []).map(inline).join('\n')
}
