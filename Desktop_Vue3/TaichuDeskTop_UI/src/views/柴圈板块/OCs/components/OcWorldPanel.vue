<!-- OcWorldPanel.vue：OC 详情页里的「世界观」一栏。
     访客：看到这个 OC 收录在哪些（自己看得到的）世界观里。
     作者：关联 / 取消关联世界观；「复制到我的世界观」变成一张人物条目；
           对应的条目可以「从这个条目更新 OC」（先预览再确认）、断开对应；查看 / 恢复 OC 的备份。 -->
<template>
  <section v-if="info && (info.isAuthor || visibleWorlds.length)" class="ocw desc-section">
    <h3 class="desc-title">🌐 世界观</h3>

    <!-- 收录于哪些世界观 -->
    <ul v-if="visibleWorlds.length" class="ocw-worlds">
      <li v-for="w in visibleWorlds" :key="w.projectId">
        <RouterLink v-if="w.canRead" :to="`/world/project/${w.projectId}`" class="ocw-world">
          <img v-if="w.coverImage" :src="coverSrc(w.coverImage)" :style="coverStyle(w.coverImage)" alt="" loading="lazy" />
          <span>{{ w.name }}</span>
        </RouterLink>
        <span v-else class="ocw-world muted">{{ w.exists ? '看不到的世界观' : '已删除的世界观' }}</span>
        <button v-if="info.isAuthor" type="button" class="ocw-link" :disabled="busy" @click="unlink(w)">取消关联</button>
      </li>
    </ul>
    <p v-else-if="info.isAuthor" class="ocw-tip">还没有关联到世界观。关联后，世界观首页的「柴圈人设」里会显示这个 OC（发布后才显示）。</p>

    <template v-if="info.isAuthor">
      <div class="ocw-actions">
        <button type="button" class="btn-line" :disabled="busy" @click="openPicker('link')">＋ 关联到世界观</button>
        <button type="button" class="btn-line" :disabled="busy" @click="openPicker('copy')">⧉ 复制到我的世界观</button>
        <button type="button" class="btn-line" @click="backupsOpen = true">🗂 备份</button>
      </div>

      <!-- 选世界观 -->
      <div v-if="picker" class="ocw-picker">
        <p class="ocw-tip">{{ picker === 'link'
          ? '关联到你是创建者或协作者的世界观（只是挂个名，OC 内容不会复制过去）。'
          : '复制成你世界观里的一张「人物」条目：简介 → 正文，短属性 → 属性，长属性 → 文本区块，能力面板、封面、图库一起带上。原 OC 不受影响。' }}</p>
        <p v-if="worldsLoading" class="ocw-tip" role="status">正在读取你的世界观…</p>
        <p v-else-if="!pickable.length" class="ocw-tip">{{ picker === 'link' ? '你还没有可以关联的世界观。' : '你还没有能编辑的世界观（要是创建者或编写者）。' }}
          <RouterLink to="/world/create">去创建一个</RouterLink></p>
        <template v-else>
          <select v-model="chosen" aria-label="选择世界观">
            <option value="" disabled>选择世界观…</option>
            <option v-for="p in pickable" :key="p.id" :value="p.id">{{ p.name }}{{ p.myRole === 'owner' ? '' : p.myRole === 'editor' ? '（编写者）' : '（只读成员）' }}</option>
          </select>
          <button type="button" class="btn-line primary" :disabled="!chosen || busy" @click="confirmPicker">{{ busy ? '处理中…' : picker === 'link' ? '关联' : '复制' }}</button>
        </template>
        <button type="button" class="ocw-link" :disabled="busy" @click="picker = null">取消</button>
      </div>

      <!-- 对应的条目 -->
      <div v-if="info.cards.length" class="ocw-pairs">
        <h4>对应的世界观条目</h4>
        <ul>
          <li v-for="c in info.cards" :key="c.cardId">
            <div class="ocw-pair-main">
              <RouterLink v-if="c.cardExists && c.cardTitle" :to="`/world/project/${c.projectId}/card/${c.cardId}`">《{{ c.cardTitle }}》</RouterLink>
              <span v-else class="muted">{{ c.cardExists ? '看不到的条目' : '条目已经被删除了' }}</span>
              <small>{{ c.projectName ? `世界观《${c.projectName}》· ` : '' }}{{ describeSync(c.lastSyncAt, c.lastSyncDirection) }}</small>
            </div>
            <div class="ocw-pair-ops">
              <button v-if="c.canSync" type="button" class="ocw-link" :disabled="busy" @click="openSync(c)">从这个条目更新 OC</button>
              <button type="button" class="ocw-link" :disabled="busy" @click="disconnect(c)">断开对应</button>
            </div>
          </li>
        </ul>
        <p class="ocw-tip">「从这个条目更新 OC」会先列出要新增、修改的内容，确认后才改；改之前自动备份。在条目编辑页也能反过来「从柴圈 OC 更新」条目。</p>
      </div>
      <p v-if="error" class="ocw-error" role="alert">{{ error }}</p>
    </template>

    <OcSyncPreview v-if="syncTarget" v-model:open="syncOpen" :oc-id="characterId" :card-id="syncTarget.cardId" direction="card-to-oc" @done="onSynced" />
    <OcBackups v-if="info.isAuthor" v-model:open="backupsOpen" :oc-id="characterId" @restored="$emit('changed')" />
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { worldApi, type WorldProject } from '@/api/worldApi'
import { getErrorMessage } from '@/utils/apiError'
import { coverSrc, coverStyle } from '@/utils/coverFocus'
import { ocWorldApi, describeSync, type OcCardPair, type OcWorldInfo, type OcWorldLink } from '../ocWorld'
import OcSyncPreview from './OcSyncPreview.vue'
import OcBackups from './OcBackups.vue'

const props = defineProps<{ characterId: string }>()
const emit = defineEmits<{ (e: 'changed'): void }>()
const router = useRouter()

const info = ref<OcWorldInfo | null>(null)
const busy = ref(false)
const error = ref('')
const backupsOpen = ref(false)
const visibleWorlds = computed(() => (info.value?.worlds ?? []).filter(w => info.value?.isAuthor || w.canRead))

let version = 0
async function load() {
  const v = ++version
  try {
    const res = await ocWorldApi.ocInfo(props.characterId)
    if (v === version) info.value = res
  } catch {
    // 读不到就不显示这一栏（不影响详情页其他内容）
    if (v === version) info.value = null
  }
}
watch(() => props.characterId, id => { info.value = null; if (id) void load() }, { immediate: true })

// ---------- 选世界观（关联 / 复制） ----------
const picker = ref<'link' | 'copy' | null>(null)
const chosen = ref('')
const myWorlds = ref<WorldProject[]>([])
const worldsLoading = ref(false)
const linkedIds = computed(() => new Set((info.value?.worlds ?? []).map(w => w.projectId)))
// 关联：创建者或任何协作者都行（已经关联过的不再列出）；复制：要能编辑（创建者、编写者）
const pickable = computed(() => myWorlds.value.filter(p => picker.value === 'link'
  ? Boolean(p.myRole) && !linkedIds.value.has(p.id)
  : p.myRole === 'owner' || p.myRole === 'editor'))

async function openPicker(mode: 'link' | 'copy') {
  picker.value = mode
  chosen.value = ''
  error.value = ''
  if (myWorlds.value.length) return
  worldsLoading.value = true
  try { myWorlds.value = await worldApi.getProjects() }
  catch (e) { error.value = getErrorMessage(e, '读取你的世界观失败') }
  finally { worldsLoading.value = false }
}

async function confirmPicker() {
  if (!chosen.value || busy.value) return
  busy.value = true
  error.value = ''
  try {
    if (picker.value === 'link') {
      const res = await ocWorldApi.link(props.characterId, chosen.value)
      ElMessage.success(res.message)
    } else {
      const res = await ocWorldApi.copyToWorld(props.characterId, chosen.value)
      if (res.warning) ElMessage.warning(res.warning)
      try {
        await ElMessageBox.confirm('已经复制成世界观里的一张人物条目，要现在去看看吗？', '复制好了', { confirmButtonText: '去看看', cancelButtonText: '留在这里', type: 'success' })
        void router.push(`/world/project/${res.projectId}/card/${res.cardId}`)
      } catch { /* 留在这里 */ }
    }
    picker.value = null
    await load()
  } catch (e) {
    error.value = getErrorMessage(e, picker.value === 'link' ? '关联失败' : '复制失败')
  } finally {
    busy.value = false
  }
}

async function unlink(w: OcWorldLink) {
  try {
    await ElMessageBox.confirm(`取消和世界观${w.name ? `《${w.name}》` : ''}的关联？只是不再显示在那个世界观的「柴圈人设」里，OC 和条目都不会变。`, '取消关联',
      { confirmButtonText: '取消关联', cancelButtonText: '不了', type: 'warning' })
  } catch { return }
  busy.value = true
  try { await ocWorldApi.unlink(props.characterId, w.projectId); await load() }
  catch (e) { ElMessage.error(getErrorMessage(e, '取消关联失败')) }
  finally { busy.value = false }
}

// ---------- 对应的条目：同步 / 断开 ----------
const syncTarget = ref<OcCardPair | null>(null)
const syncOpen = ref(false)
function openSync(c: OcCardPair) { syncTarget.value = c; syncOpen.value = true }
function onSynced(message: string) {
  ElMessage.success(message)
  void load()
  emit('changed')
}

async function disconnect(c: OcCardPair) {
  try {
    await ElMessageBox.confirm('断开以后就不能直接「从另一边更新」了。只删对应关系，OC 和条目的内容都不会变。', '断开对应',
      { confirmButtonText: '断开', cancelButtonText: '不了', type: 'warning' })
  } catch { return }
  busy.value = true
  try { const res = await ocWorldApi.disconnect(props.characterId, c.cardId); ElMessage.success(res.message); await load() }
  catch (e) { ElMessage.error(getErrorMessage(e, '断开失败')) }
  finally { busy.value = false }
}
</script>

<style scoped>
.ocw { display: grid; gap: 10px; }
/* 标题和详情页其他栏（角色简介、图库）一个样子：父组件的样式管不到这里面，照抄一份 */
.ocw .desc-title { font-size: 15px; font-weight: 400; letter-spacing: 0.2em; margin: 0; color: var(--ink-black, var(--text-primary)); }
.ocw-tip { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.ocw-error { margin: 0; color: var(--danger, #c0392b); font-size: 13px; }
.ocw-worlds { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 8px; }
.ocw-worlds li { display: flex; align-items: center; gap: 6px; border: 1px solid var(--border-line); background: var(--paper-card); padding: 4px 8px 4px 4px; }
.ocw-world { display: inline-flex; align-items: center; gap: 6px; color: var(--text-primary); text-decoration: none; font-size: 13px; }
.ocw-world img { width: 28px; height: 28px; object-fit: cover; }
.ocw-world:hover span { color: var(--accent-color); }
.muted { color: var(--text-secondary); font-size: 13px; }
.ocw-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.ocw-actions .btn-line, .ocw-picker .btn-line { padding: 5px 14px; font-size: 13px; }
.btn-line.primary { border-color: var(--accent-color); color: var(--accent-color); }
.ocw-link { border: 0; background: none; color: var(--accent-color); cursor: pointer; font: inherit; font-size: 12px; padding: 0; }
.ocw-link:disabled { opacity: .5; cursor: not-allowed; }
.ocw-picker { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 10px 12px; border: 1px dashed var(--border-line); background: var(--bg-sub); }
.ocw-picker .ocw-tip { flex-basis: 100%; }
.ocw-picker select { min-width: 200px; max-width: 100%; padding: 5px 8px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.ocw-pairs h4 { margin: 4px 0 6px; font-size: 13px; font-weight: 600; color: var(--text-primary); }
.ocw-pairs ul { list-style: none; margin: 0 0 6px; padding: 0; display: grid; gap: 6px; }
.ocw-pairs li { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 6px 12px; padding: 8px 10px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ocw-pair-main { display: grid; gap: 2px; min-width: 0; font-size: 13px; }
.ocw-pair-main a { color: var(--text-primary); text-decoration: none; }
.ocw-pair-main a:hover { color: var(--accent-color); }
.ocw-pair-main small { color: var(--text-secondary); }
.ocw-pair-ops { display: flex; gap: 12px; align-items: center; }
</style>
