<!-- 管理面板 · 用户：给某个人授予 / 收回头衔。授予后对方收到通知，可以在交易行「我的装扮」里自己换戴；也可以直接帮他戴上。 -->
<template>
  <Teleport to="body">
    <div class="tg-mask" @mousedown.self="emit('close')">
      <form class="tg" role="dialog" aria-modal="true" :aria-label="`给 ${user.username} 的头衔`" @submit.prevent="grant" @keydown.esc="emit('close')">
        <header class="tg-head">
          <h3>🏷 {{ user.username }} 的头衔</h3>
          <button type="button" class="tg-x" aria-label="关闭" @click="emit('close')">×</button>
        </header>
        <div class="tg-body">
          <section>
            <h4>已经有的</h4>
            <p v-if="loading" class="tg-muted">读取中…</p>
            <p v-else-if="!titles.length" class="tg-muted">还没有任何头衔。</p>
            <ul v-else class="tg-list">
              <li v-for="t in titles" :key="t.title">
                <span class="tg-badge">{{ t.title }}</span>
                <small>{{ t.source }} · {{ t.at.slice(0, 10) }}</small>
                <b v-if="equipped === t.title" class="tg-on">正在戴</b>
                <button type="button" class="tg-link danger" :disabled="busy" @click="revoke(t.title)">收回</button>
              </li>
            </ul>
          </section>
          <section class="tg-new">
            <h4>授予新头衔</h4>
            <label class="tg-field">
              <span>头衔 <small>最多 12 个字</small></span>
              <input v-model="title" maxlength="12" required placeholder="比如：开服元老 / 首席画师 / 建筑大师" />
            </label>
            <label class="tg-field">
              <span>为什么给 <small>可选，会写在给他的通知里</small></span>
              <input v-model="note" maxlength="100" placeholder="比如：感谢你一个人建完了主城" />
            </label>
            <label class="tg-check"><input v-model="equip" type="checkbox" /> 直接帮他戴上（不勾的话，他可以自己在交易行「我的装扮」里戴）</label>
            <p class="tg-preview">预览：<b>{{ user.username }}</b><span v-if="title.trim()" class="tg-badge">{{ title.trim() }}</span></p>
          </section>
          <p v-if="error" class="tg-error" role="alert">{{ error }}</p>
        </div>
        <footer class="tg-foot">
          <button type="button" class="tg-btn" @click="emit('close')">关闭</button>
          <button type="submit" class="tg-btn primary" :disabled="busy || !title.trim()">{{ busy ? '处理中…' : '授予' }}</button>
        </footer>
      </form>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { adminUserApi } from '@/api/Admin/AdminUser'

const props = defineProps<{ user: { id: string; username: string } }>()
const emit = defineEmits<{ close: []; changed: [equipped: string | null] }>()

const titles = ref<{ title: string; source: string; at: string }[]>([])
const equipped = ref<string | null>(null)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const title = ref('')
const note = ref('')
const equip = ref(true)
const errText = (e: any, f: string) => e?.response?.data?.message || f

async function load() {
  loading.value = true
  try { const r = await adminUserApi.titles(props.user.id); titles.value = r.titles; equipped.value = r.equipped }
  catch (e) { error.value = errText(e, '头衔读不出来（后端更新了吗？）') }
  finally { loading.value = false }
}
void load()

async function grant() {
  const t = title.value.trim()
  if (!t) return
  busy.value = true; error.value = ''
  try {
    const r = await adminUserApi.grantTitle(props.user.id, { title: t, equip: equip.value, note: note.value.trim() || undefined })
    ElMessage.success(r.message)
    title.value = ''; note.value = ''
    await load()
    emit('changed', equipped.value)
  } catch (e) { error.value = errText(e, '授予失败') }
  finally { busy.value = false }
}
async function revoke(t: string) {
  if (!window.confirm(`收回 ${props.user.username} 的头衔「${t}」？${equipped.value === t ? '他正戴着，会一起取下。' : ''}`)) return
  busy.value = true; error.value = ''
  try {
    const r = await adminUserApi.revokeTitle(props.user.id, t)
    ElMessage.success(r.message)
    await load()
    emit('changed', equipped.value)
  } catch (e) { error.value = errText(e, '收回失败') }
  finally { busy.value = false }
}
</script>

<style scoped>
.tg-mask { position: fixed; inset: 0; z-index: 3000; display: flex; align-items: flex-start; justify-content: center; padding: 8vh 16px; background: rgba(0, 0, 0, .4); overflow-y: auto; }
.tg { width: min(520px, 100%); background: var(--paper-card); color: var(--text-primary); border-top: 3px solid var(--accent-color); box-shadow: 0 20px 50px rgba(0, 0, 0, .25); }
.tg-head { display: flex; align-items: center; padding: 14px 18px; border-bottom: 1px solid var(--border-line); }
.tg-head h3 { flex: 1; margin: 0; font-size: 16px; }
.tg-x { width: 30px; height: 30px; border: 0; background: none; color: var(--text-secondary); font-size: 20px; cursor: pointer; }
.tg-body { display: grid; gap: 18px; padding: 16px 18px; }
.tg-body h4 { margin: 0 0 8px; font-size: 13px; }
.tg-muted { margin: 0; font-size: 13px; color: var(--text-secondary); }
.tg-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.tg-list li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 6px 8px; background: var(--bg-sub); }
.tg-list small { flex: 1; min-width: 0; font-size: 12px; color: var(--text-secondary); }
.tg-badge { display: inline-block; margin-left: 6px; padding: 0 6px; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 12px; font-weight: 600; line-height: 18px; }
.tg-list .tg-badge { margin-left: 0; }
.tg-on { font-size: 12px; color: #16a34a; }
.tg-link { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.tg-link.danger { color: #dc2626; }
.tg-new { display: grid; gap: 10px; }
.tg-field { display: grid; gap: 4px; }
.tg-field > span { font-size: 13px; font-weight: 600; }
.tg-field small { font-weight: 400; color: var(--text-secondary); }
.tg-field input { box-sizing: border-box; width: 100%; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; }
.tg-check { display: flex; align-items: flex-start; gap: 6px; font-size: 13px; }
.tg-preview { margin: 0; padding: 8px 10px; background: var(--bg-sub); font-size: 14px; }
.tg-error { margin: 0; color: #dc2626; font-size: 13px; }
.tg-foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 18px; border-top: 1px solid var(--border-line); }
.tg-btn { padding: 7px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.tg-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.tg-btn:disabled { opacity: .55; cursor: default; }
</style>
