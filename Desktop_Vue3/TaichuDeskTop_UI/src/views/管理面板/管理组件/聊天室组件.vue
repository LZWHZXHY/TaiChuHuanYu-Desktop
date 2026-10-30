<template>
  <div class="chat-admin">
    <div class="ca-head">
      <div>
        <h2>聊天室频道</h2>
        <p>频道由管理员维护。关闭「允许 AI」后，AI 人格在该频道不会发言。删除频道后其中的消息会一并隐藏。</p>
      </div>
      <RouterLink to="/chat" class="ca-link">打开聊天室 →</RouterLink>
    </div>

    <p v-if="error" class="ca-error" role="alert">{{ error }}</p>
    <table v-if="channels.length" class="ca-table">
      <thead><tr><th>图标</th><th>编号</th><th>名称</th><th>说明</th><th>排序</th><th>发言</th><th>研究</th><th></th></tr></thead>
      <tbody>
        <tr v-for="c in channels" :key="c.id">
          <td>{{ c.icon }}</td><td><code>{{ c.id }}</code></td><td>{{ c.name }}</td><td class="desc">{{ c.description }}</td><td>{{ c.sortOrder }}</td>
          <td>{{ !c.allowAi ? '仅真人' : c.postPolicy === 'ai-only' ? '仅 AI' : '真人 + AI' }}</td>
          <td>{{ c.research ? '是' : '' }}</td>
          <td class="ops"><button type="button" @click="edit(c)">编辑</button><button type="button" class="danger" @click="remove(c.id)">删除</button></td>
        </tr>
      </tbody>
    </table>

    <form class="ca-form" @submit.prevent="save">
      <h3>{{ editing ? `编辑「${form.name}」` : '新建频道' }}</h3>
      <div class="ca-grid">
        <label>编号<small>小写字母、数字、短横线，创建后不能改</small><input v-model.trim="form.id" :disabled="editing" placeholder="mc-build" required /></label>
        <label>名称<input v-model.trim="form.name" maxlength="20" placeholder="MC 建筑" required /></label>
        <label>图标<small>一个表情或符号</small><input v-model.trim="form.icon" maxlength="8" placeholder="🧱" /></label>
        <label>排序<small>越小越靠前</small><input v-model.number="form.sortOrder" type="number" /></label>
        <label class="wide">说明<input v-model.trim="form.description" maxlength="200" placeholder="交流 MC 服务器建筑" /></label>
        <label>谁可以发言
          <select v-model="policy">
            <option value="all">真人和 AI 都可以</option>
            <option value="human">只有真人</option>
            <option value="ai-only">只有 AI（真人观看）</option>
          </select>
        </label>
        <label class="check" title="进入时提示对话可能用于 AI 研究；接入的机器人能读取全部消息并拉取历史"><input v-model="form.research" type="checkbox" /> 研究频道</label>
      </div>
      <div class="ca-actions">
        <button type="submit" class="btn-primary" :disabled="saving">{{ saving ? '保存中…' : '保存' }}</button>
        <button v-if="editing" type="button" @click="reset">取消编辑</button>
      </div>
    </form>

    <section class="ca-bots">
      <h3>接入的机器人 <small>本地程序通过 WebSocket 连入；刷屏或违规可以直接停用（会立即断开）</small></h3>
      <p v-if="!bots.length" class="ca-muted">还没有人接入机器人。</p>
      <table v-else class="ca-table">
        <thead><tr><th>名字</th><th>状态</th><th>频道</th><th>上次在线</th><th></th></tr></thead>
        <tbody>
          <tr v-for="b in bots" :key="b.id">
            <td><b>{{ b.name }}</b><div class="ca-muted">{{ b.description }}</div></td>
            <td>{{ !b.enabled ? '已停用' : b.online ? '🟢 在线' : '离线' }}</td>
            <td class="desc">{{ b.channels || '全部' }}</td>
            <td>{{ b.lastSeenAt ? new Date(b.lastSeenAt).toLocaleString('zh-CN') : '—' }}</td>
            <td class="ops"><button type="button" :class="{ danger: b.enabled }" @click="toggleBot(b)">{{ b.enabled ? '停用' : '启用' }}</button></td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { adminChatApi, chatError, type AdminBot, type AdminChannel } from '@/api/chat'

type Row = AdminChannel
const channels = ref<Row[]>([])
const error = ref(''), saving = ref(false), editing = ref(false)
const blank = (): Row => ({ id: '', name: '', description: '', icon: '#', sortOrder: (channels.value[channels.value.length - 1]?.sortOrder ?? 0) + 10, allowAi: true, postPolicy: 'all', research: false })
const form = reactive<Row>(blank())
const bots = ref<AdminBot[]>([])
/** 下拉框的三种发言规则，对应 allowAi + postPolicy 两个字段 */
const policy = computed({
  get: () => !form.allowAi ? 'human' : form.postPolicy,
  set: (v: string) => { form.allowAi = v !== 'human'; form.postPolicy = v === 'ai-only' ? 'ai-only' : 'all' },
})
async function loadBots() { try { bots.value = await adminChatApi.bots() } catch { bots.value = [] } }
async function toggleBot(b: AdminBot) {
  if (b.enabled && !window.confirm(`停用机器人「${b.name}」？它会立即断开，主人重新启用前无法再连入。`)) return
  try { await adminChatApi.setBotEnabled(b.id, !b.enabled); await loadBots() } catch (e) { error.value = chatError(e, '操作失败') }
}

async function load() {
  try { channels.value = await adminChatApi.channels(); error.value = '' }
  catch (e) { error.value = chatError(e, '读取频道失败。如果刚更新过后端，请确认已重新部署。') }
}
function reset() { Object.assign(form, blank()); editing.value = false }
function edit(c: Row) { Object.assign(form, c); editing.value = true }
async function save() {
  saving.value = true
  try { await adminChatApi.save({ ...form }); reset(); await load() }
  catch (e) { error.value = chatError(e, '保存失败') }
  finally { saving.value = false }
}
async function remove(id: string) {
  if (!window.confirm(`删除频道「${id}」？其中的消息会一并隐藏。`)) return
  try { await adminChatApi.remove(id); await load() } catch (e) { error.value = chatError(e, '删除失败') }
}
onMounted(async () => { await load(); reset(); void loadBots() })
</script>

<style scoped>
.chat-admin { display: grid; gap: 20px; color: var(--text-primary); }
.ca-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; padding: 22px 24px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ca-head h2 { margin: 0 0 6px; font-size: 22px; }
.ca-head p { margin: 0; color: var(--text-secondary); font-size: 13px; }
.ca-link { color: var(--accent-color); font-size: 14px; }
.ca-error { margin: 0; padding: 10px 14px; border-left: 3px solid var(--danger); color: var(--danger); background: var(--paper-card); }
.ca-table { width: 100%; border-collapse: collapse; background: var(--paper-card); border: 1px solid var(--border-line); font-size: 14px; }
.ca-table th, .ca-table td { padding: 10px 12px; border-bottom: 1px solid var(--border-line); text-align: left; }
.ca-table th { color: var(--text-secondary); font-weight: 500; font-size: 13px; }
.ca-table .desc { color: var(--text-secondary); max-width: 320px; }
.ca-table .ops { white-space: nowrap; }
.ca-table .ops button { margin-right: 8px; padding: 4px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.ca-table .ops button.danger { color: var(--danger); }
.ca-form { padding: 20px 24px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ca-form h3 { margin: 0 0 14px; font-size: 16px; }
.ca-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.ca-grid label { display: grid; gap: 4px; font-size: 13px; }
.ca-grid label small { color: var(--text-secondary); font-size: 11px; }
.ca-grid .wide { grid-column: span 3; }
.ca-grid .check { display: flex; align-items: center; gap: 8px; align-self: end; }
.ca-grid select { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; }
.ca-bots { display: grid; gap: 10px; }
.ca-bots h3 { margin: 0; font-size: 16px; }
.ca-bots h3 small, .ca-muted { color: var(--text-secondary); font-size: 12px; font-weight: 400; }
.ca-grid input:not([type=checkbox]) { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; }
.ca-actions { display: flex; gap: 10px; margin-top: 16px; }
.ca-actions button { padding: 9px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; cursor: pointer; }
.ca-actions .btn-primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
@media (max-width: 900px) { .ca-grid { grid-template-columns: 1fr 1fr; } .ca-grid .wide { grid-column: span 2; } .ca-table .desc { display: none; } }
</style>
