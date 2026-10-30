<script setup lang="ts">
// 个人主页留言板：复用通用评论接口（targetType = 'profile'，targetId = 主页主人的用户 ID）。
// 任何人可看；登录后可留言、回复；本人可删自己的留言，主页主人可删自己留言板上的任何留言。
import { computed, onMounted, ref } from 'vue'
import { chaiCommentApi, type ChaiComment } from '@/api/chaiComment'
import { useUserStore } from '@/stores/user'
import { relativeTime } from '@/utils/postCard'
import UserLink from '@/components/UserLink.vue'
import { LOGIN_PATH } from '@/router/auth'

const props = defineProps<{ ownerId: string; ownerName: string }>()
const emit = defineEmits<{ count: [number] }>()

const TARGET = 'profile'
const user = useUserStore()
const items = ref<ChaiComment[]>([])
const loading = ref(true), loadError = ref('')
const draft = ref(''), busy = ref(false), sendError = ref('')
const replyTo = ref<ChaiComment | null>(null)
const input = ref<HTMLTextAreaElement>()

const me = computed(() => String(user.userId || '').toLowerCase())
const isWallOwner = computed(() => Boolean(me.value) && me.value === props.ownerId.toLowerCase())
const visible = computed(() => items.value.filter(c => c.content || items.value.some(r => r.parentId === c.id)))
// 最新的留言排在最前；回复按时间顺序跟在留言下面。
const threads = computed(() => visible.value.filter(c => !c.parentId).slice().reverse())
const repliesOf = (id: string) => visible.value.filter(c => c.parentId === id)
const total = computed(() => items.value.filter(c => c.content).length)

async function load() {
  loading.value = true; loadError.value = ''
  try {
    items.value = await chaiCommentApi.list(TARGET, props.ownerId) || []
    emit('count', total.value)
  } catch { loadError.value = '留言加载失败。' }
  finally { loading.value = false }
}
onMounted(load)

function startReply(c: ChaiComment) {
  replyTo.value = c
  input.value?.focus()
}

async function send() {
  const content = draft.value.trim()
  if (!content || busy.value) return
  busy.value = true; sendError.value = ''
  try {
    const created = await chaiCommentApi.create({ targetType: TARGET, targetId: props.ownerId, content, parentId: replyTo.value?.id })
    items.value = [...items.value, created]
    draft.value = ''; replyTo.value = null
    emit('count', total.value)
  } catch (e: any) {
    sendError.value = e?.response?.data?.message || e?.friendlyMessage || '发送失败，请重试。'
  } finally { busy.value = false }
}

const canDelete = (c: ChaiComment) => Boolean(c.content) && (isWallOwner.value || String(c.userId).toLowerCase() === me.value)
async function remove(c: ChaiComment) {
  if (!confirm('确定删除这条留言吗？')) return
  try {
    await chaiCommentApi.remove(c.id)
    c.content = ''
    items.value = [...items.value]
    emit('count', total.value)
  } catch { sendError.value = '删除失败，请重试。' }
}
</script>

<template>
  <section class="guestbook" aria-label="留言板">
    <div class="gb-composer">
      <template v-if="user.isLoggedIn">
        <p v-if="replyTo" class="gb-replying">
          回复 <strong>{{ replyTo.userName }}</strong>
          <button type="button" class="gb-text-btn" @click="replyTo = null">取消</button>
        </p>
        <textarea
          ref="input"
          v-model="draft"
          maxlength="1000"
          rows="3"
          :placeholder="replyTo ? `回复 ${replyTo.userName}…` : isWallOwner ? '在自己的留言板写点什么…' : `给 ${ownerName} 留言…`"
          @keydown.ctrl.enter="send"
          @keydown.meta.enter="send"
        ></textarea>
        <div class="gb-actions">
          <small>{{ draft.length }}/1000 · Ctrl + Enter 发送</small>
          <button type="button" class="btn-line btn-primary" :disabled="!draft.trim() || busy" @click="send">{{ busy ? '发送中…' : replyTo ? '回复' : '留言' }}</button>
        </div>
        <p v-if="sendError" class="gb-error" role="alert">{{ sendError }}</p>
      </template>
      <p v-else class="gb-login">
        <RouterLink :to="{ path: LOGIN_PATH, query: { redirect: $route.fullPath } }">登录</RouterLink> 后可以留言。
      </p>
    </div>

    <p v-if="loading" class="gb-state" role="status">正在加载留言…</p>
    <p v-else-if="loadError" class="gb-state" role="alert">{{ loadError }} <button type="button" class="gb-text-btn" @click="load">重试</button></p>
    <p v-else-if="!threads.length" class="gb-state">还没有人留言，来做第一个吧。</p>

    <ol v-else class="gb-list">
      <li v-for="c in threads" :key="c.id" class="gb-item">
        <UserLink class="gb-avatar" :id="c.content ? c.userId : null" :name="c.userName">
          <span aria-hidden="true">{{ c.userName?.[0] || '?' }}</span>
          <img v-if="c.avatar" :src="c.avatar" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
        </UserLink>
        <div class="gb-body">
          <div class="gb-meta">
            <UserLink v-if="c.content" :id="c.userId" :name="c.userName"><strong>{{ c.userName }}</strong></UserLink>
            <strong v-else>已删除</strong>
            <b v-if="c.content && String(c.userId).toLowerCase() === ownerId.toLowerCase()" class="gb-owner">主人</b>
            <time :datetime="c.createdAt">{{ relativeTime(c.createdAt) }}</time>
          </div>
          <p class="gb-content" :class="{ deleted: !c.content }">{{ c.content || '这条留言已删除。' }}</p>
          <div class="gb-tools">
            <button v-if="user.isLoggedIn && c.content" type="button" class="gb-text-btn" @click="startReply(c)">回复</button>
            <button v-if="canDelete(c)" type="button" class="gb-text-btn danger" @click="remove(c)">删除</button>
          </div>

          <ul v-if="repliesOf(c.id).length" class="gb-replies">
            <li v-for="r in repliesOf(c.id)" :key="r.id">
              <div class="gb-meta">
                <UserLink v-if="r.content" :id="r.userId" :name="r.userName"><strong>{{ r.userName }}</strong></UserLink>
                <strong v-else>已删除</strong>
                <b v-if="r.content && String(r.userId).toLowerCase() === ownerId.toLowerCase()" class="gb-owner">主人</b>
                <time :datetime="r.createdAt">{{ relativeTime(r.createdAt) }}</time>
              </div>
              <p class="gb-content" :class="{ deleted: !r.content }">{{ r.content || '这条回复已删除。' }}</p>
              <div class="gb-tools">
                <button v-if="canDelete(r)" type="button" class="gb-text-btn danger" @click="remove(r)">删除</button>
              </div>
            </li>
          </ul>
        </div>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.guestbook { display: grid; gap: 20px; }
.gb-composer { padding: 16px; border: 1px solid var(--border-line); border-top: 2px solid var(--accent-color); background: var(--paper-card); }
.gb-composer textarea { width: 100%; box-sizing: border-box; resize: vertical; min-height: 76px; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-bg); color: var(--text-primary); font: inherit; line-height: 1.6; }
.gb-composer textarea:focus { outline: none; border-color: var(--accent-color); box-shadow: inset 3px 0 var(--signal-mint); }
.gb-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-top: 10px; }
.gb-actions small { color: var(--text-secondary); font: 11px var(--font-mono); }
.gb-replying { display: flex; align-items: center; gap: 8px; margin: 0 0 8px; font-size: 13px; color: var(--text-secondary); }
.gb-login { margin: 0; color: var(--text-secondary); font-size: 14px; }
.gb-login a { color: var(--accent-color); }
.gb-error { margin: 8px 0 0; color: var(--danger, var(--accent-color)); font-size: 13px; }
.gb-state { margin: 0; padding: 24px 0; text-align: center; color: var(--text-secondary); font-size: 14px; }

.gb-list { list-style: none; margin: 0; padding: 0; border-top: 1px solid var(--border-line); }
.gb-item { display: flex; gap: 14px; padding: 18px 0; border-bottom: 1px solid var(--border-line); }
.gb-avatar { position: relative; flex: 0 0 38px; width: 38px; height: 38px; display: grid; place-items: center; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint)); color: #fff; font-weight: 600; clip-path: polygon(0 0, 100% 0, 100% calc(100% - 8px), calc(100% - 8px) 100%, 0 100%); }
.gb-avatar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
.gb-body { flex: 1; min-width: 0; }
.gb-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; font-size: 14px; }
.gb-meta time { color: var(--text-secondary); font: 11px var(--font-mono); }
.gb-owner { padding: 1px 6px; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); font: 600 10px/1.5 var(--font-mono); }
.gb-content { margin: 6px 0 0; line-height: 1.75; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
.gb-content.deleted { color: var(--text-secondary); font-style: italic; }
.gb-tools { display: flex; gap: 14px; margin-top: 6px; }
.gb-text-btn { padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.gb-text-btn:hover { color: var(--accent-color); }
.gb-text-btn.danger:hover { color: var(--danger, #c0392b); }
.gb-replies { list-style: none; margin: 12px 0 0; padding: 0 0 0 14px; border-left: 2px solid var(--border-line); display: grid; gap: 12px; }
.gb-replies .gb-meta { font-size: 13px; }
@media (max-width: 640px) { .gb-actions small { display: none; } .gb-item { gap: 10px; } .gb-avatar { flex-basis: 32px; width: 32px; height: 32px; } }
</style>
