// 研发报告的日志工具：排序、进度变化、把旧的长正文按小标题拆成一条条日志。
import type { ResearchLog } from '@/api/devProject'

/** 按日期从新到旧排序（没有日期的排最后，保持原有顺序）。 */
export function sortLogs<T extends { date?: string | null }>(logs: T[]): T[] {
  return logs
    .map((log, index) => ({ log, index }))
    .sort((a, b) => (b.log.date || '').localeCompare(a.log.date || '') || a.index - b.index)
    .map(item => item.log)
}

/** 每条日志相对上一条（更早的）有记录进度的日志，进度变化了多少。 */
export function progressDeltas(sortedNewestFirst: ResearchLog[]): Map<string, number> {
  const deltas = new Map<string, number>()
  for (let i = 0; i < sortedNewestFirst.length; i++) {
    const current = sortedNewestFirst[i]
    if (current.progress == null) continue
    const older = sortedNewestFirst.slice(i + 1).find(l => l.progress != null)
    if (older?.progress != null) deltas.set(current.id, current.progress - older.progress)
  }
  return deltas
}

export function latestLog(logs?: ResearchLog[] | null): ResearchLog | undefined {
  return logs?.length ? sortLogs(logs)[0] : undefined
}

/** 显示用日期：2026-10-28 → 2026 年 10 月 28 日 */
export function formatLogDate(value?: string | null) {
  if (!value) return ''
  const m = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(value)
  return m ? `${m[1]} 年 ${Number(m[2])} 月 ${Number(m[3])} 日` : value
}

/** 2026-10-28 → 2026-10，用于按月份跳转 */
export const monthOf = (value?: string | null) => value && /^\d{4}-\d{2}/.test(value) ? value.slice(0, 7) : '未注明日期'

// 从小标题里认出日期：2026-10-01、2026/10/1、2026.10.1、2026年10月1日、10月1日（默认今年）
function parseDate(text: string): string | null {
  const full = /(\d{4})\s*[-/.年]\s*(\d{1,2})\s*[-/.月]\s*(\d{1,2})/.exec(text)
  const pad = (n: string | number) => String(n).padStart(2, '0')
  if (full) return `${full[1]}-${pad(full[2])}-${pad(full[3])}`
  const short = /(\d{1,2})\s*月\s*(\d{1,2})\s*[日号]?/.exec(text)
  if (short) return `${new Date().getFullYear()}-${pad(short[1])}-${pad(short[2])}`
  return null
}

/**
 * 把一大段 Markdown 正文按 #、##、### 小标题拆开：
 * 第一个小标题之前的内容保留为项目介绍，之后每个小标题变成一条日志（标题里有日期就自动识别）。
 * 如果有带日期的小标题，就只按带日期的小标题拆，其他小标题留在当天的内容里，避免一天被拆碎。
 */
export function splitContentToLogs(content: string, newId: () => string): { intro: string; logs: ResearchLog[] } {
  const lines = content.replace(/\r\n/g, '\n').split('\n')
  const HEADING = /^#{1,3}\s+(.+?)\s*#*\s*$/
  const datedOnly = lines.some(line => { const h = HEADING.exec(line); return Boolean(h && parseDate(h[1])) })
  const intro: string[] = []
  const logs: ResearchLog[] = []
  let current: ResearchLog | null = null
  let body: string[] = []
  const flush = () => {
    if (current) { current.content = body.join('\n').trim(); logs.push(current) }
    body = []
  }
  for (const line of lines) {
    const heading = HEADING.exec(line)
    if (heading && (!datedOnly || parseDate(heading[1]))) {
      flush()
      const title = heading[1].trim()
      const date = parseDate(title)
      // 标题以日期开头时把日期去掉，只留下内容标题；没有日期的标题原样保留（例如「3D 模型」）
      const cleanTitle = date ? title.replace(/^[\s\d年月日号./-]+[:：\s]*/, '').trim() : title
      current = { id: newId(), date, title: cleanTitle || title, content: '', images: [], progress: null }
    } else if (current) {
      body.push(line)
    } else {
      intro.push(line)
    }
  }
  flush()
  return { intro: intro.join('\n').trim(), logs: sortLogs(logs) }
}
