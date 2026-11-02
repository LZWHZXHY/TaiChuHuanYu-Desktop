<!-- OcPanelsView.vue：OC 详情页里的「能力面板」：展示面板（自由设计）和约战面板（官方约战模板）各一张雷达图。
     两份都没有时：访客什么都看不到；作者看到「添加展示面板 / 填写约战面板」的入口（跳到编辑页对应位置）。 -->
<template>
  <div v-if="hasShow || hasBattle || canEdit" class="ocv">
    <section v-if="hasShow" class="ocv-card">
      <StatPanelChart :panel="panels!.show!" />
    </section>
    <section v-if="hasBattle && template" class="ocv-card">
      <!-- 标题写明是官方模板，和上面自由设计的展示面板区分开 -->
      <StatPanelChart :panel="battlePanel!" />
    </section>
    <div v-if="canEdit && (!hasShow || !hasBattle)" class="ocv-add">
      <RouterLink v-if="!hasShow" :to="`/ocs/edit/${characterId}#panel`">＋ 添加能力面板</RouterLink>
      <RouterLink v-if="!hasBattle" :to="`/ocs/edit/${characterId}#battle-panel`">＋ 填写约战面板</RouterLink>
      <small>（约战面板是参加柴圈约战用的官方模板，方便和对手比较）</small>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import StatPanelChart from '@/components/StatPanelChart.vue'
import { ocPanelApi, battleFilled, battleToStatPanel, type BattlePanelTemplate, type OcPanels } from '../ocPanel'
import { cleanStatPanel } from '@/utils/statPanel'

const props = defineProps<{ characterId: string }>()

const panels = ref<OcPanels | null>(null)
const template = ref<BattlePanelTemplate | null>(null)
let version = 0
watch(() => props.characterId, async id => {
  const v = ++version
  panels.value = null
  if (!id) return
  // 读不到就当没有面板（不影响详情页其他内容）
  const [p, t] = await Promise.allSettled([ocPanelApi.get(id), ocPanelApi.template()])
  if (v !== version) return
  if (p.status === 'fulfilled') panels.value = { ...p.value, show: p.value.show ? cleanStatPanel(p.value.show) : null }
  if (t.status === 'fulfilled') template.value = t.value
}, { immediate: true })

const hasShow = computed(() => Boolean(panels.value?.show))
const hasBattle = computed(() => Boolean(template.value && battleFilled(template.value, panels.value?.battle)))
const battlePanel = computed(() => template.value ? battleToStatPanel(template.value, panels.value?.battle, `⚔ ${template.value.title}（官方模板）`) : null)
// 作者才显示入口：以接口返回的 canEdit 为准（没读到就不显示）
const canEdit = computed(() => Boolean(panels.value?.canEdit))
</script>

<style scoped>
.ocv { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; }
.ocv-card { min-width: 0; padding: 14px; border: 1px solid var(--line-raw, var(--border-line)); background: var(--paper-card); }
.ocv-add { grid-column: 1 / -1; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; padding: 12px 14px; border: 1px dashed var(--border-line); font-size: 13px; }
.ocv-add a { color: var(--accent-color); text-decoration: none; }
.ocv-add a:hover { text-decoration: underline; }
.ocv-add small { color: var(--text-secondary); }
</style>
