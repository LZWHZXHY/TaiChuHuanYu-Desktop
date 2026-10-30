<template>
  <div class="interaction" @click.stop>
    <div class="interact-actions" :class="{ 'is-vertical': vertical }" :aria-busy="busy">
      <button type="button" :class="{ active: status.isLiked }" :aria-pressed="status.isLiked" :disabled="busy" @click="handleAction('Like')">
        {{ status.isLiked ? '已点赞' : '点赞' }}<span v-if="showCount !== false"> {{ status.likesCount }}</span>
      </button>
      <button type="button" :class="{ active: status.isFavorited }" :aria-pressed="status.isFavorited" :disabled="busy" @click="handleAction('Favorite')">
        {{ status.isFavorited ? '已收藏' : '收藏' }}
      </button>
      <button type="button" disabled title="举报流程尚未开放">举报</button>
    </div>
    <p v-if="error" class="interaction-error" role="alert">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { interactApi } from '../api/interact'
import { useArtworkStore } from '../stores/artwork'
import { useUserStore } from '../stores/user'

const props = defineProps<{
  targetId: number | string
  targetType: 'Artwork' | 'Post' | 'Blog'
  initialStats: { likesCount: number; isLiked?: boolean; isFavorited?: boolean }
  showCount?: boolean
  vertical?: boolean
}>()
const store = useArtworkStore()
const user = useUserStore()
const error = ref('')
const status = computed(() => store.stats[Number(props.targetId)] ?? props.initialStats)
const busy = computed(() => !!store.pending[Number(props.targetId)])
watch(() => [props.targetId, user.token], () => { error.value = '' })

async function handleAction(action: 'Like' | 'Favorite') {
  if (busy.value) return
  if (!user.isLoggedIn) { error.value = '请先登录后再点赞或收藏'; return }
  const id = props.targetId, token = user.token
  error.value = ''
  try {
    const active = action === 'Like' ? status.value.isLiked : status.value.isFavorited
    await store.mutate(Number(id), () => interactApi.toggleAction(id, props.targetType, action, !active))
  } catch (e: any) {
    if (props.targetId === id && user.token === token) error.value = e.friendlyMessage || '操作失败，请重试'
  }
}
</script>

<style scoped>
.interact-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.is-vertical { flex-direction: column; }
button { display: inline-flex; align-items: center; gap: 6px; font: inherit; font-size: .8rem; padding: 6px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; }
button.active { color: var(--accent-color); border-color: var(--accent-color); }
button:disabled { opacity: .5; cursor: default; }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.interaction-error { color: var(--accent-color); font-size: .8rem; margin: 8px 0 0; }
</style>
