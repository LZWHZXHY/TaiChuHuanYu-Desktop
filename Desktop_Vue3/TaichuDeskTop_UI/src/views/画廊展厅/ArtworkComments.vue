<template>
  <section class="comments" aria-label="作品评论">
    <header class="comments-heading"><h2>评论 <span>{{ store.stats[artworkId]?.commentsCount ?? initialCount }}</span></h2><span>聊聊你的想法</span></header>
    <form v-if="user.isLoggedIn" class="comment-composer" @submit.prevent="submit">
      <div class="comment-avatar" aria-hidden="true"><span>{{ user.userInfo?.username?.[0] || '我' }}</span><img v-if="user.userInfo?.avatar" :src="user.userInfo.avatar" alt="" @error="($event.target as HTMLImageElement).hidden = true" /></div>
      <div class="composer-body">
        <div v-if="replyTo" class="reply-preview">
          <div><span>回复 <strong>{{ replyTo.authorName }}</strong></span><button type="button" class="text-action" :disabled="busy" @click="replyTo = null">取消回复</button></div>
          <p>{{ replyTo.content }}</p>
        </div>
        <div class="composer-field">
          <label class="sr-only" :for="'artwork-comment-' + artworkId">{{ replyTo ? '写下回复' : '写下你的想法' }}</label>
          <textarea :id="'artwork-comment-' + artworkId" ref="input" v-model="draft" maxlength="1000" rows="3" :disabled="busy" :placeholder="replyTo ? '写下你的回复…' : '分享感受，也欢迎认真交流…'" />
          <div class="form-actions"><span>{{ draft.length }} / 1000</span><button class="submit-comment" :disabled="busy || !draft.trim()">{{ busy ? '处理中…' : replyTo ? '发送回复' : '发表评论' }}</button></div>
        </div>
      </div>
    </form>
    <p v-else class="comment-state">登录后可以发表评论、参与回复。</p>
    <p v-if="error" role="alert" class="comment-error">{{ error }}</p>
    <p v-if="loading && !items.length" class="comment-state" role="status">正在加载评论…</p>
    <div v-else-if="!items.length && !loadError" class="comment-state"><strong>还没有评论</strong><p>你的感受，也许就是创作者期待的回应。</p></div>
    <ol class="comment-list">
      <li v-for="item in items" :key="item.id" :class="{ 'is-reply': item.parentId, 'is-deleted': item.isDeleted }">
        <UserLink class="comment-avatar" :id="item.isDeleted ? null : item.userId" :name="item.authorName"><span aria-hidden="true">{{ item.authorName?.[0] || '访' }}</span><img v-if="item.avatar" :src="item.avatar" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" /></UserLink>
        <div class="comment-body">
          <div class="comment-meta"><UserLink :id="item.isDeleted ? null : item.userId" :name="item.authorName"><strong>{{ item.authorName || '社区用户' }}</strong></UserLink><time :datetime="item.createdAt" :title="new Date(item.createdAt).toLocaleString()">{{ formatDate(item.createdAt) }}</time></div>
          <p v-if="item.replyToAuthor" class="reply-context">回复 <span>{{ item.replyToAuthor }}</span></p>
          <p class="comment-content">{{ item.isDeleted ? '这条评论已删除，回复仍然保留。' : item.content }}</p>
          <div v-if="!item.isDeleted" class="comment-actions">
            <button v-if="user.isLoggedIn" class="text-action" :disabled="busy" @click="startReply(item)">回复</button>
            <template v-if="item.canDelete">
              <button v-if="deleteId !== item.id" class="text-action delete-action" :disabled="busy" @click="deleteId = item.id">删除</button>
              <template v-else><span class="delete-hint">删除这条评论？</span><button class="text-action danger-action" :disabled="busy" @click="remove(item.id)">确认删除</button><button class="text-action" :disabled="busy" @click="deleteId = null">取消</button></template>
            </template>
          </div>
        </div>
      </li>
    </ol>
    <button v-if="loadError || hasMore" class="load-comments" :disabled="loading" @click="load()">{{ loading ? '加载中…' : loadError ? '重新加载评论' : '加载更多评论' }}</button>
  </section>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import { computed, onUnmounted, ref, watch } from 'vue'
import { artworkApi, type ArtworkComment } from '../../api/artwork'
import { useArtworkStore } from '../../stores/artwork'
import { useUserStore } from '../../stores/user'

const props = defineProps<{ artworkId: number; initialCount: number }>()
const store = useArtworkStore(), user = useUserStore()
const items = ref<ArtworkComment[]>([]), draft = ref(''), error = ref('')
const loading = ref(false), loadError = ref(false), hasMore = ref(false)
const replyTo = ref<ArtworkComment | null>(null), deleteId = ref<string | null>(null)
const input = ref<HTMLTextAreaElement>()
function startReply(item: ArtworkComment) {
  replyTo.value = item
  input.value?.scrollIntoView({ block: 'center' })
  input.value?.focus({ preventScroll: true })
}
const formatDate = (value: string) => new Date(value).toLocaleDateString('zh-CN', { year: 'numeric', month: 'short', day: 'numeric' })
const busy = computed(() => !!store.pending[props.artworkId])
let generation = 0, request = 0, offset = 0

async function load(reset = false) {
  if (loading.value && !reset) return
  const current = ++request, scope = generation
  if (reset) offset = 0
  loading.value = true
  loadError.value = false
  error.value = ''
  try {
    const page = await artworkApi.getComments(props.artworkId, offset)
    if (scope !== generation || current !== request) return
    items.value = reset ? page.items : [...new Map([...items.value, ...page.items].map(c => [c.id, c])).values()]
    offset += page.items.length
    hasMore.value = page.hasMore
  } catch (e: any) {
    if (scope === generation && current === request) { loadError.value = true; error.value = e.friendlyMessage || '评论加载失败，请重试' }
  } finally {
    if (scope === generation && current === request) loading.value = false
  }
}

async function submit() {
  if (!user.isLoggedIn || busy.value || !draft.value.trim()) return
  const scope = generation, id = props.artworkId
  error.value = ''
  try {
    const result = await store.mutate(id, () => artworkApi.addComment(id, draft.value.trim(), replyTo.value?.id))
    if (!result || scope !== generation) return
    draft.value = ''; replyTo.value = null
    await load(true)
  } catch (e: any) {
    if (scope === generation) error.value = e.friendlyMessage || '发表失败，输入内容已保留，请重试'
  }
}

async function remove(commentId: string) {
  if (busy.value) return
  const scope = generation, id = props.artworkId
  error.value = ''
  try {
    const result = await store.mutate(id, () => artworkApi.deleteComment(id, commentId))
    if (!result || scope !== generation) return
    deleteId.value = null
    if (replyTo.value?.id === commentId) replyTo.value = null
    await load(true)
  } catch (e: any) {
    if (scope === generation) error.value = e.friendlyMessage || '删除失败，请重试'
  }
}

watch(() => [props.artworkId, user.token], () => {
  generation++
  items.value = []; draft.value = ''; replyTo.value = null; deleteId.value = null
  hasMore.value = false
  void load(true)
}, { immediate: true })
onUnmounted(() => { generation++; request++ })
</script>

<style scoped>
.comments { margin-top: 32px; padding-top: 28px; border-top: 1px solid var(--border-line); color: var(--text-primary); text-align: left; }
.comments-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 22px; }
.comments-heading h2 { display: flex; align-items: center; gap: 10px; margin: 0; font-size: 1.1rem; }
.comments-heading h2 span { min-width: 24px; padding: 2px 7px; border-radius: 0; background: var(--bg-sub); font-size: .75rem; text-align: center; }
.comments-heading > span, .form-actions, .comment-meta time, .reply-context, .delete-hint { color: var(--text-secondary); font-size: .75rem; }
.comment-composer, .comment-list li { display: flex; align-items: flex-start; gap: 12px; }
.comment-avatar { position: relative; display: grid; place-items: center; flex: 0 0 34px; width: 34px; height: 34px; overflow: hidden; border-radius: 0; color: var(--text-secondary); background: var(--bg-sub); font-size: .85rem; }
.comment-avatar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
.composer-body, .comment-body { flex: 1; min-width: 0; }
.composer-field { border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-sub); overflow: hidden; }
.composer-field:focus-within { border-color: var(--accent-color); background: var(--paper-card); }
textarea { display: block; width: 100%; min-height: 90px; box-sizing: border-box; resize: vertical; padding: 14px; font: inherit; font-size: .9rem; line-height: 1.7; color: inherit; background: transparent; border: 0; }
textarea:focus { outline: 0; } textarea::placeholder { color: var(--text-secondary); }
button { font: inherit; cursor: pointer; } button:disabled { opacity: .5; cursor: default; }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
.form-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 0 12px 12px 14px; font-variant-numeric: tabular-nums; }
.submit-comment { padding: 8px 16px; border: 0; border-radius: 0; background: var(--accent-color); color: var(--on-accent); font-size: .8rem; font-weight: 600; }
.submit-comment:disabled { color: var(--text-secondary); background: var(--paper-card); opacity: 1; }
.reply-preview { padding: 10px 12px; margin-bottom: 10px; border-left: 2px solid var(--accent-color); background: var(--accent-soft); border-radius: 0; font-size: .8rem; }
.reply-preview > div { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.reply-preview p { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; color: var(--text-secondary); margin: 4px 0 0; line-height: 1.6; }
.comment-list { padding: 0; margin: 20px 0 0; list-style: none; }
.comment-list li { padding: 20px 0; }
.comment-list li + li { border-top: 1px solid var(--border-line); }
.comment-meta { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 12px; }
.comment-meta strong { font-size: .85rem; overflow-wrap: anywhere; }
.comment-meta time { margin-left: auto; }
.comment-content { margin: 8px 0; font-size: .9rem; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.85; }
.reply-context { margin: 8px 0 0; padding-left: 9px; border-left: 2px solid var(--border-line); }
.reply-context span { color: var(--text-primary); }
.is-deleted .comment-content { color: var(--text-secondary); font-size: .8rem; }
.comment-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 4px 14px; }
.text-action { border: 0; background: none; padding: 4px 0; font-size: .75rem; color: var(--text-secondary); }
.text-action:hover { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; }
.delete-action { margin-left: auto; } .danger-action, .comment-error { color: var(--accent-color); }
.comment-error { padding: 12px; font-size: .85rem; border-left: 2px solid currentColor; background: var(--accent-soft); }
.comment-state { padding: 28px 12px; margin: 12px 0 0; text-align: center; color: var(--text-secondary); font-size: .85rem; }
.comment-state strong { color: var(--text-primary); font-weight: 500; } .comment-state p { margin: 8px 0 0; line-height: 1.7; }
.load-comments { display: block; margin: 12px auto 0; padding: 10px 20px; color: var(--text-secondary); background: var(--bg-sub); border: 1px solid var(--border-line); border-radius: 0; font-size: .8rem; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
@media (max-width: 520px) {
  .comment-composer > .comment-avatar { display: none; }
  .comment-meta { display: block; } .comment-meta time { display: block; margin-top: 4px; }
  .comments-heading > span { display: none; }
}
</style>
