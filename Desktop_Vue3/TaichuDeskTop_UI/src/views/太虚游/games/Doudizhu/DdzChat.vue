<!-- DdzChat.vue：牌桌里的小聊天框（可收起）。显示最近的评论和「A 给 B 砸了个番茄」这样的提示；
     发言由父组件通过 send 交给服务器（每人 3 秒一条、最多 100 字，服务器也会再检查一遍）。 -->
<template>
  <aside class="dchat" :class="{ open }" aria-label="牌桌聊天">
    <button type="button" class="dchat-head" :aria-expanded="open" @click="toggle">
      <span class="dchat-title">💬 牌桌聊天</span>
      <span v-if="!open && unread" class="dchat-unread">{{ unread > 99 ? '99+' : unread }} 条新消息</span>
      <span v-else-if="!open && lastLine" class="dchat-peek">{{ lineText(lastLine) }}</span>
      <span class="dchat-fold" aria-hidden="true">{{ open ? '收起 ▾' : '展开 ▴' }}</span>
    </button>
    <template v-if="open">
      <ol ref="listEl" class="dchat-list" aria-live="polite">
        <li v-if="!lines.length" class="dchat-empty">还没人说话，打个招呼吧～</li>
        <li v-for="l in lines" :key="l.id" class="dchat-line" :class="['k-' + l.kind, { mine: l.kind === 'chat' && !!myUserId && l.userId === myUserId }]">
          <template v-if="l.kind === 'chat'"><small v-if="l.spectator" class="dchat-watch" title="这个人在观战">观战</small><b>{{ l.name }}</b><span>{{ l.text }}</span></template>
          <template v-else><i aria-hidden="true">{{ l.kind === 'tomato' ? '🍅' : '👍' }}</i><span>{{ l.text }}</span></template>
          <time :datetime="l.at">{{ clock(l.at) }}</time>
        </li>
      </ol>
      <div class="dchat-quick" role="group" aria-label="快捷发言">
        <button v-for="q in QUICK" :key="q" type="button" :disabled="!canSend || cooling > 0" @click="submit(q)">{{ q }}</button>
      </div>
      <form class="dchat-form" @submit.prevent="submit()">
        <input v-model="text" type="text" :maxlength="maxLength" :disabled="!canSend" :placeholder="canSend ? `说点什么（最多 ${maxLength} 字）` : '连上牌桌后才能聊天'" aria-label="聊天内容" enterkeyhint="send" />
        <button type="submit" :disabled="!canSend || !text.trim() || cooling > 0 || sending">{{ cooling > 0 ? `${cooling} 秒` : '发送' }}</button>
      </form>
      <p v-if="error" class="dchat-error" role="alert">{{ error }}</p>
    </template>
  </aside>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import type { ChatLine } from './ddzSocial'

const props = withDefaults(defineProps<{
  lines: ChatLine[]
  myUserId?: string | null
  canSend?: boolean
  maxLength?: number
  /** 发言间隔（秒），和后端 ChatCooldownSeconds 一致 */
  cooldown?: number
  /** 交给服务器；返回错误文字，成功返回 null */
  send: (text: string) => Promise<string | null>
}>(), { myUserId: null, canSend: true, maxLength: 100, cooldown: 3 })

const QUICK = ['快点呀～', '好牌！', '我太难了', '再来一局']
const OPEN_KEY = 'ddz-chat-open'

function readOpen() {
  try { return localStorage.getItem(OPEN_KEY) !== '0' } catch { return true }
}
const open = ref(readOpen())
const text = ref('')
const error = ref('')
const sending = ref(false)
const cooling = ref(0)
const unread = ref(0)
const listEl = ref<HTMLElement | null>(null)
let coolTimer = 0, errorTimer = 0

const lastLine = computed(() => props.lines[props.lines.length - 1])
const lineText = (l: ChatLine) => (l.kind === 'chat' ? `${l.name}：${l.text}` : l.text)
const clock = (iso: string) => new Date(iso).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })

function toggle() {
  open.value = !open.value
  try { localStorage.setItem(OPEN_KEY, open.value ? '1' : '0') } catch { /* 无痕模式等存不了，忽略 */ }
  if (open.value) { unread.value = 0; void scrollToEnd(true) }
}

/** 新消息来了：如果本来就在底部（或强制），就滚到最新一条 */
async function scrollToEnd(force = false) {
  const el = listEl.value
  const nearBottom = !el || el.scrollHeight - el.scrollTop - el.clientHeight < 40
  await nextTick()
  if (listEl.value && (force || nearBottom)) listEl.value.scrollTop = listEl.value.scrollHeight
}
watch(() => lastLine.value?.id, (id, old) => {
  if (id === undefined || id === old) return
  if (open.value) void scrollToEnd()
  else if (old !== undefined) unread.value++
})

function showError(t: string) {
  error.value = t
  window.clearTimeout(errorTimer)
  errorTimer = window.setTimeout(() => { error.value = '' }, 3000)
}

function startCooldown() {
  cooling.value = props.cooldown
  window.clearInterval(coolTimer)
  coolTimer = window.setInterval(() => {
    cooling.value = Math.max(0, cooling.value - 1)
    if (!cooling.value) window.clearInterval(coolTimer)
  }, 1000)
}

async function submit(quick?: string) {
  const content = (quick ?? text.value).trim()
  if (!content || !props.canSend || sending.value || cooling.value > 0) return
  if (content.length > props.maxLength) { showError(`一条最多 ${props.maxLength} 个字`); return }
  sending.value = true
  try {
    const err = await props.send(content)
    if (err) showError(err)
    else {
      if (!quick) text.value = ''
      startCooldown()
      void scrollToEnd(true)
    }
  } finally { sending.value = false }
}

onBeforeUnmount(() => { window.clearInterval(coolTimer); window.clearTimeout(errorTimer) })
</script>

<style scoped>
.dchat { border: 1px solid var(--border-line); border-top: 0; background: var(--paper-card); color: var(--text-primary); }
.dchat-head { display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 12px; border: 0; border-radius: 0; background: var(--bg-sub); color: inherit; font: inherit; font-size: 13px; text-align: left; cursor: pointer; touch-action: manipulation; }
.dchat-head:hover { color: var(--accent-color); }
.dchat-title { font-weight: 600; white-space: nowrap; }
.dchat-unread { padding: 1px 8px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; white-space: nowrap; }
.dchat-peek { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-secondary); font-size: 12px; }
.dchat-fold { margin-left: auto; color: var(--text-secondary); font: 11px var(--font-mono, monospace); white-space: nowrap; }
.dchat-list { display: grid; align-content: start; gap: 4px; height: 168px; margin: 0; padding: 8px 12px; overflow-y: auto; list-style: none; overscroll-behavior: contain; }
.dchat-empty { color: var(--text-secondary); font-size: 12px; }
.dchat-line { display: flex; align-items: baseline; gap: 6px; font-size: 13px; line-height: 1.5; }
.dchat-line b { flex-shrink: 0; max-width: 8em; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--accent-color); font-weight: 600; }
.dchat-line b::after { content: '：'; color: var(--text-secondary); font-weight: 400; }
.dchat-line span { min-width: 0; word-break: break-word; }
.dchat-line time { margin-left: auto; flex-shrink: 0; color: var(--text-secondary); font: 10px var(--font-mono, monospace); opacity: .7; }
.dchat-line.mine b { color: var(--text-primary); }
.dchat-watch { flex-shrink: 0; align-self: center; padding: 0 3px; border: 1px solid var(--border-line); color: var(--text-secondary); font-size: 10px; line-height: 1.4; }
.dchat-line.k-like, .dchat-line.k-tomato { padding: 1px 6px; color: var(--text-secondary); font-size: 12px; background: var(--bg-sub); }
.dchat-line.k-tomato { border-left: 2px solid #e5484d; }
.dchat-line.k-like { border-left: 2px solid var(--accent-color); }
.dchat-line i { font-style: normal; }
.dchat-quick { display: flex; flex-wrap: wrap; gap: 6px; padding: 0 12px 8px; }
.dchat-quick button { padding: 3px 9px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; touch-action: manipulation; }
.dchat-quick button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.dchat-quick button:disabled { opacity: .45; cursor: default; }
.dchat-form { display: flex; gap: 0; padding: 0 12px 10px; }
.dchat-form input { flex: 1; min-width: 0; padding: 8px 10px; border: 1px solid var(--border-line); border-right: 0; border-radius: 0; background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 14px; }
.dchat-form input:focus { outline: none; border-color: var(--accent-color); }
.dchat-form button { min-width: 64px; padding: 8px 14px; border: 1px solid var(--accent-color); border-radius: 0; background: var(--accent-color); color: var(--on-accent, #fff); font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; touch-action: manipulation; }
.dchat-form button:disabled { opacity: .45; cursor: default; }
.dchat-error { margin: 0 12px 10px; padding: 3px 10px; border-left: 3px solid var(--danger, #dc2626); color: var(--danger, #dc2626); font-size: 12px; }
@media (max-width: 700px) { .dchat-list { height: 132px; } }
</style>
