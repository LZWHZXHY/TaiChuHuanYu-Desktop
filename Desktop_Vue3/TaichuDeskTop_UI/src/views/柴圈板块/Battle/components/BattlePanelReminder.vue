<!-- BattlePanelReminder.vue：发起 / 报名约战选 OC 时的小提醒：「所选的 OC 还没填约战面板」+ 去填写的链接。
     只是提醒，不拦着报名（是否强制还在和站长确认，后端报名接口也没改）。
     own = 这些 OC 是当前用户自己的：显示「去填写」链接（新标签页打开，正在填的约战表单不会丢），名字从「我的 OC」里取；
     不是自己的（比如对手的 OC）：只提示，名字用 names 传进来的。 -->
<template>
  <p v-if="missing.length" class="bpr" role="note">
    <span>⚠ {{ missing.map(m => `「${m.name}」`).join('') }}还没填约战面板（官方约战模板，方便大家对比、讨论）。</span>
    <template v-if="own">
      <a v-for="m in missing" :key="m.id" :href="router.resolve(`/ocs/edit/${m.id}#battle-panel`).href" target="_blank" rel="noopener">去填写「{{ m.name }}」↗</a>
    </template>
  </p>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { stickmanApi } from '../../OCs/stickman'
import { battleFilled, ocPanelApi, type BattlePanelData, type BattlePanelTemplate } from '../../OCs/ocPanel'

const props = withDefaults(defineProps<{ ocIds: string[]; names?: string[]; own?: boolean }>(), { names: () => [], own: false })
const router = useRouter()

const template = ref<BattlePanelTemplate | null>(null)
const panels = ref<Record<string, BattlePanelData>>({})
const loadedIds = ref<string[]>([])
const myTitles = ref<Record<string, string>>({})

let version = 0
watch(() => props.ocIds.join(','), async () => {
  const v = ++version
  const ids = [...props.ocIds]
  if (!ids.length) { loadedIds.value = []; return }
  try {
    const [t, res] = await Promise.all([ocPanelApi.template(), ocPanelApi.battlePanels(ids)])
    if (v !== version) return
    template.value = t
    panels.value = res.items
    loadedIds.value = ids
  } catch {
    // 读不到就不提醒（不能因为提醒读失败影响报名）
    if (v === version) loadedIds.value = []
  }
  // 自己的 OC：名字从「我的 OC」里取（只读一次）
  if (props.own && !Object.keys(myTitles.value).length) {
    try {
      const mine = await stickmanApi.getMyCharacters()
      myTitles.value = Object.fromEntries(mine.map(c => [c.id, c.title]))
    } catch { /* 拿不到名字就用「OC」代替 */ }
  }
}, { immediate: true })

const missing = computed(() => {
  const t = template.value
  if (!t) return []
  return loadedIds.value.filter(id => !battleFilled(t, panels.value[id])).map(id => ({
    id,
    name: myTitles.value[id] || props.names[props.ocIds.indexOf(id)] || 'OC',
  }))
})
</script>

<style scoped>
/* flex-basis：放在按钮那一行（flex 容器）里时自己占一整行 */
/* 和约战页其它提示条一致：警示色左边线 + 浅黄底 */
.bpr { flex-basis: 100%; margin: 6px 0 0; padding: 6px 10px; border-left: 3px solid var(--warning, #e0a526); background: var(--warning-soft, var(--bg-sub)); color: var(--text-primary); font-size: 12px; line-height: 1.7; display: flex; flex-wrap: wrap; gap: 2px 12px; }
.bpr a { color: var(--accent-color); text-decoration: none; white-space: nowrap; }
.bpr a:hover { text-decoration: underline; }
</style>
