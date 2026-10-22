<template>
  <section class="comments" aria-label="作品评论">
    <h2>评论 <span>{{ store.stats[artworkId]?.commentsCount ?? initialCount }}</span></h2>
    <form v-if="user.isLoggedIn" @submit.prevent="submit">
      <p v-if="replyTo" class="reply-target">回复 {{ replyTo.authorName }} <button type="button" @click="replyTo = null">取消回复</button></p>
      <label class="comment-label" for="artwork-comment">写下你的想法</label>
      <textarea id="artwork-comment" v-model="draft" maxlength="1000" rows="3" :disabled="busy" placeholder="请友善交流，最多 1000 字" />
      <div class="form-actions"><span>{{ draft.length }}/1000</span><button :disabled="busy || !draft.trim()">{{ busy ? '处理中…' : '发表评论' }}</button></div>
    </form>
    <p v-else class="comment-state">登录后可以发表评论、参与回复。</p>
    <p v-if="error" role="alert" class="comment-error">{{ error }}</p>
    <p v-if="loading && !items.length" class="comment-state" role="status">正在加载评论…</p>
    <p v-else-if="!items.length && !loadError" class="comment-state">还没有评论，分享你的想法吧。</p>
    <ol class="comment-list">
      <li v-for="item in items" :key="item.id">
        <div class="comment-meta"><strong>{{ item.authorName }}</strong><time :datetime="item.createdAt">{{ new Date(item.createdAt).toLocaleString() }}</time></div>
        <p v-if="item.replyToAuthor" class="reply-target">回复 {{ item.replyToAuthor }}</p>
        <p class="comment-content">{{ item.isDeleted ? '这条评论已删除，回复仍然保留。' : item.content }}</p>
        <div v-if="!item.isDeleted" class="comment-actions">
          <button v-if="user.isLoggedIn" :disabled="busy" @click="replyTo = item">回复</button>
          <template v-if="item.canDelete">
            <button v-if="deleteId !== item.id" :disabled="busy" @click="deleteId = item.id">删除</button>
            <template v-else><button :disabled="busy" @click="remove(item.id)">确认删除</button><button :disabled="busy" @click="deleteId = null">取消</button></template>
          </template>
        </div>
      </li>
    </ol>
    <button v-if="loadError || hasMore" :disabled="loading" @click="load()">{{ loading ? '加载中…' : loadError ? '重新加载' : '加载更多评论' }}</button>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { artworkApi, type ArtworkComment } from '../../api/artwork'
import { useArtworkStore } from '../../stores/artwork'
import { useUserStore } from '../../stores/user'

const props = defineProps<{ artworkId: number; initialCount: number }>()
const store = useArtworkStore(), user = useUserStore()
const items = ref<ArtworkComment[]>([]), draft = ref(''), error = ref('')
const loading = ref(false), loadError = ref(false), hasMore = ref(false)
const replyTo = ref<ArtworkComment | null>(null), deleteId = ref<string | null>(null)
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
.comments { margin-top: 40px; padding-top: 24px; border-top: 1px solid var(--border-line); text-align: left; }
h2 { font-size: 1.1rem; } h2 span, .comment-state, .comment-meta, .reply-target, .form-actions { color: var(--text-secondary); font-size: .85rem; }
.comment-label { display: block; margin-bottom: 8px; }
textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 12px; font: inherit; color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 4px; }
button { font: inherit; font-size: .85rem; padding: 6px 12px; color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 4px; cursor: pointer; }
button:disabled { opacity: .5; cursor: default; }
button:focus-visible, textarea:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.form-actions, .comment-meta { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 8px; margin: 8px 0; }
.comment-list { padding: 0; list-style: none; }
.comment-list li { padding: 20px 0; border-bottom: 1px solid var(--border-line); }
.comment-content { white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.7; }
.comment-actions { display: flex; gap: 8px; }.comment-error { color: var(--accent-color); }
</style>
