<!-- 条目页底部「编年史中的事件」：编年史里提到这个条目的事（关联、地点、标题或描述里 @ 了它），点了跳到编年史里那件事。 -->
<template>
  <section v-if="rows.length || ownLines.length" class="doc-section cce">
    <h2 class="section-title">
      编年史中的事件
      <span class="section-count">{{ rows.length }}</span>
      <router-link class="cce-all" :to="chronicleLink({ card: cardId })">在编年史里看全部 →</router-link>
    </h2>
    <p v-if="ownLines.length" class="cce-own">
      有关它的时间线：
      <router-link v-for="l in ownLines" :key="l.id" :to="chronicleLink({ card: cardId })" class="cce-line" :style="{ '--lc': l.color || 'var(--accent-color)' }">{{ l.name }}</router-link>
    </p>
    <ol class="cce-list">
      <li v-for="r in rows.slice(0, expanded ? rows.length : 8)" :key="r.ev.id" class="cce-item" :style="{ '--lc': r.color }">
        <span class="cce-when">{{ r.when }}</span>
        <router-link class="cce-title" :to="chronicleLink({ event: r.ev.id })" :title="`在编年史里看这件事（${r.line}）`">
          {{ r.title }}<small>{{ r.line }}{{ r.role ? ' · ' + r.role : '' }}</small>
        </router-link>
      </li>
    </ol>
    <button v-if="rows.length > 8" type="button" class="cce-more" @click="expanded = !expanded">{{ expanded ? '收起' : `还有 ${rows.length - 8} 件，展开` }}</button>
  </section>
</template>

<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue'
import { chronicleApi, type CardChronicle } from '@/api/chronicle'
import { useWorldStore } from '@/stores/world'
import { LINE_COLORS, normalizeCalendar, eventStart, formatEventDate, plainTitle, refIds } from '@/utils/chronicle'

const props = defineProps<{ projectId: string; cardId: string }>()
const store = useWorldStore()
const data = ref<CardChronicle | null>(null)
const expanded = ref(false)
let version = 0
async function load() {
  const v = ++version
  data.value = null; expanded.value = false
  try { const d = await chronicleApi.byCard(props.projectId, props.cardId); if (v === version) data.value = d }
  catch { /* 没有编年史或者读不出来：不显示 */ }
}
watch(() => [props.projectId, props.cardId], load, { immediate: true })
onScopeDispose(() => { version++ })

const findCard = (id: string) => store.getCardById(id, props.projectId)
const chronicleLink = (query: Record<string, string>) => ({ name: 'ProjectDetail', params: { id: props.projectId }, query: { view: 'timeline', ...query } })
const ownLines = computed(() => (data.value?.timelines ?? []).filter(l => data.value!.ownLines.includes(l.id)))
const rows = computed(() => {
  const d = data.value
  if (!d) return []
  const cal = normalizeCalendar(d.calendar)
  const lines = new Map(d.timelines.map((l, i) => [l.id, { ...l, color: l.color || LINE_COLORS[i % LINE_COLORS.length]! }]))
  return d.events.filter(e => lines.has(e.timelineId))
    .map(e => {
      const role = e.locationCardId === props.cardId ? '发生地点' : e.cardIds.includes(props.cardId) ? '' : refIds(e.title).includes(props.cardId) || refIds(e.description).includes(props.cardId) ? '文中提到' : ''
      return { ev: e, t: eventStart(cal, e), when: formatEventDate(cal, e), title: plainTitle(e.title, findCard), line: lines.get(e.timelineId)!.name, color: lines.get(e.timelineId)!.color, role }
    })
    .sort((a, b) => (a.t ?? Infinity) - (b.t ?? Infinity) || a.ev.sortOrder - b.ev.sortOrder)
})
</script>

<style scoped>
.cce-all { margin-left: auto; font-size: 12px; font-weight: 400; color: var(--accent-color); text-decoration: none; }
.cce-all:hover { text-decoration: underline; }
.section-title { display: flex; align-items: center; gap: 8px; margin: 0 0 14px; padding: 0 0 8px 12px; font-size: 12px; font-weight: 600; letter-spacing: 1px; color: var(--text-secondary);
  border-bottom: 1px solid var(--surface-edge, var(--border-line)); border-left: 3px solid var(--surface-color, var(--accent-color)); }
.section-count { font-size: 11px; font-weight: 400; letter-spacing: 0; color: var(--text-secondary); }
.cce-all { letter-spacing: 0; }
.cce-own { margin: 0 0 8px; font-size: 13px; color: var(--text-secondary); }
.cce-line { margin-right: 8px; padding: 1px 8px; border-left: 3px solid var(--lc); background: var(--bg-sub); color: var(--text-primary); text-decoration: none; }
.cce-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.cce-item { display: grid; grid-template-columns: minmax(90px, 30%) minmax(0, 1fr); gap: 12px; padding: 6px 8px; border-left: 3px solid var(--lc); }
.cce-item:hover { background: var(--bg-sub); }
.cce-when { font-size: 12px; font-weight: 600; color: var(--accent-color); overflow-wrap: anywhere; }
.cce-title { min-width: 0; color: var(--text-primary); text-decoration: none; font-size: 14px; overflow-wrap: anywhere; }
.cce-title small { margin-left: 8px; font-size: 12px; color: var(--text-secondary); }
.cce-title:hover { color: var(--accent-color); }
.cce-more { margin-top: 6px; padding: 4px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
@media (max-width: 640px) { .cce-item { grid-template-columns: 1fr; gap: 2px; } }
</style>
