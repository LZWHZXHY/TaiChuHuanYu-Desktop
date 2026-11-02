<!-- 约战详情里的「出战人设」：每一方派出的 OC 都列出来（封面、简介、战绩、主要设定），可以展开看完整设定、点进人设页。
     以前约战页只写了 OC 的名字，路人不知道双方是什么角色，讨论区就没法讨论。
     约战面板：每个 OC 显示自己的「约战面板」小雷达图（只用官方约战模板那份，不显示自由设计的展示面板）；
     双方都有人填了约战面板时，上面再画一张叠加对比图（每方一种颜色，图例写 OC 名字）。 -->
<template>
  <div class="boc">
    <h3>🎭 出战人设</h3>
    <!-- 双方约战面板叠加对比（放在 v-if / v-else 那一对的前面，不能插在中间） -->
    <section v-if="compare" class="boc-compare">
      <header><b>⚔ 约战面板对比</b><small>官方约战模板 · 悬停维度名可以看双方的等级</small></header>
      <StatPanelChart :panel="compare.axes" :series="compare.series" hide-title />
    </section>
    <p v-if="!sides.length" class="boc-empty">还没有人报名</p>
    <div v-else class="boc-sides" :class="{ duel: sides.length === 2 }">
      <section v-for="side in sides" :key="side.participantId" class="boc-side" :class="side.color === 0 ? 'a' : 'b'">
        <header class="boc-side-head">
          <span class="boc-role">{{ side.role }}</span>
          <UserLink :id="side.userId" :name="side.userName"><b>{{ side.userName }}</b></UserLink>
        </header>
        <p v-if="!side.ocIds.length" class="boc-empty">没有选 OC</p>
        <article v-for="id in side.ocIds" :key="id" class="boc-card">
          <template v-if="ocs[id]">
            <div class="boc-top">
              <img v-if="ocs[id]!.coverUrl" :src="ocs[id]!.coverUrl" alt="" class="boc-cover" loading="lazy" />
              <span v-else class="boc-cover boc-cover-empty" aria-hidden="true">{{ ocs[id]!.title.slice(0, 1) }}</span>
              <div class="boc-main">
                <RouterLink :to="`/ocs/${id}`" class="boc-name">{{ ocs[id]!.title }}</RouterLink>
                <small class="boc-record">{{ ocs[id]!.battleTier || '新人' }} · {{ ocs[id]!.battleWins }} 胜 {{ ocs[id]!.battleLosses }} 负 {{ ocs[id]!.battleDraws }} 平</small>
                <p v-if="ocs[id]!.description" class="boc-desc">{{ ocs[id]!.description }}</p>
              </div>
            </div>
            <!-- 约战面板（官方模板）：小雷达图；没填就提示一句，作者自己看到去填写的链接 -->
            <div v-if="template" class="boc-panel">
              <StatPanelChart v-if="battleFilled(template, battles[id])" :panel="battleToStatPanel(template, battles[id])" size="mini" :color="sideColor(side.color)" hide-title />
              <p v-else class="boc-nopanel">还没填约战面板<RouterLink v-if="isMine(id)" :to="`/ocs/edit/${id}#battle-panel`">去填写 →</RouterLink></p>
            </div>
            <!-- 短设定（性别、年龄、能力……）直接显示成小标签 -->
            <ul v-if="shortAttrs(id).length" class="boc-tags">
              <li v-for="a in shortAttrs(id)" :key="a.id"><span>{{ a.key }}</span>{{ a.value }}</li>
            </ul>
            <!-- 长设定（背景故事、能力详解……）默认收起 -->
            <template v-if="longAttrs(id).length">
              <button type="button" class="boc-more" :aria-expanded="open.has(id)" @click="toggle(id)">
                {{ open.has(id) ? '收起完整设定 ▴' : `展开完整设定（${longAttrs(id).length} 项）▾` }}
              </button>
              <dl v-if="open.has(id)" class="boc-long">
                <template v-for="a in longAttrs(id)" :key="a.id">
                  <dt>{{ a.key }}</dt>
                  <dd>{{ a.value }}</dd>
                </template>
              </dl>
            </template>
          </template>
          <p v-else-if="failed.has(id)" class="boc-missing">「{{ nameOf(side, id) }}」的人设看不到了（可能还是草稿或已删除）</p>
          <p v-else class="boc-missing">正在加载「{{ nameOf(side, id) }}」…</p>
        </article>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import request from '@/utils/request'
import UserLink from '@/components/UserLink.vue'
import StatPanelChart from '@/components/StatPanelChart.vue'
import { useUserStore } from '@/stores/user'
import type { ChartSeries } from '@/utils/statPanel'
import type { StickmanCharacter, StickmanAttribute } from '../../OCs/stickman'
import { battleFilled, battleToStatPanel, ocPanelApi, type BattlePanelData, type BattlePanelTemplate } from '../../OCs/ocPanel'

interface Participant { id: string; userId: string; userName: string; ocIds?: string[]; ocNames?: string[]; teamNumber?: number }
const props = defineProps<{ participants: Participant[] }>()
// 读到的 OC（名字、封面）交给详情页顶部的对阵图用，省得那边再请求一遍
const emit = defineEmits<{ (e: 'loaded', ocs: Record<string, { title?: string; coverUrl?: string } | undefined>): void }>()

const sides = computed(() => (props.participants ?? []).map((p, i) => ({
  // 颜色和顶部对阵图一致：团队战按队伍（A 队主题色、B 队橙色），否则发起方主题色、其他人依次往后排
  color: p.teamNumber === 1 || p.teamNumber === 2 ? p.teamNumber - 1 : i,
  participantId: p.id,
  userId: p.userId,
  userName: p.userName,
  // 叫法和顶部对阵图一致：单挑叫发起方 / 应战方，多人分队才叫 A 队 / B 队
  role: p.teamNumber && props.participants.length > 2 ? `${p.teamNumber === 1 ? 'A' : 'B'} 队` : i === 0 ? '发起方' : `应战方${props.participants.length > 2 ? ` ${i}` : ''}`,
  ocIds: p.ocIds ?? [],
  ocNames: p.ocNames ?? [],
})))
const nameOf = (side: { ocIds: string[]; ocNames: string[] }, id: string) => side.ocNames[side.ocIds.indexOf(id)] || 'OC'

const ocs = reactive<Record<string, StickmanCharacter | undefined>>({})
const failed = ref(new Set<string>())
const open = ref(new Set<string>())
function toggle(id: string) { const s = new Set(open.value); if (s.has(id)) s.delete(id); else s.add(id); open.value = s }

const sorted = (id: string): StickmanAttribute[] => [...(ocs[id]?.attributes ?? [])].filter(a => (a.value ?? '').trim()).sort((a, b) => a.sortOrder - b.sortOrder)
// 短文本但内容很长的，也当长设定收起来
const shortAttrs = (id: string) => sorted(id).filter(a => a.type !== 'long' && (a.value ?? '').length <= 40).slice(0, 12)
const longAttrs = (id: string) => sorted(id).filter(a => a.type === 'long' || (a.value ?? '').length > 40)

async function load() {
  const ids = [...new Set(sides.value.flatMap(s => s.ocIds))].filter(id => !ocs[id] && !failed.value.has(id))
  await Promise.all(ids.map(async id => {
    try { ocs[id] = await request.get<StickmanCharacter>(`/StickMan/${id}`, { silentError: true }) }
    catch { failed.value = new Set([...failed.value, id]) }
  }))
  emit('loaded', Object.fromEntries(Object.entries(ocs).map(([id, oc]) => [id, oc ? { title: oc.title, coverUrl: oc.coverUrl } : undefined])))
}
watch(() => sides.value.flatMap(s => s.ocIds).join(','), () => { void load(); void loadPanels() }, { immediate: true })

// ===== 约战面板 =====
const userStore = useUserStore()
const template = ref<BattlePanelTemplate | null>(null)
const battles = reactive<Record<string, BattlePanelData | undefined>>({})
/** 每一方的颜色：发起方主题色，应战方橙色（和上面卡片顶边的颜色一致），多方约战再往后排 */
const SIDE_COLORS = ['var(--accent-color)', '#d97706', '#7a5cc4', '#5b8c3a', '#c46a9a']
const sideColor = (si: number) => SIDE_COLORS[si % SIDE_COLORS.length]
// 身份判断和约战详情页一样用 userStore.userId，不分大小写比较
const isMine = (id: string) => Boolean(ocs[id]?.authorId && userStore.userId && String(ocs[id]!.authorId).toLowerCase() === String(userStore.userId).toLowerCase())
async function loadPanels() {
  const ids = [...new Set(sides.value.flatMap(s => s.ocIds))]
  if (!ids.length) return
  // 读不到就不显示面板，不影响人设卡片本身
  try {
    const [t, res] = await Promise.all([ocPanelApi.template(), ocPanelApi.battlePanels(ids)])
    template.value = t
    for (const id of ids) battles[id] = res.items[id]
  } catch { /* 静默 */ }
}
/** 叠加对比：至少两方各有一个填了约战面板的 OC 才画；同一方的第二个 OC 起用虚线 */
const compare = computed(() => {
  const t = template.value
  if (!t) return null
  const series: ChartSeries[] = []
  const sidesWithPanel = new Set<number>()
  sides.value.forEach((side) => {
    side.ocIds.filter(id => battleFilled(t, battles[id])).forEach((id, k) => {
      sidesWithPanel.add(side.color)
      series.push({ name: `${ocs[id]?.title || nameOf(side, id)}（${side.role}）`, color: sideColor(side.color), panel: battleToStatPanel(t, battles[id]), dashed: k > 0 })
    })
  })
  return sidesWithPanel.size >= 2 ? { axes: battleToStatPanel(t, null, '约战面板对比'), series } : null
})
</script>

<style scoped>
/* 标题样式和约战详情页的其它区块一致（左边一道竖线） */
.boc h3 { margin: 0 0 12px; padding-left: 10px; border-left: 3px solid var(--text-primary); font-size: 17px; font-weight: 600; line-height: 1.3; color: var(--text-primary); }
.boc-empty { color: var(--text-secondary); font-size: 13px; margin: 0; }
.boc-sides { display: grid; gap: 14px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 300px), 1fr)); }
.boc-side { display: grid; gap: 10px; align-content: start; padding: 12px 14px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); min-width: 0; }
.boc-side.b { border-top-color: #d97706; }
.boc-side-head { display: flex; align-items: baseline; gap: 8px; font-size: 14px; }
.boc-role { padding: 1px 6px; font-size: 12px; font-weight: 600; color: var(--paper-card); background: var(--accent-color); }
.boc-side.b .boc-role { background: #d97706; color: #1f1300; }
.boc-card { display: grid; gap: 8px; padding-top: 10px; border-top: 1px dashed var(--border-line); min-width: 0; }
.boc-top { display: flex; gap: 12px; align-items: flex-start; }
.boc-cover { width: 76px; height: 76px; object-fit: cover; flex-shrink: 0; border: 1px solid var(--border-line); }
.boc-cover-empty { display: grid; place-items: center; background: var(--bg-sub); color: var(--text-secondary); font: 700 28px/1 var(--font-display); }
.boc-main { min-width: 0; display: grid; gap: 3px; }
.boc-name { color: var(--text-primary); font: 600 18px/1.3 var(--font-display); text-decoration: none; overflow-wrap: anywhere; }
.boc-name:hover { color: var(--accent-color); text-decoration: underline; }
.boc-record { color: var(--text-secondary); font-size: 12px; font-variant-numeric: tabular-nums; }
.boc-desc { margin: 2px 0 0; color: var(--text-secondary); font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
.boc-tags { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; }
.boc-tags li { padding: 2px 8px; background: var(--bg-sub); font-size: 12px; color: var(--text-primary); overflow-wrap: anywhere; }
.boc-tags li span { margin-right: 4px; color: var(--text-secondary); }
.boc-tags li span::after { content: '：'; }
.boc-more { justify-self: start; min-height: 0 !important; padding: 4px 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.boc-long { margin: 0; display: grid; gap: 4px; }
.boc-long dt { font-size: 13px; font-weight: 600; color: var(--text-primary); }
.boc-long dd { margin: 0 0 8px; font-size: 13px; line-height: 1.8; color: var(--text-secondary); white-space: pre-wrap; overflow-wrap: anywhere; }
.boc-missing { margin: 0; color: var(--text-secondary); font-size: 13px; }
.boc-compare { display: grid; gap: 8px; margin-bottom: 16px; padding: 12px; border: 1px solid var(--border-line); background: var(--paper-card); box-shadow: inset 0 3px 0 var(--caution); }
.boc-compare header { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 10px; font-size: 14px; color: var(--text-primary); }
.boc-compare header small { font-size: 12px; color: var(--text-secondary); }
.boc-panel { max-width: 260px; }
.boc-nopanel { margin: 0; padding: 6px 8px; border: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 12px; }
.boc-nopanel a { margin-left: 8px; color: var(--accent-color); text-decoration: none; }
</style>
