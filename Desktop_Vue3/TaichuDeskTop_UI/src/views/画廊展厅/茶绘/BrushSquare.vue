<!-- BrushSquare.vue：公开的茶绘笔刷（知识库「茶绘笔刷」分类，画室里的「笔刷库」也用它）。可以搜索、按热门 / 最新排序、收藏到自己的笔刷列表。 -->
<template>
  <section class="bsq" :class="{ compact }">
    <div class="bsq-head">
      <div v-if="!compact" class="sorts" role="radiogroup" aria-label="排序">
        <button type="button" role="radio" :aria-checked="sort === 'hot'" :class="{ on: sort === 'hot' }" @click="sort = 'hot'">最受欢迎</button>
        <button type="button" role="radio" :aria-checked="sort === 'new'" :class="{ on: sort === 'new' }" @click="sort = 'new'">最新</button>
      </div>
      <h3 v-else>茶绘笔刷 <span v-if="!loading">({{ total }})</span></h3>
      <form v-if="ownSearch" class="bsq-search" role="search" @submit.prevent="applySearch">
        <input v-model.trim="qInput" maxlength="50" placeholder="搜索笔刷名字" aria-label="搜索笔刷" />
        <button type="submit">搜索</button>
      </form>
      <RouterLink v-if="showStudioLink" class="to-studio" to="/gallery/paint">去茶绘画室用 →</RouterLink>
    </div>

    <p v-if="loading && !items.length" class="state" role="status">正在加载笔刷…</p>
    <p v-else-if="error" class="state" role="alert">{{ error }} <button type="button" @click="load(true)">重试</button></p>
    <p v-else-if="!items.length" class="state">{{ query ? '没有找到这个名字的笔刷。' : '还没有人公开笔刷。在茶绘画室里「制作笔刷」，勾选公开就会出现在这里。' }}</p>

    <ul v-else class="grid">
      <li v-for="b in shown" :key="b.id" class="card">
        <canvas :ref="el => setCanvas(b.id, el as HTMLCanvasElement | null)" width="320" height="96" :aria-label="`${b.name} 的效果示意`"></canvas>
        <div class="meta">
          <b :title="b.name">{{ b.name }}</b>
          <small>{{ b.ownerName }}<template v-if="b.params.tip === 'image'"> · 图案笔尖</template></small>
        </div>
        <div class="actions">
          <span class="fav-count" title="收藏人数">♥ {{ b.favorites }}</span>
          <span v-if="b.mine" class="tag">我的</span>
          <button v-else-if="user.isLoggedIn" type="button" class="fav" :class="{ on: b.favorited }" :disabled="busy === b.id" @click="toggleFavorite(b)">{{ b.favorited ? '已收藏' : '收藏' }}</button>
          <button v-if="pickable && (b.mine || b.favorited)" type="button" class="use" @click="emit('use', b)">用这支</button>
        </div>
      </li>
    </ul>

    <div class="bsq-foot">
      <button v-if="compact && total > shown.length" type="button" class="more" @click="emit('more')">查看全部 {{ total }} 支笔刷</button>
      <button v-else-if="!compact && items.length < total" type="button" class="more" :disabled="loading" @click="load(false)">{{ loading ? '加载中…' : '加载更多' }}</button>
      <p v-if="!user.isLoggedIn && !compact" class="hint">登录后可以收藏笔刷，收藏的笔刷会出现在茶绘画室的「我的笔刷」里。</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useUserStore } from '@/stores/user'
import { brushApi, drawBrushPreview, errorText, onTipLoaded, type BrushView } from './brushApi'

const props = withDefaults(defineProps<{
  /** 外部给的搜索词（知识库的搜索框）；不给就显示自己的搜索框 */
  search?: string | null
  /** 精简模式：只显示 6 支（知识库「全部资料」里用） */
  compact?: boolean
  /** 画室里用：已收藏 / 自己的笔刷显示「用这支」 */
  pickable?: boolean
  showStudioLink?: boolean
}>(), { search: null, compact: false, pickable: false, showStudioLink: false })
const emit = defineEmits<{ more: []; use: [brush: BrushView]; changed: [] }>()

const user = useUserStore()
const PAGE = 24
const sort = ref<'hot' | 'new'>('hot')
const qInput = ref('')
const ownQuery = ref('')
const ownSearch = computed(() => props.search == null)
const query = computed(() => (props.search ?? ownQuery.value).trim())
const items = ref<BrushView[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const busy = ref('')
const shown = computed(() => (props.compact ? items.value.slice(0, 6) : items.value))
let version = 0

async function load(reset: boolean) {
  const v = ++version
  loading.value = true
  error.value = ''
  try {
    const res = await brushApi.square({ sort: sort.value, q: query.value || undefined, offset: reset ? 0 : items.value.length, limit: props.compact ? 6 : PAGE })
    if (v !== version) return
    items.value = reset ? res.items : [...items.value, ...res.items]
    total.value = res.total
    await nextTick()
    drawAll()
  } catch (e) {
    if (v === version) error.value = errorText(e, '笔刷加载失败')
  } finally { if (v === version) loading.value = false }
}
function applySearch() { ownQuery.value = qInput.value }

const canvases = new Map<string, HTMLCanvasElement>()
function setCanvas(id: string, el: HTMLCanvasElement | null) { if (el) canvases.set(id, el); else canvases.delete(id) }
function drawAll() {
  for (const b of shown.value) {
    const c = canvases.get(b.id)
    if (c) drawBrushPreview(c, b.params, '#1b2128', Math.max(0.6, b.params.opacity))
  }
}

async function toggleFavorite(b: BrushView) {
  busy.value = b.id
  try {
    const res = await brushApi.favorite(b.id, !b.favorited)
    b.favorited = res.favorited
    b.favorites = res.favorites
    emit('changed')
  } catch (e) { error.value = errorText(e, '操作失败') } finally { busy.value = '' }
}

watch([sort, query], () => void load(true))
const off = onTipLoaded(drawAll)
onMounted(() => void load(true))
onBeforeUnmount(() => { off() })
</script>

<style scoped>
.bsq { display: grid; gap: 12px; }
.bsq-head { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 14px; }
.bsq-head h3 { margin: 0; font-size: 16px; }
.sorts { display: inline-flex; border: 1px solid var(--border-line); }
.sorts button { padding: 6px 14px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.sorts button:last-child { border-right: 0; }
.sorts button.on { background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.bsq-search { display: flex; gap: 6px; }
.bsq-search input { width: 200px; padding: 6px 9px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; }
.bsq-search button, .more, .state button { padding: 6px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.to-studio { margin-left: auto; color: var(--accent-color); font-size: 13px; text-decoration: none; }
.state { margin: 0; padding: 24px 12px; border: 1px dashed var(--border-line); text-align: center; color: var(--text-secondary); font-size: 13px; }
.grid { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 10px; }
.card { display: grid; gap: 8px; padding: 10px; border: 1px solid var(--border-line); background: var(--paper-card); }
.card canvas { width: 100%; height: auto; aspect-ratio: 320 / 96; background: #fff; border: 1px solid var(--border-line); }
.meta { display: grid; gap: 2px; min-width: 0; }
.meta b { font-size: 14px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.meta small { font-size: 12px; color: var(--text-secondary); }
.actions { display: flex; align-items: center; gap: 8px; }
.fav-count { font: 12px var(--font-mono, monospace); color: var(--text-secondary); margin-right: auto; }
.tag { padding: 1px 6px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; }
.fav, .use { padding: 4px 10px; border: 1px solid var(--accent-color); background: transparent; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.fav.on { background: var(--accent-color); color: var(--on-accent, #fff); }
.use { background: var(--accent-color); color: var(--on-accent, #fff); }
button:disabled { opacity: .5; cursor: not-allowed; }
.bsq-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.hint { margin: 0; font-size: 12px; color: var(--text-secondary); }
.compact .grid { grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); }
</style>
