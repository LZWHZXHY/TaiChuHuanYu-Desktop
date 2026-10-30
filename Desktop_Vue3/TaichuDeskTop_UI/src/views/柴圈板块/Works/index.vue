<template>
  <div class="works-page content-page">
    <header class="content-header">
      <div>
        <p class="eyebrow">柴圈作品</p>
        <h1>动画作品</h1>
        <p class="intro">火柴人动画与练习，这里是你展示身手的地方。</p>
      </div>
      <div class="content-actions">
        <RouterLink to="/works/create" class="primary">上传作品</RouterLink>
      </div>
    </header>

    <div class="works-toolbar">
      <input v-model="keyword" type="search" placeholder="搜索作品标题或说明…" class="search-input" @keyup.enter="reload" />
      <button class="sort-btn" :class="{ active: sort === 'latest' }" @click="setSort('latest')">最新</button>
      <button class="sort-btn" :class="{ active: sort === 'hot' }" @click="setSort('hot')">最热</button>
    </div>

    <div v-if="allTags.length" class="tag-filter">
      <button class="tag-chip" :class="{ active: !tag }" @click="setTag('')">全部</button>
      <button v-for="t in allTags" :key="t" class="tag-chip" :class="{ active: tag === t }" @click="setTag(t)">#{{ t }}</button>
    </div>

    <ParticleState v-if="loading" class="state" text="正在加载作品" />
    <p v-else-if="error" class="state" role="alert">{{ error }} <button @click="reload">重试</button></p>
    <div v-else-if="!items.length" class="state">还没有作品，来上传第一个吧。</div>

    <div v-else class="works-grid">
      <RouterLink v-for="w in items" :key="w.id" :to="`/works/${w.id}`" class="work-card">
        <div class="work-cover" data-depth>
          <img v-if="w.coverUrl" :src="withThumbnail(w.coverUrl, 400)" :alt="w.title" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
          <span v-else class="cover-placeholder">{{ w.mediaType === 'video' ? '▶' : '🖼' }}</span>
          <span v-if="w.mediaType === 'video'" class="play-badge">▶</span>
        </div>
        <div class="work-info">
          <h3 class="work-title">{{ w.title }}</h3>
          <div class="work-meta">
            <UserLink nested :id="w.authorId" :name="w.authorName" />
            <span>{{ w.views }} 次观看</span>
          </div>
          <div class="work-stats">
            <span>❤ {{ w.likeCount }}</span>
            <span>⭐ {{ w.favoriteCount }}</span>
            <span>💬 {{ w.commentCount }}</span>
          </div>
        </div>
      </RouterLink>
    </div>

    <div class="pagination" v-if="items.length">
      <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
      <span>{{ page }} / {{ totalPages }}</span>
      <button :disabled="page >= totalPages" @click="changePage(page + 1)">下一页</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import UserLink from '@/components/UserLink.vue'
import { ref, computed, onMounted } from 'vue'
import { chaiWorkApi, type ChaiWorkBrief } from '@/api/chaiWork'
import { withThumbnail } from '@/utils/imageThumb'

const items = ref<ChaiWorkBrief[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 12
const total = ref(0)
const keyword = ref('')
const tag = ref('')
const sort = ref('latest')

// 标签筛选：从当前页作品里收集标签
const allTags = computed(() => {
  const set = new Set<string>()
  for (const w of items.value) for (const t of (w.tags || [])) set.add(t)
  return [...set].slice(0, 24)
})

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await chaiWorkApi.getList({ page: page.value, pageSize, keyword: keyword.value || undefined, tag: tag.value || undefined, sort: sort.value })
    items.value = res.items
    total.value = res.total
  } catch (e: any) {
    error.value = e.friendlyMessage || '作品加载失败，请重试'
  } finally {
    loading.value = false
  }
}

function reload() { page.value = 1; load() }
function setSort(s: string) { sort.value = s; reload() }
function setTag(t: string) { tag.value = t; reload() }
function changePage(p: number) { page.value = p; load() }

onMounted(load)
</script>

<style scoped>
.eyebrow { color: var(--text-secondary); font-size: .78rem; letter-spacing: .15em; }
.content-header h1 { margin: 8px 0; }
.intro { color: var(--text-secondary); }
.content-actions .primary { background: var(--accent-color); color: var(--on-accent); border: none; padding: 8px 18px; border-radius: 0; text-decoration: none; font-size: .85rem; }
.works-toolbar { display: flex; gap: 10px; align-items: center; margin: 20px 0; }
.search-input { flex: 1; max-width: 360px; padding: 8px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font-size: .85rem; }
.sort-btn { padding: 8px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .85rem; }
.sort-btn.active { color: var(--accent-color); border-color: var(--accent-color); }
.state { padding: 40px 0; text-align: center; color: var(--text-secondary); }
.tag-filter { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 18px; }
.tag-chip { padding: 4px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .78rem; }
.tag-chip:hover { border-color: var(--accent-color); color: var(--accent-color); }
.tag-chip.active { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.works-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 18px; }
.work-card { border: 1px solid var(--border-line); border-radius: 0; overflow: hidden; background: var(--paper-card); text-decoration: none; color: inherit; transition: transform .2s, border-color .2s; }
.work-card:hover { transform: translateY(-3px); border-color: var(--accent-color); }
.work-cover { position: relative; aspect-ratio: 16 / 9; background: var(--paper-sub); overflow: hidden; }
.work-cover img { width: 100%; height: 100%; object-fit: cover; }
.cover-placeholder { display: grid; place-items: center; height: 100%; font-size: 2rem; color: var(--text-secondary); }
.play-badge { position: absolute; right: 8px; bottom: 8px; width: 26px; height: 26px; display: grid; place-items: center; border-radius: 0; background: rgba(0,0,0,.55); color: #fff; font-size: .8rem; }
.work-info { padding: 12px 14px; }
.work-title { margin: 0 0 6px; font-size: .95rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.work-meta { display: flex; justify-content: space-between; font-size: .75rem; color: var(--text-secondary); margin-bottom: 6px; }
.work-stats { display: flex; gap: 12px; font-size: .75rem; color: var(--text-secondary); }
.pagination { display: flex; justify-content: center; align-items: center; gap: 16px; padding: 24px 0; }
.pagination button { padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.pagination button:disabled { opacity: .4; cursor: default; }
</style>
