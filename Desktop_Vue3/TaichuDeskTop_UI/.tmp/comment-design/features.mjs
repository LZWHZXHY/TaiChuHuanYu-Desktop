import fs from 'node:fs';
let p='src/api/artwork.ts',s=fs.readFileSync(p,'utf8').replace('export interface ArtworkItemDto extends ArtworkStats {','export interface ArtworkItemDto extends ArtworkStats {\n  rankingLikes?: number');s=s.replace('getGallery: (offset: number, limit: number = 20, search?: string)', 'getGallery: (offset: number, limit: number = 20, search?: string, rankPeriod?: string)').replace('params: { offset, limit, search:', 'params: { offset, limit, rankPeriod, search:');fs.writeFileSync(p,s);
p='src/views/画廊展厅/index.vue';s=fs.readFileSync(p,'utf8').replace("['日', '周', '月', '年']","['总', '日', '周', '月', '年']").replace("const currentTab = ref('月');","const currentTab = ref('总');").replace('<ArtworkRanking :period="currentTab"','<ArtworkRanking @open="openArtwork" :period="currentTab"');fs.writeFileSync(p,s);
fs.writeFileSync('src/views/画廊展厅/ArtworkRanking.vue',`<script setup lang="ts">
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
`);
p='src/views/个人中心/index.vue';s=fs.readFileSync(p,'utf8');s=s.replace("const fileInput =",`const experienceLogs = ref<{id:number;change:number;reason:string;createdAt:string}[]>([])
const experienceLoading = ref(false), experienceError = ref(''), experienceLoaded = ref(false), experienceMore = ref(false)
let experienceVersion = 0
async function loadExperience(more = false) {
  if (experienceLoading.value) return
  const version = ++experienceVersion
  experienceLoading.value = true; experienceError.value = ''
  try {
    const result = await request.get<{items:typeof experienceLogs.value;hasMore:boolean}>('/User/experience-history', { params: { offset: more ? experienceLogs.value.length : 0, limit: 20 } })
    if (version !== experienceVersion) return
    experienceLogs.value = more ? [...experienceLogs.value,...result.items] : result.items
    experienceMore.value = result.hasMore; experienceLoaded.value = true
  } catch { if (version === experienceVersion) experienceError.value = '经验记录加载失败，请重试。' }
  finally { if (version === experienceVersion) experienceLoading.value = false }
}
watch(() => userStore.token, () => { experienceVersion++; experienceLogs.value = []; experienceLoaded.value = false; experienceMore.value = false; experienceLoading.value = false; experienceError.value = '' })
onUnmounted(() => { experienceVersion++ })
const fileInput =`);
s=s.replace('    <div class="content-layout">',`    <details class="experience-history content-panel" @toggle="($event.target as HTMLDetailsElement).open && !experienceLoaded && loadExperience()">
      <summary>经验变动记录</summary><p class="history-note">查看已记录的收支原因。早期未记录的变动无法补回。</p>
      <p v-if="experienceError" role="alert">{{ experienceError }} <button class="btn-line" @click="loadExperience(experienceLogs.length > 0)">重试</button></p>
      <ol><li v-for="log in experienceLogs" :key="log.id"><div><strong>{{ log.reason || '经验变动' }}</strong><time>{{ new Date(log.createdAt).toLocaleString() }}</time></div><span :class="{ income: log.change > 0 }">{{ log.change > 0 ? '+' : '' }}{{ log.change }}</span></li></ol>
      <p v-if="experienceLoading" role="status">正在读取记录…</p><p v-else-if="experienceLoaded && !experienceLogs.length">暂无经验记录。</p>
      <button v-if="experienceMore" class="btn-line" :disabled="experienceLoading" @click="loadExperience(true)">加载更多</button>
    </details>
    <div class="content-layout">`);
s=s.replace('<style scoped>','<style scoped>\n.experience-history { margin: 0 0 24px; padding: 18px 22px; }\n.experience-history summary { cursor: pointer; font-weight: 600; }\n.history-note, .experience-history time { display: block; color: var(--text-secondary); font-size: .8rem; margin-top: 6px; }\n.experience-history ol { list-style: none; padding: 0; }\n.experience-history li { display: flex; justify-content: space-between; gap: 20px; padding: 14px 0; border-bottom: 1px solid var(--border-line); }\n.experience-history strong { font-size: .9rem; font-weight: 500; }\n.experience-history li > span { font-variant-numeric: tabular-nums; white-space: nowrap; }\n.experience-history .income { color: var(--accent-color); }');fs.writeFileSync(p,s);
