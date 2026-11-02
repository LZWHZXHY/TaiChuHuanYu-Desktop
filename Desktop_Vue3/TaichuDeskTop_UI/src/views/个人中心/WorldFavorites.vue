<!-- 个人中心「我的收藏」里的世界观和小说（画廊作品的收藏在下面另一块）。都没有就整块不显示。 -->
<template>
  <div v-if="worlds.length || novels.length" class="wfav">
    <section v-if="worlds.length" class="wfav-group">
      <h3 class="wfav-h">🌌 世界观 <small>{{ worlds.length }}</small></h3>
      <ul class="wfav-list">
        <li v-for="w in worlds" :key="w.id">
          <RouterLink :to="`/world/project/${w.id}`" class="wfav-item">
            <span class="wfav-cover" aria-hidden="true">
              <img v-if="w.coverImage" :src="withThumbnail(coverSrc(w.coverImage), 240)" :style="coverStyle(w.coverImage)" alt="" loading="lazy" />
              <template v-else>{{ (w.name || '?').slice(0, 1) }}</template>
            </span>
            <span class="wfav-main">
              <b class="wfav-title">{{ w.name }}</b>
              <small class="wfav-meta">{{ w.ownerName || '未知作者' }}<template v-if="!w.isPublic"> · 私有</template></small>
            </span>
          </RouterLink>
        </li>
      </ul>
    </section>
    <section v-if="novels.length" class="wfav-group">
      <h3 class="wfav-h">📖 小说 <small>{{ novels.length }}</small></h3>
      <ul class="wfav-list">
        <li v-for="n in novels" :key="n.id">
          <RouterLink :to="`/novels/${n.id}`" class="wfav-item">
            <span class="wfav-cover tall" aria-hidden="true">
              <img v-if="n.coverUrl" :src="n.coverUrl" alt="" loading="lazy" />
              <template v-else>{{ (n.title || '?').slice(0, 1) }}</template>
            </span>
            <span class="wfav-main">
              <b class="wfav-title">{{ n.title }}</b>
              <small class="wfav-meta">{{ n.authorName }} · {{ n.wordCount }} 字</small>
            </span>
          </RouterLink>
        </li>
      </ul>
    </section>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { worldSocialApi, type FavoriteNovel, type FavoriteWorld } from '@/api/worldSocial'
import { coverSrc, coverStyle } from '@/utils/coverFocus'
import { withThumbnail } from '@/utils/imageThumb'

const worlds = ref<FavoriteWorld[]>([])
const novels = ref<FavoriteNovel[]>([])

onMounted(async () => {
  try {
    const r = await worldSocialApi.favorites()
    worlds.value = r?.worlds ?? []
    novels.value = r?.novels ?? []
  } catch { /* 后端没更新：不显示 */ }
})
</script>

<style scoped>
.wfav { display: grid; gap: 18px; margin-bottom: 24px; }
.wfav-h { display: flex; align-items: baseline; gap: 8px; margin: 0 0 10px; font-size: 15px; font-weight: 600; }
.wfav-h small { font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.wfav-list { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 8px; }
.wfav-item { display: flex; align-items: center; gap: 10px; min-width: 0; padding: 8px 10px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); text-decoration: none; transition: border-color .15s, translate .15s; }
.wfav-item:hover, .wfav-item:focus-visible { border-color: var(--accent-color); translate: 0 -1px; }
.wfav-cover { flex-shrink: 0; width: 64px; height: 36px; display: grid; place-items: center; overflow: hidden; background: var(--bg-sub); color: var(--accent-color); font-weight: 700; }
.wfav-cover.tall { width: 34px; height: 46px; }
.wfav-cover img { width: 100%; height: 100%; object-fit: cover; }
.wfav-main { min-width: 0; display: grid; gap: 2px; }
.wfav-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; }
.wfav-meta { font-size: 12px; color: var(--text-secondary); }
</style>
