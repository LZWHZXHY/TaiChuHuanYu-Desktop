<!-- 岁月史书一段下面的评论：平时只显示最近两条，「查看全部」展开；登录后可以评论、回复某条；自己删自己的，管理员能删任何一条。 -->
<template>
  <div class="hcm">
    <ol v-if="shown.length" class="hcm-list">
      <li v-for="c in shown" :key="c.id" class="hcm-item">
        <UserLink :id="c.author.id" :name="c.author.username" class="hcm-avatar-link">
          <img v-if="c.author.avatar" :src="c.author.avatar" class="hcm-avatar" alt="" />
          <span v-else class="hcm-avatar hcm-avatar-empty" aria-hidden="true">{{ c.author.username.slice(0, 1) }}</span>
        </UserLink>
        <div class="hcm-body">
          <p class="hcm-text">
            <UserLink :id="c.author.id" :name="c.author.username" class="hcm-name">{{ c.author.username }}</UserLink>
            <template v-if="c.replyTo"><span class="hcm-reply-label"> 回复 </span><UserLink :id="c.replyTo.id" :name="c.replyTo.username" class="hcm-name">@{{ c.replyTo.username }}</UserLink></template>：{{ c.content }}
          </p>
          <div class="hcm-meta">
            <time :datetime="c.createdAt" :title="fullTime(c.createdAt)">{{ shortTime(c.createdAt) }}</time>
            <button v-if="userStore.isLoggedIn" type="button" class="hcm-op" @click="startReply(c)">回复</button>
            <button v-if="c.canDelete" type="button" class="hcm-op danger" @click="remove(c)">删除</button>
          </div>
        </div>
      </li>
    </ol>
    <button v-if="!all && entry.commentCount > entry.recentComments.length" type="button" class="hcm-all" :disabled="loading" @click="loadAll">
      {{ loading ? '读取中…' : `查看全部 ${entry.commentCount} 条评论` }}
    </button>
    <button v-else-if="all && all.length > PREVIEW" type="button" class="hcm-all" @click="all = null">收起评论</button>

    <form v-if="userStore.isLoggedIn" class="hcm-form" @submit.prevent="send">
      <span v-if="replyTo" class="hcm-replying">回复 @{{ replyTo.author.username }} <button type="button" aria-label="不回复了" @click="replyTo = null">×</button></span>
      <div class="hcm-input-row">
        <textarea ref="input" v-model="text" rows="1" maxlength="500" :placeholder="replyTo ? `回复 ${replyTo.author.username}…` : '说点什么…'" aria-label="写评论"
          @keydown.enter.exact.prevent="send" @input="autoGrow"></textarea>
        <button type="submit" class="hcm-send" :disabled="busy || !text.trim()">{{ busy ? '…' : '发送' }}</button>
      </div>
      <small v-if="text.length > 400" class="hcm-count">{{ text.length }}/500</small>
    </form>
    <p v-else-if="!entry.commentCount" class="hcm-login"><RouterLink :to="{ path: LOGIN_PATH, query: { redirect: route.fullPath } }">登录</RouterLink>后可以评论</p>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { historyApi, errorText, type HistoryComment, type HistoryEntry } from '@/api/history'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import UserLink from '@/components/UserLink.vue'

const props = defineProps<{ entry: HistoryEntry }>()
const emit = defineEmits<{ update: [patch: Pick<HistoryEntry, 'commentCount' | 'recentComments'>] }>()
const userStore = useUserStore()
const route = useRoute()
const PREVIEW = 2

/** 展开后的全部评论；null = 只看最近两条 */
const all = ref<HistoryComment[] | null>(null)
const shown = computed(() => all.value ?? props.entry.recentComments)
const loading = ref(false)
async function loadAll() {
  loading.value = true
  try { all.value = await historyApi.comments(props.entry.id) }
  catch (e) { ElMessage.error(errorText(e, '评论读不出来，稍后再试')) }
  finally { loading.value = false }
}

const text = ref('')
const busy = ref(false)
const replyTo = ref<HistoryComment | null>(null)
const input = ref<HTMLTextAreaElement | null>(null)
function startReply(c: HistoryComment) {
  replyTo.value = c
  void nextTick(() => input.value?.focus())
}
function autoGrow() {
  const el = input.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = Math.min(el.scrollHeight, 160) + 'px'
}
function publish(list: HistoryComment[], count: number) {
  emit('update', { commentCount: count, recentComments: list.slice(-PREVIEW) })
}
async function send() {
  const content = text.value.trim()
  if (!content || busy.value) return
  busy.value = true
  try {
    const c = await historyApi.addComment(props.entry.id, content, replyTo.value?.id)
    text.value = ''; replyTo.value = null
    void nextTick(autoGrow)
    if (all.value) all.value = [...all.value, c]
    publish(all.value ?? [...props.entry.recentComments, c], props.entry.commentCount + 1)
  } catch (e) { ElMessage.error(errorText(e, '没发出去，稍后再试')) }
  finally { busy.value = false }
}
async function remove(c: HistoryComment) {
  if (!window.confirm(c.author.id === userStore.userId ? '删除这条评论？' : `删除「${c.author.username}」的这条评论？`)) return
  try {
    await historyApi.deleteComment(c.id)
    if (all.value) all.value = all.value.filter(x => x.id !== c.id)
    const count = Math.max(0, props.entry.commentCount - 1)
    // 只显示最近两条时删了一条：重新读一下，把更早的那条补上来
    if (!all.value && count > props.entry.recentComments.length - 1) {
      try { publish(await historyApi.comments(props.entry.id), count); return } catch { /* 读不出来就先少显示一条 */ }
    }
    publish(all.value ?? props.entry.recentComments.filter(x => x.id !== c.id), count)
  } catch (e) { ElMessage.error(errorText(e, '删除失败')) }
}

const p2 = (n: number) => String(n).padStart(2, '0')
function fullTime(iso: string) {
  const t = new Date(new Date(iso).getTime() + 8 * 3600_000)
  return `${t.getUTCFullYear()}-${p2(t.getUTCMonth() + 1)}-${p2(t.getUTCDate())} ${p2(t.getUTCHours())}:${p2(t.getUTCMinutes())}`
}
function shortTime(iso: string) {
  const diff = Date.now() - new Date(iso).getTime()
  if (diff < 60_000) return '刚刚'
  if (diff < 3600_000) return `${Math.floor(diff / 60_000)} 分钟前`
  if (diff < 86_400_000) return `${Math.floor(diff / 3600_000)} 小时前`
  return fullTime(iso).slice(0, 10)
}
</script>

<style scoped>
.hcm { margin-top: 10px; }
.hcm-list { list-style: none; margin: 0 0 6px; padding: 8px 10px; display: grid; gap: 10px; background: var(--bg-sub); }
.hcm-item { display: flex; gap: 8px; min-width: 0; }
.hcm-avatar { width: 26px; height: 26px; object-fit: cover; display: block; flex-shrink: 0; }
.hcm-avatar-empty { display: flex; align-items: center; justify-content: center; background: var(--paper-card); font-size: 12px; font-weight: 700; color: var(--text-secondary); }
.hcm-body { min-width: 0; flex: 1; }
.hcm-text { margin: 0; font-size: 13px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
.hcm-name { font-weight: 600; color: var(--text-primary); }
.hcm-reply-label { color: var(--text-secondary); }
.hcm-meta { display: flex; align-items: center; gap: 10px; margin-top: 2px; font-size: 11px; color: var(--text-secondary); }
.hcm-op { padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 11px; cursor: pointer; }
.hcm-op:hover { color: var(--accent-color); }
.hcm-op.danger:hover { color: #dc2626; }
.hcm-all { margin: 0 0 8px; padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.hcm-form { display: grid; gap: 4px; }
.hcm-replying { display: inline-flex; align-items: center; gap: 4px; justify-self: start; padding: 1px 6px; background: color-mix(in srgb, var(--accent-color) 12%, transparent); font-size: 12px; }
.hcm-replying button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; }
.hcm-input-row { display: flex; gap: 6px; align-items: flex-end; }
.hcm-input-row textarea { flex: 1; min-width: 0; box-sizing: border-box; min-height: 34px; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; line-height: 1.5; resize: none; }
.hcm-input-row textarea:focus { outline: none; border-color: var(--accent-color); }
.hcm-send { height: 34px; padding: 0 14px; border: 1px solid var(--accent-color); background: var(--accent-color); color: #fff; font: inherit; font-size: 13px; cursor: pointer; }
.hcm-send:disabled { opacity: .5; cursor: default; }
.hcm-count { justify-self: end; font-size: 11px; color: var(--text-secondary); }
.hcm-login { margin: 0; font-size: 12px; color: var(--text-secondary); }
.hcm-login a { color: var(--accent-color); }
</style>
