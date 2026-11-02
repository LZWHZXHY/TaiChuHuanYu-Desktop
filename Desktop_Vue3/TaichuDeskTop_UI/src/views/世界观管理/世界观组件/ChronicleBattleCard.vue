<!-- ChronicleBattleCard.vue：编年史事件详情里的「约战记录」卡片。
     事件是从柴圈约战的战后记录写进来的（battle_aftermath 里记着事件 id）才显示：
     约战名（链到约战页）、出战的 OC（链到 OC 页）和结果、战后状态、说明、视频卡片。
     编年史事件本身没有视频字段，所以视频在这里单独显示；不是约战写进来的事件什么都不显示。 -->
<template>
  <div v-if="items.length" class="cbc">
    <h4>⚔ 约战记录</h4>
    <article v-for="it in items" :key="it.battleId + it.ocId" class="cbc-item">
      <RouterLink :to="`/battles/${it.battleId}`" class="cbc-battle">约战《{{ it.battleTitle }}》<small>{{ outcomeLabel(it.outcome) }}</small></RouterLink>
      <p class="cbc-who">
        <RouterLink v-if="it.ocExists" :to="`/ocs/${it.ocId}`" class="cbc-oc">
          <img v-if="it.ocCover" :src="withThumbnail(it.ocCover, 64) || it.ocCover" alt="" loading="lazy" />{{ it.ocName }}
        </RouterLink>
        <span v-else>{{ it.ocName }}</span>
        <span class="cbc-result" :class="'r-' + (it.result || 'none')">{{ resultLabel(it.result, it.outcome) }}</span>
        <span class="cbc-status" :class="'tone-' + statusTone(it.record.statusTag)">{{ it.record.statusTag }}</span>
      </p>
      <p v-if="it.opponents.length" class="cbc-vs">对手：{{ it.opponents.map(sideLabel).join('、') }}</p>
      <p v-if="it.record.statusNote" class="cbc-note">{{ it.record.statusNote }}</p>
      <VideoLinkCard v-if="it.record.videoUrl || it.record.videoHidden" :url="it.record.videoUrl" :title="it.record.videoTitle" :cover="it.record.videoCover"
        :site-work="it.record.siteWork" :hidden="it.record.videoHidden" compact />
    </article>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import VideoLinkCard from '@/components/VideoLinkCard.vue'
import { battleAftermathApi, statusTone, resultLabel, outcomeLabel, sideLabel, type EventBattleItem } from '@/api/battleAftermath'
import { withThumbnail } from '@/utils/imageThumb'

const props = defineProps<{ eventId: string }>()
const items = ref<EventBattleItem[]>([])

let version = 0
watch(() => props.eventId, async id => {
  const v = ++version
  items.value = []
  if (!id) return
  try {
    const res = await battleAftermathApi.event(id)
    if (v === version) items.value = res.items
  } catch { /* 读不到就不显示这张卡片，编年史照常 */ }
}, { immediate: true })
</script>

<style scoped>
.cbc { margin-top: 16px; display: grid; gap: 8px; }
.cbc h4 { margin: 0; font-size: 13px; font-weight: 600; color: var(--text-primary); }
.cbc-item { display: grid; gap: 6px; padding: 10px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-sub); min-width: 0; }
.cbc-battle { font-size: 13px; font-weight: 600; color: var(--text-primary); text-decoration: none; overflow-wrap: anywhere; }
.cbc-battle small { margin-left: 6px; font-weight: 400; color: var(--text-secondary); }
.cbc-battle:hover { color: var(--accent-color); }
.cbc-who { margin: 0; display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 13px; }
.cbc-oc { display: inline-flex; align-items: center; gap: 6px; color: var(--text-primary); text-decoration: none; }
.cbc-oc:hover { color: var(--accent-color); }
.cbc-oc img { width: 22px; height: 22px; object-fit: cover; }
.cbc-result { padding: 0 6px; border: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.cbc-result.r-win { border-color: var(--success, #3a8f5c); color: var(--success, #3a8f5c); }
.cbc-result.r-lose { border-color: var(--danger, #c0392b); color: var(--danger, #c0392b); }
.cbc-result.r-draw { border-color: var(--warning, #c08a1e); color: var(--warning, #c08a1e); }
.cbc-status { padding: 0 6px; font-size: 12px; font-weight: 600; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); }
.cbc-status.tone-ok { color: var(--success, #3a8f5c); border-color: var(--success, #3a8f5c); }
.cbc-status.tone-warn { color: var(--warning, #c08a1e); border-color: var(--warning, #c08a1e); }
.cbc-status.tone-bad { color: var(--danger, #c0392b); border-color: var(--danger, #c0392b); }
.cbc-status.tone-dead { color: var(--paper-card); border-color: var(--text-primary); background: var(--text-primary); }
.cbc-status.tone-up { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); }
.cbc-vs { margin: 0; font-size: 12px; color: var(--text-secondary); overflow-wrap: anywhere; }
.cbc-note { margin: 0; font-size: 13px; line-height: 1.7; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
</style>
