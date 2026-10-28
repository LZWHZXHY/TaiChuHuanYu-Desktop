<template>
  <div class="chai-comments">
    <h3 class="comments-title">💬 讨论{{ items.length ? ` (${items.length})` : '' }}</h3>

    <div v-if="replyTo" class="reply-banner">
      回复 <strong>@{{ replyTo.userName }}</strong>
      <button class="reply-cancel" @click="replyTo = null">取消</button>
    </div>

    <div class="comment-input">
      <input ref="inputRef" v-model="draft" type="text" maxlength="1000" :placeholder="replyTo ? `回复 @${replyTo.userName}…` : '写下你的评论…'" @keyup.enter="submit" />
      <button :disabled="!draft.trim() || busy" @click="submit">发送</button>
    </div>
    <p v-if="error" class="comment-error" role="alert">{{ error }}</p>

    <ul v-if="topLevel.length" class="comment-list">
      <li v-for="c in topLevel" :key="c.id" class="comment-item">
        <div class="comment-head">
          <strong>{{ c.userName }}</strong>
          <time>{{ relTime(c.createdAt) }}</time>
        </div>
        <p class="comment-content">{{ c.content || '已删除评论' }}</p>
        <div class="comment-actions">
          <button class="act-btn" :class="{ active: c.liked }" @click="like(c)">❤ {{ c.likeCount || 0 }}</button>
          <button class="act-btn" @click="startReply(c)">回复</button>
        </div>

        <ul v-if="repliesOf(c.id).length" class="reply-list">
          <li v-for="r in repliesOf(c.id)" :key="r.id" class="reply-item">
            <div class="comment-head">
              <strong>{{ r.userName }}</strong>
              <time>{{ relTime(r.createdAt) }}</time>
            </div>
            <p class="comment-content">
              <span v-if="replyName(r)" class="quote">回复 @{{ replyName(r) }}：</span>{{ r.content || '已删除评论' }}
            </p>
            <div class="comment-actions">
              <button class="act-btn" :class="{ active: r.liked }" @click="like(r)">❤ {{ r.likeCount || 0 }}</button>
              <button class="act-btn" @click="startReply(r)">回复</button>
            </div>
          </li>
        </ul>
      </li>
    </ul>
    <p v-else-if="!loading" class="comment-empty">还没有评论，来抢沙发～</p>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { chaiCommentApi, type ChaiComment } from '@/api/chaiComment'
import { useUserStore } from '@/stores/user'

const props = defineProps<{ targetType: string; targetId: string }>()
const user = useUserStore()
const items = ref<ChaiComment[]>([])
const draft = ref('')
const busy = ref(false)
const error = ref('')
const loading = ref(false)
const replyTo = ref<ChaiComment | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)

const topLevel = computed(() => items.value.filter(c => !c.parentId))
const repliesOf = (id: string) => items.value.filter(c => c.parentId === id)
const replyName = (c: ChaiComment) => items.value.find(x => x.id === c.parentId)?.userName || ''

function relTime(v: string) {
  const diff = Date.now() - new Date(v).getTime()
  const m = Math.floor(diff / 60000)
  if (m < 1) return '刚刚'
  if (m < 60) return `${m} 分钟前`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} 小时前`
  return `${Math.floor(h / 24)} 天前`
}

async function load() {
  loading.value = true
  try {
    items.value = await chaiCommentApi.list(props.targetType, props.targetId)
  } catch {
    /* 静默 */
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!draft.value.trim() || busy.value) return
  busy.value = true
  error.value = ''
  try {
    const c = await chaiCommentApi.create({
      targetType: props.targetType,
      targetId: props.targetId,
      content: draft.value.trim(),
      parentId: replyTo.value?.id
    })
    items.value.push(c)
    draft.value = ''
    replyTo.value = null
  } catch (e: any) {
    error.value = e.friendlyMessage || '评论失败，请先登录或重试'
  } finally {
    busy.value = false
  }
}

async function like(c: ChaiComment) {
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  try {
    const res = await chaiCommentApi.like(c.id)
    c.liked = res.liked
    c.likeCount = res.count
  } catch (e: any) { error.value = e.friendlyMessage || '点赞失败' }
}

function startReply(c: ChaiComment) {
  replyTo.value = c
  inputRef.value?.focus()
}

onMounted(load)
watch(() => props.targetId, load)
</script>

<style scoped>
.chai-comments { margin-top: 32px; padding-top: 20px; border-top: 1px dashed var(--border-line); }
.comments-title { margin: 0 0 14px; font-size: 1.05rem; font-weight: 600; }
.reply-banner { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; padding: 6px 12px; border-radius: 8px; background: var(--bg-sub); font-size: .8rem; color: var(--text-secondary); }
.reply-banner strong { color: var(--accent-color); }
.reply-cancel { margin-left: auto; border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: .76rem; text-decoration: underline; }
.comment-input { display: flex; gap: 10px; }
.comment-input input { flex: 1; padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); font-size: .85rem; }
.comment-input button { padding: 9px 18px; border: none; border-radius: 8px; background: var(--accent-color); color: var(--on-accent); cursor: pointer; font-size: .85rem; }
.comment-input button:disabled { opacity: .5; cursor: default; }
.comment-error { color: var(--danger); font-size: .8rem; margin: 8px 0 0; }
.comment-list { list-style: none; margin: 16px 0 0; padding: 0; display: grid; gap: 14px; }
.comment-item { padding-bottom: 12px; border-bottom: 1px solid var(--border-line); }
.comment-head { display: flex; justify-content: space-between; align-items: center; }
.comment-head strong { font-size: .85rem; }
.comment-head time { font-size: .72rem; color: var(--text-secondary); }
.comment-content { margin: 6px 0 0; font-size: .88rem; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.quote { color: var(--accent-color); }
.comment-actions { display: flex; gap: 14px; margin-top: 6px; }
.act-btn { border: 0; background: none; padding: 0; cursor: pointer; font-size: .76rem; color: var(--text-secondary); }
.act-btn:hover { color: var(--accent-color); }
.act-btn.active { color: var(--accent-color); font-weight: 600; }
.reply-list { list-style: none; margin: 10px 0 0; padding: 0 0 0 16px; border-left: 2px solid var(--border-line); display: grid; gap: 10px; }
.reply-item { padding-bottom: 8px; }
.reply-item .comment-content { font-size: .84rem; }
.comment-empty { color: var(--text-secondary); font-size: .85rem; padding: 12px 0; }
</style>
