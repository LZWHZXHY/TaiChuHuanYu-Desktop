import { zip, unzip, strToU8, strFromU8, type Zippable } from 'fflate'
import type { CardDetail, WorldProject } from '@/api/types/world'
import { parseWorldChronicle, parseWorldText, worldMarkdown, type WorldTextItem } from './worldTransfer'
import type { ChronicleExport } from './chronicle'

// 浏览器内存保护，与账号的世界/条目配额分开；不再限制 50 条。
export const worldTextBytes = 64 * 1024 * 1024
export const worldArchiveBytes = 256 * 1024 * 1024
const expandedBytes = 512 * 1024 * 1024
const imageBytes = 32 * 1024 * 1024
const imagePath = /^\.\/images\/[a-f0-9]{64}\.(png|jpg|gif|webp|avif|bmp|ico|svg)$/
export interface WorldBackup {
  project: Pick<WorldProject, 'name' | 'description'>
  items: WorldTextItem[]
  images: Map<string, { bytes: Uint8Array<ArrayBuffer>; type: string }>
  /** 编年史（时间线、事件、历法）；老备份没有就是 null */
  chronicle: ChronicleExport | null
}
type Progress = (message: string) => void
const digest = async (bytes: Uint8Array<ArrayBuffer>) => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), b => b.toString(16).padStart(2, '0')).join('')
const utf8 = (bytes: Uint8Array) => new TextDecoder('utf-8', { fatal: true }).decode(bytes)

/** 只识别图片字段/图片语法，不把普通外链当成需要抓取的资源。 */
export function worldImageUrls(project: Pick<WorldProject, 'description'>, cards: unknown[]): string[] {
  const urls = new Set<string>()
  const add = (value: unknown) => { if (typeof value === 'string' && value.trim()) urls.add(value.trim()) }
  const scan = (value: unknown, key = '') => {
    if (Array.isArray(value)) { value.forEach(v => scan(v, key)); return }
    if (value && typeof value === 'object') { Object.entries(value).forEach(([k,v]) => scan(v,k)); return }
    if (typeof value !== 'string') return
    if (key === 'coverImage' || key === 'content' || key === 'layout') {
      try { scan(JSON.parse(value), key === 'coverImage' ? 'galleryImages' : ''); return } catch { /* 旧封面直接保存 URL */ }
    }
    // images：区块里的图片块；image：时间线事件的配图
    if (key === 'coverImage' || key === 'galleryImages' || key === 'images' || key === 'image') { add(value); return }
    if (/^(https?:\/\/|\/[^/]|\.\/images\/|data:image\/)/i.test(value) && /(?:\.(png|jpe?g|gif|webp|svg|avif|bmp|ico)(?:[?#].*)?$|^data:image\/)/i.test(value)) add(value)
    for (const m of value.matchAll(/!\[(?:\\.|[^\]\\])*\]\(\s*(?:<([^>]+)>|([^\s)]+))(?:\s+["'][^"']*["'])?\s*\)/g)) add(m[1] || m[2])
    for (const m of value.matchAll(/<img\b[^>]*\bsrc\s*=\s*["']([^"']+)["'][^>]*>/gi)) add(m[1]?.replace(/&amp;/g,'&'))
    const labels = [...value.matchAll(/!\[([^\]]*)\](?:\[([^\]]*)\])?/g)].map(m => (m[2] || m[1] || '').toLowerCase())
    for (const m of value.matchAll(/^\s*\[([^\]]+)\]:\s*<?([^\s>]+)>?/gm)) if (labels.includes(m[1]!.toLowerCase())) add(m[2])
  }
  scan(project.description); cards.forEach(c => scan(c))
  return [...urls]
}

/** 图片替换覆盖旧 JSON 封面、自定义 JSON、Markdown/HTML 和时间线，保留其他字段。 */
export function replaceWorldImages<T>(value: T, urls: Map<string, string>): T {
  const entries = [...urls].sort((a,b) => b[0].length-a[0].length)
  const walk = (v: unknown): unknown => {
    if (Array.isArray(v)) return v.map(walk)
    if (v && typeof v === 'object') return Object.fromEntries(Object.entries(v).map(([k,x]) => [k,walk(x)]))
    if (typeof v !== 'string') return v
    try {
      const parsed = JSON.parse(v)
      if (parsed && typeof parsed === 'object') {
        const changed = JSON.stringify(walk(parsed))
        return changed === JSON.stringify(parsed) ? v : changed
      }
    } catch { /* 普通文本 */ }
    for (const [source,target] of entries) {
      v = (v as string).split(source).join(target)
      if (source.includes('&')) v = (v as string).split(source.replace(/&/g,'&amp;')).join(target.replace(/&/g,'&amp;'))
    }
    return v
  }
  return walk(value) as T
}

function imageFormat(bytes: Uint8Array) {
  const start = strFromU8(bytes.subarray(0, Math.min(bytes.length,512)))
  if (bytes[0] === 137 && start.slice(1,4) === 'PNG') return ['png','image/png'] as const
  if (bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) return ['jpg','image/jpeg'] as const
  if (/^GIF8[79]a/.test(start)) return ['gif','image/gif'] as const
  if (start.startsWith('RIFF') && start.slice(8,12) === 'WEBP') return ['webp','image/webp'] as const
  if (start.slice(4,8) === 'ftyp' && /avif|avis/.test(start.slice(8,40))) return ['avif','image/avif'] as const
  if (start.startsWith('BM')) return ['bmp','image/bmp'] as const
  if (bytes[0] === 0 && bytes[1] === 0 && bytes[2] === 1 && bytes[3] === 0) return ['ico','image/x-icon'] as const
  if (/^\s*(?:<\?xml[^>]*>\s*)?(?:<!--[\s\S]*?-->\s*)*<svg\b/i.test(start)) return ['svg','image/svg+xml'] as const
  throw new Error('图片格式无效，或原图地址返回了错误页面。')
}

async function downloadImage(source: string, signal?: AbortSignal): Promise<Uint8Array<ArrayBuffer>> {
  const url = new URL(source, document.baseURI)
  if (!['http:','https:','data:','blob:'].includes(url.protocol)) throw new Error('图片地址协议不受支持。')
  // 图片展示先命中的 CDN 缓存可能没有 CORS 头；备份单独取新响应，原地址和签名 URL 保持不变。
  if (url.hostname === 'img.bianyuzhou.com' && url.pathname.startsWith('/world/restores/') && !url.search)
    url.searchParams.set('world-backup', crypto.randomUUID())
  const timeout = AbortSignal.timeout(30000)
  const response = await fetch(url, { credentials:'omit', referrerPolicy:'no-referrer', signal:signal ? AbortSignal.any([signal,timeout]) : timeout })
  if (!response.ok || !response.body) throw new Error(`读取图片失败（${response.status}）`)
  if (Number(response.headers.get('content-length')) > imageBytes) throw new Error('单张图片超过 32MB。')
  const reader=response.body.getReader(), chunks:Uint8Array[]=[]
  let size=0
  try {
    while (true) {
      const {done,value}=await reader.read(); if(done) break
      size+=value.byteLength
      if(size>imageBytes) throw new Error('单张图片超过 32MB。')
      chunks.push(value)
    }
  } finally { await reader.cancel() }
  const bytes=new Uint8Array(size); let offset=0
  for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length}
  return bytes
}

export async function createWorldBackup(project: WorldProject, cards: CardDetail[], progress: Progress = () => {},
  options: { signal?: AbortSignal; loadImage?: (url: string) => Promise<Uint8Array<ArrayBuffer>>; chronicle?: ChronicleExport | null } = {}) {
  const files: Zippable = Object.create(null), images: Record<string,{hash:string;type:string;size:number}> = Object.create(null)
  // 编年史事件里的配图也一起打包
  const chronicle = options.chronicle ?? null
  const urls=worldImageUrls(project,chronicle ? [...cards,chronicle] : cards), replacements=new Map<string,string>()
  let total=0
  for(const [index,url] of urls.entries()) {
    options.signal?.throwIfAborted()
    progress(`正在备份原图 ${index+1} / ${urls.length}…`)
    try {
      const bytes=await (options.loadImage ? options.loadImage(url) : downloadImage(url,options.signal))
      if(bytes.length>imageBytes) throw new Error('单张图片超过 32MB。')
      const [extension,type]=imageFormat(bytes),hash=await digest(bytes),path=`images/${hash}.${extension}`
      if(!files[path]) { total+=bytes.length; if(total>worldArchiveBytes) throw new Error('图片总量超过 256MB，请减少单张原图体积后重试。'); files[path]=[bytes,{level:0}]; images[`./${path}`]={hash,type,size:bytes.length} }
      replacements.set(url,`./${path}`)
    } catch(e) { throw new Error(`备份未完成：第 ${index+1} 张原图无法保存（${url.slice(0,160)}）。${e instanceof Error?e.message:''}`) }
  }
  options.signal?.throwIfAborted()
  const localProject=replaceWorldImages(project,replacements),localCards=replaceWorldImages(cards,replacements)
  const localChronicle=chronicle ? replaceWorldImages(chronicle,replacements) : null
  const md=strToU8(worldMarkdown(localProject,localCards,true,localChronicle))
  if(md.length>worldTextBytes || total+md.length>worldArchiveBytes) throw new Error('备份超过浏览器处理容量（正文 64MB、合计 256MB）。')
  // 导出前就校验引用，不能给用户一个无法恢复的“成功”备份。
  parseWorldText(utf8(md),'world.md')
  parseWorldChronicle(utf8(md))
  files['world.md']=md
  files['manifest.json']=strToU8(JSON.stringify({format:'taichu-world-backup',version:1,
    project:{name:localProject.name,description:localProject.description},markdownHash:await digest(md),images}))
  progress('正在打包正文和图片…')
  const bytes=await new Promise<Uint8Array<ArrayBuffer>>((resolve,reject)=>zip(files,{level:6},(error,data)=>error?reject(error):resolve(data)))
  options.signal?.throwIfAborted()
  if(bytes.length>worldArchiveBytes) throw new Error('备份文件超过 256MB。')
  return { bytes, imageCount:Object.keys(images).length }
}

export async function readWorldBackup(bytes: Uint8Array<ArrayBuffer>): Promise<WorldBackup> {
  if(bytes.length>worldArchiveBytes) throw new Error('备份包超过 256MB。')
  let total=0,problem='';const seen=new Set<string>()
  const files=await new Promise<Record<string,Uint8Array<ArrayBuffer>>>((resolve,reject)=>unzip(bytes,{filter:file=>{
    if(seen.has(file.name) || !(file.name==='manifest.json' || file.name==='world.md' || imagePath.test(`./${file.name}`))) problem='备份包包含重复或不支持的文件路径。'
    seen.add(file.name);total+=file.originalSize
    const limit=file.name==='world.md'?worldTextBytes:file.name==='manifest.json'?worldTextBytes:imageBytes
    if(file.originalSize>limit || total>expandedBytes) problem='备份包解压后超过浏览器处理容量。'
    return !problem
  }},(error,data)=>error?reject(error):resolve(data)))
  if(problem) throw new Error(problem)
  if(!files['manifest.json'] || !files['world.md']) throw new Error('不是完整的世界观备份包。')
  const manifest=JSON.parse(utf8(files['manifest.json']))
  if(manifest.format!=='taichu-world-backup'||manifest.version!==1||typeof manifest.project?.name!=='string'||!manifest.project.name.trim()||manifest.project.name.length>100
    || !(manifest.project.description===null||typeof manifest.project.description==='string') || !manifest.images || typeof manifest.images!=='object' || Array.isArray(manifest.images)) throw new Error('备份清单格式或版本不受支持。')
  if(await digest(files['world.md'])!==manifest.markdownHash) throw new Error('正文数据校验失败，文件可能已损坏或被修改。')
  const items=parseWorldText(utf8(files['world.md']),'world.md')
  if(items.some(item=>!item.payload)) throw new Error('备份缺少条目恢复数据。')
  // 编年史：老备份没有，就当没有编年史，照常恢复条目
  const chronicle=parseWorldChronicle(utf8(files['world.md']))
  const images: WorldBackup['images']=new Map()
  for(const [path,raw] of Object.entries(manifest.images)) {
    const image=raw as {hash:string;type:string;size:number}, data=files[path.slice(2)]
    if(!imagePath.test(path)||!data||!image||data.length!==image.size||await digest(data)!==image.hash||path!==`./images/${image.hash}.${imageFormat(data)[0]}`||image.type!==imageFormat(data)[1]) throw new Error('图片缺失或校验失败，未开始恢复。')
    images.set(path,{bytes:data,type:image.type})
  }
  if(Object.keys(files).length!==images.size+2) throw new Error('备份中存在未登记的图片。')
  const project={name:manifest.project.name,description:manifest.project.description} as WorldBackup['project']
  for(const url of worldImageUrls(project,[...items.map(i=>i.payload),...(chronicle ? [chronicle] : [])])) if(!images.has(url)) throw new Error('备份包含未打包的图片，不能作为完整备份恢复。')
  return {project,items,images,chronicle}
}
