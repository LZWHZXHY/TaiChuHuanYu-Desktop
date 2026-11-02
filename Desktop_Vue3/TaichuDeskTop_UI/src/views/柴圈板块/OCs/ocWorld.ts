// 柴圈 OC ↔ 世界观 联动的接口（后端 Controllers/ChaiCommunity/StickmanWorldController.cs）。
// 转换规则（属性 ↔ 区块怎么对应）全在后端 Services/ChaiCommunity/OcWorldConverter.cs，前端只负责显示预览、让用户确认。
import request from '@/utils/request'
import type { StatPanel } from '@/utils/statPanel'

/** 同步方向：card-to-oc = 用条目更新 OC；oc-to-card = 用 OC 更新条目 */
export type SyncDirection = 'card-to-oc' | 'oc-to-card'

/** OC 关联的一个世界观 */
export interface OcWorldLink {
  projectId: string
  /** 看不到这个世界观（比如已经被移出私有世界）时为 null */
  name: string | null
  coverImage: string | null
  exists: boolean
  canRead: boolean
  canEdit: boolean
  linkedAt: string
}
/** OC 对应的一张条目（只有作者看得到） */
export interface OcCardPair {
  cardId: string
  projectId: string
  cardTitle: string | null
  cardExists: boolean
  projectName: string | null
  /** 能不能同步：条目还在、自己能编辑那个世界观 */
  canSync: boolean
  createdAt: string
  lastSyncAt: string | null
  lastSyncDirection: SyncDirection | null
}
export interface OcWorldInfo { isAuthor: boolean; worlds: OcWorldLink[]; cards: OcCardPair[] }

/** 世界观首页「柴圈人设」的一个 OC */
export interface WorldOcItem {
  id: string
  title: string
  description: string
  coverUrl: string | null
  authorId: string
  authorName: string
  linkedAt: string
  mine: boolean
}

/** 条目对应的 OC */
export interface CardOcPair {
  ocId: string
  title: string
  status: string
  coverUrl: string | null
  authorId: string
  authorName: string
  mine: boolean
  canSync: boolean
  /** 谁是原件：oc = 条目是从这个 OC 复制来的；card = OC 是从这个条目发布出去的 */
  origin: 'oc' | 'card'
  createdAt: string
  lastSyncAt: string | null
  lastSyncDirection: SyncDirection | null
}
export interface CardOcInfo { canEdit: boolean; isCharacter: boolean; items: CardOcPair[] }

/** 预览里的一项：add 新增 / update 修改 / keep 保留不动 / skip 跳过（不会同步） */
export interface SyncChange {
  action: 'add' | 'update' | 'keep' | 'skip'
  field: string
  before: string | null
  after: string | null
  note: string | null
}
export interface SyncPreview {
  direction: SyncDirection
  ocTitle: string
  cardTitle: string
  projectId: string
  changes: SyncChange[]
  hasChanges: boolean
  /** 确认执行时带回去；期间内容变了后端会拒绝，要重新预览 */
  fingerprint: string
}

export interface OcBackupBrief {
  id: number
  reason: string
  createdAt: string
  createdBy: string | null
  title: string | null
  attributeCount: number
  imageCount: number
  hasShowPanel: boolean
  readable: boolean
}
export interface OcBackupDetail {
  id: number
  reason: string
  createdAt: string
  title: string
  description: string | null
  coverUrl: string | null
  status: string | null
  attributes: { key: string; value: string | null; type: string }[]
  images: string[]
  showPanel: StatPanel | null
  hasBattlePanel: boolean
}

const base = '/stickman-world'

export const ocWorldApi = {
  // ---------- OC 这边 ----------
  ocInfo: (ocId: string) => request.get<OcWorldInfo>(`${base}/oc/${ocId}`, { silentError: true }),
  link: (ocId: string, projectId: string) =>
    request.post<{ ok: boolean; added: boolean; message: string }>(`${base}/oc/${ocId}/links`, { projectId }, { silentError: true }),
  /** 取消关联（OC 作者）/ 移出（世界观创建者、编写者）：只删关联，不动 OC */
  unlink: (ocId: string, projectId: string) =>
    request.delete<{ ok: boolean; removed: boolean }>(`${base}/oc/${ocId}/links/${projectId}`, { silentError: true }),
  copyToWorld: (ocId: string, projectId: string) =>
    request.post<{ cardId: string; projectId: string; message: string; warning?: string | null }>(
      `${base}/oc/${ocId}/copy`, { projectId, linkBase: window.location.origin }, { silentError: true }),
  backups: (ocId: string) => request.get<{ items: OcBackupBrief[] }>(`${base}/oc/${ocId}/backups`, { silentError: true }),
  backup: (ocId: string, id: number) => request.get<OcBackupDetail>(`${base}/oc/${ocId}/backups/${id}`, { silentError: true }),
  restore: (ocId: string, id: number) => request.post<{ message: string }>(`${base}/oc/${ocId}/backups/${id}/restore`, {}, { silentError: true }),

  // ---------- 世界观这边 ----------
  worldOcs: (projectId: string) => request.get<{ canManage: boolean; items: WorldOcItem[] }>(`${base}/world/${projectId}/ocs`, { silentError: true }),
  cardInfo: (cardId: string) => request.get<CardOcInfo>(`${base}/card/${cardId}`, { silentError: true }),
  publishCard: (cardId: string) =>
    request.post<{ ocId: string; message: string; warning?: string | null }>(`${base}/card/${cardId}/publish`, {}, { silentError: true }),

  // ---------- 两边之间 ----------
  preview: (ocId: string, cardId: string, direction: SyncDirection) =>
    request.get<SyncPreview>(`${base}/pair/${ocId}/${cardId}/preview`, { params: { direction }, silentError: true }),
  sync: (ocId: string, cardId: string, direction: SyncDirection, fingerprint: string) =>
    request.post<{ changed: number; message: string }>(`${base}/pair/${ocId}/${cardId}/sync`, { direction, fingerprint }, { silentError: true }),
  /** 断开对应关系：只删对应记录，两边内容都不动 */
  disconnect: (ocId: string, cardId: string) => request.delete<{ ok: boolean; message: string }>(`${base}/pair/${ocId}/${cardId}`, { silentError: true }),
}

/** 「上次同步」的说法 */
export function describeSync(at: string | null, direction: SyncDirection | null) {
  if (!at) return '还没同步过'
  const when = new Date(at).toLocaleString('zh-CN', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })
  return `${when} ${direction === 'card-to-oc' ? '从条目更新了 OC' : '从 OC 更新了条目'}`
}
