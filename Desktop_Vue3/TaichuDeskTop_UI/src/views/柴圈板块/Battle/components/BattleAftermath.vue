<!-- BattleAftermath.vue：约战详情页（已结束）里的「战后记录」一栏。
     列出所有参赛 OC 的战后状态、说明、视频卡片，以及「已写进世界观《xxx》」的链接；
     参赛者本人在自己的 OC 上有「写战后记录 / 修改 / 删除」（弹窗表单见 BattleAftermathForm.vue）。
     约战还没结束时什么都不显示。用法：<BattleAftermath :battle-id="battle.id" :status="battle.status" /> -->
<template>
  <section v-if="data && data.finished" class="bam">
    <header class="bam-head">
      <h3>🩹 战后记录</h3>
      <p>这一战之后，各位 OC 怎么样了？参赛者可以给自己的 OC 记下战后状态，配上视频，还能写进世界观的编年史。</p>
    </header>

    <ul class="bam-list">
      <li v-for="it in data.items" :key="it.participantId + it.ocId" class="bam-item" :class="{ empty: !it.record }">
        <div class="bam-who">
          <RouterLink v-if="it.ocExists" :to="`/ocs/${it.ocId}`" class="bam-oc">
            <img v-if="it.ocCover" :src="withThumbnail(it.ocCover, 96) || it.ocCover" alt="" loading="lazy" />
            <span v-else class="bam-oc-ph" aria-hidden="true">{{ it.ocName.slice(0, 1) }}</span>
            <b>{{ it.ocName }}</b>
          </RouterLink>
          <span v-else class="bam-oc"><span class="bam-oc-ph" aria-hidden="true">?</span><b>{{ it.ocName }}</b></span>
          <small class="bam-meta">{{ it.userName }}{{ it.teamName ? ` · ${it.teamName}` : '' }}</small>
          <span class="bam-result" :class="'r-' + (it.result || 'none')">{{ resultLabel(it.result, data.outcome) }}</span>
          <span v-if="it.record" class="bam-status" :class="'tone-' + statusTone(it.record.statusTag)">{{ it.record.statusTag }}</span>
        </div>

        <template v-if="it.record">
          <p v-if="it.record.statusNote" class="bam-note">{{ it.record.statusNote }}</p>
          <VideoLinkCard v-if="it.record.videoUrl || it.record.videoHidden" :url="it.record.videoUrl" :title="it.record.videoTitle" :cover="it.record.videoCover"
            :site-work="it.record.siteWork" :hidden="it.record.videoHidden" />
          <p v-if="it.record.videoPending" class="bam-tip">这个视频是还在审核的约战作品，审核通过前别人看不到。</p>
          <p v-if="it.record.projectId && it.record.chronicleEventId" class="bam-world">
            📜 已写进世界观<RouterLink :to="chronicleEventPath(it.record.projectId, it.record.chronicleEventId)">《{{ it.record.projectName || '世界观' }}》的编年史</RouterLink>
          </p>
        </template>
        <p v-else class="bam-tip">{{ it.canEdit ? '还没写战后记录。' : '还没有战后记录。' }}</p>

        <div v-if="it.canEdit" class="bam-ops">
          <button type="button" class="btn-line" :disabled="busy" @click="openForm(it)">{{ it.record ? '✎ 修改' : '＋ 写战后记录' }}</button>
          <button v-if="it.record" type="button" class="bam-link" :disabled="busy" @click="remove(it)">删除</button>
        </div>
      </li>
    </ul>

    <BattleAftermathForm v-if="editing" :key="editing.participantId + editing.ocId" v-model:open="formOpen" :battle-id="battleId" :item="editing"
      :default-video="data.myDefaultVideo" :default-title="defaultTitle" @saved="load" />
  </section>
  <p v-else-if="loadError && finishedHint" class="bam-tip" role="alert">战后记录暂时读不出来。<button type="button" class="bam-link" @click="load">重新加载</button></p>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import VideoLinkCard from '@/components/VideoLinkCard.vue'
import BattleAftermathForm from './BattleAftermathForm.vue'
import {
  battleAftermathApi, statusTone, resultLabel, chronicleEventPath,
  type BattleAftermathData, type BattleAftermathItem,
} from '@/api/battleAftermath'
import { getErrorMessage } from '@/utils/apiError'
import { withThumbnail } from '@/utils/imageThumb'

const props = defineProps<{
  battleId: string
  /** 约战状态：变成 finished（刚公布结果）时自动重新读一次 */
  status?: string
}>()

const data = ref<BattleAftermathData | null>(null)
const loadError = ref(false)
const busy = ref(false)
const finishedHint = computed(() => !props.status || props.status === 'finished')

let version = 0
async function load() {
  const v = ++version
  loadError.value = false
  try {
    const res = await battleAftermathApi.battle(props.battleId)
    if (v === version) data.value = res
  } catch {
    // 读不到就不显示这一栏（不影响约战页其他内容）
    if (v === version) { data.value = null; loadError.value = true }
  }
}
watch(() => [props.battleId, props.status] as const, ([id, status]) => {
  // 还没结束的约战不用读（父组件不传 status 时照常读，由后端说结束了没有）
  if (id && (!status || status === 'finished')) void load()
  else data.value = null
}, { immediate: true })

/** 编年史事件默认标题：约战《xxx》：A vs B（同队的用顿号连起来） */
const defaultTitle = computed(() => {
  const d = data.value
  if (!d) return ''
  const sides = new Map<string, string[]>()
  for (const it of d.items) {
    const key = it.teamNumber != null ? `t${it.teamNumber}` : `p${it.participantId}`
    if (!sides.has(key)) sides.set(key, [])
    sides.get(key)!.push(it.ocName)
  }
  const names = [...sides.values()].map(list => list.join('、'))
  return names.length >= 2 ? `约战《${d.title}》：${names.join(' vs ')}` : `约战《${d.title}》`
})

// ---------- 写 / 改 ----------
const editing = ref<BattleAftermathItem | null>(null)
const formOpen = ref(false)
function openForm(it: BattleAftermathItem) { editing.value = it; formOpen.value = true }

async function remove(it: BattleAftermathItem) {
  const inChronicle = Boolean(it.record?.chronicleEventId)
  try {
    await ElMessageBox.confirm(
      `删除「${it.ocName}」这一战的战后记录？${inChronicle ? '已经写进编年史的那件事不会跟着删，不需要的话请到编年史里删除。' : ''}`,
      '删除战后记录', { confirmButtonText: '删除', cancelButtonText: '不了', type: 'warning' })
  } catch { return }
  busy.value = true
  try {
    const res = await battleAftermathApi.remove(props.battleId, it.ocId)
    ElMessage.success(res.message)
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e, '删除失败'))
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.bam { display: grid; gap: 12px; }
/* 标题和约战详情页其它区块一致：左边一道竖线 */
.bam-head h3 { margin: 0 0 6px; padding-left: 10px; border-left: 3px solid var(--text-primary); font-size: 17px; font-weight: 600; line-height: 1.3; color: var(--text-primary); }
.bam-head p { margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.bam-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.bam-item { display: grid; gap: 8px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); min-width: 0; }
.bam-item.empty { border-left-color: var(--border-line); }
.bam-who { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-width: 0; }
.bam-oc { display: inline-flex; align-items: center; gap: 8px; min-width: 0; color: var(--text-primary); text-decoration: none; }
a.bam-oc:hover b { color: var(--accent-color); }
.bam-oc img, .bam-oc-ph { width: 32px; height: 32px; flex-shrink: 0; object-fit: cover; }
.bam-oc-ph { display: grid; place-items: center; background: var(--accent-soft); color: var(--accent-color); font-size: 14px; }
.bam-oc b { font-size: 14px; overflow-wrap: anywhere; }
.bam-meta { font-size: 12px; color: var(--text-secondary); }
.bam-result { padding: 1px 8px; border: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.bam-result.r-win { border-color: var(--success, #3a8f5c); color: var(--success, #3a8f5c); }
.bam-result.r-lose { border-color: var(--danger, #c0392b); color: var(--danger, #c0392b); }
/* 平局用蓝色，和详情页参战名单、结果横幅的平手颜色一致 */
.bam-result.r-draw { border-color: var(--info, #286390); color: var(--info, #286390); }
/* 战后状态标签：颜色跟着状态走（伤得越重越红，阵亡黑底，觉醒 / 突破用主题色） */
.bam-status { padding: 1px 8px; font-size: 12px; font-weight: 600; background: var(--bg-sub); color: var(--text-primary); border: 1px solid var(--border-line); }
.bam-status.tone-ok { color: var(--success, #3a8f5c); border-color: var(--success, #3a8f5c); background: color-mix(in srgb, var(--success, #3a8f5c) 12%, transparent); }
.bam-status.tone-warn { color: var(--warning, #c08a1e); border-color: var(--warning, #c08a1e); background: color-mix(in srgb, var(--warning, #c08a1e) 12%, transparent); }
.bam-status.tone-bad { color: var(--danger, #c0392b); border-color: var(--danger, #c0392b); background: color-mix(in srgb, var(--danger, #c0392b) 12%, transparent); }
.bam-status.tone-dead { color: var(--paper-card); border-color: var(--text-primary); background: var(--text-primary); }
.bam-status.tone-up { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); }
.bam-note { margin: 0; font-size: 14px; line-height: 1.75; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
.bam-tip { margin: 0; font-size: 12px; color: var(--text-secondary); }
.bam-world { margin: 0; font-size: 13px; color: var(--text-secondary); }
.bam-world a { color: var(--accent-color); text-decoration: none; }
.bam-world a:hover { text-decoration: underline; }
.bam-ops { display: flex; align-items: center; gap: 14px; }
.bam-ops .btn-line { padding: 4px 14px; font-size: 13px; }
.bam-link { min-height: 0 !important; border: 0; padding: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.bam-link:hover { color: var(--danger, #c0392b); }
.bam-link:disabled { opacity: .5; cursor: not-allowed; }
</style>
