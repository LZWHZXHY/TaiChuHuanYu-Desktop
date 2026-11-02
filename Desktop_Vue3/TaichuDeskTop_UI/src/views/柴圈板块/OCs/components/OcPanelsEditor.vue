<!-- OcPanelsEditor.vue：OC 编辑页下面的「能力面板」：展示面板（自由设计）+ 约战面板（官方约战模板）。
     两份面板各有自己的保存按钮，走自己的接口（/api/StickMan/{id}/panel/...），和上面 OC 表单的保存互不影响：
     OC 保存失败不会动面板，面板保存失败也不会动 OC 的属性和图片。
     编辑页点「保存」时，如果面板有没保存的修改，会在 OC 保存成功后顺便保存（见 edit.vue 调用的 saveAll）。 -->
<template>
  <div class="ocp">
    <p v-if="loading" class="ocp-hint">正在读取能力面板…</p>
    <p v-else-if="loadError" class="ocp-hint" role="alert">{{ loadError }} <button type="button" class="ocp-btn" @click="load">重新读取</button></p>
    <template v-else>
      <!-- 展示面板 -->
      <section id="panel" class="ocp-section">
        <header class="ocp-head">
          <h3>✦ 展示面板</h3>
          <span class="ocp-sub">自由设计：维度、刻度都可以自己定，显示在 OC 详情页</span>
          <span v-if="dirtyShow" class="ocp-state">有未保存的修改</span>
        </header>
        <template v-if="show">
          <StatPanelEditor :model-value="show" @update:model-value="v => { show = v }" />
          <div class="ocp-actions">
            <button type="button" class="ocp-btn primary" :disabled="savingShow || !dirtyShow" @click="saveShow">{{ savingShow ? '保存中…' : '保存展示面板' }}</button>
            <button type="button" class="ocp-btn danger" :disabled="savingShow" @click="removeShow">删除展示面板</button>
            <span v-if="showMsg" class="ocp-msg" :class="{ bad: showBad }" role="status">{{ showMsg }}</span>
          </div>
        </template>
        <button v-else type="button" class="ocp-add" @click="addShow">＋ 添加展示面板（像 JOJO 替身面板那样的雷达图）</button>
      </section>

      <!-- 约战面板 -->
      <section id="battle-panel" class="ocp-section">
        <header class="ocp-head">
          <h3>⚔ 约战面板</h3>
          <span class="ocp-sub">参加柴圈约战用的官方模板，所有 OC 同一套维度，约战页会把双方叠在一起对比</span>
          <span v-if="dirtyBattle" class="ocp-state">有未保存的修改</span>
        </header>
        <p v-if="!template" class="ocp-hint" role="alert">{{ templateError || '正在读取约战模板…' }}</p>
        <template v-else>
          <BattlePanelEditor :template="template" :model-value="battle" @update:model-value="v => { battle = v }" />
          <div class="ocp-actions">
            <button type="button" class="ocp-btn primary" :disabled="savingBattle || !dirtyBattle" @click="saveBattle">{{ savingBattle ? '保存中…' : '保存约战面板' }}</button>
            <button v-if="battleSaved" type="button" class="ocp-btn danger" :disabled="savingBattle" @click="clearBattle">清空约战面板</button>
            <span v-if="battleMsg" class="ocp-msg" :class="{ bad: battleBad }" role="status">{{ battleMsg }}</span>
          </div>
        </template>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import StatPanelEditor from '@/components/StatPanelEditor.vue'
import BattlePanelEditor from '../../components/BattlePanelEditor.vue'
import { cleanStatPanel, createStatPanel, type StatPanel } from '@/utils/statPanel'
import { ocPanelApi, panelError, type BattlePanelTemplate } from '../ocPanel'

const props = defineProps<{ characterId: string }>()
const route = useRoute()

const loading = ref(true)
const loadError = ref('')
const template = ref<BattlePanelTemplate | null>(null)
const templateError = ref('')

// 展示面板：show 是正在编辑的，savedShow 是服务器上的（用来判断有没有改过）
const show = ref<StatPanel | null>(null)
const savedShow = ref('null')
const showJson = (p: StatPanel | null) => JSON.stringify(p ? cleanStatPanel(p) : null)
const dirtyShow = computed(() => showJson(show.value) !== savedShow.value)
const savingShow = ref(false)
const showMsg = ref('')
const showBad = ref(false)

// 约战面板：{ 维度key: 等级 }
const battle = ref<Record<string, string>>({})
const savedBattle = ref('{}')
const battleSaved = ref(false)
const dirtyBattle = computed(() => JSON.stringify(battle.value) !== savedBattle.value)
const savingBattle = ref(false)
const battleMsg = ref('')
const battleBad = ref(false)

let version = 0
async function load() {
  const v = ++version
  loading.value = true
  loadError.value = ''
  // 模板单独读：模板读不到时展示面板照样能编辑
  ocPanelApi.template().then(t => { if (v === version) template.value = t })
    .catch(err => { if (v === version) templateError.value = panelError(err, '约战模板读取失败，请刷新页面重试') })
  try {
    const data = await ocPanelApi.get(props.characterId)
    if (v !== version) return
    show.value = data.show ? cleanStatPanel(data.show) : null
    savedShow.value = showJson(show.value)
    battle.value = { ...(data.battle?.values ?? {}) }
    savedBattle.value = JSON.stringify(battle.value)
    battleSaved.value = Boolean(data.battle)
  } catch (err) {
    if (v === version) loadError.value = panelError(err, '能力面板读取失败')
  } finally {
    if (v === version) loading.value = false
  }
  // 从详情页点「添加能力面板 / 填写约战面板」过来的：滚到对应位置
  if (v === version && (route.hash === '#panel' || route.hash === '#battle-panel')) {
    await nextTick()
    document.getElementById(route.hash.slice(1))?.scrollIntoView({ block: 'start', behavior: 'smooth' })
  }
}
watch(() => props.characterId, () => { void load() }, { immediate: true })

function addShow() { show.value = createStatPanel('jojo'); showMsg.value = '' }

async function saveShow(): Promise<boolean> {
  if (!show.value || savingShow.value) return !show.value
  savingShow.value = true
  showMsg.value = ''
  try {
    const res = await ocPanelApi.saveShow(props.characterId, cleanStatPanel(show.value)!)
    show.value = cleanStatPanel(res.show)
    savedShow.value = showJson(show.value)
    showBad.value = false
    showMsg.value = '展示面板已保存'
    return true
  } catch (err) {
    showBad.value = true
    showMsg.value = panelError(err, '展示面板保存失败，请重试（填的内容还在）')
    return false
  } finally {
    savingShow.value = false
  }
}

async function removeShow() {
  // 还没保存过的：直接收起；保存过的：问一句再删（只删展示面板，约战面板和 OC 本身不动）
  if (savedShow.value === 'null') { show.value = null; return }
  if (!confirm('确定删除展示面板吗？删除后 OC 详情页就不显示这张雷达图了（约战面板不受影响）。')) return
  savingShow.value = true
  try {
    await ocPanelApi.remove(props.characterId, 'show')
    show.value = null
    savedShow.value = 'null'
    showBad.value = false
    showMsg.value = ''
  } catch (err) {
    showBad.value = true
    showMsg.value = panelError(err, '删除失败，请重试')
  } finally {
    savingShow.value = false
  }
}

async function saveBattle(): Promise<boolean> {
  if (savingBattle.value) return false
  savingBattle.value = true
  battleMsg.value = ''
  try {
    const res = await ocPanelApi.saveBattle(props.characterId, battle.value)
    battle.value = { ...res.battle.values }
    savedBattle.value = JSON.stringify(battle.value)
    battleSaved.value = true
    battleBad.value = false
    battleMsg.value = res.battle.filled ? '约战面板已保存' : '已保存，不过每一项都是「?」，约战页会显示成「还没填约战面板」'
    return true
  } catch (err) {
    battleBad.value = true
    battleMsg.value = panelError(err, '约战面板保存失败，请重试（选的等级还在）')
    return false
  } finally {
    savingBattle.value = false
  }
}

async function clearBattle() {
  if (!confirm('确定清空约战面板吗？（展示面板和 OC 本身不受影响）')) return
  savingBattle.value = true
  try {
    await ocPanelApi.remove(props.characterId, 'battle')
    battle.value = {}
    savedBattle.value = '{}'
    battleSaved.value = false
    battleBad.value = false
    battleMsg.value = '约战面板已清空'
  } catch (err) {
    battleBad.value = true
    battleMsg.value = panelError(err, '清空失败，请重试')
  } finally {
    savingBattle.value = false
  }
}

/** 有没有没保存的面板修改（编辑页保存 OC 时用） */
const dirty = computed(() => !loading.value && !loadError.value && (dirtyShow.value || dirtyBattle.value))
/** 把没保存的面板都保存了；全部成功返回 true */
async function saveAll() {
  let ok = true
  if (dirtyShow.value && show.value) ok = (await saveShow()) && ok
  if (dirtyBattle.value) ok = (await saveBattle()) && ok
  return ok
}
defineExpose({ dirty, saveAll })
</script>

<style scoped>
.ocp { display: grid; gap: 28px; }
.ocp-section { display: grid; gap: 12px; scroll-margin-top: 16px; }
.ocp-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; padding-bottom: 8px; border-bottom: 1px solid var(--line-raw, var(--border-line)); }
.ocp-head h3 { margin: 0; font-size: 16px; font-weight: 400; letter-spacing: .2em; color: var(--text-primary); }
.ocp-sub { font-size: 12px; color: var(--text-secondary); }
.ocp-state { margin-left: auto; font-size: 12px; color: var(--warning, #e0a526); }
.ocp-hint { margin: 0; font-size: 13px; color: var(--text-secondary); }
.ocp-add { justify-self: start; padding: 10px 18px; border: 1px dashed var(--border-line); background: none; color: var(--accent-color); font: inherit; font-size: 14px; cursor: pointer; }
.ocp-add:hover { border-color: var(--accent-color); background: var(--accent-soft); }
.ocp-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 10px; }
.ocp-btn { padding: 6px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.ocp-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.ocp-btn:disabled { opacity: .5; cursor: default; }
.ocp-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.ocp-btn.primary:hover:not(:disabled) { color: #fff; opacity: .9; }
.ocp-btn.danger:hover:not(:disabled) { border-color: var(--danger); color: var(--danger); }
.ocp-msg { font-size: 13px; color: var(--success, #5b8c3a); }
.ocp-msg.bad { color: var(--danger); }
</style>
