<template>
  <div class="timeline-view">
    <header class="tl-header">
      <h2 class="tl-heading">🕐 世界时间线</h2>
      <p class="tl-sub">按时间轴纵览整个世界的大事记，可按纪元分段。</p>
    </header>

    <p v-if="loading" class="tl-empty">正在加载时间线…</p>
    <p v-else-if="!items.length" class="tl-empty">
      还没有时间线事件。进入某张卡片，在「时间线事件」里添加时间 + 标题即可在这里汇聚成时间轴。
    </p>

    <template v-else>
      <template v-for="group in groups" :key="group.era">
        <h3 v-if="group.era" class="tl-era-title">{{ group.era }}</h3>
        <ol class="tl-list">
          <li v-for="(item, i) in group.items" :key="item.cardId + ':' + i" class="tl-item">
            <div class="tl-rail">
              <span class="tl-dot" :class="'type-' + item.cardType"></span>
              <span v-if="i < group.items.length - 1 || group !== lastGroup" class="tl-line"></span>
            </div>
            <div class="tl-content">
              <span class="tl-date">{{ item.date || '未标注时间' }}</span>
              <h3 class="tl-title">{{ item.title }}</h3>
              <p v-if="item.description" class="tl-desc">{{ item.description }}</p>
              <button class="tl-source" @click="$emit('select', item.cardId)">
                📄 {{ item.cardTitle }}
              </button>
            </div>
          </li>
        </ol>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { worldApi, type TimelineItem } from '@/api/worldApi'

const props = defineProps<{ projectId: string }>()
defineEmits<{ select: [id: string] }>()
const items = ref<TimelineItem[]>([])
const loading = ref(false)

// 数字感知排序，让「创世历2年」排在「创世历10年」之前
const sortedItems = computed(() =>
  [...items.value].sort((a, b) => a.date.localeCompare(b.date, 'zh-CN', { numeric: true }))
)

// 按纪元分组（纪元按首次出现顺序排列；空纪元归为一组不显示标题）
const groups = computed(() => {
  const result: { era: string; items: TimelineItem[] }[] = []
  const map = new Map<string, TimelineItem[]>()
  for (const item of sortedItems.value) {
    const era = (item.era || '').trim()
    if (!map.has(era)) {
      map.set(era, [])
      result.push({ era, items: map.get(era)! })
    }
    map.get(era)!.push(item)
  }
  return result
})
const lastGroup = computed(() => groups.value[groups.value.length - 1])

async function load() {
  loading.value = true
  try {
    items.value = await worldApi.getTimeline(props.projectId)
  } catch {
    items.value = []
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(() => props.projectId, load)
</script>

<style scoped>
.timeline-view { padding: 40px 40px 120px; max-width: 720px; margin: 0 auto; }
.tl-heading { margin: 0 0 6px; font-size: 22px; font-weight: 600; }
.tl-sub { margin: 0 0 28px; font-size: 13px; color: var(--text-secondary); }
.tl-empty { color: var(--text-secondary); font-size: 14px; padding: 40px 0; text-align: center; }
.tl-era-title {
  margin: 24px 0 4px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  padding: 6px 12px;
  background: var(--bg-sub);
  border-radius: 8px;
  display: inline-block;
}
.tl-list { list-style: none; margin: 0; padding: 0; }
.tl-item { display: flex; gap: 16px; }
.tl-rail { display: flex; flex-direction: column; align-items: center; width: 20px; flex-shrink: 0; }
.tl-dot { width: 12px; height: 12px; border-radius: 50%; background: var(--accent-color); margin-top: 6px; flex-shrink: 0; }
.tl-dot.type-event { background: #f59e0b; }
.tl-dot.type-location { background: #10b981; }
.tl-dot.type-item { background: #8b5cf6; }
.tl-dot.type-faction { background: #ef4444; }
.tl-dot.type-species { background: #14b8a6; }
.tl-dot.type-ecology { background: #84cc16; }
.tl-dot.type-lore { background: #64748b; }
.tl-line { flex: 1; width: 2px; background: var(--border-line); margin-top: 6px; }
.tl-content { flex: 1; padding-bottom: 28px; min-width: 0; }
.tl-date {
  display: inline-block;
  font-size: 12px;
  font-weight: 600;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 2px 10px;
  border-radius: 10px;
  margin-bottom: 8px;
}
.tl-title { margin: 0 0 6px; font-size: 16px; font-weight: 600; }
.tl-desc { margin: 0 0 8px; font-size: 14px; line-height: 1.7; color: var(--text-secondary); white-space: pre-wrap; }
.tl-source {
  border: none;
  background: none;
  padding: 0;
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
}
.tl-source:hover { color: var(--accent-color); }
@media (max-width: 820px) {
  .timeline-view { padding: 24px 20px 80px; }
}
</style>
