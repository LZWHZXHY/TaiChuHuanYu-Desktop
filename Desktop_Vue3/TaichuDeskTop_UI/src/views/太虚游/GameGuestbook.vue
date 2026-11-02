<!-- GameGuestbook.vue：游戏留言板（通用，按游戏 key 区分，现在只接在斗地主大厅下面）。
     上半部分：给「这个游戏」点赞 / 砸番茄（花土豆，可以多次），显示总数和最近谁赞了 / 砸了；
     下半部分：留言、回复、删自己的（管理员能删任何一条）。 -->
<template>
  <section class="gb" :aria-label="`${title}留言板`">
    <header class="gb-head">
      <div>
        <p class="gb-eyebrow">GUESTBOOK / 留言板</p>
        <h3>大家对{{ title }}说</h3>
      </div>
      <p class="gb-sub">觉得好玩就点个赞，玩得窝火就砸个番茄（都要花土豆，花掉就没了，不会给任何人）。</p>
    </header>

    <!-- 给游戏点赞 / 砸番茄 -->
    <div class="gb-react">
      <button type="button" class="gb-rbtn like" :disabled="busyReact" @click="react('like')">
        <span class="gb-emoji" aria-hidden="true">👍</span>
        <span class="gb-rtext"><b>{{ fmtCount(reactions.likes) }}</b><small>赞一个 · {{ reactions.likeCost }} 🥔</small></span>
        <span v-for="p in pops.filter(x => x.kind === 'like')" :key="p.id" class="gb-pop" aria-hidden="true">+1</span>
      </button>
      <button type="button" class="gb-rbtn tomato" :disabled="busyReact" @click="react('tomato')">
        <span class="gb-emoji" aria-hidden="true">🍅</span>
        <span class="gb-rtext"><b>{{ fmtCount(reactions.tomatoes) }}</b><small>砸番茄 · {{ reactions.tomatoCost }} 🥔</small></span>
        <span v-for="p in pops.filter(x => x.kind === 'tomato')" :key="p.id" class="gb-pop" aria-hidden="true">+1</span>
      </button>
      <div class="gb-recent">
        <small v-if="user.isLoggedIn && (reactions.myLikes || reactions.myTomatoes)" class="gb-mine">你赞了 {{ reactions.myLikes }} 次 · 砸了 {{ reactions.myTomatoes }} 个番茄</small>
        <p v-if="!reactions.recent.length" class="gb-muted">还没人赞过或砸过，来当第一个吧。</p>
        <ul v-else aria-label="最近谁赞了 / 砸了">
          <li v-for="(r, i) in reactions.recent" :key="i" :title="`${r.name} ${r.kind === 'like' ? '赞了一下' : '砸了个番茄'} · ${ago(r.at)}`">
            <span class="gb-chip-emoji" aria-hidden="true">{{ r.kind === 'like' ? '👍' : '🍅' }}</span>{{ r.name }}<em>{{ ago(r.at) }}</em>
          </li>
        </ul>
      </div>
      <p v-if="reactTip" class="gb-tip" role="status">{{ reactTip }}</p>
    </div>

    <!-- 写留言 -->
    <form v-if="user.isLoggedIn" class="gb-compose" @submit.prevent="post()">
      <textarea v-model="draft" :maxlength="MAX" rows="3" placeholder="说说你对这个游戏的感受、建议，或者约人来打牌…" aria-label="写留言"></textarea>
      <div class="gb-compose-foot">
        <small :class="{ warn: draft.length > MAX - 30 }">{{ draft.length }} / {{ MAX }}</small>
        <button type="submit" class="gb-btn primary" :disabled="posting || !draft.trim()">{{ posting ? '发送中…' : '发表留言' }}</button>
      </div>
    </form>
    <p v-else class="gb-login">登录后才能留言、点赞和砸番茄。</p>
    <p v-if="error" class="gb-error" role="alert">{{ error }}</p>

    <!-- 留言列表 -->
    <ul class="gb-list">
      <li v-if="loading && !items.length" class="gb-muted gb-empty">正在读取留言…</li>
      <li v-else-if="!items.length" class="gb-muted gb-empty">还没有人留言，抢个沙发吧。</li>
      <li v-for="m in items" :key="m.id" class="gb-item" :class="{ deleted: m.deleted }">
        <template v-if="m.deleted">
          <p class="gb-gone">这条留言已删除</p>
        </template>
        <template v-else>
          <div class="gb-avatar-col">
            <AvatarFrame :user-id="m.userId" size="sm"><span class="gb-avatar"><img v-if="m.avatar" :src="m.avatar" alt="" loading="lazy" /><template v-else>{{ m.name.slice(0, 1) }}</template></span></AvatarFrame>
          </div>
          <div class="gb-body">
            <p class="gb-meta"><UserLink :id="m.userId" :name="m.name" class="gb-name" /><time :datetime="m.createdAt" :title="full(m.createdAt)">{{ ago(m.createdAt) }}</time></p>
            <p class="gb-text">{{ m.content }}</p>
            <p class="gb-actions">
              <button v-if="user.isLoggedIn" type="button" class="gb-link" @click="openReply(m.id, m.id, m.name)">回复</button>
              <button v-if="m.canDelete" type="button" class="gb-link danger" @click="remove(m.id)">删除</button>
            </p>
          </div>
        </template>

        <!-- 回复 -->
        <ul v-if="m.replies.length" class="gb-replies">
          <li v-for="r in m.replies" :key="r.id" class="gb-reply">
            <AvatarFrame :user-id="r.userId" size="sm"><span class="gb-avatar sm"><img v-if="r.avatar" :src="r.avatar" alt="" loading="lazy" /><template v-else>{{ r.name.slice(0, 1) }}</template></span></AvatarFrame>
            <div class="gb-body">
              <p class="gb-meta">
                <UserLink :id="r.userId" :name="r.name" class="gb-name" />
                <template v-if="r.replyToName && r.replyToUserId !== m.userId"><span class="gb-to">回复</span><UserLink :id="r.replyToUserId" :name="r.replyToName" class="gb-name" /></template>
                <time :datetime="r.createdAt" :title="full(r.createdAt)">{{ ago(r.createdAt) }}</time>
              </p>
              <p class="gb-text">{{ r.content }}</p>
              <p class="gb-actions">
                <button v-if="user.isLoggedIn" type="button" class="gb-link" @click="openReply(m.id, r.id, r.name)">回复</button>
                <button v-if="r.canDelete" type="button" class="gb-link danger" @click="remove(r.id, m.id)">删除</button>
              </p>
            </div>
          </li>
        </ul>

        <!-- 回复框：点哪条的「回复」就出现在那条主留言下面 -->
        <form v-if="replying && replying.rootId === m.id" class="gb-compose reply" @submit.prevent="post(replying.targetId)">
          <textarea ref="replyBox" v-model="replyDraft" :maxlength="MAX" rows="2" :placeholder="`回复 ${replying.name}…`" :aria-label="`回复 ${replying.name}`" @keydown.esc="replying = null"></textarea>
          <div class="gb-compose-foot">
            <small>{{ replyDraft.length }} / {{ MAX }}</small>
            <button type="button" class="gb-btn" @click="replying = null">取消</button>
            <button type="submit" class="gb-btn primary" :disabled="posting || !replyDraft.trim()">{{ posting ? '发送中…' : '回复' }}</button>
          </div>
        </form>
      </li>
    </ul>
    <button v-if="hasMore" type="button" class="gb-btn more" :disabled="loading" @click="load(true)">{{ loading ? '读取中…' : '看看更早的留言' }}</button>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useUserStore } from '@/stores/user'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserLink from '@/components/UserLink.vue'
import { notifyPotatoChanged } from '@/api/potato'
import { errorText, gameBoardApi, type BoardMessage, type GameReactions, type ReactKind } from '@/api/gameSocial'

const props = withDefaults(defineProps<{
  /** 游戏 key，和后端 GameSocialService.Games 对应（斗地主是 ddz） */
  game: string
  /** 显示用的游戏名 */
  title?: string
}>(), { title: '这个游戏' })

/** 留言最多多少字（和后端 BoardMaxLength 一致） */
const MAX = 500

const user = useUserStore()
const items = ref<BoardMessage[]>([])
const hasMore = ref(false)
const loading = ref(false)
const posting = ref(false)
const error = ref('')
const draft = ref('')
const replyDraft = ref('')
const replying = ref<{ rootId: number; targetId: number; name: string } | null>(null)
const replyBox = ref<HTMLTextAreaElement[] | HTMLTextAreaElement | null>(null)
const reactions = ref<GameReactions>({ likes: 0, tomatoes: 0, myLikes: 0, myTomatoes: 0, likeCost: 1, tomatoCost: 2, recent: [] })
const busyReact = ref(false)
const reactTip = ref('')
/** 点完按钮冒出来的「+1」 */
const pops = ref<{ id: number; kind: ReactKind }[]>([])
let popSeq = 0
let errorTimer = 0, tipTimer = 0

function showError(text: string) {
  error.value = text
  window.clearTimeout(errorTimer)
  errorTimer = window.setTimeout(() => { error.value = '' }, 4000)
}
function showTip(text: string) {
  reactTip.value = text
  window.clearTimeout(tipTimer)
  tipTimer = window.setTimeout(() => { reactTip.value = '' }, 2500)
}

// ---------- 时间显示 ----------
function ago(iso: string) {
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return ''
  const s = Math.max(0, (Date.now() - t) / 1000)
  if (s < 60) return '刚刚'
  if (s < 3600) return `${Math.floor(s / 60)} 分钟前`
  if (s < 86400) return `${Math.floor(s / 3600)} 小时前`
  if (s < 86400 * 7) return `${Math.floor(s / 86400)} 天前`
  return new Date(t).toLocaleDateString('zh-CN', { month: 'numeric', day: 'numeric' })
}
const full = (iso: string) => new Date(iso).toLocaleString('zh-CN')
const fmtCount = (n: number) => (n >= 10000 ? `${(n / 10000).toFixed(1)} 万` : String(n))

// ---------- 读取 ----------
async function load(more = false) {
  if (loading.value) return
  loading.value = true
  try {
    const before = more && items.value.length ? items.value[items.value.length - 1]!.id : undefined
    const page = await gameBoardApi.messages(props.game, before)
    items.value = more ? [...items.value, ...page.items] : page.items
    hasMore.value = page.hasMore
  } catch (e) {
    if (more) showError(errorText(e, '留言没读出来，稍后再试'))
  } finally { loading.value = false }
}
async function loadReactions() {
  try { reactions.value = await gameBoardApi.reactions(props.game) } catch { /* 后端没部署时保持 0 */ }
}

// ---------- 点赞 / 砸番茄 ----------
async function react(kind: ReactKind) {
  if (!user.isLoggedIn) { showTip('登录后才能点赞、砸番茄'); return }
  if (busyReact.value) return
  busyReact.value = true
  try {
    const res = await gameBoardApi.react(props.game, kind)
    reactions.value = res.reactions
    const id = ++popSeq
    pops.value = [...pops.value, { id, kind }]
    window.setTimeout(() => { pops.value = pops.value.filter(p => p.id !== id) }, 900)
    showTip(kind === 'like' ? `赞了一下，花了 ${res.reactions.likeCost} 个土豆` : `砸了个番茄，花了 ${res.reactions.tomatoCost} 个土豆`)
    notifyPotatoChanged()
  } catch (e) {
    showTip(errorText(e, '网络不稳定，没送出去'))
  } finally { busyReact.value = false }
}

// ---------- 留言 / 回复 / 删除 ----------
async function openReply(rootId: number, targetId: number, name: string) {
  replying.value = { rootId, targetId, name }
  replyDraft.value = ''
  await nextTick()
  const box = Array.isArray(replyBox.value) ? replyBox.value[0] : replyBox.value
  box?.focus()
}

async function post(parentId?: number) {
  const text = (parentId ? replyDraft.value : draft.value).trim()
  if (!text || posting.value) return
  posting.value = true
  try {
    const saved = await gameBoardApi.post(props.game, text, parentId)
    if (saved.parentId) {
      // 回复：挂到对应主留言下面
      const root = items.value.find(m => m.id === saved.parentId)
      if (root) root.replies = [...root.replies, saved]
      replyDraft.value = ''
      replying.value = null
    } else {
      items.value = [{ ...saved, deleted: false, replies: [] }, ...items.value]
      draft.value = ''
    }
  } catch (e) {
    showError(errorText(e, '没发出去，稍后再试'))
  } finally { posting.value = false }
}

async function remove(id: number, rootId?: number) {
  if (!window.confirm('确定删除这条留言吗？删了就找不回来了。')) return
  try {
    await gameBoardApi.remove(props.game, id)
    if (rootId) {
      const root = items.value.find(m => m.id === rootId)
      if (root) root.replies = root.replies.filter(r => r.id !== id)
    } else {
      const root = items.value.find(m => m.id === id)
      // 下面还有回复的就留个「已删除」的壳，没有回复的直接拿掉
      if (root && root.replies.length) { root.deleted = true; root.content = ''; root.canDelete = false }
      else items.value = items.value.filter(m => m.id !== id)
    }
  } catch (e) {
    showError(errorText(e, '没删掉，稍后再试'))
  }
}

onMounted(() => { void load(); void loadReactions() })
onBeforeUnmount(() => { window.clearTimeout(errorTimer); window.clearTimeout(tipTimer) })
</script>

<style scoped>
.gb { position: relative; display: grid; gap: 14px; padding: 18px 20px 20px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); }
.gb::before { content: ''; position: absolute; left: -1px; top: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.gb-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 6px 20px; }
.gb-eyebrow { margin: 0 0 4px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.gb-head h3 { margin: 0; font-size: 17px; }
.gb-sub { margin: 0; max-width: 460px; color: var(--text-secondary); font-size: 12px; line-height: 1.6; }
.gb-muted { margin: 0; color: var(--text-secondary); font-size: 13px; }

/* 点赞 / 砸番茄 */
.gb-react { display: grid; grid-template-columns: auto auto minmax(0, 1fr); align-items: stretch; gap: 10px; padding-bottom: 14px; border-bottom: 1px dashed var(--border-line); }
.gb-rbtn { position: relative; display: flex; align-items: center; gap: 10px; min-width: 150px; padding: 10px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-sub); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; touch-action: manipulation; transition: border-color .15s, transform .1s; }
.gb-rbtn:hover:not(:disabled) { border-color: var(--accent-color); }
.gb-rbtn:active:not(:disabled) { transform: scale(.97); }
.gb-rbtn:disabled { opacity: .6; cursor: default; }
.gb-rbtn.like { border-left: 3px solid var(--accent-color); }
.gb-rbtn.tomato { border-left: 3px solid #e5484d; }
.gb-emoji { font-size: 26px; line-height: 1; }
.gb-rtext { display: grid; gap: 2px; }
.gb-rtext b { font: 700 20px/1 var(--font-mono, monospace); font-variant-numeric: tabular-nums; }
.gb-rtext small { color: var(--text-secondary); font-size: 11px; }
.gb-pop { position: absolute; right: 10px; top: 4px; color: var(--accent-color); font: 700 14px var(--font-mono, monospace); pointer-events: none; animation: gb-pop .9s ease-out forwards; }
.gb-rbtn.tomato .gb-pop { color: #e5484d; }
@keyframes gb-pop { from { transform: translateY(0); opacity: 1; } to { transform: translateY(-26px); opacity: 0; } }
.gb-recent { display: grid; align-content: center; gap: 6px; min-width: 0; }
.gb-mine { color: var(--text-secondary); font-size: 11px; }
.gb-recent ul { display: flex; flex-wrap: wrap; gap: 6px; margin: 0; padding: 0; list-style: none; }
.gb-recent li { display: inline-flex; align-items: center; gap: 4px; max-width: 180px; padding: 2px 8px; border: 1px solid var(--border-line); background: var(--bg-sub); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.gb-recent li em { margin-left: 2px; color: var(--text-secondary); font-style: normal; font-size: 11px; }
.gb-chip-emoji { font-size: 13px; }
.gb-tip { grid-column: 1 / -1; margin: 0; padding: 4px 10px; border-left: 3px solid var(--accent-color); background: var(--accent-soft, color-mix(in srgb, var(--accent-color) 8%, transparent)); font-size: 12px; }

/* 写留言 */
.gb-compose { display: grid; gap: 6px; }
.gb-compose textarea { width: 100%; box-sizing: border-box; padding: 9px 11px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 14px; line-height: 1.6; resize: vertical; }
.gb-compose textarea:focus { outline: none; border-color: var(--accent-color); }
.gb-compose-foot { display: flex; align-items: center; justify-content: flex-end; gap: 8px; }
.gb-compose-foot small { margin-right: auto; color: var(--text-secondary); font: 11px var(--font-mono, monospace); }
.gb-compose-foot small.warn { color: #d97706; }
.gb-compose.reply { margin: 8px 0 0 46px; }
.gb-btn { padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; touch-action: manipulation; }
.gb-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.gb-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.gb-btn.primary:hover:not(:disabled) { color: var(--on-accent, #fff); filter: brightness(1.08); }
.gb-btn:disabled { opacity: .5; cursor: default; }
.gb-btn.more { justify-self: center; }
.gb-login { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); font-size: 13px; color: var(--text-secondary); }
.gb-error { margin: 0; padding: 4px 12px; border-left: 3px solid var(--danger, #dc2626); background: color-mix(in srgb, var(--danger, #dc2626) 10%, var(--paper-card)); color: var(--danger, #dc2626); font-size: 13px; }

/* 留言列表 */
.gb-list { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.gb-empty { padding: 18px 0; text-align: center; }
.gb-item { display: grid; grid-template-columns: 36px minmax(0, 1fr); column-gap: 10px; padding: 12px 0; border-top: 1px solid var(--border-line); }
.gb-item:first-child { border-top: 0; }
.gb-item > .gb-replies, .gb-item > .gb-compose { grid-column: 2; }
.gb-item.deleted > .gb-replies, .gb-item.deleted > .gb-compose { grid-column: 1 / -1; }
.gb-gone { grid-column: 1 / -1; margin: 0; color: var(--text-secondary); font-size: 13px; font-style: italic; }
.gb-avatar { display: grid; place-items: center; width: 36px; height: 36px; overflow: hidden; border: 1px solid var(--accent-color); background: var(--accent-soft, color-mix(in srgb, var(--accent-color) 10%, var(--paper-card))); color: var(--accent-color); font-weight: 700; font-size: 14px; }
.gb-avatar.sm { width: 28px; height: 28px; font-size: 12px; }
.gb-avatar img { width: 100%; height: 100%; object-fit: cover; }
.gb-body { display: grid; gap: 4px; min-width: 0; }
.gb-meta { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; margin: 0; font-size: 13px; }
.gb-name { font-weight: 600; }
.gb-to { color: var(--text-secondary); font-size: 12px; }
.gb-meta time { color: var(--text-secondary); font-size: 11px; }
.gb-text { margin: 0; font-size: 14px; line-height: 1.7; white-space: pre-wrap; word-break: break-word; }
.gb-actions { display: flex; gap: 12px; margin: 0; }
.gb-link { padding: 4px 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; touch-action: manipulation; }
.gb-link:hover { color: var(--accent-color); }
.gb-link.danger:hover { color: var(--danger, #dc2626); }
.gb-replies { display: grid; gap: 10px; margin: 10px 0 0; padding: 10px 0 0 12px; border-left: 2px solid var(--border-line); list-style: none; }
.gb-reply { display: grid; grid-template-columns: 28px minmax(0, 1fr); gap: 8px; }

@media (max-width: 700px) {
  .gb { padding: 14px 12px 16px; }
  .gb-react { grid-template-columns: 1fr 1fr; }
  .gb-rbtn { min-width: 0; }
  .gb-recent { grid-column: 1 / -1; }
  .gb-compose.reply { margin-left: 0; }
}
@media (prefers-reduced-motion: reduce) { .gb-pop { animation-duration: .01s; } .gb-rbtn { transition: none; } }
</style>
