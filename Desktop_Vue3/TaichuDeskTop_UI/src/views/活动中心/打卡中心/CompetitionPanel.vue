<template>
  <section v-if="detail && detail.kind !== 'general'" class="competition">
    <header class="comp-head">
      <h2>{{ detail.kind === 'match' ? '🏆 赛事进程' : '🎨 参赛作品' }}</h2>
      <span class="comp-sub">{{ detail.kind === 'match' ? `${matches.length} 场对阵` : `${detail.entries.length} 件作品 · ${totalVotes} 票` }}</span>
    </header>

    <!-- 时间线 -->
    <div v-if="detail.signupDeadline || detail.startAt || detail.endAt" class="comp-timeline">
      <span v-if="detail.signupDeadline">报名截止 {{ fmt(detail.signupDeadline) }}</span>
      <span v-if="detail.startAt">开始 {{ fmt(detail.startAt) }}</span>
      <span v-if="detail.endAt">结束 {{ fmt(detail.endAt) }}</span>
    </div>

    <p v-if="detail.rules" class="comp-rules">{{ detail.rules }}</p>

    <!-- ===== 作品赛 ===== -->
    <template v-if="detail.kind === 'contest'">
      <div class="entry-toolbar">
        <button class="btn primary" @click="showSubmit = !showSubmit">{{ showSubmit ? '收起' : '＋ 提交作品' }}</button>
        <span v-if="error" class="comp-error">{{ error }}</span>
      </div>

      <div v-if="showSubmit" class="entry-form">
        <input v-model="form.title" placeholder="作品标题（必填）" />
        <div class="upload-row">
          <button type="button" class="btn" :disabled="!!uploading" @click="pickImg">{{ uploading === 'img' ? '上传中…' : '🖼 上传封面图' }}</button>
          <button type="button" class="btn" :disabled="!!uploading" @click="pickFile">{{ uploading === 'file' ? '上传中…' : '📎 上传文件（图片/文档/压缩包）' }}</button>
          <span v-if="form.fileName" class="file-name">{{ form.fileName }}<button type="button" class="link-btn" @click="form.fileUrl = ''; form.fileName = ''">移除</button></span>
        </div>
        <img v-if="form.coverUrl" :src="form.coverUrl" class="cover-preview" alt="封面预览" referrerpolicy="no-referrer" />
        <input v-model="form.coverUrl" placeholder="封面图链接（也可直接粘贴）" />
        <input v-model="form.link" placeholder="作品链接（可选，画廊/视频/网盘）" />
        <textarea v-model="form.description" rows="2" placeholder="作品说明"></textarea>
        <div class="form-actions">
          <button class="btn" @click="showSubmit = false">取消</button>
          <button class="btn primary" :disabled="submitting" @click="submitEntry">{{ submitting ? '提交中…' : '提交' }}</button>
        </div>
        <input ref="imgInput" type="file" accept="image/*" hidden @change="onImgPicked" />
        <input ref="fileInput" type="file" hidden @change="onFilePicked" />
      </div>

      <p v-if="!detail.entries.length" class="comp-empty">还没有作品，来投第一件吧。</p>
      <div v-else class="entry-grid">
        <article v-for="(e, i) in detail.entries" :key="e.id" class="entry-card">
          <div class="entry-cover" :class="{ 'no-img': !e.coverUrl }">
            <img v-if="e.coverUrl" :src="e.coverUrl" :alt="e.title" loading="lazy" referrerpolicy="no-referrer" />
            <span v-else>{{ e.title.slice(0, 1) }}</span>
            <span v-if="i < 3" class="rank-badge">{{ ['🥇', '🥈', '🥉'][i] }}</span>
          </div>
          <div class="entry-body">
            <h3 class="entry-title">
              <a v-if="e.link" :href="e.link" target="_blank" rel="noopener noreferrer">{{ e.title }}</a>
              <span v-else>{{ e.title }}</span>
            </h3>
            <p class="entry-author">{{ e.authorName }}</p>
            <p v-if="e.description" class="entry-desc">{{ e.description }}</p>
            <a v-if="e.fileUrl" class="entry-file" :href="e.fileUrl" target="_blank" rel="noopener noreferrer">📎 {{ e.fileName || '附件' }}</a>
            <button class="vote-btn" :class="{ voted: detail.myVote === e.id }" @click="vote(e.id)">
              {{ detail.myVote === e.id ? '✓ 已投票' : '投票' }} · {{ e.votes }}
            </button>
          </div>
        </article>
      </div>

      <p v-if="detail.entries.length" class="comp-result">
        当前第一：<strong>{{ detail.entries[0].title }}</strong>（{{ detail.entries[0].votes }} 票）
      </p>
    </template>

    <!-- ===== 对战赛 ===== -->
    <template v-else-if="detail.kind === 'match'">
      <p v-if="!matches.length" class="comp-empty">对阵还没排出来。</p>
      <div v-else class="match-rounds">
        <div v-for="r in rounds" :key="r.round" class="match-round">
          <h3 class="round-title">{{ roundLabel(r.round) }}</h3>
          <div class="match-list">
            <div v-for="m in r.items" :key="m.id" class="match-row" :class="m.status">
              <span class="side" :class="{ win: m.winner && m.winner === m.sideA }">{{ m.sideA || '待定' }}</span>
              <span class="score">
                <template v-if="m.scoreA !== null || m.scoreB !== null">{{ m.scoreA ?? '-' }} : {{ m.scoreB ?? '-' }}</template>
                <template v-else>VS</template>
              </span>
              <span class="side right" :class="{ win: m.winner && m.winner === m.sideB }">{{ m.sideB || '待定' }}</span>
            </div>
          </div>
        </div>
      </div>
      <p v-if="champion" class="comp-result">🏆 冠军：<strong>{{ champion }}</strong></p>

      <!-- 管理员：对阵录入 / 比分 -->
      <div v-if="isAdmin" class="match-admin">
        <h4 class="match-admin-title">对阵管理</h4>
        <div v-for="m in matches" :key="m.id" class="match-edit-row">
          <span class="me-round">R{{ m.round }}</span>
          <input v-model="m.sideA" placeholder="A 方" />
          <input v-model="m.sideB" placeholder="B 方" />
          <input v-model.number="m.scoreA" type="number" class="me-score" placeholder="A分" />
          <input v-model.number="m.scoreB" type="number" class="me-score" placeholder="B分" />
          <select v-model="m.winner">
            <option :value="null">胜者</option>
            <option :value="m.sideA">{{ m.sideA || 'A 方' }}</option>
            <option :value="m.sideB">{{ m.sideB || 'B 方' }}</option>
          </select>
          <select v-model="m.status">
            <option value="pending">未开始</option>
            <option value="live">进行中</option>
            <option value="done">已结束</option>
          </select>
          <button class="mini-btn" @click="saveMatch(m)">保存</button>
          <button class="mini-btn danger" @click="removeMatch(m.id)">删除</button>
        </div>
        <div class="match-edit-row">
          <input v-model.number="newMatch.round" type="number" class="me-score" placeholder="轮次" />
          <input v-model="newMatch.sideA" placeholder="A 方" />
          <input v-model="newMatch.sideB" placeholder="B 方" />
          <button class="mini-btn" @click="addMatch">＋ 添加对阵</button>
        </div>
        <p v-if="error" class="comp-error">{{ error }}</p>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import request from '@/utils/request'
import { useUserStore } from '@/stores/user'
import { useCos } from '@/composables/useCos'

interface Entry { id: number; authorName: string; title: string; coverUrl: string | null; link: string | null; fileUrl: string | null; fileName: string | null; description: string | null; votes: number }
interface Match { id: number; round: number; slot: number; sideA: string | null; sideB: string | null; scoreA: number | null; scoreB: number | null; winner: string | null; status: string }
interface Detail {
  id: number; title: string; rules: string | null; kind: string; status: string
  signupDeadline: string | null; startAt: string | null; endAt: string | null
  entries: Entry[]; matches: Match[]; myVote: number | null
}

const props = defineProps<{ activityId: number }>()
const user = useUserStore()
const detail = ref<Detail | null>(null)
const showSubmit = ref(false)
const submitting = ref(false)
const error = ref('')
const form = ref({ title: '', coverUrl: '', link: '', fileUrl: '', fileName: '', description: '' })

// ===== 上传（图片 / 任意文件） =====
const { uploadFile } = useCos()
const imgInput = ref<HTMLInputElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref<'' | 'img' | 'file'>('')
const pickImg = () => imgInput.value?.click()
const pickFile = () => fileInput.value?.click()

async function onImgPicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  uploading.value = 'img'
  try { form.value.coverUrl = (await uploadFile(file, 'activity/entries')).url }
  catch { error.value = '图片上传失败' } finally { uploading.value = '' }
}
async function onFilePicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  uploading.value = 'file'
  try {
    form.value.fileUrl = (await uploadFile(file, 'activity/entries')).url
    form.value.fileName = file.name
  } catch { error.value = '文件上传失败' } finally { uploading.value = '' }
}

// ===== 管理员：对阵录入 =====
const isAdmin = computed(() => user.permissions?.includes('SuperAdmin') ?? false)
const newMatch = ref({ round: 1, sideA: '', sideB: '' })
async function addMatch() {
  if (!newMatch.value.sideA.trim() || !newMatch.value.sideB.trim()) { error.value = '请填双方名称'; return }
  error.value = ''
  try {
    await request.post(`/activities/${props.activityId}/competition/matches`, {
      round: newMatch.value.round || 1, slot: 0, sideA: newMatch.value.sideA.trim(), sideB: newMatch.value.sideB.trim(), status: 'pending'
    }, { silentError: true })
    newMatch.value = { round: newMatch.value.round, sideA: '', sideB: '' }
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '添加失败' }
}
async function saveMatch(m: Match) {
  error.value = ''
  try {
    await request.put(`/activities/${props.activityId}/competition/matches/${m.id}`, {
      round: m.round, slot: m.slot, sideA: m.sideA, sideB: m.sideB,
      scoreA: m.scoreA === null || m.scoreA === undefined || (m.scoreA as any) === '' ? null : Number(m.scoreA),
      scoreB: m.scoreB === null || m.scoreB === undefined || (m.scoreB as any) === '' ? null : Number(m.scoreB),
      winner: m.winner, status: m.status
    }, { silentError: true })
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '保存失败' }
}
async function removeMatch(id: number) {
  if (!confirm('删除这场对阵？')) return
  await request.delete(`/activities/${props.activityId}/competition/matches/${id}`)
  await load()
}

const matches = computed(() => detail.value?.matches || [])
const totalVotes = computed(() => (detail.value?.entries || []).reduce((s, e) => s + e.votes, 0))
const rounds = computed(() => {
  const map = new Map<number, Match[]>()
  for (const m of matches.value) {
    if (!map.has(m.round)) map.set(m.round, [])
    map.get(m.round)!.push(m)
  }
  return [...map.entries()].sort((a, b) => a[0] - b[0]).map(([round, items]) => ({ round, items }))
})
const champion = computed(() => {
  const maxRound = Math.max(0, ...matches.value.map(m => m.round))
  const final = matches.value.find(m => m.round === maxRound && m.winner)
  return final?.winner || ''
})
const roundLabel = (r: number) => {
  const max = Math.max(0, ...matches.value.map(m => m.round))
  if (r === max) return '决赛'
  if (r === max - 1) return '半决赛'
  return `第 ${r} 轮`
}
const fmt = (v: string) => new Date(v).toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })

async function load() {
  if (!props.activityId) return
  try { detail.value = await request.get<Detail>(`/activities/${props.activityId}/competition`, { silentError: true }) }
  catch { detail.value = null }
}

async function submitEntry() {
  if (!form.value.title.trim()) { error.value = '请填作品标题'; return }
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  submitting.value = true; error.value = ''
  try {
    await request.post(`/activities/${props.activityId}/competition/entries`, { ...form.value }, { silentError: true })
    showSubmit.value = false
    form.value = { title: '', coverUrl: '', link: '', fileUrl: '', fileName: '', description: '' }
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '提交失败' } finally { submitting.value = false }
}

async function vote(entryId: number) {
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  try {
    await request.post(`/activities/${props.activityId}/competition/entries/${entryId}/vote`, undefined, { silentError: true })
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '投票失败' }
}

onMounted(load)
watch(() => props.activityId, load)
</script>

<style scoped>
.competition { margin: 24px 0; padding: 22px 24px; border: 1px solid var(--border-line); border-radius: 14px; background: var(--paper-card); }
.comp-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.comp-head h2 { margin: 0; font-size: 1.15rem; }
.comp-sub { font-size: .78rem; color: var(--text-secondary); }
.comp-timeline { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px; }
.comp-timeline span { font-size: .75rem; color: var(--text-secondary); border: 1px solid var(--border-line); border-radius: 999px; padding: 3px 12px; }
.comp-rules { margin: 14px 0 0; font-size: .86rem; line-height: 1.8; color: var(--text-secondary); white-space: pre-wrap; }
.comp-error { color: var(--danger); font-size: .8rem; }
.comp-empty { padding: 30px 0; text-align: center; color: var(--text-secondary); font-size: .86rem; }
.comp-result { margin: 18px 0 0; padding-top: 14px; border-top: 1px dashed var(--border-line); font-size: .88rem; }

.entry-toolbar { display: flex; align-items: center; gap: 12px; margin: 18px 0 0; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
.entry-form { display: grid; gap: 8px; margin-top: 14px; padding: 14px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); }
.entry-form input, .entry-form textarea { padding: 8px 11px; border: 1px solid var(--border-line); border-radius: 7px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .85rem; }
.form-actions { display: flex; justify-content: flex-end; gap: 8px; }

.entry-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 210px), 1fr)); gap: 16px; margin-top: 18px; }
.entry-card { border: 1px solid var(--border-line); border-radius: 12px; overflow: hidden; background: var(--bg-main); }
.entry-cover { position: relative; aspect-ratio: 4 / 3; background: var(--bg-sub); display: grid; place-items: center; overflow: hidden; }
.entry-cover img { width: 100%; height: 100%; object-fit: cover; display: block; }
.entry-cover.no-img span { font-size: 2rem; font-weight: 700; color: var(--accent-color); }
.rank-badge { position: absolute; left: 8px; top: 8px; font-size: 1.1rem; }
.entry-body { padding: 12px 14px; display: grid; gap: 6px; }
.entry-title { margin: 0; font-size: .92rem; overflow-wrap: anywhere; }
.entry-title a { color: inherit; text-decoration: none; }
.entry-title a:hover { color: var(--accent-color); }
.entry-author { margin: 0; font-size: .75rem; color: var(--text-secondary); }
.entry-desc { margin: 0; font-size: .78rem; color: var(--text-secondary); line-height: 1.6; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.vote-btn { justify-self: start; padding: 6px 14px; border: 1px solid var(--accent-color); border-radius: 999px; background: transparent; color: var(--accent-color); cursor: pointer; font-size: .8rem; }
.vote-btn.voted { background: var(--accent-color); color: var(--on-accent); }

.match-rounds { display: grid; gap: 22px; margin-top: 18px; }
.round-title { margin: 0 0 10px; font-size: .95rem; }
.match-list { display: grid; gap: 8px; }
.match-row { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 10px; padding: 12px 16px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); font-size: .88rem; }
.match-row.done { border-color: var(--accent-color); }
.match-row .side { text-align: left; }
.match-row .side.right { text-align: right; }
.match-row .side.win { color: var(--accent-color); font-weight: 700; }
.match-row .score { font-variant-numeric: tabular-nums; color: var(--text-secondary); }

.upload-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.file-name { display: inline-flex; align-items: center; gap: 6px; font-size: .78rem; color: var(--text-secondary); }
.link-btn { border: 0; background: none; color: var(--danger); cursor: pointer; font-size: .75rem; padding: 0 2px; }
.cover-preview { max-width: 160px; border-radius: 8px; border: 1px solid var(--border-line); }
.entry-file { font-size: .76rem; color: var(--accent-color); text-decoration: none; overflow-wrap: anywhere; }
.entry-file:hover { text-decoration: underline; }

.match-admin { margin-top: 22px; padding-top: 16px; border-top: 1px dashed var(--border-line); }
.match-admin-title { margin: 0 0 10px; font-size: .9rem; color: var(--text-secondary); }
.match-edit-row { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin-bottom: 8px; }
.match-edit-row input, .match-edit-row select { padding: 6px 9px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .8rem; min-width: 0; }
.match-edit-row input { flex: 1 1 110px; }
.match-edit-row select { flex: 0 0 auto; }
.me-round { font-size: .72rem; color: var(--text-secondary); flex: 0 0 auto; }
.me-score { flex: 0 0 64px !important; width: 64px; }
.mini-btn { padding: 5px 11px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .76rem; flex: 0 0 auto; }
.mini-btn.danger { color: var(--danger); border-color: var(--danger); }
</style>
