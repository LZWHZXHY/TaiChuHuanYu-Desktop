<template>
  <div class="chai-comments">
    <h3 class="comments-title">评论{{ items.length ? ` (${items.length})` : '' }}</h3>
    <div class="comment-input">
      <input v-model="draft" type="text" maxlength="1000" placeholder="写下你的评论…" @keyup.enter="submit" />
      <button :disabled="!draft.trim() || busy" @click="submit">发送</button>
    </div>
    <p v-if="error" class="comment-error" role="alert">{{ error }}</p>
    <ul v-if="items.length" class="comment-list">
      <li v-for="c in items" :key="c.id" class="comment-item">
        <div class="comment-head">
          <strong>{{ c.userName }}</strong>
          <time>{{ relTime(c.createdAt) }}</time>
        </div>
        <p class="comment-content">{{ c.content || '已删除评论' }}</p>
      </li>
    </ul>
    <p v-else-if="!loading" class="comment-empty">还没有评论，来抢沙发～</p>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { chaiCommentApi, type ChaiComment } from '@/api/chaiComment'

const props = defineProps<{ targetType: string; targetId: string }>()
const items = ref<ChaiComment[]>([])
const draft = ref('')
const busy = ref(false)
const error = ref('')
const loading = ref(false)

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
    const c = await chaiCommentApi.create({ targetType: props.targetType, targetId: props.targetId, content: draft.value.trim() })
    items.value.push(c)
    draft.value = ''
  } catch (e: any) {
    error.value = e.friendlyMessage || '评论失败，请先登录或重试'
  } finally {
    busy.value = false
  }
}

onMounted(load)
watch(() => props.targetId, load)
</script>

<style scoped>
.chai-comments { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--border-line); }
.comments-title { margin: 0 0 12px; font-size: .95rem; }
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
.comment-content { margin: 6px 0 0; font-size: .88rem; line-height: 1.6; white-space: pre-wrap; }
.comment-empty { color: var(--text-secondary); font-size: .85rem; padding: 12px 0; }
</style>
