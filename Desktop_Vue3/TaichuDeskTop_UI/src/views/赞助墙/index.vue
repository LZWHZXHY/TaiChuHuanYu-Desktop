<template>
  <div class="sponsor-page content-page">
    <header class="sp-header">
      <div>
        <h1>💛 赞助墙</h1>
        <p class="sub">感谢每一位支持太初寰宇的同行者</p>
      </div>
      <div class="sp-header-actions">
        <a class="btn sponsor-btn" :href="AFDIAN_URL" target="_blank" rel="noopener noreferrer" @click.prevent="openExternal(AFDIAN_URL)">💛 去爱发电支持</a>
        <button v-if="isAdmin" class="btn primary" @click="openEditor()">+ 添加赞助者</button>
      </div>
    </header>

    <!-- 统计 -->
    <div class="sp-stats">
      <div class="sp-stat"><span class="num">¥{{ totalAmount.toFixed(2) }}</span><span class="lbl">累计赞助</span></div>
      <div class="sp-stat"><span class="num">{{ totalCount }}</span><span class="lbl">赞助人数</span></div>
      <div class="sp-stat"><span class="num">{{ tierList.length }}</span><span class="lbl">档位</span></div>
    </div>

    <!-- 档位分布 -->
    <div v-if="tierCounts.length" class="tier-row">
      <span v-for="t in tierCounts" :key="t.tier" class="tier-chip" :class="tierClass(t.tier)">{{ t.tier }} · {{ t.count }} 人</span>
    </div>

    <!-- 爱发电自动同步（管理员） -->
    <div v-if="isAdmin" class="sync-box">
      <button class="sync-toggle" @click="syncOpen = !syncOpen">⚙ 爱发电自动同步 {{ syncOpen ? '▴' : '▾' }}</button>
      <div v-if="syncOpen" class="sync-panel">
        <p class="sync-hint">填入爱发电「开发者/API」里的 User ID 和 Token。开启后每 10 分钟自动：订单 → 财政流水，赞助者 → 赞助墙。</p>
        <p class="sync-hint">实时同步（可选）：把下面地址填到爱发电「开发者后台 → Webhook 通知地址」<br /><code class="webhook-url">{{ webhookUrl }}</code></p>
        <label class="field"><span>User ID</span><input v-model="afdianForm.userId" placeholder="爱发电用户 ID" /></label>
        <label class="field"><span>API Token</span><input v-model="afdianForm.token" type="password" :placeholder="afdianConfig?.hasToken ? `已配置 ${afdianConfig?.maskedToken}` : '爱发电 API Token'" autocomplete="off" /></label>
        <label class="check"><input v-model="afdianForm.enabled" type="checkbox" /> 开启自动同步</label>
        <div class="sync-actions">
          <button class="btn primary" :disabled="syncBusy" @click="saveAfdianConfig">{{ syncBusy ? '保存中…' : '保存配置' }}</button>
          <button class="btn" :disabled="syncBusy" @click="runSync">{{ syncBusy ? '同步中…' : '立即同步' }}</button>
        </div>
        <p v-if="afdianConfig?.lastSyncAt" class="sync-hint">上次同步：{{ new Date(afdianConfig.lastSyncAt).toLocaleString('zh-CN') }}</p>
      </div>
    </div>

    <p v-if="loading" class="sp-empty">加载中…</p>
    <p v-else-if="!sponsors.length" class="sp-empty">还没有赞助记录，来成为第一位吧。</p>

    <div v-else class="sp-grid">
      <article v-for="s in sponsors" :key="s.id" class="sp-card" :class="tierClass(s.tier)">
        <div class="sp-card-head">
          <span class="sp-badge" :class="tierClass(s.tier)">{{ s.tier }}</span>
          <span class="sp-amount">¥{{ s.amount.toFixed(2) }}</span>
        </div>
        <h2 class="sp-name">{{ s.name }}</h2>
        <p v-if="s.message" class="sp-message">"{{ s.message }}"</p>
        <div class="sp-card-foot">
          <time class="sp-date">{{ formatDate(s.sponsoredAt) }}</time>
          <div v-if="isAdmin" class="sp-actions">
            <button class="mini-btn" @click.stop="openEditor(s)">编辑</button>
            <button class="mini-btn danger" @click.stop="removeSponsor(s)">删除</button>
          </div>
        </div>
      </article>
    </div>

    <!-- 编辑器 -->
    <div v-if="showEditor" class="sp-mask" @click.self="showEditor = false">
      <div class="sp-editor">
        <h2 class="sp-editor-title">{{ editorId ? '编辑赞助者' : '添加赞助者' }}</h2>
        <label class="field"><span>名字</span><input v-model="form.name" placeholder="赞助者昵称" /></label>
        <div class="field-row">
          <label class="field"><span>档位</span>
            <select v-model="form.tier">
              <option v-for="t in tierList" :key="t" :value="t">{{ t }}</option>
            </select>
          </label>
          <label class="field"><span>金额</span><input v-model.number="form.amount" type="number" min="0" step="0.01" /></label>
        </div>
        <label class="field"><span>留言（可选）</span><input v-model="form.message" placeholder="想说的话" /></label>
        <div class="field-row2">
          <label class="check"><input v-model="form.isAnonymous" type="checkbox" /> 匿名展示</label>
          <label class="check"><input v-model="form.isVisible" type="checkbox" /> 公开展示</label>
        </div>
        <p class="hint">保存后会自动在「财政」里记一笔收入流水。</p>
        <div class="sp-editor-actions">
          <button class="btn" @click="showEditor = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveSponsor">{{ saving ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { sponsorApi, type SponsorItem, type SponsorPayload, type AfdianConfigInfo } from '@/api/sponsor'
import { useUserStore } from '@/stores/user'

const AFDIAN_URL = 'https://ifdian.net/a/taichuhuanyu'
function openExternal(url: string) {
  const host = (window as any).chrome?.webview
  if (host) host.postMessage({ cmd: 'load_external_url', url })
  else window.open(url, '_blank', 'noopener,noreferrer')
}

const userStore = useUserStore()
const sponsors = ref<SponsorItem[]>([])
const totalAmount = ref(0)
const totalCount = ref(0)
const loading = ref(false)
const showEditor = ref(false)
const editorId = ref<number | null>(null)
const saving = ref(false)

const isAdmin = computed(() => userStore.permissions?.includes('SuperAdmin') ?? false)

// 爱发电同步
const syncOpen = ref(false)
const syncBusy = ref(false)
const afdianConfig = ref<AfdianConfigInfo | null>(null)
const afdianForm = ref({ userId: '', token: '', enabled: true })
const webhookUrl = computed(() => window.location.origin + '/api/Sponsor/webhook')

async function loadAfdianConfig() {
  try {
    const cfg = await sponsorApi.getAfdianConfig()
    afdianConfig.value = cfg
    afdianForm.value = { userId: cfg.userId, token: '', enabled: cfg.enabled }
  } catch { /* ignore */ }
}
async function saveAfdianConfig() {
  syncBusy.value = true
  try {
    await sponsorApi.saveAfdianConfig({ userId: afdianForm.value.userId, token: afdianForm.value.token || undefined, enabled: afdianForm.value.enabled })
    await loadAfdianConfig()
    alert('配置已保存')
  } catch (e: any) { alert(e.friendlyMessage || '保存失败') } finally { syncBusy.value = false }
}
async function runSync() {
  syncBusy.value = true
  try {
    const res = await sponsorApi.syncAfdian()
    await load()
    await loadAfdianConfig()
    alert(`同步完成：新增 ${res.ordersCreated} 条财政收入，${res.sponsorsSynced} 位赞助者`)
  } catch (e: any) { alert(e.friendlyMessage || '同步失败') } finally { syncBusy.value = false }
}

const tierList = ['觉醒者', '超凡者', '大超凡者', '君者']
const tierClass = (t: string) => ({ '觉醒者': 'tier-awaken', '超凡者': 'tier-transcend', '大超凡者': 'tier-great', '君者': 'tier-lord' } as Record<string, string>)[t] || 'tier-awaken'

const tierCounts = computed(() => {
  const map: Record<string, number> = {}
  for (const s of sponsors.value) map[s.tier] = (map[s.tier] || 0) + 1
  return Object.entries(map).map(([tier, count]) => ({ tier, count }))
})

const emptyForm = (): SponsorPayload => ({ name: '', tier: '觉醒者', amount: 5, message: '', isAnonymous: false, isVisible: true })
const form = ref<SponsorPayload>(emptyForm())

const formatDate = (v: string) => v ? new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''

async function load() {
  loading.value = true
  try {
    const data = await sponsorApi.list()
    sponsors.value = (data.sponsors || []).slice().sort((a, b) => b.amount - a.amount)
    totalAmount.value = data.totalAmount || 0
    totalCount.value = data.totalCount || 0
  } catch { sponsors.value = [] } finally { loading.value = false }
}

function openEditor(s?: SponsorItem) {
  editorId.value = s?.id ?? null
  form.value = s ? { name: s.name, tier: s.tier, amount: s.amount, message: s.message || '', isAnonymous: s.isAnonymous, isVisible: true } : emptyForm()
  showEditor.value = true
}

async function saveSponsor() {
  if (!form.value.name.trim()) { alert('请填写名字'); return }
  saving.value = true
  try {
    if (editorId.value) await sponsorApi.update(editorId.value, form.value)
    else await sponsorApi.create(form.value)
    showEditor.value = false
    await load()
  } catch (e: any) {
    alert(e.friendlyMessage || '保存失败')
  } finally { saving.value = false }
}

async function removeSponsor(s: SponsorItem) {
  if (!confirm(`确认删除赞助者「${s.name}」？财政流水也会一并删除。`)) return
  await sponsorApi.remove(s.id)
  await load()
}

onMounted(async () => { await load(); if (isAdmin.value) await loadAfdianConfig() })
</script>

<style scoped>
.sponsor-page { max-width: 1100px; margin: 0 auto; padding: 0 0 48px; }
.sp-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 24px 0 20px; }
.sp-header-actions { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
.sponsor-btn { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); text-decoration: none; font-weight: 600; }
.sponsor-btn:hover { opacity: .88; }
.sp-header h1 { margin: 0; font-size: clamp(1.7rem, 3.4vw, 2.3rem); }
.sp-header .sub { margin: 8px 0 0; color: var(--text-secondary); font-size: .9rem; }
.sp-stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; margin-bottom: 20px; }
.sp-stat { display: grid; gap: 6px; padding: 18px 20px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.sp-stat .num { font-size: 1.5rem; font-weight: 700; }
.sp-stat .lbl { font-size: .78rem; color: var(--text-secondary); }
.tier-row { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }
.tier-chip { font-size: .76rem; padding: 4px 12px; border-radius: 14px; border: 1px solid var(--border-line); color: var(--text-secondary); }
/* 爱发电同步 */
.sync-box { margin-bottom: 20px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); overflow: hidden; }
.sync-toggle { width: 100%; text-align: left; padding: 12px 16px; background: none; border: none; color: var(--text-secondary); cursor: pointer; font-size: .85rem; font-weight: 600; }
.sync-toggle:hover { color: var(--text-primary); }
.sync-panel { padding: 0 16px 16px; display: grid; gap: 12px; }
.sync-hint { margin: 0; font-size: .78rem; color: var(--text-secondary); line-height: 1.6; }
.sync-hint.locked { color: var(--success); font-weight: 600; }
.webhook-url { display: inline-block; margin-top: 4px; padding: 3px 8px; border-radius: 6px; background: var(--bg-sub); font-size: .76rem; color: var(--text-primary); overflow-wrap: anywhere; word-break: break-all; }
.sync-actions { display: flex; gap: 10px; }
.sp-empty { text-align: center; color: var(--text-secondary); padding: 48px 0; }
.sp-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 16px; }
.sp-card { padding: 18px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); border-top: 3px solid var(--border-line); }
.sp-card.tier-awaken { border-top-color: #54b677; }
.sp-card.tier-transcend { border-top-color: #4a90d9; }
.sp-card.tier-great { border-top-color: #9b6dd6; }
.sp-card.tier-lord { border-top-color: #d4a017; }
.sp-card-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.sp-badge { font-size: .72rem; padding: 2px 10px; border-radius: 10px; background: var(--bg-sub); color: var(--text-secondary); }
.sp-badge.tier-awaken { background: #e8f6ee; color: #2da44e; }
.sp-badge.tier-transcend { background: #e8f1fb; color: #4a90d9; }
.sp-badge.tier-great { background: #f1eafb; color: #9b6dd6; }
.sp-badge.tier-lord { background: #fbf3e0; color: #b8860b; }
.sp-amount { font-size: .9rem; font-weight: 700; color: var(--text-primary); }
.sp-name { margin: 12px 0 6px; font-size: 1.05rem; overflow-wrap: anywhere; }
.sp-message { margin: 0 0 12px; font-size: .85rem; color: var(--text-secondary); line-height: 1.6; font-style: italic; }
.sp-card-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-top: auto; }
.sp-date { font-size: .72rem; color: var(--text-secondary); }
.sp-actions { display: flex; gap: 6px; }
.mini-btn { padding: 3px 9px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .72rem; }
.mini-btn.danger { color: var(--danger); }
/* 编辑器 */
.sp-mask { position: fixed; inset: 0; z-index: 100; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.sp-editor { width: min(520px, 100%); max-height: 90vh; overflow-y: auto; background: var(--paper-card); border-radius: 12px; padding: 24px; display: grid; gap: 14px; }
.sp-editor-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .8rem; color: var(--text-secondary); }
.field input, .field select { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.field-row2 { display: flex; gap: 20px; font-size: .85rem; }
.check { display: flex; align-items: center; gap: 6px; cursor: pointer; }
.hint { margin: 0; font-size: .75rem; color: var(--text-secondary); }
.sp-editor-actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
@media (max-width: 640px) { .sp-stats { gap: 8px; } .sp-stat { padding: 12px; } .sp-stat .num { font-size: 1.1rem; } .sp-grid { grid-template-columns: 1fr; } }
</style>
