// src/utils/serverTime.ts：解析后端给的时间
// 后端存的时间都是 UTC。现在后端输出时会带上 Z，但正好 00:00:00 的值（为了照顾“日期”字段）不带。
// 明确是“时刻”的字段（发布时间、到期时间、开始 / 结束时间……）用这个解析：没带时区的一律按 UTC 处理。
export function serverDate(value: string | number | Date | null | undefined): Date {
  if (value instanceof Date) return value
  if (typeof value === 'number') return new Date(value)
  if (!value) return new Date(NaN)
  const s = String(value).trim()
  // 只有日期（2026-10-30）或已经带时区（…Z / +08:00）的原样解析
  if (!/T\d/.test(s) || /(Z|[+-]\d{2}:?\d{2})$/i.test(s)) return new Date(s)
  return new Date(s + 'Z')
}
