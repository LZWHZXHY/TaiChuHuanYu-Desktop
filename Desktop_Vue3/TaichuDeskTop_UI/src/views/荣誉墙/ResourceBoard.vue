<template>
  <!-- 资源贡献榜：按公开的资源数，或者按被多少人下载过（同一个人重复下载只算一次） -->
  <section class="rb" aria-label="资源贡献榜">
    <div class="rb-head">
      <h3>📦 资源贡献榜</h3>
      <p>{{ by === 'count' ? '按在知识库公开的资源数排名，同数量按被下载的人数。' : '按资源被多少个不同的人下载过排名，同一个人重复下载只算一次。' }}<RouterLink to="/knowledge?type=resource">去知识库分享资源 →</RouterLink></p>
      <div class="rb-range" role="group" aria-label="排名方式">
        <button type="button" :class="{ on: by === 'count' }" :aria-pressed="by === 'count'" @click="by = 'count'">资源数</button>
        <button type="button" :class="{ on: by === 'downloads' }" :aria-pressed="by === 'downloads'" @click="by = 'downloads'">下载人数</button>
      </div>
    </div>
    <p v-if="loading" class="rb-empty">读取中…</p>
    <p v-else-if="!rows.length" class="rb-empty">还没有人上榜，去知识库上传第一个资源吧。</p>
    <ol v-else class="rb-list">
      <li v-for="row in rows" :key="row.userId" :class="{ top: row.rank <= 3 }">
        <span class="rb-rank">{{ row.rank <= 3 ? ['👑', '🥈', '🥉'][row.rank - 1] : String(row.rank).padStart(2, '0') }}</span>
        <AvatarFrame :user-id="row.userId" size="sm" class="rb-frame"><img v-if="row.avatar" :src="row.avatar" alt="" class="rb-avatar" loading="lazy" /><span v-else class="rb-avatar ph">{{ row.name.slice(0, 1) }}</span></AvatarFrame>
        <span class="rb-who"><UserLink :id="row.userId" :name="row.name" class="rb-name" /><UserTitle :user-id="row.userId" /></span>
        <span class="rb-value">{{ by === 'count' ? `${row.approved} 个` : `${row.downloads} 人` }}</span>
        <span class="rb-sub">{{ by === 'count' ? `${row.downloads} 人下载过` : `${row.approved} 个资源 · 共 ${row.totalDownloads} 次` }}</span>
      </li>
    </ol>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import UserLink from '@/components/UserLink.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserTitle from '@/components/UserTitle.vue'
import { resourceApi, type ResourceBoard, type ResourceRankRow } from '@/api/resources'

const by = ref<ResourceBoard>('count')
const loading = ref(false)
const rows = ref<ResourceRankRow[]>([])

async function load() {
  loading.value = true
  try { rows.value = await resourceApi.leaderboard(by.value) } catch { rows.value = [] } finally { loading.value = false }
}
watch(by, load)
onMounted(load)
</script>

<style scoped>
.rb { display: grid; gap: 16px; }
.rb-head { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 6px 16px; }
.rb-head h3 { margin: 0; font-size: 18px; }
.rb-head p { grid-column: 1; margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.6; }
.rb-head p a { margin-left: 8px; color: var(--accent-color); }
.rb-range { grid-column: 2; grid-row: 1 / 3; display: flex; border: 1px solid var(--border-line); }
.rb-range button { padding: 6px 12px; border: 0; border-right: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.rb-range button:last-child { border-right: 0; }
.rb-range button.on { background: var(--accent-color); color: var(--on-accent); }
.rb-empty { padding: 24px 0; color: var(--text-secondary); text-align: center; }
.rb-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.rb-list li { display: grid; grid-template-columns: 44px 40px minmax(0, 1fr) auto auto; align-items: center; gap: 14px; padding: 12px 18px; background: var(--paper-card); border-bottom: 1px solid var(--border-line); }
.rb-list li.top { border-left: 3px solid var(--signal-mint); }
.rb-rank { font: 600 15px var(--font-mono, monospace); color: var(--text-secondary); text-align: center; }
.top .rb-rank { font-size: 20px; }
.rb-avatar { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; }
.rb-avatar.ph { display: grid; place-items: center; background: var(--accent-soft, var(--bg-sub)); color: var(--accent-color); font-weight: 600; }
.rb-frame { justify-self: start; }
.rb-who { display: flex; align-items: center; min-width: 0; }
.rb-name { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rb-value { font-size: 17px; font-weight: 700; font-variant-numeric: tabular-nums; }
.rb-sub { min-width: 140px; color: var(--text-secondary); font-size: 12px; text-align: right; }
@media (max-width: 700px) {
  .rb-head { grid-template-columns: minmax(0, 1fr); }
  .rb-range { grid-column: 1; grid-row: auto; justify-self: start; }
  .rb-list li { grid-template-columns: 32px 32px minmax(0, 1fr) auto; padding: 10px 12px; gap: 10px; }
  .rb-sub { display: none; }
}
</style>
