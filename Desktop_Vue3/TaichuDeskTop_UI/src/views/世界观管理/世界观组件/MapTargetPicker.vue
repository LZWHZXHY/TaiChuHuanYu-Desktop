<!-- MapTargetPicker.vue：多层地图——给图钉选「下一层地图」。列出这个世界里所有有地图区块的条目，
     一个条目有好几张地图时每张单独一行（显示区块标题），点一行就选好。图钉连到的条目排在最前面。 -->
<template>
  <div class="map-target-picker" role="dialog" aria-label="选择下一层地图" @keydown.esc.stop="$emit('close')">
    <div class="mtp-search">
      <span aria-hidden="true">⌕</span>
      <input ref="searchInput" v-model="keyword" placeholder="搜索有地图的条目…" aria-label="搜索有地图的条目" />
      <button type="button" class="mtp-x" aria-label="关闭" @click="$emit('close')">×</button>
    </div>
    <div class="mtp-list">
      <p v-if="loading" class="mtp-note">正在查找这个世界里的地图…</p>
      <p v-else-if="failed" class="mtp-note">读取失败了。<button type="button" class="mtp-retry" @click="load(true)">重试</button></p>
      <template v-else>
        <button v-for="m in matches" :key="m.cardId + '/' + m.blockId" type="button" class="mtp-item" @click="$emit('confirm', { cardId: m.cardId, blockId: m.blockId })">
          <span class="mtp-thumb"><img v-if="m.image" :src="thumb(m.image)" alt="" loading="lazy" /><span v-else aria-hidden="true">⌖</span></span>
          <span class="mtp-text">
            <b>{{ m.cardTitle }}<em v-if="m.cardId === preferCardId">图钉连到的条目</em></b>
            <small>{{ getTypeLabel(m.cardType) }}<template v-if="multi.has(m.cardId)"> · {{ m.blockTitle }}</template> · {{ m.image ? `${m.pinCount} 个图钉` : '还没上传底图' }}</small>
          </span>
        </button>
        <p v-if="!matches.length" class="mtp-note">{{ keyword.trim() ? '没有匹配的地图' : '这个世界里还没有别的地图。先在别的条目里加一个「地图」区块并保存。' }}</p>
      </template>
    </div>
    <p class="mtp-foot">只列出已经保存的地图；刚加的地图要先保存那个条目。</p>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import type { MapTarget } from '@/utils/worldLayout'
import { useWorldMaps } from '@/composables/世界观管理/useWorldMaps'

const props = defineProps<{ projectId: string; exclude?: MapTarget | null; preferCardId?: string | null }>()
defineEmits<{ close: []; confirm: [target: MapTarget] }>()
const keyword = ref('')
const searchInput = ref<HTMLInputElement | null>(null)
const { maps, loading, failed, load } = useWorldMaps(() => props.projectId)
onMounted(() => { searchInput.value?.focus(); void load() })

// 自己这张地图不能当自己的下一层
const pool = computed(() => maps.value.filter(m => !(props.exclude && m.cardId === props.exclude.cardId && m.blockId === props.exclude.blockId)))
// 同一个条目有好几张地图时，要显示区块标题才分得清
const multi = computed(() => {
  const count = new Map<string, number>()
  for (const m of pool.value) count.set(m.cardId, (count.get(m.cardId) ?? 0) + 1)
  return new Set([...count].filter(([, n]) => n > 1).map(([id]) => id))
})
const matches = computed(() => {
  const q = keyword.value.trim().toLowerCase()
  const list = pool.value.filter(m => !q || m.cardTitle.toLowerCase().includes(q) || m.blockTitle.toLowerCase().includes(q))
  // 图钉连到的条目排最前（多半就是要进入它的地图）
  return [...list.filter(m => m.cardId === props.preferCardId), ...list.filter(m => m.cardId !== props.preferCardId)].slice(0, 100)
})
const thumb = (url: string) => withThumbnail(url, 96)
</script>

<style scoped>
.map-target-picker { width: min(360px, calc(100vw - 32px)); display: flex; flex-direction: column; max-height: 400px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); box-shadow: 0 14px 36px rgba(0, 0, 0, .18); color: var(--text-primary); }
.mtp-search { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--border-line); color: var(--text-secondary); }
.mtp-search input { flex: 1; min-width: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 13px; outline: none; }
.mtp-x { border: 0; background: none; color: var(--text-secondary); font-size: 16px; cursor: pointer; }
.mtp-list { flex: 1; min-height: 100px; overflow-y: auto; padding: 4px 0; }
.mtp-item { width: 100%; display: grid; grid-template-columns: 40px minmax(0, 1fr); align-items: center; gap: 10px; padding: 6px 12px; border: 0; background: none; color: inherit; font: inherit; text-align: left; cursor: pointer; }
.mtp-item:hover, .mtp-item:focus-visible { background: var(--accent-soft); outline: none; }
.mtp-thumb { width: 40px; height: 40px; display: grid; place-items: center; overflow: hidden; background: var(--bg-sub); color: var(--text-secondary); }
.mtp-thumb img { width: 100%; height: 100%; object-fit: cover; }
.mtp-text { display: grid; min-width: 0; }
.mtp-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 500; }
.mtp-text b em { margin-left: 6px; padding: 0 4px; font-style: normal; font-size: 10px; font-weight: 400; color: var(--accent-color); border: 1px solid var(--accent-color); }
.mtp-text small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; color: var(--text-secondary); }
.mtp-note { margin: 0; padding: 18px 12px; text-align: center; font-size: 12px; line-height: 1.6; color: var(--text-secondary); }
.mtp-retry { border: 0; background: none; color: var(--accent-color); font: inherit; cursor: pointer; text-decoration: underline; }
.mtp-foot { margin: 0; padding: 6px 12px; border-top: 1px solid var(--border-line); font-size: 11px; color: var(--text-secondary); }
</style>
