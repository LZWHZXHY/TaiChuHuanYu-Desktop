<template>
  <div class="novels-page content-page">
    <header class="content-header">
      <div>
        <p class="eyebrow">世界观 · 小说</p>
        <h1>小说书架</h1>
        <p class="intro">这里放世界观配套的长篇故事。作者可以连载，读者可以按章阅读。</p>
      </div>
      <div class="content-actions">
        <button class="primary" @click="openCreate">＋ 写小说</button>
      </div>
    </header>

    <div class="novel-toolbar">
      <button class="filter-btn" :class="{ active: !mine }" @click="setMine(false)">全部</button>
      <button class="filter-btn" :class="{ active: mine }" @click="setMine(true)">我的作品</button>
    </div>

    <p v-if="loading" class="state">正在加载书架…</p>
    <p v-else-if="error" class="state" role="alert">{{ error }}</p>
    <p v-else-if="!items.length" class="state">书架还是空的，来写第一部吧。</p>

    <div v-else class="novel-grid">
      <RouterLink v-for="n in items" :key="n.id" :to="`/novels/${n.id}`" class="novel-card">
        <div class="novel-cover">
          <img v-if="n.coverUrl" :src="n.coverUrl" :alt="n.title" loading="lazy" />
          <span v-else class="cover-text">{{ n.title.slice(0, 1) }}</span>
          <span v-if="!n.isPublic" class="draft-tag">草稿</span>
        </div>
        <div class="novel-body">
          <h2 class="novel-title">{{ n.title }}</h2>
          <p class="novel-desc">{{ n.description || '暂无简介' }}</p>
          <div class="novel-meta">
            <span>{{ n.authorName }}</span>
            <span>{{ n.chapterCount }} 章</span>
            <span>{{ formatWords(n.wordCount) }} 字</span>
            <span v-if="n.projectName" class="world-tag">🌌 {{ n.projectName }}</span>
          </div>
        </div>
      </RouterLink>
    </div>

    <!-- 新建小说 -->
    <div v-if="showCreate" class="mask" @click.self="showCreate = false">
      <div class="modal">
        <h2 class="modal-title">写一部新小说</h2>
        <label class="field"><span>标题 *</span><input v-model="form.title" placeholder="小说的名字" /></label>
        <label class="field"><span>简介</span><textarea v-model="form.description" rows="3" placeholder="一句话说明这是个什么故事"></textarea></label>
        <label class="field"><span>封面图链接</span><input v-model="form.coverUrl" placeholder="https://…（可选）" /></label>
        <label class="field"><span>绑定世界观</span>
          <select v-model="form.projectId">
            <option :value="null">不绑定（独立小说）</option>
            <option v-for="p in worldStore.projects" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </label>
        <div class="field-row">
          <label class="field"><span>状态</span>
            <select v-model="form.status">
              <option value="ongoing">连载中</option>
              <option value="completed">已完结</option>
              <option value="paused">暂停</option>
            </select>
          </label>
          <label class="field"><span>可见性</span>
            <select v-model="form.isPublic">
              <option :value="false">仅自己可见</option>
              <option :value="true">公开</option>
            </select>
          </label>
        </div>
        <p v-if="createError" class="err">{{ createError }}</p>
        <div class="modal-actions">
          <button class="btn" @click="showCreate = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="submitCreate">{{ saving ? '创建中…' : '创建' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { worldNovelApi, type NovelBrief, type NovelPayload } from '@/api/worldNovel'
import { useUserStore } from '@/stores/user'
import { useWorldStore } from '@/stores/world'

const router = useRouter()
const route = useRoute()
const user = useUserStore()
const worldStore = useWorldStore()
const items = ref<NovelBrief[]>([])
const loading = ref(false)
const error = ref('')
const mine = ref(false)
const showCreate = ref(false)
const saving = ref(false)
const createError = ref('')

const emptyForm = (): NovelPayload => ({ title: '', description: '', coverUrl: '', status: 'ongoing', isPublic: true, projectId: null })
const form = ref<NovelPayload>(emptyForm())

const formatWords = (n: number) => (n >= 10000 ? `${(n / 10000).toFixed(1)} 万` : `${n}`)

async function load() {
  loading.value = true
  error.value = ''
  try { items.value = await worldNovelApi.list({ mine: mine.value }) }
  catch (e: any) { error.value = e.friendlyMessage || '加载失败' }
  finally { loading.value = false }
}

function setMine(v: boolean) {
  if (v && !user.isLoggedIn) { error.value = '请先登录'; return }
  mine.value = v
  void load()
}

function openCreate() {
  if (!user.isLoggedIn) { router.push({ path: '/LoginRegister', query: { redirect: '/novels' } }); return }
  form.value = emptyForm()
  createError.value = ''
  showCreate.value = true
  if (!worldStore.projects.length) void worldStore.fetchProjects()
}

async function submitCreate() {
  if (!form.value.title.trim()) { createError.value = '请填写标题'; return }
  saving.value = true
  createError.value = ''
  try {
    const res = await worldNovelApi.create(form.value)
    showCreate.value = false
    await router.push(`/novels/${res.id}`)
  } catch (e: any) {
    createError.value = e.friendlyMessage || '创建失败'
  } finally { saving.value = false }
}

onMounted(() => {
  void load()
  // 从世界观详情页带参进入：直接打开新建并预选该世界观
  if (route.query.create === '1') {
    openCreate()
    if (route.query.project) form.value.projectId = String(route.query.project)
  }
})
</script>

<style scoped>
.novels-page { max-width: 1100px; margin: 0 auto; padding-bottom: 48px; }
.content-actions .primary { background: var(--accent-color); color: var(--on-accent); border: none; padding: 9px 20px; border-radius: 8px; cursor: pointer; font-size: .88rem; }
.novel-toolbar { display: flex; gap: 8px; margin: 18px 0 22px; }
.filter-btn { padding: 7px 18px; border: 1px solid var(--border-line); border-radius: 999px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .84rem; }
.filter-btn.active { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.state { padding: 48px 0; text-align: center; color: var(--text-secondary); }
.novel-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 220px), 1fr)); gap: 18px; }
.novel-card { border: 1px solid var(--border-line); border-radius: 12px; overflow: hidden; background: var(--paper-card); text-decoration: none; color: inherit; transition: transform .2s, border-color .2s; }
.novel-card:hover { transform: translateY(-3px); border-color: var(--accent-color); }
.novel-cover { position: relative; aspect-ratio: 3 / 4; background: linear-gradient(160deg, var(--accent-soft), var(--bg-sub)); display: grid; place-items: center; overflow: hidden; }
.novel-cover img { width: 100%; height: 100%; object-fit: cover; display: block; }
.cover-text { font-size: 3rem; font-weight: 800; color: var(--accent-color); }
.draft-tag { position: absolute; left: 8px; top: 8px; padding: 2px 9px; border-radius: 6px; background: rgba(0,0,0,.55); color: #fff; font-size: .68rem; }
.novel-body { padding: 12px 14px 16px; }
.novel-title { margin: 0 0 6px; font-size: .98rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.novel-desc { margin: 0 0 10px; font-size: .78rem; color: var(--text-secondary); line-height: 1.6; height: 2.6em; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.novel-meta { display: flex; flex-wrap: wrap; gap: 8px; font-size: .72rem; color: var(--text-secondary); }
.world-tag { color: var(--accent-color); }
.mask { position: fixed; inset: 0; z-index: 120; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.modal { width: min(560px, 100%); max-height: 90vh; overflow-y: auto; display: grid; gap: 14px; padding: 24px; border-radius: 14px; background: var(--paper-card); }
.modal-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .82rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: .88rem; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.err { margin: 0; color: var(--danger); font-size: .82rem; }
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .85rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
</style>
