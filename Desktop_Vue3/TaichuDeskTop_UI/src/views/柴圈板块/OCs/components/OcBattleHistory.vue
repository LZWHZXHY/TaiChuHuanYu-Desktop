<!-- OcBattleHistory.vue：OC 详情页的「战斗经历」。
     这个 OC 打过的、已经结束的约战：约战名、对手、结果、战后状态、说明、视频卡片、写进了哪个世界观的编年史。
     顺便把「当前状态」（最近一场约战的战后状态）通过 current 事件交给详情页，显示在名字旁边。
     一场都没打过时整栏不显示。 -->
<template>
  <section v-if="history && history.items.length" class="obh desc-section">
    <h3 class="desc-title">⚔ 战斗经历 <small>{{ history.items.length }} 场</small></h3>
    <ol class="obh-list">
      <li v-for="b in shown" :key="b.battleId" class="obh-item">
        <div class="obh-top">
          <RouterLink :to="`/battles/${b.battleId}`" class="obh-title">《{{ b.title }}》</RouterLink>
          <span class="obh-result" :class="'r-' + (b.result || 'none')">{{ resultLabel(b.result, b.outcome) }}</span>
          <time v-if="b.finishedAt" class="obh-time" :datetime="b.finishedAt">{{ formatDate(b.finishedAt) }}</time>
        </div>
        <p v-if="b.opponents.length" class="obh-vs">对手：{{ b.opponents.map(sideLabel).join('、') }}</p>
        <p v-if="b.teammates.length" class="obh-vs">队友：{{ b.teammates.map(sideLabel).join('、') }}</p>
        <template v-if="b.record">
          <p class="obh-after">
            战后：<span class="obh-status" :class="'tone-' + statusTone(b.record.statusTag)">{{ b.record.statusTag }}</span>
            <span v-if="b.record.statusNote" class="obh-note">{{ b.record.statusNote }}</span>
          </p>
          <VideoLinkCard v-if="b.record.videoUrl || b.record.videoHidden" :url="b.record.videoUrl" :title="b.record.videoTitle" :cover="b.record.videoCover"
            :site-work="b.record.siteWork" :hidden="b.record.videoHidden" compact />
          <p v-if="b.record.projectId && b.record.chronicleEventId" class="obh-world">
            📜 已写进<RouterLink :to="chronicleEventPath(b.record.projectId, b.record.chronicleEventId)">《{{ b.record.projectName || '世界观' }}》的编年史</RouterLink>
          </p>
        </template>
        <p v-else-if="history.isAuthor" class="obh-tip">还没写战后记录，<RouterLink :to="`/battles/${b.battleId}`">去约战页写一条</RouterLink></p>
      </li>
    </ol>
    <button v-if="history.items.length > LIMIT" type="button" class="btn-more" @click="all = !all">
      {{ all ? '收起' : `显示全部 ${history.items.length} 场` }}
    </button>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import VideoLinkCard from '@/components/VideoLinkCard.vue'
import {
  battleAftermathApi, statusTone, resultLabel, sideLabel, chronicleEventPath,
  type OcBattleHistory, type OcCurrentStatus,
} from '@/api/battleAftermath'

const props = defineProps<{ characterId: string }>()
const emit = defineEmits<{ (e: 'current', status: OcCurrentStatus | null): void }>()

const LIMIT = 5
const history = ref<OcBattleHistory | null>(null)
const all = ref(false)
const shown = computed(() => (all.value ? history.value?.items ?? [] : (history.value?.items ?? []).slice(0, LIMIT)))

let version = 0
async function load(id: string) {
  const v = ++version
  try {
    const res = await battleAftermathApi.oc(id)
    if (v !== version) return
    history.value = res
    emit('current', res.current)
  } catch {
    // 读不到就不显示（不影响详情页其他内容）
    if (v !== version) return
    history.value = null
    emit('current', null)
  }
}
watch(() => props.characterId, id => {
  history.value = null
  all.value = false
  emit('current', null)
  if (id) void load(id)
}, { immediate: true })

function formatDate(at: string) {
  const d = new Date(at)
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('zh-CN', { year: 'numeric', month: 'numeric', day: 'numeric' })
}
</script>

<style scoped>
.obh { display: grid; gap: 10px; }
/* 标题和详情页其他栏一个样子：父组件的样式管不到这里面，照抄一份 */
.obh .desc-title { font-size: 15px; font-weight: 400; letter-spacing: 0.2em; margin: 0; color: var(--ink-black, var(--text-primary)); }
.obh .desc-title small { font-size: 12px; letter-spacing: 0; color: var(--text-secondary); }
.obh-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.obh-item { display: grid; gap: 6px; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); min-width: 0; }
.obh-top { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; }
.obh-title { font-size: 14px; color: var(--text-primary); text-decoration: none; overflow-wrap: anywhere; }
.obh-title:hover { color: var(--accent-color); }
.obh-time { margin-left: auto; font-size: 12px; color: var(--text-secondary); }
.obh-result { padding: 0 8px; border: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.obh-result.r-win { border-color: var(--success, #3a8f5c); color: var(--success, #3a8f5c); }
.obh-result.r-lose { border-color: var(--danger, #c0392b); color: var(--danger, #c0392b); }
.obh-result.r-draw { border-color: var(--warning, #c08a1e); color: var(--warning, #c08a1e); }
.obh-vs { margin: 0; font-size: 12px; color: var(--text-secondary); overflow-wrap: anywhere; }
.obh-after { margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); overflow-wrap: anywhere; }
.obh-note { margin-left: 6px; color: var(--text-primary); white-space: pre-wrap; }
.obh-status { padding: 1px 8px; font-size: 12px; font-weight: 600; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-primary); }
.obh-status.tone-ok { color: var(--success, #3a8f5c); border-color: var(--success, #3a8f5c); background: color-mix(in srgb, var(--success, #3a8f5c) 12%, transparent); }
.obh-status.tone-warn { color: var(--warning, #c08a1e); border-color: var(--warning, #c08a1e); background: color-mix(in srgb, var(--warning, #c08a1e) 12%, transparent); }
.obh-status.tone-bad { color: var(--danger, #c0392b); border-color: var(--danger, #c0392b); background: color-mix(in srgb, var(--danger, #c0392b) 12%, transparent); }
.obh-status.tone-dead { color: var(--paper-card); border-color: var(--text-primary); background: var(--text-primary); }
.obh-status.tone-up { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); }
.obh-world, .obh-tip { margin: 0; font-size: 12px; color: var(--text-secondary); }
.obh-world a, .obh-tip a { color: var(--accent-color); text-decoration: none; }
.btn-more { justify-self: center; padding: 4px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.btn-more:hover { border-color: var(--accent-color); color: var(--accent-color); }
</style>
