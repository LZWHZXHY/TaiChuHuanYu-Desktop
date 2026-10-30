<script setup lang="ts">
import { ref, watch, onUnmounted, computed } from 'vue'
import { artworkApi, type ArtworkItemDto } from '@/api/artwork'
import { withThumbnail } from '@/utils/imageThumb'

const props = defineProps<{ period: string }>()
defineEmits<{ explore: []; open: [id: number] }>()
const items = ref<ArtworkItemDto[]>([]), loading = ref(false), error = ref('')
let version = 0

const coverUrl = (url: string | null | undefined) => {
  if (!url) return ''
  const full = /^https?:/.test(url) ? url : 'https://img.bianyuzhou.com/uploads/' + url
  return withThumbnail(full, 400)
}
const likes = (item: ArtworkItemDto) => item.rankingLikes ?? item.likesCount

const medals = [
  { cls: 'gold', badge: '👑' },
  { cls: 'silver', badge: '2' },
  { cls: 'bronze', badge: '3' },
] as const

// 前三领奖台：左二、中一、右三
const podium = computed(() => {
  const top = items.value.slice(0, 3)
  if (top.length < 3) return []
  return ([1, 0, 2] as const).map(rank => ({ item: top[rank], ...medals[rank] }))
})
const rest = computed(() => (items.value.length >= 3 ? items.value.slice(3) : items.value))
const restStart = computed(() => (items.value.length >= 3 ? 4 : 1))

async function load() {
  const current = ++version
  loading.value = true; error.value = ''; items.value = []
  try {
    const result = await artworkApi.getGallery(0, 20, undefined, ({ 总: 'all', 日: 'day', 周: 'week', 月: 'month', 年: 'year' } as Record<string, string>)[props.period] || 'all')
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
    <div v-else-if="!items.length" class="content-state">
      <h2>这个周期还没有点赞记录</h2>
      <p>可以切换总榜，或去发现更多作品。</p>
      <button @click="$emit('explore')">浏览全部作品</button>
    </div>

    <template v-else>
      <!-- 前三尊位 -->
      <div v-if="podium.length" class="ranking-podium">
        <article
          v-for="slot in podium"
          :key="slot.item.id"
          class="podium-card"
          :class="'is-' + slot.cls"
          @click="$emit('open', slot.item.id)"
        >
          <span class="podium-medal" :class="'is-' + slot.cls" aria-hidden="true">{{ slot.badge }}</span>
          <div class="podium-cover">
            <img v-if="slot.item.coverImageUrl" :src="coverUrl(slot.item.coverImageUrl)" :alt="slot.item.title" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
            <span v-else class="podium-noimg">无封面</span>
          </div>
          <div class="podium-meta">
            <strong class="podium-title">{{ slot.item.title || '无标题作品' }}</strong>
            <small class="podium-author">{{ slot.item.authorName }}</small>
            <span class="podium-likes">{{ likes(slot.item) }} 赞</span>
          </div>
        </article>
      </div>

      <!-- 4 名以后 -->
      <ol class="ranking-list">
        <li v-for="(item, index) in rest" :key="item.id">
          <span class="rank-position">{{ restStart + index }}</span>
          <button class="rank-work" @click="$emit('open', item.id)">
            <img v-if="item.coverImageUrl" :src="coverUrl(item.coverImageUrl)" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
            <span><strong>{{ item.title || '无标题作品' }}</strong><small>{{ item.authorName }}</small></span>
          </button>
          <span class="rank-score">{{ likes(item) }}<small>点赞</small></span>
        </li>
      </ol>
    </template>
  </section>
</template>

<style scoped>
.ranking-note { font-size: .85rem; color: var(--text-secondary); }

/* ===== 前三领奖台 ===== */
.ranking-podium {
  display: grid;
  grid-template-columns: 1fr 1.18fr 1fr;
  gap: 18px;
  align-items: end;
  margin: 26px 0 32px;
}
.podium-card {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 26px 14px 16px;
  border: 1px solid var(--border-line);
  border-radius: 14px;
  background: var(--paper-card);
  cursor: pointer;
  text-align: center;
  transition: transform .2s, box-shadow .2s;
}
.podium-card:hover { transform: translateY(-4px); }
.podium-card.is-gold { padding-top: 38px; border-color: #d4af37; box-shadow: 0 12px 34px rgba(212, 175, 55, .20); }
.podium-card.is-silver { border-color: #aeb7c2; }
.podium-card.is-bronze { border-color: #b87333; }

.podium-medal {
  position: absolute;
  top: -15px;
  left: 50%;
  transform: translateX(-50%);
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  font-size: .95rem;
  font-weight: 700;
  color: #fff;
  background: var(--text-secondary);
  box-shadow: 0 3px 10px rgba(0, 0, 0, .18);
}
.podium-medal.is-gold { background: linear-gradient(135deg, #f9dc82, #d4af37); }
.podium-medal.is-silver { background: linear-gradient(135deg, #eef1f5, #aeb7c2); color: #5a6470; }
.podium-medal.is-bronze { background: linear-gradient(135deg, #e4b077, #b87333); }

.podium-cover {
  width: 100%;
  aspect-ratio: 1 / 1;
  border-radius: 10px;
  overflow: hidden;
  background: var(--paper-sub);
}
.podium-card.is-gold .podium-cover { box-shadow: 0 0 0 2px rgba(212, 175, 55, .30); }
.podium-cover img { width: 100%; height: 100%; object-fit: cover; display: block; }
.podium-noimg { display: grid; place-items: center; height: 100%; color: var(--text-secondary); font-size: .8rem; }

.podium-meta { display: flex; flex-direction: column; align-items: center; gap: 4px; min-width: 0; width: 100%; }
.podium-title {
  font-size: .95rem;
  font-weight: 600;
  color: var(--text-primary);
  overflow-wrap: anywhere;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.podium-card.is-gold .podium-title { font-size: 1.05rem; }
.podium-author { font-size: .75rem; color: var(--text-secondary); }
.podium-likes { font-size: .82rem; font-weight: 700; color: var(--accent-color); }

/* ===== 4 名以后 ===== */
.ranking-list { list-style: none; margin: 0; padding: 0; }
.ranking-list li { display: flex; align-items: center; gap: 20px; padding: 18px 0; border-bottom: 1px solid var(--border-line); }
.rank-position { width: 28px; text-align: center; font-size: 1.2rem; color: var(--accent-color); font-variant-numeric: tabular-nums; }
.rank-work { display: flex; align-items: center; gap: 16px; flex: 1; min-width: 0; padding: 0; border: 0; color: var(--text-primary); background: none; text-align: left; cursor: pointer; }
.rank-work img { width: 72px; height: 72px; object-fit: cover; border-radius: 8px; }
.rank-work span { min-width: 0; }
.rank-work strong { overflow-wrap: anywhere; }
.rank-work small { display: block; margin-top: 6px; font-size: .75rem; color: var(--text-secondary); }
.rank-score { min-width: 38px; text-align: right; font-variant-numeric: tabular-nums; }
.rank-score small { display: block; font-size: .7rem; color: var(--text-secondary); }
.rank-work:hover strong { color: var(--accent-color); }

.content-state { padding: 24px 0; color: var(--text-secondary); text-align: center; }
.content-state button { color: var(--accent-color); background: var(--paper-card); border: 1px solid var(--border-line); padding: 6px 14px; border-radius: 8px; cursor: pointer; }

@media (max-width: 560px) {
  .ranking-podium { gap: 8px; }
  .podium-card { padding: 22px 8px 12px; gap: 8px; }
  .podium-card.is-gold { padding-top: 30px; }
  .podium-title, .podium-card.is-gold .podium-title { font-size: .82rem; }
  .podium-author { font-size: .68rem; }
  .podium-likes { font-size: .75rem; }
  .ranking-list li { gap: 10px; }
  .rank-work { gap: 10px; }
  .rank-work img { width: 48px; height: 48px; }
}
</style>
