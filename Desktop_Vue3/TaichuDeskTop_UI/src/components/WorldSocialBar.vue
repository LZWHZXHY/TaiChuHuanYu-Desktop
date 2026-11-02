<!-- 世界观 / 小说的「点赞、收藏、评论」一排按钮。评论按钮点了滚到下面的评论区（commentAnchor 是评论区元素的 id）。
     后端还没更新（接口 404）时整排不显示。 -->
<template>
  <div v-if="stats" class="wsb" role="group" :aria-label="kind === 'world' ? '给这个世界观点赞、收藏' : '给这本小说点赞、收藏'">
    <button type="button" class="wsb-btn like" :class="{ on: stats.liked }" :aria-pressed="stats.liked" :disabled="busy === 'like'" @click="toggle('like')">
      <span class="wsb-ico" aria-hidden="true">{{ stats.liked ? '♥' : '♡' }}</span>
      <span>{{ stats.liked ? '已点赞' : '点赞' }}</span>
      <b>{{ stats.likeCount }}</b>
    </button>
    <button type="button" class="wsb-btn fav" :class="{ on: stats.favorited }" :aria-pressed="stats.favorited" :disabled="busy === 'favorite'" @click="toggle('favorite')">
      <span class="wsb-ico" aria-hidden="true">{{ stats.favorited ? '★' : '☆' }}</span>
      <span>{{ stats.favorited ? '已收藏' : '收藏' }}</span>
      <b>{{ stats.favoriteCount }}</b>
    </button>
    <a v-if="commentAnchor" class="wsb-btn" :href="`#${commentAnchor}`" @click.prevent="toComments">
      <span class="wsb-ico" aria-hidden="true">💬</span>
      <span>评论</span>
      <b>{{ stats.commentCount }}</b>
    </a>
    <span v-if="error" class="wsb-err" role="alert">{{ error }}</span>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { worldSocialApi, type SocialStats } from '@/api/worldSocial'
import { useUserStore } from '@/stores/user'

const props = defineProps<{ kind: 'world' | 'novel'; targetId: string; commentAnchor?: string }>()
const user = useUserStore()
const stats = ref<SocialStats | null>(null)
const busy = ref<'' | 'like' | 'favorite'>('')
const error = ref('')

let version = 0
async function load() {
  const v = ++version
  stats.value = null
  error.value = ''
  try {
    const s = await worldSocialApi.stats(props.kind, props.targetId)
    if (v === version) stats.value = s
  } catch { /* 后端没更新或看不到：不显示 */ }
}
watch(() => [props.kind, props.targetId, user.token], () => { if (props.targetId) void load() }, { immediate: true })

async function toggle(action: 'like' | 'favorite') {
  if (!stats.value || busy.value) return
  if (!user.isLoggedIn) { error.value = '登录后才能点赞和收藏'; return }
  busy.value = action
  error.value = ''
  try {
    const r = await worldSocialApi.toggle(props.kind, props.targetId, action)
    if (!stats.value) return
    if (action === 'like') stats.value = { ...stats.value, liked: r.active, likeCount: r.count }
    else stats.value = { ...stats.value, favorited: r.active, favoriteCount: r.count }
  } catch (e: any) {
    error.value = e?.friendlyMessage || e?.response?.data?.message || '操作失败，请重试'
  } finally { busy.value = '' }
}

function toComments() {
  const el = props.commentAnchor ? document.getElementById(props.commentAnchor) : null
  el?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  window.setTimeout(() => el?.querySelector<HTMLInputElement>('input')?.focus({ preventScroll: true }), 450)
}
</script>

<style scoped>
.wsb { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.wsb-btn {
  display: inline-flex; align-items: center; gap: 6px; min-height: 32px; padding: 0 12px;
  border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary);
  font: inherit; font-size: 13px; text-decoration: none; cursor: pointer; transition: border-color .15s, color .15s, background .15s, translate .15s;
}
.wsb-btn b { font: 600 12px var(--font-mono, monospace); color: var(--text-primary); }
.wsb-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); translate: 0 -1px; }
.wsb-btn:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.wsb-btn:disabled { opacity: .6; cursor: default; }
.wsb-ico { font-size: 14px; line-height: 1; }
.wsb-btn.like.on { border-color: var(--danger); color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.wsb-btn.like.on .wsb-ico { animation: wsb-pop .35s cubic-bezier(.2, .8, .2, 1.4); }
.wsb-btn.fav.on { border-color: var(--warning, #d97706); color: var(--warning, #d97706); background: color-mix(in srgb, var(--warning, #d97706) 8%, var(--paper-card)); }
.wsb-btn.fav.on .wsb-ico { animation: wsb-pop .35s cubic-bezier(.2, .8, .2, 1.4); }
.wsb-err { font-size: 12px; color: var(--danger); }
@keyframes wsb-pop { from { transform: scale(.5); } }
@media (prefers-reduced-motion: reduce) { .wsb-btn .wsb-ico { animation: none !important; } }
</style>
