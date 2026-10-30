// src/api/reports.ts：举报（用户提交）+ 管理面板处理
import request from '@/utils/request'

export type ReportTargetType = 'post' | 'blog' | 'artwork' | 'comment' | 'history' | 'history-comment' | 'work' | 'character' | 'user' | 'world' | 'novel' | 'other'
export type ReportReason = 'spam' | 'abuse' | 'porn' | 'privacy' | 'copyright' | 'illegal' | 'other'
export type ReportStatus = 'pending' | 'resolved' | 'dismissed'

export const REASON_LABELS: Record<ReportReason, string> = {
  spam: '广告 / 刷屏', abuse: '人身攻击 / 骂人', porn: '色情低俗', privacy: '泄露别人隐私', copyright: '侵权 / 搬运', illegal: '违法违规', other: '其他',
}

export interface ReportInput { targetType: ReportTargetType; targetId: string; targetTitle?: string; targetUrl?: string; reason: ReportReason; detail?: string }
export interface AdminReport {
  id: number
  targetType: ReportTargetType
  typeLabel: string
  targetId: string
  targetTitle: string | null
  targetUrl: string | null
  reason: ReportReason
  detail: string | null
  status: ReportStatus
  reporter: { id: string; username: string }
  handledByName: string | null
  handledAt: string | null
  handleNote: string | null
  createdAt: string
  /** 这个内容一共有几条还没处理的举报 */
  sameTargetPending: number
}
export interface AdminReportPage { counts: Record<ReportStatus, number>; hasMore: boolean; items: AdminReport[] }

export const reportApi = {
  create: (body: ReportInput) => request.post<{ message: string }>('/reports', body, { silentError: true }),
  adminList: (status: ReportStatus | 'all', page = 1) => request.get<AdminReportPage>('/admin/reports', { params: { status, page }, silentError: true }),
  adminPendingCount: () => request.get<{ count: number }>('/admin/reports/pending-count', { silentError: true }),
  adminHandle: (id: number, body: { status: 'resolved' | 'dismissed'; note?: string; all?: boolean }) =>
    request.post<{ message: string; handled: number }>(`/admin/reports/${id}/handle`, body, { silentError: true }),
}
