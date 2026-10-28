import { renderMarkdown } from './markdown'

/** 与页面主题分离，保证夜间模式导出的纸张仍是白底深色字。 */
export function worldPrintHtml(title: string, markdown: string): string {
  const doc = document.implementation.createHTMLDocument(title)
  doc.documentElement.lang = 'zh-CN'
  const charset = doc.createElement('meta')
  charset.setAttribute('charset', 'utf-8')
  doc.head.prepend(charset)
  const base = doc.createElement('base')
  base.href = 'about:srcdoc'
  doc.head.append(base)
  const style = doc.createElement('style')
  style.textContent = `
    :root { color-scheme: light; }
    * { box-sizing: border-box; }
    body { max-width: 176mm; margin: 24px auto; padding: 0 16px; color: #222; background: #fff;
      font: 11pt/1.85 "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", sans-serif; overflow-wrap: anywhere; }
    h1 { font-size: 25pt; margin: 12mm 0 8mm; } h2 { font-size: 19pt; margin: 0 0 7mm; }
    h3,h4 { margin: 6mm 0 3mm; } h1,h2,h3,h4 { line-height: 1.45; break-after: avoid; }
    p,li { orphans: 3; widows: 3; } a { color: #333; text-underline-offset: 3px; }
    .world-print-card { margin-top: 12mm; border-top: 1px solid #ddd; padding-top: 8mm; }
    figure { margin: 6mm 0; break-inside: avoid; text-align: center; }
    img { display: block; max-width: 100%; max-height: 215mm; width: auto; height: auto; margin: auto; object-fit: contain; }
    figcaption { font-size: 9pt; color: #555; margin-top: 2mm; }
    pre { white-space: pre-wrap; overflow-wrap: anywhere; background: #f5f5f5; padding: 4mm; font-size: 9pt; }
    blockquote { margin-left: 0; border-left: 2px solid #bbb; padding-left: 4mm; }
    table { border-collapse: collapse; table-layout: fixed; width: 100%; font-size: 10pt; }
    th,td { border: 1px solid #ccc; padding: 2mm; overflow-wrap: anywhere; }
    thead { display: table-header-group; } tr { break-inside: avoid; }
    @page { size: A4; margin: 18mm 17mm 20mm; @bottom-center { content: counter(page); font-size: 9pt; color: #666; } }
    @media print { body { margin: 0; padding: 0; max-width: none; }
      .world-print-card { break-before: page; margin: 0; padding: 0; border: 0; } }
  `
  doc.head.append(style)
  const source = doc.createElement('div')
  source.innerHTML = renderMarkdown(markdown)
  let section: HTMLElement = doc.body
  for (const node of [...source.childNodes]) {
    if (node instanceof HTMLElement && node.tagName === 'H2') {
      section = doc.createElement('section')
      section.className = 'world-print-card'
      doc.body.append(section)
    }
    section.append(node)
  }
  for (const img of [...doc.images]) {
    // srcdoc 使用独立锚点地址；图片仍需基于网站地址解析，避免 /uploads/... 失效。
    const rawSource = img.getAttribute('src') || ''
    if (rawSource) {
      try { img.src = new URL(rawSource, document.baseURI).href } catch { /* 由准备流程提示加载失败。 */ }
    }
    img.loading = 'eager'
    img.referrerPolicy = 'no-referrer'
    const figure = doc.createElement('figure')
    const caption = doc.createElement('figcaption')
    caption.textContent = img.alt || '插图'
    const parent = img.parentElement
    // figure 不能嵌套在 p 中，否则浏览器重新解析时会产生多余空段落。
    if (parent?.tagName === 'P' && parent.childNodes.length === 1) parent.replaceWith(figure)
    else img.replaceWith(figure)
    figure.append(img, caption)
  }
  return '<!doctype html>' + doc.documentElement.outerHTML
}

/** 等待真实图片和字体；失败图片明确留链接，不能空白打印却报告成功。 */
export async function prepareWorldPrint(doc: Document, timeoutMs = 15000): Promise<number> {
  const results = await Promise.all([...doc.images].map(img => new Promise<boolean>(resolve => {
    let settled = false
    const finish = (ok: boolean) => {
      if (settled) return
      settled = true
      clearTimeout(timer)
      img.removeEventListener('load', loaded)
      img.removeEventListener('error', failed)
      if (!ok) {
        const link = doc.createElement('a')
        link.href = img.src
        link.textContent = `图片未能加载：${img.alt || '查看原图'}`
        img.replaceWith(link)
      }
      resolve(ok)
    }
    const loaded = () => finish(img.naturalWidth > 0)
    const failed = () => finish(false)
    const timer = setTimeout(failed, timeoutMs)
    img.addEventListener('load', loaded, { once: true })
    img.addEventListener('error', failed, { once: true })
    if (img.complete) loaded()
  })))
  let fontTimer: ReturnType<typeof setTimeout> | undefined
  await Promise.race([doc.fonts.ready, new Promise(resolve => { fontTimer = setTimeout(resolve, timeoutMs) })])
  clearTimeout(fontTimer)
  return results.filter(ok => !ok).length
}
