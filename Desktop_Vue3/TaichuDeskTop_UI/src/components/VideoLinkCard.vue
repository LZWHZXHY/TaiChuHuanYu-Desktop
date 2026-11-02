<!-- VideoLinkCard.vue：通用的「视频链接卡片」（战后记录、OC 战斗经历、编年史约战记录都用它）。
     · 站内视频（柴圈作品里的视频，或者直接是 mp4 等视频文件）→ 页面里直接播放；
     · B 站链接 → 用必吃榜那套接口抓封面和标题，显示封面卡片，点开去 B 站；
     · 其他链接 → 普通链接卡片（显示域名）。
     封面拿不到时显示占位图；链接只认 http / https，别的不显示。 -->
<template>
  <p v-if="hidden" class="vlc-hidden">🎬 视频是还在审核的约战作品，审核通过后才能看到</p>
  <div v-else-if="kind !== 'none'" class="vlc" :class="['vlc-' + kind, { compact }]">
    <!-- 站内视频：直接播放 -->
    <template v-if="kind === 'site-video' || kind === 'file-video'">
      <div class="vlc-player">
        <video :src="playSrc" :poster="poster || undefined" controls preload="metadata" playsinline></video>
      </div>
      <div class="vlc-bar">
        <span class="vlc-src">站内视频</span>
        <RouterLink v-if="siteWork" :to="`/works/${siteWork.id}`" class="vlc-bar-title">{{ displayTitle }}</RouterLink>
        <span v-else class="vlc-bar-title">{{ displayTitle }}</span>
      </div>
    </template>

    <!-- 站内作品（图片作品，或者作品读不到详情）：卡片，点开去作品页 -->
    <RouterLink v-else-if="kind === 'site-link'" :to="`/works/${siteWork?.id || workId}`" class="vlc-card">
      <span class="vlc-thumb">
        <img v-if="coverSrc" :src="coverSrc" alt="" loading="lazy" @error="coverBroken = true" />
        <span v-else class="vlc-ph" aria-hidden="true">站内作品</span>
      </span>
      <span class="vlc-text">
        <b>{{ displayTitle }}</b>
        <small>站内作品 · 点开观看</small>
      </span>
    </RouterLink>

    <!-- B 站 / 其他链接：新窗口打开 -->
    <a v-else :href="link!" target="_blank" rel="noopener noreferrer" class="vlc-card" :title="link!">
      <span class="vlc-thumb">
        <!-- B 站图片不让带来源页，和必吃榜一样不带 referrer -->
        <img v-if="coverSrc" :src="coverSrc" alt="" loading="lazy" referrerpolicy="no-referrer" @error="coverBroken = true" />
        <span v-else class="vlc-ph" aria-hidden="true">{{ kind === 'bili' ? 'B 站视频' : host.slice(0, 1).toUpperCase() || '链接' }}</span>
        <span v-if="kind === 'bili'" class="vlc-play" aria-hidden="true">▶</span>
      </span>
      <span class="vlc-text">
        <b>{{ displayTitle }}</b>
        <small>{{ kind === 'bili' ? '哔哩哔哩 · 去 B 站看' : title ? host : '外部链接 · 新窗口打开' }} ↗</small>
      </span>
    </a>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { AftermathSiteWork } from '@/api/battleAftermath'
import {
  safeWebLink, hostOf, isBilibiliLink, siteWorkIdOf, isSameSite, isVideoFile, fetchBilibiliMeta, type BilibiliMeta,
} from '@/utils/videoLink'

const props = withDefaults(defineProps<{
  url?: string | null
  /** 已知的标题（B 站标题保存时已经抓过就直接用） */
  title?: string | null
  /** 已知的封面 */
  cover?: string | null
  /** 后端认出来的站内作品（有播放地址就直接播） */
  siteWork?: AftermathSiteWork | null
  /** 视频是还在审核的约战作品：只显示一句提示 */
  hidden?: boolean
  /** 窄一点的样子（侧栏、列表里） */
  compact?: boolean
}>(), { url: null, title: null, cover: null, siteWork: null, hidden: false, compact: false })

const link = computed(() => safeWebLink(props.url))
const workId = computed(() => (link.value ? siteWorkIdOf(link.value) : null))
const host = computed(() => (link.value ? hostOf(link.value) : ''))

type Kind = 'none' | 'site-video' | 'file-video' | 'site-link' | 'bili' | 'link'
const kind = computed<Kind>(() => {
  const url = link.value
  if (!url) return 'none'
  if (props.siteWork?.mediaType === 'video' && safeWebLink(props.siteWork.mediaUrl)) return 'site-video'
  // 站内作品页的链接：后端认出来了（图片作品），或者至少是本站地址
  if (props.siteWork || (workId.value && isSameSite(url))) return 'site-link'
  if (isVideoFile(url)) return 'file-video'
  if (isBilibiliLink(url)) return 'bili'
  return 'link'
})

const playSrc = computed(() => (kind.value === 'site-video' ? safeWebLink(props.siteWork?.mediaUrl) : link.value) ?? undefined)
const poster = computed(() => safeWebLink(props.siteWork?.coverUrl) ?? safeWebLink(props.cover))

// B 站：没有现成的封面 / 标题就用必吃榜的接口抓一次（同一个链接全站只抓一次）
const fetched = ref<BilibiliMeta | null>(null)
const coverBroken = ref(false)
watch([link, () => props.cover, () => props.title, kind], ([url]) => {
  coverBroken.value = false
  fetched.value = null
  if (!url || kind.value !== 'bili' || (props.cover && props.title)) return
  void fetchBilibiliMeta(url).then(meta => { if (link.value === url) fetched.value = meta })
}, { immediate: true })

const coverSrc = computed(() => {
  if (coverBroken.value) return null
  return safeWebLink(props.cover) ?? fetched.value?.coverImage ?? safeWebLink(props.siteWork?.coverUrl) ?? null
})
const displayTitle = computed(() => props.title || fetched.value?.title || props.siteWork?.title
  || (kind.value === 'bili' ? 'B 站视频' : kind.value === 'site-link' ? '站内作品'
    : kind.value === 'site-video' || kind.value === 'file-video' ? '视频' : host.value || '链接'))
</script>

<style scoped>
.vlc { max-width: 560px; border: 1px solid var(--border-line); background: var(--paper-card); }
.vlc.compact { max-width: 100%; }
.vlc-hidden { max-width: 560px; box-sizing: border-box; margin: 0; padding: 8px 10px; border: 1px dashed var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; }

/* 直接播放 */
.vlc-player { background: #000; aspect-ratio: 16 / 9; }
.vlc-player video { display: block; width: 100%; height: 100%; object-fit: contain; background: #000; }
.vlc-bar { display: flex; align-items: center; gap: 8px; min-width: 0; padding: 6px 10px; font-size: 12px; }
.vlc-src { flex-shrink: 0; padding: 1px 6px; background: var(--accent-soft); color: var(--accent-color); }
.vlc-bar-title { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-primary); text-decoration: none; }
a.vlc-bar-title:hover { color: var(--accent-color); }

/* 卡片：左边封面，右边标题 */
.vlc-card { display: flex; align-items: stretch; min-width: 0; color: var(--text-primary); text-decoration: none; }
.vlc-card:hover, .vlc-card:focus-visible { background: var(--bg-sub); }
.vlc-card:hover b, .vlc-card:focus-visible b { color: var(--accent-color); }
.vlc-thumb { position: relative; flex: 0 0 168px; aspect-ratio: 16 / 9; overflow: hidden; background: var(--bg-sub); }
.compact .vlc-thumb { flex-basis: 120px; }
.vlc-thumb img { display: block; width: 100%; height: 100%; object-fit: cover; }
/* 占位：拿不到封面时 */
.vlc-ph { position: absolute; inset: 0; display: grid; place-items: center; font-size: 12px; letter-spacing: .15em; color: var(--accent-color);
  background: linear-gradient(135deg, var(--accent-soft), var(--paper-card) 70%); }
/* B 站占位：字往左挪一点，别被右下角的播放图标压住 */
.vlc-bili .vlc-ph { padding-right: 34px; }
.vlc-play { position: absolute; right: 6px; bottom: 6px; width: 26px; height: 26px; display: grid; place-items: center; font-size: 11px;
  background: rgba(0, 0, 0, .62); color: #fff; }
.vlc-text { display: grid; align-content: center; gap: 4px; min-width: 0; padding: 8px 12px; }
.vlc-text b { font-size: 13px; font-weight: 600; line-height: 1.45; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow-wrap: anywhere; }
.vlc-text small { font-size: 12px; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

@media (max-width: 420px) {
  .vlc-thumb { flex-basis: 112px; }
  .vlc-text { padding: 6px 10px; }
}
</style>
