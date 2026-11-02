<!-- 赞助福利组件.vue：管理面板 →「赞助福利」（只有超级管理员能看到、能改）。
     这里写的内容会显示在赞助墙上：顶部说明、每个等级的价格和介绍、每条福利从哪个等级开始有。
     · 带「⚙ 实际权限」标记的福利，是网站真的在判断的特权（比如开发进度详情、生活管理）：
       改它的等级会马上改变谁能用；不能删，只能「不公开」（不公开照样生效）。
     · 其它福利只是展示用的文字（比如「专属头衔」），网站不会自动判断，需要你自己兑现。 -->
<template>
  <div class="spa">
    <header class="spa-header">
      <div>
        <h1>赞助福利</h1>
        <p>这里写的内容会显示在赞助墙上。等级高的自动包含等级低的全部福利，所以每条福利只要选「从哪个等级开始有」。</p>
      </div>
      <div class="spa-actions">
        <RouterLink class="btn-line" to="/sponsors">查看赞助墙</RouterLink>
        <button type="button" class="btn-line" :disabled="saving || !dirty" @click="reset">撤销修改</button>
        <button type="button" class="btn-line btn-primary" :disabled="saving || !dirty" @click="save">{{ saving ? '保存中…' : dirty ? '保存' : '已保存' }}</button>
      </div>
    </header>
    <p class="spa-safe">带「⚙ 实际权限」的福利是网站真正在判断的：改它的等级，保存后马上改变谁能用（比如把「开发进度详情」改成大超凡者，超凡者就看不了了）。这类福利不能删，只能设为不公开，不公开照样生效。</p>
    <p v-if="message" class="spa-message" :class="{ error: messageIsError }" role="status">{{ message }}</p>
    <p v-if="loading" class="spa-state" role="status">正在读取…</p>
    <p v-else-if="loadError" class="spa-state" role="alert">{{ loadError }} <button type="button" class="btn-line" @click="load">重试</button></p>

    <fieldset v-else class="spa-form" :disabled="saving">
      <label class="spa-field">
        <span>赞助墙顶部说明（{{ draft.intro.length }}/500）</span>
        <textarea v-model="draft.intro" rows="3" maxlength="500" placeholder="怎么赞助、赞助后怎么开通……"></textarea>
      </label>

      <div class="spa-field">
        <span>各等级介绍</span>
        <div class="spa-tiers">
          <div v-for="t in draft.tiers" :key="t.rank" class="spa-tier">
            <b>{{ tierName(t.rank) }}</b>
            <input v-model="t.price" maxlength="30" placeholder="价格说明，比如：每月 ¥10" :aria-label="`${tierName(t.rank)}的价格说明`" />
            <textarea v-model="t.blurb" rows="2" maxlength="200" placeholder="一句话介绍（可以不写）" :aria-label="`${tierName(t.rank)}的介绍`"></textarea>
          </div>
        </div>
      </div>

      <div class="spa-field">
        <span>福利（{{ draft.perks.length }} 条，按顺序显示）</span>
        <ol class="spa-perks">
          <li v-for="(p, i) in draft.perks" :key="p.uid" class="spa-perk" :class="{ hidden: !p.visible }">
            <div class="spa-perk-main">
              <div class="spa-perk-row">
                <input v-model="p.title" maxlength="30" placeholder="福利名字" class="spa-title" :aria-label="`第 ${i + 1} 条福利的名字`" />
                <select v-model.number="p.minTier" :aria-label="`${p.title || '这条福利'}从哪个等级开始有`">
                  <option v-for="r in 4" :key="r" :value="r">{{ tierName(r) }} 起</option>
                </select>
                <label class="spa-check"><input v-model="p.visible" type="checkbox" /> 公开</label>
              </div>
              <textarea v-model="p.description" rows="2" maxlength="300" placeholder="说明（可以不写）" :aria-label="`${p.title || '这条福利'}的说明`"></textarea>
              <small v-if="p.key" class="spa-key" :title="`特权标识：${p.key}`">⚙ 实际权限 · 默认 {{ tierName(defaultTier(p.key)) }} 起<template v-if="p.minTier !== defaultTier(p.key)">（已改成 {{ tierName(p.minTier) }} 起）</template></small>
            </div>
            <div class="spa-ops">
              <button type="button" :disabled="i === 0" aria-label="上移" @click="move(i, -1)">↑</button>
              <button type="button" :disabled="i === draft.perks.length - 1" aria-label="下移" @click="move(i, 1)">↓</button>
              <button type="button" class="del" :disabled="!!p.key" :title="p.key ? '实际权限不能删，可以取消「公开」' : '删除这条'" @click="remove(i)">删除</button>
            </div>
          </li>
        </ol>
        <button type="button" class="btn-line spa-add" :disabled="draft.perks.length >= 100" @click="add">+ 加一条福利</button>
      </div>
    </fieldset>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { sponsorPerksApi, type PerkCatalog, type PerkCatalogPayload } from '@/api/sponsorPerks'
import { getErrorMessage } from '@/utils/apiError'

const TIER_NAMES = ['觉醒者', '超凡者', '大超凡者', '君者']
const tierName = (r: number) => TIER_NAMES[r - 1] ?? `等级${r}`

interface DraftPerk { uid: number; id: string; key: string | null; title: string; description: string; minTier: number; visible: boolean }
interface Draft { intro: string; tiers: { rank: number; price: string; blurb: string }[]; perks: DraftPerk[] }

let uidSeq = 0
const saved = ref<PerkCatalog | null>(null)
const draft = ref<Draft>({ intro: '', tiers: [], perks: [] })
const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const message = ref('')
const messageIsError = ref(false)

function toDraft(c: PerkCatalog): Draft {
  return {
    intro: c.intro,
    tiers: [1, 2, 3, 4].map(r => { const t = c.tiers.find(x => x.rank === r); return { rank: r, price: t?.price ?? '', blurb: t?.blurb ?? '' } }),
    perks: c.perks.map(p => ({ uid: ++uidSeq, id: p.id, key: p.key, title: p.title, description: p.description, minTier: p.minTier, visible: p.visible })),
  }
}
const toPayload = (d: Draft): PerkCatalogPayload => ({
  intro: d.intro.trim(),
  tiers: d.tiers.map(t => ({ rank: t.rank, price: t.price.trim(), blurb: t.blurb.trim() })),
  perks: d.perks.map(p => ({ id: p.id, key: p.key, title: p.title.trim(), description: p.description.trim(), minTier: p.minTier, visible: p.visible })),
})
const dirty = computed(() => !!saved.value && JSON.stringify(toPayload(draft.value)) !== JSON.stringify(toPayload(toDraft(saved.value))))
const defaultTier = (key: string) => saved.value?.builtIns?.find(b => b.key === key)?.defaultTier ?? 1

async function load() {
  loading.value = true; loadError.value = ''
  try {
    const c = await sponsorPerksApi.get()
    if (!c.canEdit) { loadError.value = '只有超级管理员能修改赞助福利'; return }
    saved.value = c
    draft.value = toDraft(c)
  } catch (e) {
    loadError.value = getErrorMessage(e, '读取赞助福利失败')
  } finally { loading.value = false }
}
onMounted(load)

function reset() { if (saved.value) draft.value = toDraft(saved.value); message.value = '' }
function move(i: number, dir: -1 | 1) {
  const list = draft.value.perks
  const j = i + dir
  if (j < 0 || j >= list.length) return
  ;[list[i], list[j]] = [list[j]!, list[i]!]
}
function remove(i: number) {
  const p = draft.value.perks[i]
  if (!p || p.key) return
  if (p.title.trim() && !confirm(`删除「${p.title.trim()}」？保存后才真正删除，没保存前可以点「撤销修改」。`)) return
  draft.value.perks.splice(i, 1)
}
/** 新福利不带编号（id 留空），保存时后端生成；新加的只是展示文字，不控制权限 */
function add() { draft.value.perks.push({ uid: ++uidSeq, id: '', key: null, title: '', description: '', minTier: 1, visible: true }) }

async function save() {
  if (!saved.value) return
  message.value = ''
  const d = draft.value
  if (d.perks.some(p => !p.title.trim())) return fail('每条福利都要有名字（不要的可以删掉）')
  // 实际权限的等级变了：保存前说清楚影响
  const changed = d.perks.filter(p => p.key && saved.value!.perks.find(x => x.key === p.key)?.minTier !== p.minTier)
  if (changed.length) {
    const lines = changed.map(p => `「${p.title.trim()}」：${tierName(saved.value!.perks.find(x => x.key === p.key)?.minTier ?? 1)} 起 → ${tierName(p.minTier)} 起`)
    if (!confirm(`这些是网站实际判断的权限，保存后马上生效：\n\n${lines.join('\n')}\n\n确定保存吗？`)) return
  }
  saving.value = true
  try {
    saved.value = await sponsorPerksApi.save(toPayload(d))
    draft.value = toDraft(saved.value)
    messageIsError.value = false
    message.value = '已保存，赞助墙刷新后就是新内容。'
  } catch (e) {
    fail(getErrorMessage(e, '保存失败，请重试（修改还在）'))
  } finally { saving.value = false }
}
function fail(text: string) { messageIsError.value = true; message.value = text }
</script>

<style scoped>
.spa { display: grid; gap: 18px; color: var(--text-primary); }
.spa-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 20px; flex-wrap: wrap; }
.spa-header h1 { margin: 0; font-size: 26px; letter-spacing: -.03em; }
.spa-header p { margin: 8px 0 0; color: var(--text-secondary); font-size: 13px; max-width: 640px; }
.spa-actions { display: flex; gap: 10px; flex-wrap: wrap; }
.spa-actions a { text-decoration: none; }
.spa-safe { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: var(--accent-soft); font-size: 13px; line-height: 1.7; }
.spa-message { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); font-size: 13px; }
.spa-message.error { border-left-color: var(--danger, #c0392b); background: var(--danger-soft, rgba(192,57,43,.08)); }
.spa-state { color: var(--text-secondary); }
.spa-form { min-width: 0; margin: 0; display: grid; gap: 20px; padding: 18px 20px; border: 1px solid var(--border-line); border-top: 2px solid var(--accent-color); background: var(--paper-card); }
.spa-field { display: grid; gap: 6px; min-width: 0; font-size: 13px; }
.spa-field > span { color: var(--text-secondary); font-size: 12px; letter-spacing: .04em; }
input:not([type='checkbox']), textarea, select { box-sizing: border-box; width: 100%; min-width: 0; padding: 7px 9px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-bg); color: var(--text-primary); font: inherit; font-size: 13px; }
textarea { resize: vertical; line-height: 1.6; }
input:focus, textarea:focus, select:focus { outline: none; border-color: var(--accent-color); }
.spa-tiers { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; }
.spa-tier { display: grid; gap: 6px; padding: 10px; background: var(--bg-sub); }
.spa-perks { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.spa-perk { display: flex; gap: 10px; align-items: flex-start; padding: 10px; background: var(--bg-sub); }
.spa-perk.hidden { opacity: .6; }
.spa-perk-main { flex: 1; min-width: 0; display: grid; gap: 6px; }
.spa-perk-row { display: grid; grid-template-columns: minmax(0, 1fr) 120px auto; gap: 8px; align-items: center; }
.spa-title { font-weight: 600; }
.spa-check { display: flex; align-items: center; gap: 4px; white-space: nowrap; font-size: 13px; }
.spa-check input { accent-color: var(--accent-color); }
.spa-key { color: var(--accent-color); font-size: 12px; cursor: help; }
.spa-ops { display: flex; flex-direction: column; gap: 2px; }
.spa-ops button { padding: 3px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.spa-ops button:disabled { opacity: .4; cursor: default; }
.spa-ops button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.spa-ops .del:hover:not(:disabled) { border-color: var(--danger, #c0392b); color: var(--danger, #c0392b); }
.spa-add { justify-self: start; }
@media (max-width: 900px) { .spa-tiers { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) {
  .spa-tiers { grid-template-columns: 1fr; }
  .spa-perk-row { grid-template-columns: minmax(0, 1fr) auto; }
  .spa-perk-row select { grid-column: 1; }
}
</style>
