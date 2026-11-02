<!-- 岁月史书年鉴：把一整年的岁月史书排成一本「书」（封面、目录、按月份排的正文），可以直接打印，或者在打印窗口里「另存为 PDF」。
     整页盖在网站上面（Teleport 到 body），打印时只印这本书，不印网站的菜单和顶栏。 -->
<template>
  <Teleport to="body">
    <div class="yb-root">
      <div class="yb-toolbar" role="toolbar" aria-label="年鉴">
        <RouterLink :to="{ name: 'CommunityHistory' }" class="yb-btn">← 回到岁月史书</RouterLink>
        <label class="yb-year">年份
          <select v-model.number="year" aria-label="选择年份">
            <option v-for="y in yearOptions" :key="y.year" :value="y.year">{{ y.year }}（{{ y.count }} 段）</option>
          </select>
        </label>
        <span class="yb-sp"></span>
        <span v-if="items.length" class="yb-tip">{{ imagesLeft ? `图片加载中（还剩 ${imagesLeft} 张）…` : '在打印窗口里把「目标打印机」选成「另存为 PDF」就能存成 PDF' }}</span>
        <button type="button" class="yb-btn primary" :disabled="!items.length || loading" @click="print">🖨 打印 / 存成 PDF</button>
      </div>

      <p v-if="loading" class="yb-state">正在装订 {{ year }} 年的年鉴…</p>
      <p v-else-if="error" class="yb-state" role="alert">{{ error }} <button type="button" class="yb-link" @click="load">重试</button></p>
      <p v-else-if="!items.length" class="yb-state">{{ year }} 年还没有记录。</p>

      <article v-else class="yb-book" :aria-label="`岁月史书 ${year} 年鉴`">
        <!-- 封面 -->
        <section class="yb-cover">
          <p class="yb-cover-top">太初寰宇 · TAICHU HUANYU</p>
          <h1><small>岁月史书</small><span>{{ year }}</span><small>年鉴</small></h1>
          <p class="yb-cover-sub">这一年，社区里的名场面、日常、和大家做出来的东西。</p>
          <dl class="yb-cover-stats">
            <div><dt>记下</dt><dd>{{ items.length }} 段</dd></div>
            <div><dt>参与记录</dt><dd>{{ contributors }} 人</dd></div>
            <div><dt>截图 / 照片</dt><dd>{{ imageCount }} 张</dd></div>
            <div><dt>评论</dt><dd>{{ commentCount }} 条</dd></div>
          </dl>
          <p class="yb-cover-foot">装订于 {{ printedAt }}</p>
        </section>

        <!-- 目录 -->
        <section class="yb-toc">
          <h2>目录</h2>
          <ol>
            <li v-for="m in months" :key="m.key"><a :href="`#yb-${m.key}`" @click.prevent="jump(m.key)"><span>{{ m.label }}</span><i></i><b>{{ m.entries.length }} 段</b></a></li>
          </ol>
          <h3 class="yb-toc-kinds-title">这一年都记了些什么</h3>
          <ul class="yb-toc-kinds">
            <li v-for="k in kindStats" :key="k.kind">{{ KIND_META[k.kind].icon }} {{ KIND_META[k.kind].label }} <b>{{ k.count }}</b></li>
          </ul>
        </section>

        <!-- 正文 -->
        <section v-for="m in months" :id="`yb-${m.key}`" :key="m.key" class="yb-month">
          <h2 class="yb-month-title"><span>{{ m.label }}</span><small>{{ m.entries.length }} 段</small></h2>
          <article v-for="e in m.entries" :key="e.id" class="yb-entry">
            <header class="yb-entry-head">
              <span class="yb-date">{{ dateText(e) }}</span>
              <span class="yb-kind">{{ KIND_META[e.kind].icon }} {{ KIND_META[e.kind].label }}</span>
            </header>
            <h3 v-if="e.title" class="yb-title">{{ e.title }}</h3>
            <p v-if="e.content" class="yb-content">{{ e.content }}</p>
            <div v-if="e.images.length" class="yb-images" :class="'n' + Math.min(e.images.length, 3)">
              <img v-for="url in e.images" :key="url" :src="e.images.length === 1 ? withThumbnail(url, 1200) : withThumbnail(url, 600)" alt="" @load="imageDone" @error="imageDone" />
            </div>
            <footer class="yb-entry-foot">
              <span>— {{ e.author.username }} 记</span>
              <span v-if="e.mentions?.length">提到 {{ e.mentions.map(p => '@' + p.username).join(' ') }}</span>
              <span v-if="reactionText(e)">{{ reactionText(e) }}</span>
              <span v-if="e.commentCount">💬 {{ e.commentCount }} 条评论</span>
            </footer>
          </article>
        </section>

        <p class="yb-end">— {{ year }} 年，完 —</p>
      </article>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { historyApi, errorText, KIND_META, REACTION_META, type HistoryEntry, type HistoryKind, type ReactionKind } from '@/api/history'
import { withThumbnail } from '@/utils/imageThumb'

const route = useRoute()
const router = useRouter()
const year = ref(Number(route.query.year) || new Date().getFullYear())
const items = ref<HistoryEntry[]>([])
const years = ref<{ year: number; count: number }[]>([])
const contributors = ref(0)
const loading = ref(false)
const error = ref('')
const imagesLeft = ref(0)
let seq = 0

async function load() {
  const mine = ++seq
  loading.value = true; error.value = ''
  try {
    const res = await historyApi.yearbook(year.value)
    if (mine !== seq) return
    items.value = res.items
    years.value = res.years
    contributors.value = res.contributors
    imagesLeft.value = res.items.reduce((s, e) => s + e.images.length, 0)
  } catch (e) {
    if (mine === seq) { error.value = errorText(e, '年鉴读不出来'); items.value = [] }
  } finally { if (mine === seq) loading.value = false }
}
watch(year, y => { void router.replace({ query: { ...route.query, year: String(y) } }).catch(() => {}); void load() })
onMounted(() => { document.body.classList.add('yb-printing'); void load() })
onBeforeUnmount(() => document.body.classList.remove('yb-printing'))

const yearOptions = computed(() => years.value.some(y => y.year === year.value) ? years.value : [{ year: year.value, count: 0 }, ...years.value])
const imageCount = computed(() => items.value.reduce((s, e) => s + e.images.length, 0))
const commentCount = computed(() => items.value.reduce((s, e) => s + e.commentCount, 0))
const printedAt = new Date().toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' })

/** 按月分组；只记得年份的放在最后「这一年」 */
const months = computed(() => {
  const map = new Map<string, { key: string; label: string; order: number; entries: HistoryEntry[] }>()
  for (const e of items.value) {
    const m = Number(e.happenedOn.slice(5, 7))
    const key = e.precision === 'year' ? 'year' : `m${m}`
    if (!map.has(key)) map.set(key, { key, label: key === 'year' ? '这一年（日子记不清的）' : `${m} 月`, order: key === 'year' ? 99 : m, entries: [] })
    map.get(key)!.entries.push(e)
  }
  return [...map.values()].sort((a, b) => a.order - b.order)
})
const kindStats = computed(() => {
  const counts = new Map<HistoryKind, number>()
  for (const e of items.value) counts.set(e.kind, (counts.get(e.kind) ?? 0) + 1)
  return [...counts.entries()].map(([kind, count]) => ({ kind, count })).sort((a, b) => b.count - a.count)
})

function dateText(e: HistoryEntry) {
  const [, m, d] = e.happenedOn.split('-').map(Number)
  return e.precision === 'day' ? `${m} 月 ${d} 日` : e.precision === 'month' ? `${m} 月（哪天记不清了）` : '这一年'
}
function reactionText(e: HistoryEntry) {
  return (Object.keys(REACTION_META) as ReactionKind[]).filter(k => e.reactions[k]).map(k => `${REACTION_META[k].icon} ${e.reactions[k]}`).join('  ')
}
function imageDone() { if (imagesLeft.value > 0) imagesLeft.value-- }
function jump(key: string) { document.getElementById(`yb-${key}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }
function print() {
  if (imagesLeft.value && !window.confirm(`还有 ${imagesLeft.value} 张图片没加载出来，现在打印的话这些图会是空白。继续？`)) return
  window.print()
}
</script>

<style scoped>
.yb-root { position: fixed; inset: 0; z-index: 3000; overflow-y: auto; background: #e9e6df; color: #1f1d1a; }
.yb-toolbar { position: sticky; top: 0; z-index: 2; display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding: 10px 16px; background: #faf8f3; border-bottom: 1px solid #d6d1c4; }
.yb-sp { flex: 1; }
.yb-tip { font-size: 12px; color: #6b665c; }
.yb-btn { display: inline-flex; align-items: center; padding: 7px 14px; border: 1px solid #cfc9bb; background: #fff; color: #1f1d1a; font: inherit; font-size: 13px; text-decoration: none; cursor: pointer; }
.yb-btn.primary { background: #1f1d1a; border-color: #1f1d1a; color: #fff; }
.yb-btn:disabled { opacity: .5; cursor: default; }
.yb-year { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; }
.yb-year select { padding: 6px 8px; border: 1px solid #cfc9bb; background: #fff; font: inherit; font-size: 13px; }
.yb-state { padding: 80px 16px; text-align: center; color: #6b665c; }
.yb-link { padding: 0; border: 0; background: none; color: #8a4b0f; text-decoration: underline; font: inherit; cursor: pointer; }

/* 书：屏幕上像一页页纸，打印时就是纸 */
.yb-book { max-width: 820px; margin: 24px auto 64px; font-family: "Noto Serif SC", "Source Han Serif SC", "Songti SC", serif; }
.yb-book > section { background: #fffdf8; padding: 56px 64px; margin-bottom: 18px; box-shadow: 0 2px 10px rgb(0 0 0 / .08); }
.yb-cover { min-height: 860px; display: flex; flex-direction: column; justify-content: center; text-align: center; border-top: 10px solid #8a4b0f; }
.yb-cover-top { margin: 0 0 48px; font-size: 13px; letter-spacing: .3em; color: #8a4b0f; }
.yb-cover h1 { margin: 0; display: grid; gap: 8px; font-weight: 700; }
.yb-cover h1 small { font-size: 28px; letter-spacing: .4em; }
.yb-cover h1 span { font-size: 120px; line-height: 1; letter-spacing: -.02em; color: #8a4b0f; }
.yb-cover-sub { margin: 32px 0 48px; font-size: 16px; color: #5c574d; }
.yb-cover-stats { display: grid; grid-template-columns: repeat(4, 1fr); margin: 0 auto; max-width: 560px; border-top: 1px solid #d6d1c4; border-bottom: 1px solid #d6d1c4; }
.yb-cover-stats div { padding: 14px 4px; }
.yb-cover-stats dt { font-size: 12px; color: #6b665c; }
.yb-cover-stats dd { margin: 4px 0 0; font-size: 20px; font-weight: 700; }
.yb-cover-foot { margin: 56px 0 0; font-size: 12px; color: #8f897c; }

.yb-toc h2, .yb-month-title { margin: 0 0 24px; font-size: 26px; border-bottom: 2px solid #1f1d1a; padding-bottom: 10px; }
.yb-toc ol { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.yb-toc a { display: flex; align-items: baseline; gap: 8px; color: inherit; text-decoration: none; font-size: 17px; }
.yb-toc a i { flex: 1; border-bottom: 1px dotted #b5ae9f; }
.yb-toc a b { font-weight: 400; color: #6b665c; font-size: 14px; }
.yb-toc-kinds-title { margin: 36px 0 12px; font-size: 16px; }
.yb-toc-kinds { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 8px 18px; font-size: 15px; }
.yb-toc-kinds b { color: #8a4b0f; }

.yb-month-title { display: flex; align-items: baseline; justify-content: space-between; }
.yb-month-title small { font-size: 14px; font-weight: 400; color: #6b665c; }
.yb-entry { padding: 18px 0 22px; border-bottom: 1px solid #e3ded2; }
.yb-entry:last-child { border-bottom: 0; }
.yb-entry-head { display: flex; align-items: baseline; gap: 12px; font-size: 13px; color: #6b665c; }
.yb-date { font-weight: 700; color: #8a4b0f; font-size: 15px; }
.yb-title { margin: 8px 0 6px; font-size: 19px; line-height: 1.5; }
.yb-content { margin: 6px 0 0; font-size: 15px; line-height: 1.95; white-space: pre-wrap; overflow-wrap: anywhere; }
.yb-images { display: grid; gap: 6px; margin-top: 12px; }
.yb-images.n1 { grid-template-columns: 1fr; }
.yb-images.n2 { grid-template-columns: 1fr 1fr; }
.yb-images.n3 { grid-template-columns: 1fr 1fr 1fr; }
.yb-images img { width: 100%; display: block; object-fit: cover; background: #eee9de; }
.yb-images.n1 img { max-height: 520px; object-fit: contain; }
.yb-images:not(.n1) img { aspect-ratio: 1; }
.yb-entry-foot { display: flex; flex-wrap: wrap; gap: 4px 16px; margin-top: 12px; font-size: 13px; color: #6b665c; }
.yb-end { text-align: center; color: #8f897c; letter-spacing: .3em; margin: 32px 0; }
@media (max-width: 720px) {
  .yb-book > section { padding: 28px 18px; }
  .yb-cover { min-height: 0; padding-top: 80px !important; padding-bottom: 80px !important; }
  .yb-cover h1 span { font-size: 80px; }
  .yb-cover-stats { grid-template-columns: 1fr 1fr; }
}
</style>

<style>
/* 打印：只印年鉴这本书（网站的菜单、顶栏都不印），每个月从新的一页开始 */
@media print {
  @page { size: A4; margin: 14mm 12mm; }
  html, body { height: auto !important; overflow: visible !important; background: #fff !important; }
  body.yb-printing > *:not(.yb-root) { display: none !important; }
  body.yb-printing .yb-root { position: static !important; overflow: visible !important; background: #fff !important; }
  body.yb-printing .yb-toolbar { display: none !important; }
  body.yb-printing .yb-book { margin: 0 !important; max-width: none !important; }
  body.yb-printing .yb-book > section { box-shadow: none !important; margin: 0 !important; padding: 0 !important; background: #fff !important; break-after: page; }
  body.yb-printing .yb-cover { min-height: 250mm !important; border-top: 0 !important; }
  body.yb-printing .yb-book > section:last-of-type { break-after: auto; }
  body.yb-printing .yb-entry { break-inside: avoid-page; }
  body.yb-printing .yb-images { break-inside: avoid; }
  body.yb-printing .yb-toc a { color: #000 !important; }
  body.yb-printing * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
}
</style>
