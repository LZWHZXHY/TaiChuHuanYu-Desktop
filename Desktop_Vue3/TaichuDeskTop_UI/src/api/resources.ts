import axios from 'axios'
import request from '@/utils/request'

/**
 * 知识库「下载资源」：用户上传的社区资源 + 站长放在 NAS 上的官方资源。
 * 官方资源照旧用 knowledgeApi.download（匿名、免费）；社区资源下载要登录（计数、付费资源扣经验）。
 * 价格单位是经验：付费资源第一次下载扣下载者的经验，同样多的经验加给上传者（资源区不碰土豆）。
 */
export type ResourceStatus = 'uploading' | 'pending' | 'approved' | 'rejected' | 'removed'
export type ResourceSort = 'new' | 'downloads' | 'free' | 'paid'
export type ResourceBoard = 'count' | 'downloads'

export interface ResourceUploader { id: string; name: string; avatar?: string | null }

export interface ResourceItem {
  /** 社区资源是 Guid；官方资源是「official:文件路径」 */
  id: string
  kind: 'community' | 'official'
  /** 官方资源在桶里的文件路径（下载用）；社区资源为 null */
  key: string | null
  title: string
  description: string
  category: string
  /** 位置（展示用的文件夹路径，比如「PS笔刷/分类A」）。社区资源没填时是分类名；官方资源是 NAS 里的文件夹，根目录是空字符串 */
  folder: string
  tags: string[]
  fileName: string
  /** 小写、带点，比如 .zip；没有扩展名是空字符串 */
  ext: string
  size: number
  /** 价格（经验）；0 = 免费 */
  price: number
  /** 下载次数（同一个人多次下载都算） */
  downloadCount: number
  /** 多少个不同的人下载过 */
  uniqueDownloadCount: number
  uploader: ResourceUploader | null
  publishedAt: string | null
  /** 我买过（付费资源） */
  purchased: boolean
  /** 我上传的 */
  mine: boolean
}

/** 我的资源 / 管理员看到的额外字段 */
export interface MyResourceItem extends ResourceItem {
  status: ResourceStatus
  /** 上传者自己填的位置；null = 没填（显示用分类名） */
  folderRaw: string | null
  /** 文件存在哪：nas = NAS，cos = 腾讯云（后端没更新时没有这个字段，当 NAS） */
  storage?: ResourceStorageKey
  /** 打回 / 下架理由 */
  reviewNote: string | null
  reviewedAt: string | null
  /** 这个资源一共给我带来多少经验（付费下载收入 + 免费资源下载奖励；审核经验不算） */
  expEarned: number
  createdAt: string
  updatedAt: string
  partSize: number
  partCount: number
}

export interface AdminResourceItem extends MyResourceItem {
  contentType: string
  /** 审核提醒（压缩包、伪装扩展名、大文件） */
  warnings: string[]
  /** 能不能在浏览器里直接预览（图片 / 音频 / 视频 / PDF / 纯文本） */
  previewable: boolean
  reviewerName: string | null
}

export interface ResourceListResult {
  items: ResourceItem[]
  total: number
  page: number
  pageSize: number
  /** NAS 上的官方资源目录没读到 */
  officialError: boolean
  /** 社区资源没读到 */
  communityError: boolean
}

/** 资源文件存在哪：NAS（站长的 MinIO）/ 腾讯云 COS */
export type ResourceStorageKey = 'nas' | 'cos'
export interface ResourceStorageOption { key: ResourceStorageKey; label: string; hint: string; available: boolean }
export const storageLabel = (key?: string | null) => key === 'cos' ? '腾讯云' : 'NAS'

export interface ResourceConfig {
  categories: string[]
  maxPrice: number
  maxFileBytes: number
  partSize: number
  concurrency: number
  blockedExtensions: string[]
  titleMax: number
  descriptionMax: number
  tagCountMax: number
  approveExp: number
  freeDownloadExp: number
  freeDownloadDailyCap: number
  /** 上传时能选的存放位置（后端没更新时没有，只能存 NAS） */
  storages?: ResourceStorageOption[]
  defaultStorage?: ResourceStorageKey
}

export interface ResourceMeta {
  title: string
  description: string
  category: string
  tags: string[]
  price: number
  /** 位置；空字符串 = 用分类名 */
  folder: string
}

export interface PartUrl { partNumber: number; url: string }

export interface InitUploadResult {
  id: string
  fileName: string
  partSize: number
  partCount: number
  concurrency: number
  expiresAt: string
  parts: PartUrl[]
}

export interface UploadStatusResult {
  id: string
  title: string
  fileName: string
  size: number
  partSize: number
  partCount: number
  concurrency: number
  doneParts: number[]
  /** 这次上传已经失效（超过 24 小时被清理），只能重新上传 */
  expired: boolean
}

export interface DownloadResult {
  url: string
  fileName: string
  /** 这次扣了多少经验（0 = 没扣） */
  charged: number
  /** 扣完后剩下的经验（没扣时为 null） */
  balance: number | null
}

export interface ResourceRankRow {
  rank: number
  userId: string
  name: string
  avatar?: string | null
  /** 公开的资源数 */
  approved: number
  /** 被多少个不同的人下载过 */
  downloads: number
  totalDownloads: number
}

export interface MyExp { experience: number; level: number; experienceAfter: number; levelAfter: number }

/** 位置写成好懂的样子：空的是 NAS 根目录 */
export const folderText = (folder: string | null | undefined) => (folder ? folder : '根目录')

/** 资源分类（和后端 ResourceRules.Categories 一致） */
export const RESOURCE_CATEGORIES = ['音效', '素材', '模板', '工具', '软件', '字体', '模型', '其他']

/** 价格写成「✦ 30 经验」；0 是「免费」 */
export const priceText = (price: number) => (price > 0 ? `✦ ${price} 经验` : '免费')

export type AdminCounts = Record<'pending' | 'approved' | 'rejected' | 'removed', number>

export const resourceApi = {
  list: (params: { q?: string; category?: string; sort?: ResourceSort; folder?: string; kind?: 'community' | 'official'; page?: number; pageSize?: number }) =>
    request.get<ResourceListResult>('/resources', { params, silentError: true }),
  config: () => request.get<ResourceConfig>('/resources/config', { silentError: true }),
  /** 可选的位置：官方资源的文件夹 + 社区资源用过的位置（去重排序） */
  folders: () => request.get<string[]>('/resources/folders', { silentError: true }),
  detail: (id: string) => request.get<ResourceItem & Partial<MyResourceItem>>(`/resources/${encodeURIComponent(id)}`, { silentError: true }),
  /** 拿下载链接；付费资源第一次下载要带上用户确认过的价格 */
  download: (id: string, confirmPrice?: number) =>
    request.post<DownloadResult>(`/resources/${encodeURIComponent(id)}/download`, { confirmPrice: confirmPrice ?? null }, { silentError: true }),

  initUpload: (body: ResourceMeta & { fileName: string; size: number; contentType: string; storage?: ResourceStorageKey }) =>
    request.post<InitUploadResult>('/resources/upload/init', body, { silentError: true }),
  refreshUrls: (id: string, parts: number[]) =>
    request.post<{ expiresAt: string; parts: PartUrl[] }>(`/resources/upload/${encodeURIComponent(id)}/urls`, { parts }, { silentError: true }),
  partDone: (id: string, partNumber: number, size: number) =>
    request.post<{ ok: boolean }>(`/resources/upload/${encodeURIComponent(id)}/part`, { partNumber, size }, { silentError: true }),
  uploadStatus: (id: string) => request.get<UploadStatusResult>(`/resources/upload/${encodeURIComponent(id)}`, { silentError: true }),
  /** 合并大文件可能要一会儿，给 5 分钟 */
  complete: (id: string) =>
    request.post<{ status: ResourceStatus; message: string }>(`/resources/upload/${encodeURIComponent(id)}/complete`, undefined, { silentError: true, timeout: 300000 }),
  abort: (id: string) => request.post<{ ok: boolean }>(`/resources/upload/${encodeURIComponent(id)}/abort`, undefined, { silentError: true }),

  mine: () => request.get<{ items: MyResourceItem[]; totalEarned: number; totalDownloads: number }>('/resources/mine', { silentError: true }),
  update: (id: string, meta: ResourceMeta) =>
    request.put<{ message: string; status: ResourceStatus }>(`/resources/${encodeURIComponent(id)}`, meta, { silentError: true }),
  remove: (id: string) => request.delete<{ ok: boolean }>(`/resources/${encodeURIComponent(id)}`, { silentError: true }),

  adminList: (status: Exclude<ResourceStatus, 'uploading'>, page = 1) =>
    request.get<{ items: AdminResourceItem[]; total: number; page: number; pageSize: number }>('/resources/admin', { params: { status, page }, silentError: true }),
  adminCounts: () => request.get<AdminCounts>('/resources/admin/counts', { silentError: true }),
  approve: (id: string) => request.post<{ ok: boolean; exp: number }>(`/resources/admin/${encodeURIComponent(id)}/approve`, undefined, { silentError: true }),
  reject: (id: string, reason: string) => request.post<{ ok: boolean }>(`/resources/admin/${encodeURIComponent(id)}/reject`, { reason }, { silentError: true }),
  takeDown: (id: string, reason: string) => request.post<{ ok: boolean }>(`/resources/admin/${encodeURIComponent(id)}/remove`, { reason }, { silentError: true }),
  /** 管理员改位置（空 = 用分类名），不需要重审 */
  adminSetFolder: (id: string, folder: string) =>
    request.put<{ folder: string; folderRaw: string | null }>(`/resources/admin/${encodeURIComponent(id)}/folder`, { folder }, { silentError: true }),
  restore: (id: string) => request.post<{ ok: boolean }>(`/resources/admin/${encodeURIComponent(id)}/restore`, undefined, { silentError: true }),
  adminFile: (id: string, inline = false) =>
    request.get<{ url: string; fileName: string; inline: boolean }>(`/resources/admin/${encodeURIComponent(id)}/file`, { params: { inline }, silentError: true }),

  /** 我现在的经验和等级；带 cost 时顺便算花掉后剩多少、是几级（等级算法和全站一致，由后端算） */
  myExp: (cost = 0) => request.get<MyExp>('/resources/my-exp', { params: { cost }, silentError: true }),

  leaderboard: (by: ResourceBoard = 'count') => request.get<ResourceRankRow[]>('/resources/leaderboard', { params: { by }, silentError: true }),
}

/** 接口返回的错误内容（{ code, message, ... }），用来区分「经验不够」「价格变了」等情况 */
export function resourceErrorData(error: unknown): { code?: string; message?: string; [key: string]: unknown } | null {
  if (!axios.isAxiosError(error)) return null
  const data = error.response?.data as unknown
  return data && typeof data === 'object' ? data as { code?: string; message?: string } : null
}

/** 文件大小写成好懂的样子 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

/** 让浏览器开始下载（链接是 15 分钟有效的临时地址，不保存） */
export function startBrowserDownload(url: string, fileName: string) {
  const parsed = new URL(url)
  if (!['https:', 'http:'].includes(parsed.protocol)) throw new Error('下载地址不对')
  const a = document.createElement('a')
  a.href = parsed.href
  a.download = fileName || 'download'
  a.target = '_blank'
  a.rel = 'noopener noreferrer'
  a.click()
}

export const RESOURCE_STATUS_LABELS: Record<ResourceStatus, string> = {
  uploading: '上传中',
  pending: '待审核',
  approved: '已公开',
  rejected: '被打回',
  removed: '已下架',
}
