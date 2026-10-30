import type { LayerMeta, Stroke } from './paintEngine'

export interface PaintMemberView { userId: string; name: string; color: string; online: boolean }
export interface PaintChatMsg { userId: string; name: string; color: string; text: string; at: string }
export interface PaintRoomView {
  roomId: string; name: string; ownerId: string; width: number; height: number; hasPassword: boolean; maxMembers: number; seq: number
  members: PaintMemberView[]; layers: LayerMeta[]; strokes: Stroke[]; chat: PaintChatMsg[]
}
