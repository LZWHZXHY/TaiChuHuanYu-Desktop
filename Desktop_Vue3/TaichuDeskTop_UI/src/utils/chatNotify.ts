/**
 * 聊天室提醒设置（每台设备单独记，存在浏览器里）：
 * - 有人 @我 或回复我的消息：一定提醒，不能关
 * - 普通新消息：用户自己选要不要提醒（默认不提醒；同一频道 20 秒内合并成一条）
 * desktop：网页不在前台时，用系统通知弹出（需要浏览器授权）
 */
import { ref, watch } from 'vue'

/** mention = 只提醒 @我 / 回复我；all = 普通新消息也提醒 */
export type ChatNotifyMode = 'mention' | 'all'
const MODE_KEY = 'taichu.chatNotify'
const DESKTOP_KEY = 'taichu.chatNotifyDesktop'

function read<T>(key: string, fallback: T, ok: (v: string) => boolean, parse: (v: string) => T): T {
  try {
    const v = localStorage.getItem(key)
    return v !== null && ok(v) ? parse(v) : fallback
  } catch { return fallback }
}

// 旧版本存过 'off'：@ 现在一定提醒，所以按「只提醒 @我」处理
export const chatNotifyMode = ref<ChatNotifyMode>(read<ChatNotifyMode>(MODE_KEY, 'mention', v => v === 'all' || v === 'mention' || v === 'off', v => v === 'all' ? 'all' : 'mention'))
// 默认打开：只要浏览器允许了通知，网页在后台时就弹系统通知
export const chatNotifyDesktop = ref<boolean>(read(DESKTOP_KEY, true, v => v === '1' || v === '0', v => v === '1'))
/** 浏览器的通知权限：granted 允许 / default 还没问过 / denied 被拒绝 / unsupported 不支持 */
export const notifyPermission = ref<NotificationPermission | 'unsupported'>(typeof Notification === 'undefined' ? 'unsupported' : Notification.permission)
export function refreshNotifyPermission() {
  notifyPermission.value = typeof Notification === 'undefined' ? 'unsupported' : Notification.permission
}

watch(chatNotifyMode, v => { try { localStorage.setItem(MODE_KEY, v) } catch { /* 忽略 */ } })
watch(chatNotifyDesktop, v => { try { localStorage.setItem(DESKTOP_KEY, v ? '1' : '0') } catch { /* 忽略 */ } })

/** 打开系统通知：先向浏览器要权限，拒绝了就关回去 */
export async function enableDesktopNotify(): Promise<boolean> {
  if (typeof Notification === 'undefined') return false
  let permission = Notification.permission
  if (permission === 'default') permission = await Notification.requestPermission()
  notifyPermission.value = permission
  chatNotifyDesktop.value = permission === 'granted'
  return chatNotifyDesktop.value
}
