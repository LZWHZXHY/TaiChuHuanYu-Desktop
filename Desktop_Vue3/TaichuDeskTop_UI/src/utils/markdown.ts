import { marked } from 'marked'
import DOMPurify from 'dompurify'

// Markdown → 安全 HTML（消毒防 XSS）
export function renderMarkdown(md: string): string {
  if (!md) return ''
  return DOMPurify.sanitize(marked.parse(md) as string)
}
