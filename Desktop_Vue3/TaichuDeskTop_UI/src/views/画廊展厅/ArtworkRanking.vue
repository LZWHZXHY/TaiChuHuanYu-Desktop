<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue'
import { artworkApi, type ArtworkItemDto } from '@/api/artwork'
const props = defineProps<{ period: string }>()
defineEmits<{ explore: []; open: [id: number] }>()
const items = ref<ArtworkItemDto[]>([]), loading = ref(false), error = ref('')
let version = 0
async function load() {
  const current = ++version
  loading.value = true; error.value = ''; items.value = []
  try {
    const result = await artworkApi.getGallery(0, 20, undefined, ({ 总:'all', 日:'day', 周:'week', 月:'month', 年:'year' } as Record<string,string>)[props.period] || 'all')
    if (current === version) items.value = result.data
  } catch { if (current === version) error.value = '榜单加载失败，请重试。' }
  finally { if (current === version) loading.value = false }
}
watch(() => props.period, load, { immediate: true })
onUnmounted(() => { version++ })
</script>
<template>
  <section aria-label="作品点赞榜">
    <p class="ranking-note">{{ period === '总' ? '按累计真实点赞排序' : '按最近一' + period + '收到的真实点赞排序' }} · 前 20 名</p>
    <p v-if="loading" class="content-state" role="status">正在读取榜单…</p>
    <div v-else-if="error" class="content-state" role="alert">{{ error }} <button @click="load">重试</button></div>
    <div v-else-if="!items.length" class="content-state"><h2>这个周期还没有点赞记录</h2><p>可以切换总榜，或去发现更多作品。</p><button @click="$emit('explore')">浏览全部作品</button></div>
    <ol v-else class="ranking-list">
      <li v-for="(item,index) in items" :key="item.id"><span class="rank-position">{{ index + 1 }}</span><button class="rank-work" @click="$emit('open',item.id)"><img v-if="item.coverImageUrl" :src="/^https?:/.test(item.coverImageUrl) ? item.coverImageUrl : 'https://img.bianyuzhou.com/uploads/' + item.coverImageUrl" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" /><span><strong>{{ item.title || '无标题作品' }}</strong><small>{{ item.authorName }}</small></span></button><span class="rank-score">{{ item.rankingLikes ?? item.likesCount }}<small>点赞</small></span></li>
    </ol>
  </section>
</template>
<style scoped>
.ranking-note { font-size: .85rem; color: var(--text-secondary); }
.ranking-list { list-style: none; margin: 20px 0; padding: 0; }
.ranking-list li { display: flex; align-items: center; gap: 20px; padding: 18px 0; border-bottom: 1px solid var(--border-line); }
.rank-position { width: 28px; text-align: center; font-size: 1.2rem; color: var(--accent-color); font-variant-numeric: tabular-nums; }
.rank-work { display: flex; align-items: center; gap: 16px; flex: 1; min-width: 0; padding: 0; border: 0; color: var(--text-primary); background: none; text-align: left; cursor: pointer; }
.rank-work img { width: 72px; height: 72px; object-fit: cover; border-radius: 8px; }
.rank-work span { min-width: 0; } .rank-work strong { overflow-wrap: anywhere; } small { display: block; margin-top: 6px; font-size: .75rem; color: var(--text-secondary); }
.rank-score { min-width: 38px; text-align: right; font-variant-numeric: tabular-nums; } .rank-work:hover strong { color: var(--accent-color); }
@media(max-width:520px) { .ranking-list li { gap: 10px; } .rank-work { gap: 10px; } .rank-work img { width: 48px; height: 48px; } }
</style>
