/**
 * 把一大段角色设定文本自动拆成属性（人设编辑器「粘贴整段导入」用）。
 * 规则（尽量贴近大家平时写设定的习惯）：
 * · 「姓名：林夜」「年龄: 17」这样的一行 → 一条属性；
 * · 「【背景故事】」「[能力]」「## 性格」「背景故事：」单独一行（冒号后面没内容）→ 下面的段落都算它的内容，直到下一个标题；
 * · 「性格：开朗」下一行紧接着（中间没空行）的文字，算它的续写；空一行之后的文字就不算了；
 * · 开头或中间没有标题的段落，统一放进一条「设定」；
 * · 内容短（不超过 40 字、只有一行）用短文本，否则用长文本。
 */
export interface ParsedAttr {
  key: string
  value: string
  type: 'short' | 'long'
}

const SHORT_MAX = 40
const KEY_MAX = 16

interface Draft { key: string; lines: string[]; heading: boolean; closed: boolean }

/** 一行是不是「标题 / 键：值」，是的话返回键和同一行后面的内容 */
function matchKey(line: string): { key: string; rest: string; heading: boolean } | null {
  const s = line.trim().replace(/^[-*•·]\s*/, '')
  let m = s.match(/^【([^】]{1,16})】\s*(.*)$/) ?? s.match(/^\[([^\]]{1,16})\]\s*(.*)$/)
  if (m) return { key: m[1]!.trim(), rest: m[2]!.trim(), heading: !m[2]!.trim() }
  m = s.match(/^#{1,6}\s*(.{1,16})$/)
  if (m) return { key: m[1]!.trim().replace(/[：:]$/, ''), rest: '', heading: true }
  m = s.match(/^([^：:]{1,16})[：:]\s*(.*)$/)
  if (m) {
    const key = m[1]!.trim()
    // 排除网址（https://…）、时间（12:30）、句子里带冒号的情况（键里有标点 / 空格太多）
    if (!key || /^https?$/i.test(key) || /^\d+$/.test(key) || /[，。！？,.!?；;、“”"（）()]/.test(key) || key.split(/\s+/).length > 3) return null
    if (key.length > KEY_MAX) return null
    // 「12:30」这种时间：冒号前后都是数字
    if (/\d$/.test(key) && /^\d/.test(m[2]!)) return null
    return { key, rest: m[2]!.trim(), heading: !m[2]!.trim() }
  }
  return null
}

export function parseCharacterText(text: string): ParsedAttr[] {
  const lines = text.replace(/\r\n?/g, '\n').split('\n')
  const drafts: Draft[] = []
  const loose: string[] = []
  let current: Draft | null = null
  let blankBefore = false

  for (const raw of lines) {
    const line = raw.replace(/\s+$/, '')
    if (!line.trim()) {
      blankBefore = true
      // 「键：值」一行写完、后面空了一行：这条结束，后面的文字不再算它的
      if (current && !current.heading) current.closed = true
      if (current?.heading && current.lines.length) current.lines.push('')
      continue
    }
    const k = matchKey(line)
    if (k) {
      current = { key: k.key, lines: k.rest ? [k.rest] : [], heading: k.heading, closed: false }
      drafts.push(current)
    } else if (current && !current.closed) {
      current.lines.push(line.trim())
    } else {
      if (blankBefore && loose.length) loose.push('')
      loose.push(line.trim())
      current = null
    }
    blankBefore = false
  }

  const out: ParsedAttr[] = []
  const tidy = (ls: string[]) => ls.join('\n').replace(/\n{3,}/g, '\n\n').trim()
  if (loose.length) {
    const v = tidy(loose)
    if (v) out.push({ key: '设定', value: v, type: 'long' })
  }
  for (const d of drafts) {
    const value = tidy(d.lines)
    if (!value) continue
    out.push({ key: d.key, value, type: value.includes('\n') || value.length > SHORT_MAX ? 'long' : 'short' })
  }
  return out
}
