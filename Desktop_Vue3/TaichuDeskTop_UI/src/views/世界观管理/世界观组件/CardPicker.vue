<!-- CardPicker.vue：从这个世界里多选条目（时间线事件「添加卡片」用），像 vvd 那样搜索 + 勾选 + 一次添加 -->
<template>
  <div class="card-picker" role="dialog" aria-label="选择条目" @keydown.esc.stop="$emit('close')">
    <div class="cp-search">
      <span aria-hidden="true">⌕</span>
      <input ref="searchInput" v-model="keyword" placeholder="搜索条目…" aria-label="搜索条目" />
      <select v-model="typeFilter" aria-label="按类型筛选">
        <option value="">全部类型</option>
        <option v-for="t in types" :key="t" :value="t">{{ getTypeLabel(t) }}</option>
      </select>
    </div>
    <div class="cp-list">
      <label v-for="card in matches" :key="card.id" class="cp-item">
        <span class="cp-thumb">
          <img v-if="card.coverImage" :src="thumb(card.coverImage)" alt="" loading="lazy" />
          <span v-else aria-hidden="true">▢</span>
        </span>
        <span class="cp-text"><b>{{ card.title }}</b><small>{{ getTypeLabel(card.type) }}</small></span>
        <input type="checkbox" :checked="picked.has(card.id)" :aria-label="`选择 ${card.title}`" @change="toggle(card.id)" />
      </label>
      <p v-if="!matches.length" class="cp-empty">没有匹配的条目</p>
    </div>
    <div class="cp-foot">
      <span>{{ picked.size }} 个条目已选择</span>
      <button type="button" class="cp-clear" :disabled="!picked.size" @click="picked = new Set()">清除</button>
      <button type="button" class="cp-add" :disabled="!picked.size" @click="confirm">添加 {{ picked.size }} 个条目</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel as getTypeLabel, parseCoverImage } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'

const props = defineProps<{ projectId: string; exclude?: string[] }>()
const emit = defineEmits<{ close: []; confirm: [ids: string[]] }>()
const store = useWorldStore()
const keyword = ref('')
const typeFilter = ref('')
const picked = ref(new Set<string>())
const searchInput = ref<HTMLInputElement | null>(null)
onMounted(() => searchInput.value?.focus())

const pool = computed(() => store.cards.filter(c => c.projectId === props.projectId && !(props.exclude ?? []).includes(c.id)))
const types = computed(() => [...new Set(pool.value.map(c => c.type))])
const matches = computed(() => {
  const q = keyword.value.trim().toLowerCase()
  return pool.value.filter(c => (!typeFilter.value || c.type === typeFilter.value) && (!q || c.title.toLowerCase().includes(q))).slice(0, 80)
})
const thumb = (url: string) => withThumbnail(parseCoverImage(url), 96)
function toggle(id: string) {
  const next = new Set(picked.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  picked.value = next
}
function confirm() { emit('confirm', [...picked.value]); picked.value = new Set() }
</script>

<style scoped>
.card-picker { width: min(360px, calc(100vw - 32px)); display: flex; flex-direction: column; max-height: 420px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); box-shadow: 0 14px 36px rgba(0, 0, 0, .18); color: var(--text-primary); }
.cp-search { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--border-line); color: var(--text-secondary); }
.cp-search input { flex: 1; min-width: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 13px; outline: none; }
.cp-search select { max-width: 96px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 12px; padding: 2px 4px; }
.cp-list { flex: 1; min-height: 120px; overflow-y: auto; padding: 4px 0; }
.cp-item { display: grid; grid-template-columns: 36px minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 6px 12px; cursor: pointer; }
.cp-item:hover { background: var(--accent-soft); }
.cp-thumb { width: 36px; height: 36px; display: grid; place-items: center; overflow: hidden; background: var(--bg-sub); color: var(--text-secondary); }
.cp-thumb img { width: 100%; height: 100%; object-fit: cover; }
.cp-text { display: grid; min-width: 0; }
.cp-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 500; }
.cp-text small { font-size: 11px; color: var(--text-secondary); }
.cp-item input { width: 16px; height: 16px; accent-color: var(--accent-color); }
.cp-empty { margin: 0; padding: 20px 12px; text-align: center; font-size: 12px; color: var(--text-secondary); }
.cp-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 10px 12px; border-top: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.cp-clear { margin-left: auto; border: 0; background: none; color: var(--text-secondary); font: inherit; cursor: pointer; }
.cp-clear:disabled { opacity: .4; cursor: default; }
.cp-add { width: 100%; padding: 8px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 13px; cursor: pointer; }
.cp-add:disabled { opacity: .45; cursor: default; }
</style>
