// 约战「战后记录」的接口（后端 Controllers/ChaiCommunity/BattleAftermathController.cs，表 battle_aftermath）。
// 约战结束后，参赛者给自己出战的每个 OC 写：战后状态 + 说明 + 视频，可以顺便写进世界观编年史。
// OC 的「当前状态」= 最近一场约战的战后状态（不改 OC 原来的数据）。
import request from '@/utils/request'

/** 站内视频作品（柴圈作品里类型是视频的） */
export interface AftermathSiteWork {
  id: string
  title: string
  mediaUrl: string
  coverUrl: string | null
  /** video / image */
  mediaType: string
}

/** 一条战后记录 */
export interface AftermathRecord {
  id: number
  battleId: string
  ocId: string
  userId: string
  statusTag: string
  statusNote: string | null
  /** 别人看时，还没审核通过的约战作品链接会是 null（videoHidden = true） */
  videoUrl: string | null
  videoTitle: string | null
  videoCover: string | null
  videoHidden: boolean
  /** 自己看：这个视频是还在审核的约战作品，别人暂时看不到 */
  videoPending: boolean
  /** 视频是站内作品时，作品的播放地址等（直接内嵌播放） */
  siteWork: AftermathSiteWork | null
  projectId: string | null
  projectName: string | null
  /** 写进的编年史事件（看不到那个事件的人拿到 null） */
  chronicleEventId: string | null
  /** 写过编年史，但那件事已经被删了（只有作者看得到，可以重新写一次） */
  eventDeleted: boolean
  mine: boolean
  createdAt: string
  updatedAt: string
}

/** 结果种类：win 一方胜 / pyrrhic 惨胜 / draw 平手 / mutual 同归于尽 / nocontest 无结果；没出结果是空字符串 */
export type BattleOutcome = 'win' | 'pyrrhic' | 'draw' | 'mutual' | 'nocontest' | ''

/** 约战页：一个参赛 OC */
export interface BattleAftermathItem {
  participantId: string
  userId: string
  userName: string
  teamName: string | null
  teamNumber: number | null
  /** win / lose / draw / none */
  result: string | null
  ocId: string
  ocName: string
  ocCover: string | null
  ocExists: boolean
  /** 自己能不能写（约战结束了、自己参赛、自己是 OC 作者） */
  canEdit: boolean
  record: AftermathRecord | null
}

export interface BattleAftermathData {
  battleId: string
  title: string
  status: string
  finished: boolean
  outcome: BattleOutcome
  /** 自己在这场约战交的作品（写记录时视频默认用它） */
  myDefaultVideo: { url: string; title: string; pending: boolean } | null
  items: BattleAftermathItem[]
}

export interface BattleSide { userName: string; ocNames: string[] }

/** OC 详情页「战斗经历」的一场 */
export interface OcBattleEntry {
  battleId: string
  title: string
  finishedAt: string | null
  outcome: BattleOutcome
  result: string | null
  teamName: string | null
  teammates: BattleSide[]
  opponents: BattleSide[]
  record: AftermathRecord | null
  at: string
}

/** OC 的当前状态（最近一场约战的战后状态） */
export interface OcCurrentStatus {
  statusTag: string
  statusNote: string | null
  battleId: string
  battleTitle: string
  at: string
}

export interface OcBattleHistory {
  ocId: string
  isAuthor: boolean
  current: OcCurrentStatus | null
  items: OcBattleEntry[]
}

/** 编年史事件对应的战后记录 */
export interface EventBattleItem {
  battleId: string
  battleTitle: string
  finishedAt: string | null
  outcome: BattleOutcome
  result: string | null
  userName: string | null
  ocId: string
  ocName: string
  ocCover: string | null
  ocExists: boolean
  opponents: BattleSide[]
  record: AftermathRecord
}

export interface MyVideoWork { id: string; title: string; coverUrl: string | null; mediaUrl: string; createdAt: string }

export interface AftermathChronicleInput {
  projectId: string
  /** 不填 = 世界的第一条时间线（一条都没有就自动建「约战记录」） */
  timelineId?: string | null
  /** 不填 = 「约战《xxx》：A vs B」 */
  title?: string | null
  year?: number | null
  dateText?: string | null
  /** 网站首页地址，事件内容里链回约战页、OC 页用 */
  linkBase?: string | null
}

export interface AftermathSaveInput {
  statusTag: string
  statusNote?: string | null
  videoUrl?: string | null
  videoTitle?: string | null
  videoCover?: string | null
  /** 要顺便写进世界观编年史时填 */
  chronicle?: AftermathChronicleInput | null
}

const base = '/battle-aftermath'

export const battleAftermathApi = {
  battle: (battleId: string) => request.get<BattleAftermathData>(`${base}/battle/${battleId}`, { silentError: true }),
  oc: (ocId: string) => request.get<OcBattleHistory>(`${base}/oc/${ocId}`, { silentError: true }),
  event: (eventId: string) => request.get<{ items: EventBattleItem[] }>(`${base}/event/${eventId}`, { silentError: true }),
  myVideos: () => request.get<{ items: MyVideoWork[] }>(`${base}/my-videos`, { silentError: true }),
  save: (battleId: string, ocId: string, body: AftermathSaveInput) =>
    request.put<{ message: string; warning: string | null; record: AftermathRecord }>(`${base}/battle/${battleId}/oc/${ocId}`, body, { silentError: true }),
  /** 只删这条战后记录；写进编年史的事件不会删 */
  remove: (battleId: string, ocId: string) =>
    request.delete<{ ok: boolean; message: string }>(`${base}/battle/${battleId}/oc/${ocId}`, { silentError: true }),
}

/** 预设的战后状态（也可以自己写，最多 10 个字） */
export const STATUS_PRESETS = ['毫发无伤', '轻伤', '重伤', '阵亡', '觉醒', '突破'] as const
export const MAX_STATUS_TAG = 10

/** 状态标签的颜色分类：ok 绿 / warn 黄 / bad 红 / dead 灰黑 / up 主题色（变强了）/ other 普通 */
export function statusTone(tag: string): 'ok' | 'warn' | 'bad' | 'dead' | 'up' | 'other' {
  if (/无伤|无恙|完好/.test(tag)) return 'ok'
  if (/阵亡|死亡|牺牲|殒|陨落/.test(tag)) return 'dead'
  if (/重伤|濒死|残|昏迷/.test(tag)) return 'bad'
  if (/轻伤|受伤|擦伤|疲/.test(tag)) return 'warn'
  if (/觉醒|突破|进化|升级|变强/.test(tag)) return 'up'
  return 'other'
}

export function outcomeLabel(outcome: string): string {
  switch (outcome) {
    case 'win': return '分出胜负'
    case 'pyrrhic': return '惨胜'
    case 'draw': return '平手'
    case 'mutual': return '同归于尽'
    case 'nocontest': return '无结果'
    default: return ''
  }
}

/** 某个参赛者的结果（惨胜、同归于尽单独说） */
export function resultLabel(result: string | null | undefined, outcome: string): string {
  switch (result) {
    case 'win': return outcome === 'pyrrhic' ? '惨胜' : '胜'
    case 'lose': return '负'
    case 'draw': return outcome === 'mutual' ? '同归于尽' : '平'
    default: return '不计'
  }
}

/** 一方的叫法：带了 OC 就用 OC 名字，没有就用用户名 */
export function sideLabel(side: BattleSide): string {
  const names = side.ocNames.filter(Boolean)
  return names.length ? `${names.join('、')}（${side.userName}）` : side.userName
}

/** 编年史里这件事的地址（世界观页 → 编年史 → 选中这件事） */
export function chronicleEventPath(projectId: string, eventId: string): string {
  return `/world/project/${projectId}?view=timeline&event=${eventId}`
}
