// 图片地址工具：统一走 CDN + 腾讯云「数据万象」缩略。
// 仅对已知的 COS/CDN 域名生效，避免误伤第三方图片。

// 直连 COS 域名 → CDN 域名
// 例如 https://tchy-images-1361988423.cos.ap-beijing.myqcloud.com/xxx.jpg → https://img.bianyuzhou.com/xxx.jpg
export function toCdnUrl(url: string | null | undefined): string {
  if (!url) return ''
  return url.replace(
    /https?:\/\/[a-z0-9-]+\.cos\.[a-z0-9-]+\.myqcloud\.com\//i,
    'https://img.bianyuzhou.com/'
  )
}

// 追加数据万象缩略参数，显著减小图片体积、加快加载。
export function withThumbnail(url: string | null | undefined, width = 400): string {
  if (!url) return ''
  const full = toCdnUrl(url)
  // 地址末尾可能带 #…（比如世界观封面的显示位置 #tc-focus=x,y），缩略参数要加在 # 前面
  const hash = full.indexOf('#')
  const u = hash < 0 ? full : full.slice(0, hash)
  const tail = hash < 0 ? '' : full.slice(hash)
  if (!/myqcloud\.com|bianyuzhou\.com/i.test(u)) return full
  const sep = u.includes('?') ? '&' : '?'
  return `${u}${sep}imageMogr2/thumbnail/${width}x${tail}`
}
