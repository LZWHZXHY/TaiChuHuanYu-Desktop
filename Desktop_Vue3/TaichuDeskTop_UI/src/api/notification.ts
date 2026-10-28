import request from '@/utils/request'

export interface NotificationItem {
  id: string
  type: string
  targetType: string
  targetId: string
  content: string
  link: string | null
  isRead: boolean
  createdAt: string
  actorId: string | null
  actorName: string
  actorAvatar: string | null
}

export interface NotificationPage {
  total: number
  unread: number
  items: NotificationItem[]
}

export const notificationApi = {
  list: (page = 1, pageSize = 20) => request.get<NotificationPage>(`/Notification?page=${page}&pageSize=${pageSize}`, { silentError: true }),
  unreadCount: () => request.get<{ count: number }>('/Notification/unread-count', { silentError: true }),
  markRead: (id: string) => request.post(`/Notification/${id}/read`, {}, { silentError: true }),
  markAllRead: () => request.post('/Notification/read-all', {}, { silentError: true })
}
