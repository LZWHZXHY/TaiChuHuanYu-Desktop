<template>
  <Teleport to="body">
    <div class="settings-mask" @click.self="close">
      <section role="dialog" aria-modal="true" aria-label="世界设置" class="settings-dialog workbench-skin editor-surface community-surface tone-world" @keydown.esc="close">
        <header class="settings-head">
          <nav class="settings-tabs" aria-label="设置分类">
            <button v-if="isOwner" :aria-pressed="active === 'info'" @click="active = 'info'">名字与简介</button>
            <button :aria-pressed="active === 'members'" @click="active = 'members'">{{ isOwner ? '协作者' : '成员' }}</button>
            <button :aria-pressed="active === 'legend'" @click="active = 'legend'">时间线图例</button>
          </nav>
          <button class="close-btn" aria-label="关闭" @click="close">×</button>
        </header>

        <!-- 名字与简介 -->
        <template v-if="active === 'info' && isOwner">
        <form class="info-form" @submit.prevent="saveInfo">
          <label>
            <span>世界名字</span>
            <input ref="nameInput" v-model="name" maxlength="100" placeholder="给这个世界起个名字" />
          </label>
          <label>
            <span>简介</span>
            <textarea v-model="description" rows="6" placeholder="一两句话介绍这个世界（可不填）"></textarea>
          </label>
          <p v-if="infoError" class="error" role="alert">{{ infoError }}</p>
          <div class="actions">
            <button type="button" @click="close">取消</button>
            <button type="submit" class="primary" :disabled="savingInfo || !infoChanged">{{ savingInfo ? '保存中…' : '保存' }}</button>
          </div>
        </form>

        <!-- 删除世界：要输入世界名字确认，防止误删 -->
        <section class="danger-zone" aria-label="删除世界">
          <h3>删除这个世界</h3>
          <p>会永久删除全部 {{ project.cardCount || 0 }} 个条目、条目之间的关系和图谱布局，协作者也会被移出，<b>无法恢复</b>。删除前建议先在「更多操作 → 导出 / 完整备份」里下载备份。挂在这个世界下的小说不会被删除。</p>
          <template v-if="confirmingDelete">
            <label>
              <span>请输入世界名字「{{ project.name }}」确认删除</span>
              <input v-model="deleteName" :placeholder="project.name" autocomplete="off" @keydown.enter.prevent="deleteWorld" />
            </label>
            <p v-if="deleteError" class="error" role="alert">{{ deleteError }}</p>
            <div class="actions">
              <button type="button" :disabled="deleting" @click="confirmingDelete = false; deleteName = ''">取消</button>
              <button type="button" class="danger" :disabled="deleting || deleteName.trim() !== project.name" @click="deleteWorld">{{ deleting ? '删除中…' : '永久删除' }}</button>
            </div>
          </template>
          <button v-else type="button" class="danger-outline" @click="confirmingDelete = true">删除世界…</button>
        </section>
        </template>

        <!-- 时间线图例：规定每种颜色代表什么，条目的时间线和世界编年史旁边会显示 -->
        <form v-else-if="active === 'legend'" class="legend-form" @submit.prevent="saveLegend">
          <p class="hint">
            给时间线事件的颜色起个名字，比如 <b>红 = 战争</b>、<b>灰 = 灾难</b>。起过名字的颜色会作为图例显示在条目的「时间线」和世界编年史旁边，看的人点图例就能只看这一类事件。用不到的颜色留空就行，不会显示。
            <template v-if="!isOwner"><br />只有创建者能修改图例。</template>
          </p>
          <p v-if="legendLoading" class="muted">正在读取图例…</p>
          <p v-else-if="legendLoadError" class="error" role="alert">{{ legendLoadError }} <button type="button" @click="loadLegend">重试</button></p>
          <template v-else>
            <ul class="legend-list">
              <li v-for="h in LEGEND_HUES" :key="h.key">
                <span class="legend-swatch" :style="{ background: h.swatch }" aria-hidden="true"></span>
                <span class="legend-name">{{ h.name }}</span>
                <input v-if="isOwner" v-model="legendDraft[h.key]" maxlength="20" :placeholder="`比如：${h.example}`" :aria-label="`${h.name}色代表什么`" />
                <span v-else class="legend-text" :class="{ muted: !legendDraft[h.key] }">{{ legendDraft[h.key] || '（没有定义）' }}</span>
              </li>
            </ul>
            <p class="muted legend-note">没选颜色的事件不算在图例里；编年史里没选颜色的事件和所在时间线同色。「蓝」只有编年史的事件能选。</p>
            <p v-if="legendError" class="error" role="alert">{{ legendError }}</p>
            <div v-if="isOwner" class="actions">
              <button type="button" @click="close">取消</button>
              <button type="submit" class="primary" :disabled="savingLegend || !legendChanged">{{ savingLegend ? '保存中…' : '保存' }}</button>
            </div>
          </template>
        </form>

        <!-- 协作者 -->
        <div v-else class="members">
          <p class="hint">
            <template v-if="isOwner">
              邀请别人一起写这个世界。<b>编写者</b>可以新建、修改、删除条目和关系，调整关系图谱；<b>只读成员</b>可以阅读私有世界。
              改名、封面、公开状态、导入备份和删除世界只有你能做。
            </template>
            <template v-else>这些人正在一起编写这个世界。</template>
          </p>

          <div v-if="isOwner" class="invite">
            <div class="invite-row">
              <input v-model="keyword" type="search" placeholder="输入用户名查找" aria-label="按用户名查找要邀请的人" @input="onSearch" />
              <select v-model="inviteRole" aria-label="邀请身份">
                <option value="editor">编写者</option>
                <option value="viewer">只读成员</option>
              </select>
            </div>
            <ul v-if="keyword.trim()" class="candidates" aria-live="polite">
              <li v-if="searching" class="muted">正在查找…</li>
              <li v-else-if="!candidates.length" class="muted">没有找到这个用户（已经是成员的不会再出现）</li>
              <template v-else>
                <li v-for="c in candidates" :key="c.userId">
                  <span class="avatar"><img v-if="c.avatar" :src="c.avatar" alt="" /><span v-else>{{ c.username.slice(0, 1) }}</span></span>
                  <span class="name">{{ c.username }}</span>
                  <button class="primary" :disabled="Boolean(busyId)" @click="invite(c)">{{ busyId === c.userId ? '邀请中…' : '邀请' }}</button>
                </li>
              </template>
            </ul>
          </div>

          <p v-if="loading && !members" class="muted">正在加载成员…</p>
          <p v-else-if="loadError" class="error" role="alert">{{ loadError }} <button @click="load">重试</button></p>
          <ul v-else-if="members" class="member-list">
            <li>
              <span class="avatar"><img v-if="members.owner.avatar" :src="members.owner.avatar" alt="" /><span v-else>{{ members.owner.username.slice(0, 1) }}</span></span>
              <UserLink class="name" :id="members.owner.userId" :name="members.owner.username" />
              <span class="role owner">创建者</span>
            </li>
            <li v-for="m in members.items" :key="m.userId">
              <span class="avatar"><img v-if="m.avatar" :src="m.avatar" alt="" /><span v-else>{{ m.username.slice(0, 1) }}</span></span>
              <UserLink class="name" :id="m.userId" :name="m.username" />
              <select v-if="isOwner" :value="m.role" :disabled="busyId === m.userId" :aria-label="`${m.username} 的身份`" @change="changeRole(m, ($event.target as HTMLSelectElement).value as CollaboratorRole)">
                <option value="editor">编写者</option>
                <option value="viewer">只读成员</option>
              </select>
              <span v-else class="role">{{ m.role === 'editor' ? '编写者' : '只读成员' }}</span>
              <button v-if="isOwner" class="remove" :disabled="busyId === m.userId" @click="remove(m)">移除</button>
              <button v-else-if="m.userId === userStore.userId" class="remove" :disabled="busyId === m.userId" @click="leave(m)">退出协作</button>
            </li>
            <li v-if="!members.items.length" class="muted empty">{{ isOwner ? '还没有协作者。在上面输入用户名邀请吧。' : '目前只有创建者在编写。' }}</li>
          </ul>
          <p v-if="members && isOwner" class="muted count">{{ members.items.length }} / {{ members.max }} 位协作者</p>
        </div>
      </section>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import UserLink from '@/components/UserLink.vue'
import { worldApi } from '@/api/worldApi'
import type { WorldProject, WorldMembers, WorldMember, WorldMemberCandidate, CollaboratorRole } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import { getErrorMessage } from '@/utils/apiError'
import { worldLegendApi } from '@/api/worldLegend'
import { LEGEND_HUES, setWorldLegend } from '@/utils/worldLegend'

const props = defineProps<{ project: WorldProject; tab: 'info' | 'members'; isOwner: boolean }>()
const emit = defineEmits<{ close: []; left: []; deleted: [] }>()
const store = useWorldStore()
const userStore = useUserStore()

// 'legend'（时间线图例）只在弹窗里切换，外面打开时还是 info / members
const active = ref<'info' | 'members' | 'legend'>(props.isOwner ? props.tab : 'members')
watch(() => props.tab, tab => { active.value = props.isOwner ? tab : 'members' })

function close() { emit('close') }

// ---------- 名字与简介 ----------
const name = ref(props.project.name)
const description = ref(props.project.description ?? '')
const nameInput = ref<HTMLInputElement | null>(null)
const savingInfo = ref(false)
const infoError = ref('')
const infoChanged = computed(() => name.value.trim() !== props.project.name || description.value !== (props.project.description ?? ''))

async function saveInfo() {
  const trimmed = name.value.trim()
  infoError.value = ''
  if (!trimmed) { infoError.value = '名字不能为空'; return }
  if (trimmed.length > 100) { infoError.value = '名字最多 100 个字'; return }
  savingInfo.value = true
  try {
    await store.updateProject(props.project.id, { name: trimmed, description: description.value })
    ElMessage.success('已保存')
    close()
  } catch (error) {
    infoError.value = getErrorMessage(error, '保存失败，请重试')
  } finally {
    savingInfo.value = false
  }
}

// ---------- 删除世界 ----------
const confirmingDelete = ref(false)
const deleteName = ref('')
const deleting = ref(false)
const deleteError = ref('')

async function deleteWorld() {
  if (deleting.value || deleteName.value.trim() !== props.project.name) return
  deleting.value = true
  deleteError.value = ''
  try {
    await store.deleteProject(props.project.id)
    ElMessage.success(`已删除「${props.project.name}」`)
    emit('deleted')
  } catch (error) {
    deleteError.value = getErrorMessage(error, '删除失败，请重试')
  } finally {
    deleting.value = false
  }
}

// ---------- 时间线图例 ----------
const legendDraft = ref<Record<string, string>>({})
const legendSaved = ref<Record<string, string>>({})
const legendLoading = ref(false)
const legendLoaded = ref(false)
const legendLoadError = ref('')
const savingLegend = ref(false)
const legendError = ref('')
const cleanLegend = (items: Record<string, string>) =>
  Object.fromEntries(LEGEND_HUES.map((h): [string, string] => [h.key, (items[h.key] ?? '').replace(/\s+/g, ' ').trim()]).filter(([, v]) => v))
const legendChanged = computed(() => JSON.stringify(cleanLegend(legendDraft.value)) !== JSON.stringify(cleanLegend(legendSaved.value)))

async function loadLegend() {
  legendLoading.value = true
  legendLoadError.value = ''
  try {
    const data = await worldLegendApi.get(props.project.id)
    const items = data?.items && typeof data.items === 'object' ? data.items : {}
    legendSaved.value = { ...items }
    legendDraft.value = { ...items }
    legendLoaded.value = true
    setWorldLegend(props.project.id, items)
  } catch (error) {
    legendLoadError.value = getErrorMessage(error, '图例读取失败')
  } finally {
    legendLoading.value = false
  }
}
// 第一次切到这个标签才去读，平时打开设置不多发请求
watch(active, tab => { if (tab === 'legend' && !legendLoaded.value && !legendLoading.value) void loadLegend() }, { immediate: true })

async function saveLegend() {
  if (!props.isOwner || savingLegend.value) return
  legendError.value = ''
  const items = cleanLegend(legendDraft.value)
  if (Object.values(items).some(v => v.length > 20)) { legendError.value = '每种颜色的含义最多 20 个字'; return }
  savingLegend.value = true
  try {
    const data = await worldLegendApi.save(props.project.id, items)
    const saved = data?.items && typeof data.items === 'object' ? data.items : items
    legendSaved.value = { ...saved }
    legendDraft.value = { ...saved }
    // 页面上的时间线、编年史图例马上跟着变
    setWorldLegend(props.project.id, saved)
    ElMessage.success('图例已保存')
  } catch (error) {
    legendError.value = getErrorMessage(error, '保存失败，请重试')
  } finally {
    savingLegend.value = false
  }
}

// ---------- 成员 ----------
const members = ref<WorldMembers | null>(null)
const loading = ref(false)
const loadError = ref('')
const busyId = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    members.value = await worldApi.getMembers(props.project.id)
    store.patchProject(props.project.id, { collaboratorCount: members.value.items.length })
  } catch (error) {
    loadError.value = getErrorMessage(error, '成员加载失败')
  } finally {
    loading.value = false
  }
}

const keyword = ref('')
const inviteRole = ref<CollaboratorRole>('editor')
const candidates = ref<WorldMemberCandidate[]>([])
const searching = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | undefined
let searchVersion = 0

function onSearch() {
  clearTimeout(searchTimer)
  const version = ++searchVersion
  const query = keyword.value.trim()
  candidates.value = []
  searching.value = Boolean(query)
  if (!query) return
  searchTimer = setTimeout(async () => {
    try {
      const result = await worldApi.searchMemberCandidates(props.project.id, query)
      if (version === searchVersion) candidates.value = result
    } catch {
      if (version === searchVersion) candidates.value = []
    } finally {
      if (version === searchVersion) searching.value = false
    }
  }, 300)
}

async function invite(candidate: WorldMemberCandidate) {
  busyId.value = candidate.userId
  try {
    await worldApi.addMember(props.project.id, candidate.userId, inviteRole.value)
    ElMessage.success(`已邀请 ${candidate.username}`)
    keyword.value = ''
    candidates.value = []
    await load()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '邀请失败，请重试'))
  } finally {
    busyId.value = ''
  }
}

async function changeRole(member: WorldMember, role: CollaboratorRole) {
  if (role === member.role) return
  busyId.value = member.userId
  try {
    await worldApi.updateMemberRole(props.project.id, member.userId, role)
    member.role = role
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '修改失败，请重试'))
    await load()
  } finally {
    busyId.value = ''
  }
}

async function remove(member: WorldMember) {
  try {
    await ElMessageBox.confirm(`移除后 ${member.username} 不能再编写这个世界，已写的条目会保留。`, '移除协作者', { confirmButtonText: '移除', cancelButtonText: '取消', type: 'warning' })
  } catch { return }
  busyId.value = member.userId
  try {
    await worldApi.removeMember(props.project.id, member.userId)
    await load()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '移除失败，请重试'))
  } finally {
    busyId.value = ''
  }
}

async function leave(member: WorldMember) {
  try {
    await ElMessageBox.confirm('退出后你将不能再编写这个世界（私有世界也将看不到）。', '退出协作', { confirmButtonText: '退出', cancelButtonText: '取消', type: 'warning' })
  } catch { return }
  busyId.value = member.userId
  try {
    await worldApi.removeMember(props.project.id, member.userId)
    ElMessage.success('已退出协作')
    void store.fetchProjects().catch(() => {})
    emit('left')
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '退出失败，请重试'))
  } finally {
    busyId.value = ''
  }
}

onMounted(() => {
  void load()
  if (active.value === 'info') void nextTick(() => nameInput.value?.select())
})
onBeforeUnmount(() => { clearTimeout(searchTimer); searchVersion++ })
</script>

<style scoped>
.settings-mask { position: fixed; inset: 0; z-index: 2000; background: #0006; display: grid; place-items: center; padding: 16px; }
.settings-dialog { box-sizing: border-box; width: min(560px, 100%); max-height: 85vh; overflow: auto; padding: 20px 24px 24px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: 0; }
.settings-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 16px; border-bottom: 1px solid var(--border-line); }
.settings-tabs { display: flex; gap: 4px; }
.settings-tabs button { padding: 10px 14px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); font: inherit; cursor: pointer; }
.settings-tabs button[aria-pressed="true"] { color: var(--text-primary); border-bottom-color: var(--accent-color); font-weight: 600; }
.close-btn { border: 0; background: none; color: var(--text-secondary); font-size: 22px; cursor: pointer; padding: 4px 8px; }
button, select, input, textarea { font: inherit; box-sizing: border-box; max-width: 100%; }
input, textarea, select { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); }
input:focus, textarea:focus, select:focus { outline: 2px solid var(--accent-color); outline-offset: -1px; }
.info-form label { display: block; margin-bottom: 16px; }
.info-form label span { display: block; margin-bottom: 6px; font-size: 13px; color: var(--text-secondary); }
.info-form input, .info-form textarea { width: 100%; }
.info-form textarea { resize: vertical; line-height: 1.7; }
.actions { display: flex; justify-content: flex-end; gap: 10px; }
.actions button, .candidates button, .remove, .member-list select { padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); cursor: pointer; }
.primary { background: var(--accent-color) !important; color: var(--on-accent) !important; border-color: var(--accent-color) !important; }
button:disabled { opacity: .5; cursor: default; }
.hint { margin: 0 0 16px; font-size: 13px; line-height: 1.8; color: var(--text-secondary); }
.invite { margin-bottom: 18px; }
.invite-row { display: flex; gap: 8px; }
.invite-row input { flex: 1; min-width: 0; }
.candidates, .member-list { list-style: none; margin: 0; padding: 0; }
.candidates { margin-top: 6px; border: 1px solid var(--border-line); }
.candidates li, .member-list li { display: flex; align-items: center; gap: 10px; padding: 10px 12px; }
.candidates li + li { border-top: 1px solid var(--border-line); }
.member-list li { border-bottom: 1px solid var(--border-line); padding: 12px 4px; }
.avatar { flex-shrink: 0; width: 32px; height: 32px; display: grid; place-items: center; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 14px; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.role { font-size: 12px; color: var(--text-secondary); }
.role.owner { color: var(--accent-color); }
.remove { font-size: 12px; padding: 5px 10px !important; }
.member-list select { font-size: 12px; padding: 5px 8px; }
.muted { color: var(--text-secondary); font-size: 13px; }
.empty { justify-content: center; padding: 18px 0 !important; }
.count { text-align: right; margin: 10px 0 0; font-size: 12px; }
.error { color: var(--danger); font-size: 13px; }
.danger-zone { margin-top: 28px; padding: 16px; border: 1px solid var(--danger); }
.danger-zone h3 { margin: 0 0 8px; font-size: 15px; color: var(--danger); }
.danger-zone p { margin: 0 0 12px; font-size: 13px; line-height: 1.8; color: var(--text-secondary); }
.danger-zone label { display: block; margin-bottom: 12px; }
.danger-zone label span { display: block; margin-bottom: 6px; font-size: 13px; color: var(--text-secondary); }
.danger-zone input { width: 100%; }
.danger-outline { padding: 7px 14px; border: 1px solid var(--danger); border-radius: 0; background: none; color: var(--danger); cursor: pointer; }
.danger { padding: 7px 14px; border: 1px solid var(--danger) !important; border-radius: 0; background: var(--danger) !important; color: #fff !important; cursor: pointer; }
.legend-list { list-style: none; margin: 0 0 10px; padding: 0; display: grid; gap: 8px; }
.legend-list li { display: grid; grid-template-columns: 16px 2em minmax(0, 1fr); align-items: center; gap: 10px; }
.legend-swatch { width: 16px; height: 16px; }
.legend-name { font-size: 13px; color: var(--text-secondary); }
.legend-list input { width: 100%; padding: 7px 10px; }
.legend-text { overflow-wrap: anywhere; }
.legend-note { margin: 0 0 14px; font-size: 12px; line-height: 1.7; }
@media (max-width: 520px) { .invite-row { flex-direction: column; } }
</style>
