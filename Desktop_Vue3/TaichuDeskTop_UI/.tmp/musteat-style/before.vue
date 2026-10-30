<template>
  <div class="musteat-page content-page">
    <header class="me-header">
      <div>
        <h1>🍽 必吃榜</h1>
        <p class="sub">游戏、动漫、小说、视频……我们觉得值得一试的资源推荐</p>
      </div>
      <div class="me-header-actions">
        <button v-if="userStore.isLoggedIn" class="btn" @click="openSubmit">✏️ 提交推荐</button>
        <button v-if="isAdmin" class="btn primary" @click="openEditor()">+ 添加推荐</button>
        <button v-if="isAdmin" class="btn" @click="togglePending">待审核 {{ showPending ? '▴' : '▾' }}</button>
      </div>
    </header>

    <!-- 待审核（管理员） -->
    <div v-if="isAdmin && showPending" class="pending-panel">
      <p v-if="pendingBusy" class="me-empty">加载中…</p>
      <p v-else-if="!pendingItems.length" class="me-empty">暂无待审核投稿</p>
      <div v-else class="pending-list">
        <div v-for="p in pendingItems" :key="p.id" class="pending-item">
          <div class="pending-info">
            <div class="pending-title-row"><strong>{{ p.title }}</strong><span class="me-cat">{{ categoryLabel(p.category) }}</span></div>
            <p v-if="p.description" class="pending-desc">{{ p.description }}</p>
            <a v-if="p.link" class="me-link" :href="p.link" target="_blank" rel="noopener noreferrer">资源链接 →</a>
          </div>
          <div class="pending-actions">
            <button class="btn primary" @click="approveItem(p)">通过</button>
            <button class="btn" @click="rejectItem(p)">驳回</button>
          </div>
        </div>
      </div>
    </div>

    <div class="cat-tabs">
      <button v-for="c in categories" :key="c.key" class="cat-tab" :class="{ active: filterCategory === c.key }" @click="filterCategory = c.key">{{ c.label }}</button>
    </div>

    <p v-if="loading" class="me-empty">加载中…</p>
    <p v-else-if="!filtered.length" class="me-empty">还没有推荐，先添加一条吧。</p>

    <div v-else class="me-grid">
      <article v-for="item in filtered" :key="item.id" class="me-card">
        <button class="me-cover" @click="openDetail(item)" :aria-label="'查看 ' + item.title">
          <img v-if="item.coverImage" :src="item.coverImage" :alt="item.title" loading="lazy" referrerpolicy="no-referrer" @error="($event.target as HTMLImageElement).style.display='none'" />
          <div v-else class="me-cover-placeholder">{{ categoryIcon(item.category) }}</div>
        </button>
        <div class="me-body">
          <div class="me-title-row">
            <h2 class="me-title" @click="openDetail(item)">{{ item.title }}</h2>
            <span class="me-cat">{{ categoryLabel(item.category) }}</span>
          </div>
          <p v-if="item.description" class="me-desc">{{ item.description }}</p>
          <p v-if="item.creator" class="me-creator">创作：{{ item.creator }}</p>
          <div class="me-rating-row">
            <span class="stars">{{ starText(item.averageScore) }}</span>
            <span class="me-rating-num">{{ item.averageScore > 0 ? item.averageScore : '暂无' }}</span>
            <span v-if="item.ratingCount" class="me-rating-count">({{ item.ratingCount }})</span>
          </div>
          <div class="me-footer">
            <a v-if="item.link" class="me-link" :href="item.link" target="_blank" rel="noopener noreferrer" @click.prevent="openLink(item.link!)">查看资源 →</a>
            <div v-if="isAdmin" class="me-admin">
              <button class="mini-btn" @click="openEditor(item)">编辑</button>
              <button class="mini-btn danger" @click="removeItem(item)">删除</button>
            </div>
          </div>
        </div>
      </article>
    </div>

    <!-- 详情弹窗：打分 + 评论/讨论 -->
    <div v-if="viewing" class="me-mask" @click.self="viewing = null">
      <div class="me-detail">
        <div class="me-detail-cover">
          <img v-if="viewing.coverImage" :src="viewing.coverImage" :alt="viewing.title" referrerpolicy="no-referrer" />
          <div v-else class="me-cover-placeholder">{{ categoryIcon(viewing.category) }}</div>
        </div>
        <div class="me-detail-body">
          <div class="me-detail-head">
            <h2>{{ viewing.title }}</h2>
            <span class="me-cat">{{ categoryLabel(viewing.category) }}</span>
          </div>
          <p v-if="viewing.description" class="me-detail-desc">{{ viewing.description }}</p>
          <p v-if="viewing.creator" class="me-creator">创作团队：{{ viewing.creator }}</p>
          <p v-if="viewing.source" class="me-source">来源：{{ viewing.source }}</p>
          <a v-if="viewing.link" class="me-link" :href="viewing.link" target="_blank" rel="noopener noreferrer" @click.prevent="openLink(viewing.link!)">查看资源 →</a>

          <!-- 打分 -->
          <section class="rate-box">
            <div class="rate-head">
              <span>综合评分</span>
              <strong>{{ rating?.averageScore ?? viewing.averageScore }} 分</strong>
              <span class="me-rating-count">({{ rating?.ratingCount ?? viewing.ratingCount }} 人)</span>
            </div>
            <div class="rate-stars">
              <button v-for="n in 5" :key="n" class="star-btn" :class="{ on: n <= (rating?.myScore ?? 0) }" :disabled="ratingBusy" @click="rateItem(n)" :title="n + ' 星'">★</button>
              <span v-if="rating?.myScore" class="my-score">我的评分：{{ rating.myScore }} 星</span>
              <span v-else class="my-score muted">点星打分</span>
            </div>
          </section>

          <!-- 评论 / 讨论 -->
          <section class="comment-box">
            <h3 class="comment-title">讨论 · {{ comments.length }} 条</h3>
            <div class="comment-input">
              <textarea v-model="commentText" rows="2" placeholder="说说你的看法…（支持回复楼中楼）"></textarea>
              <button class="btn primary" :disabled="commentBusy" @click="postComment()">{{ commentBusy ? '发送中…' : '发表评论' }}</button>
            </div>
            <ul class="comment-list">
              <li v-for="c in comments" :key="c.id" class="comment-item" :class="{ reply: !!c.parentId }">
                <div class="comment-meta">
                  <strong>{{ c.userName }}</strong>
                  <time>{{ formatTime(c.createdAt) }}</time>
                  <button v-if="isMine(c)" class="mini-btn danger" @click="deleteComment(c)">删除</button>
                </div>
                <p class="comment-content">{{ c.content }}</p>
                <button class="reply-btn" @click="replyTo(c)">回复</button>
              </li>
            </ul>
            <p v-if="!comments.length" class="me-empty">还没有讨论，来说两句吧。</p>
            <p v-if="replyParent" class="reply-hint">正在回复 {{ replyParent.userName }} <button class="mini-btn" @click="replyParent = null">取消</button></p>
          </section>
        </div>
      </div>
    </div>

    <!-- 管理编辑器 -->
    <div v-if="showEditor" class="me-mask" @click.self="showEditor = false">
      <div class="me-editor">
        <h2 class="me-editor-title">{{ submitMode ? '提交推荐（待审核）' : (editorId ? '编辑推荐' : '添加推荐') }}</h2>
        <div class="field-row">
          <label class="field"><span>分类</span>
            <select v-model="form.category">
              <option v-for="c in categories.filter(x => x.key)" :key="c.key" :value="c.key">{{ c.label }}</option>
            </select>
          </label>
          <label class="field"><span>排序（越小越前，可小数）</span><input v-model.number="form.sortOrder" type="number" step="any" /></label>
        </div>
        <label class="field"><span>标题</span><input v-model="form.title" placeholder="游戏/动漫/小说/视频名称" /></label>
        <label class="field"><span>创作团队 / 作者（可选）</span><input v-model="form.creator" placeholder="团队名 / 作者名" /></label>
        <label class="field"><span>推荐理由</span><textarea v-model="form.description" rows="3" placeholder="一句话说说为什么推荐"></textarea></label>
        <label class="field"><span>资源链接</span>
          <div class="link-row">
            <input v-model="form.link" placeholder="https://…（网页 / 网盘 / B站）" @blur="maybeFetchCover" />
            <button type="button" class="btn" :disabled="coverFetching" @click="fetchCover">{{ coverFetching ? '抓取中…' : '🔍 抓取封面' }}</button>
          </div>
        </label>
        <label class="field"><span>来源（可选）</span><input v-model="form.source" placeholder="出处 / 平台" /></label>
        <div class="field">
          <span>封面图（可选）</span>
          <div class="cover-row">
            <img v-if="form.coverImage" :src="form.coverImage" class="cover-preview" alt="封面" referrerpolicy="no-referrer" />
            <button type="button" class="btn" :disabled="uploading" @click="pickCover">{{ uploading ? '上传中…' : '📷 上传封面' }}</button>
            <button v-if="form.coverImage" type="button" class="mini-btn danger" @click="form.coverImage = ''">清除</button>
            <input ref="fileInput" type="file" accept="image/*" hidden @change="onCoverPicked" />
          </div>
        </div>
        <label class="check"><input v-model="form.isVisible" type="checkbox" /> 公开展示</label>
        <div class="me-editor-actions">
          <button class="btn" @click="showEditor = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveItem">{{ saving ? '提交中…' : (submitMode ? '提交审核' : '保存') }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import request from '@/utils/request'
import { mustEatApi, type MustEatItem, type MustEatPayload, type MustEatRatingInfo } from '@/api/mustEat'
import { useUserStore } from '@/stores/user'
import { useCos } from '@/composables/useCos'

const userStore = useUserStore()
const items = ref<MustEatItem[]>([])
const loading = ref(false)
const filterCategory = ref('')
const showEditor = ref(false)
const editorId = ref<number | null>(null)
const saving = ref(false)
const uploading = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const submitMode = ref(false)
const showPending = ref(false)
const pendingItems = ref<MustEatItem[]>([])
const pendingBusy = ref(false)
const coverFetching = ref(false)
const { uploadFile } = useCos()

const isAdmin = computed(() => userStore.permissions?.includes('SuperAdmin') ?? false)

// 详情 / 评分 / 评论
interface CommentItem { id: string; userId: string; userName: string; content: string; parentId: string | null; createdAt: string }
const viewing = ref<MustEatItem | null>(null)
const rating = ref<MustEatRatingInfo | null>(null)
const ratingBusy = ref(false)
const comments = ref<CommentItem[]>([])
const commentText = ref('')
const commentBusy = ref(false)
const replyParent = ref<CommentItem | null>(null)

const categories = [
  { key: '', label: '全部' },
  { key: '游戏', label: '🎮 游戏' },
  { key: '动漫', label: '📺 动漫' },
  { key: '小说', label: '📖 小说' },
  { key: '视频', label: '🎬 视频' },
  { key: '柴圈', label: '◎ 柴圈' },
  { key: '其他', label: '🍽 其他' },
]
const categoryLabel = (k: string) => ({ 游戏: '游戏', 动漫: '动漫', 小说: '小说', 视频: '视频', 柴圈: '柴圈', 其他: '其他' } as Record<string, string>)[k] || '其他'
const categoryIcon = (k: string) => ({ 游戏: '🎮', 动漫: '📺', 小说: '📖', 视频: '🎬', 柴圈: '◎' } as Record<string, string>)[k] || '🍽'

const filtered = computed(() => filterCategory.value ? items.value.filter(x => x.category === filterCategory.value) : items.value)

const emptyForm = (): MustEatPayload => ({ category: '游戏', title: '', coverImage: '', description: '', creator: '', link: '', source: '', sortOrder: 0, isVisible: true })
const form = ref<MustEatPayload>(emptyForm())

const starText = (avg: number) => '★'.repeat(Math.round(avg || 0)) + '☆'.repeat(5 - Math.round(avg || 0))
const formatTime = (v: string) => v ? new Date(v).toLocaleString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) : ''
const isMine = (c: CommentItem) => userStore.userId === c.userId

async function load() {
  loading.value = true
  try { items.value = await mustEatApi.list() } catch { items.value = [] } finally { loading.value = false }
}

async function openDetail(item: MustEatItem) {
  viewing.value = item
  rating.value = null
  comments.value = []
  commentText.value = ''
  replyParent.value = null
  await Promise.all([loadRating(item.id), loadComments(item.id)])
}

async function loadRating(id: number) {
  try { rating.value = await mustEatApi.getRating(id) } catch { /* ignore */ }
}
async function rateItem(score: number) {
  if (!viewing.value) return
  if (!userStore.isLoggedIn) { alert('请先登录再打分'); return }
  ratingBusy.value = true
  try {
    rating.value = await mustEatApi.rate(viewing.value.id, score)
    const it = items.value.find(x => x.id === viewing.value!.id)
    if (it && rating.value) { it.averageScore = rating.value.averageScore; it.ratingCount = rating.value.ratingCount }
  } catch (e: any) { alert(e.friendlyMessage || '打分失败') } finally { ratingBusy.value = false }
}

async function loadComments(id: number) {
  try { comments.value = await request.get<CommentItem[]>(`/ChaiComment/MustEat/${id}`, { silentError: true }) } catch { comments.value = [] }
}
async function postComment() {
  if (!viewing.value || !commentText.value.trim()) return
  if (!userStore.isLoggedIn) { alert('请先登录再评论'); return }
  commentBusy.value = true
  try {
    await request.post('/ChaiComment', { targetType: 'MustEat', targetId: String(viewing.value.id), content: commentText.value.trim(), parentId: replyParent.value?.id ?? null })
    commentText.value = ''
    replyParent.value = null
    await loadComments(viewing.value.id)
  } catch (e: any) { alert(e.friendlyMessage || '评论失败') } finally { commentBusy.value = false }
}
function replyTo(c: CommentItem) { replyParent.value = c }
async function deleteComment(c: CommentItem) {
  if (!confirm('删除这条评论？')) return
  await request.delete(`/ChaiComment/${c.id}`)
  if (viewing.value) await loadComments(viewing.value.id)
}

function openEditor(item?: MustEatItem) {
  submitMode.value = false
  editorId.value = item?.id ?? null
  form.value = item ? { category: item.category, title: item.title, coverImage: item.coverImage || '', description: item.description || '', creator: item.creator || '', link: item.link || '', source: item.source || '', sortOrder: item.sortOrder, isVisible: item.isVisible } : emptyForm()
  showEditor.value = true
}

function openSubmit() {
  if (!userStore.isLoggedIn) { alert('请先登录再投稿'); return }
  submitMode.value = true
  editorId.value = null
  form.value = emptyForm()
  showEditor.value = true
}

async function loadPending() {
  pendingBusy.value = true
  try { pendingItems.value = await mustEatApi.pending() } catch { pendingItems.value = [] } finally { pendingBusy.value = false }
}
async function togglePending() {
  showPending.value = !showPending.value
  if (showPending.value) await loadPending()
}
async function approveItem(item: MustEatItem) {
  await mustEatApi.approve(item.id)
  await loadPending()
  await load()
}
async function rejectItem(item: MustEatItem) {
  if (!confirm(`驳回「${item.title}」？`)) return
  await mustEatApi.reject(item.id)
  await loadPending()
}

async function saveItem() {
  if (!form.value.title.trim()) { alert('请填写标题'); return }
  // 排序可能是空字符串（v-model.number 清空时），统一转成数字，避免后端绑定失败
  form.value.sortOrder = Number(form.value.sortOrder) || 0
  saving.value = true
  try {
    if (submitMode.value) {
      const res = await mustEatApi.submit(form.value)
      showEditor.value = false
      submitMode.value = false
      alert(res.message || '已提交，等待审核')
    } else if (editorId.value) {
      await mustEatApi.update(editorId.value, form.value)
      showEditor.value = false
      await load()
    } else {
      await mustEatApi.create(form.value)
      showEditor.value = false
      await load()
    }
  } catch (e: any) { alert(e.friendlyMessage || e.message || '保存失败') } finally { saving.value = false }
}

async function removeItem(item: MustEatItem) {
  if (!confirm(`确认删除「${item.title}」？`)) return
  await mustEatApi.remove(item.id)
  await load()
}

async function fetchCover() {
  const url = form.value.link?.trim()
  if (!url) { alert('先填资源链接'); return }
  coverFetching.value = true
  try {
    const res = await mustEatApi.fetchCover(url)
    if (res.coverImage) form.value.coverImage = res.coverImage
    if (res.title && !form.value.title.trim()) form.value.title = res.title
    if (!res.coverImage) alert('没抓到封面，可能不是 B站视频链接')
  } catch (e: any) { alert(e.friendlyMessage || '抓取失败') } finally { coverFetching.value = false }
}

// 填写 B站链接后自动抓封面（没填封面时才抓）
function maybeFetchCover() {
  const url = form.value.link?.trim()
  if (!url || form.value.coverImage) return
  if (!/bilibili\.com|BV[0-9A-Za-z]{10}|av\d+/i.test(url)) return
  fetchCover()
}

function pickCover() { fileInput.value?.click() }
async function onCoverPicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  uploading.value = true
  try {
    const { url } = await uploadFile(file, 'musteat')
    form.value.coverImage = url
  } catch { alert('封面上传失败') } finally { uploading.value = false }
}

function openLink(url: string) {
  const host = (window as any).chrome?.webview
  if (host) host.postMessage({ cmd: 'load_external_url', url })
  else window.open(url, '_blank', 'noopener,noreferrer')
}

onMounted(load)
</script>

<style scoped>
.musteat-page { max-width: 1100px; margin: 0 auto; padding: 0 0 48px; }
.me-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 24px 0 20px; }
.me-header h1 { margin: 0; font-size: clamp(1.7rem, 3.4vw, 2.3rem); }
.me-header .sub { margin: 8px 0 0; color: var(--text-secondary); font-size: .9rem; }
.me-header-actions { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
.pending-panel { margin-bottom: 20px; padding: 16px; border: 1px solid var(--accent-color); border-radius: 12px; background: var(--accent-soft); }
.pending-list { display: grid; gap: 10px; }
.pending-item { display: flex; justify-content: space-between; gap: 12px; padding: 12px; border-radius: 8px; background: var(--paper-card); border: 1px solid var(--border-line); }
.pending-info { min-width: 0; flex: 1; display: grid; gap: 6px; }
.pending-title-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.pending-desc { margin: 0; font-size: .82rem; color: var(--text-secondary); line-height: 1.5; }
.pending-actions { display: flex; flex-direction: column; gap: 6px; flex-shrink: 0; }
.cat-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px; }
.cat-tab { padding: 7px 16px; border: 1px solid var(--border-line); border-radius: 18px; background: transparent; color: var(--text-secondary); cursor: pointer; font-size: .82rem; }
.cat-tab.active { background: var(--text-primary); color: var(--paper-card); border-color: var(--text-primary); }
.me-empty { text-align: center; color: var(--text-secondary); padding: 32px 0; }
.me-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
.me-card { display: flex; flex-direction: column; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); overflow: hidden; }
.me-cover { aspect-ratio: 16 / 9; background: var(--bg-sub); display: flex; align-items: center; justify-content: center; overflow: hidden; border: none; padding: 0; cursor: pointer; }
.me-cover img { width: 100%; height: 100%; object-fit: cover; display: block; }
.me-cover-placeholder { font-size: 2.4rem; }
.me-body { padding: 14px 16px 16px; display: flex; flex-direction: column; gap: 8px; flex: 1; }
.me-title-row { display: flex; align-items: center; gap: 8px; justify-content: space-between; }
.me-title { margin: 0; font-size: 1.02rem; overflow-wrap: anywhere; cursor: pointer; }
.me-title:hover { color: var(--accent-color); }
.me-cat { flex-shrink: 0; font-size: .68rem; padding: 1px 8px; border-radius: 8px; background: var(--accent-soft); color: var(--accent-color); }
.me-desc { margin: 0; font-size: .84rem; color: var(--text-secondary); line-height: 1.6; flex: 1; }
.me-rating-row { display: flex; align-items: center; gap: 6px; font-size: .82rem; }
.stars { color: #f0a500; letter-spacing: 1px; }
.me-rating-num { font-weight: 600; color: var(--text-primary); }
.me-rating-count { color: var(--text-secondary); font-size: .74rem; }
.me-source { margin: 0; font-size: .74rem; color: var(--text-secondary); }
.me-creator { margin: 0; font-size: .78rem; color: var(--text-secondary); }
.me-footer { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-top: 4px; }
.me-link { color: var(--accent-color); font-size: .84rem; font-weight: 600; text-decoration: none; }
.me-link:hover { text-decoration: underline; }
.me-admin { display: flex; gap: 6px; }
.mini-btn { padding: 3px 9px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .72rem; }
.mini-btn.danger { color: var(--danger); }
/* 详情 */
.me-mask { position: fixed; inset: 0; z-index: 100; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; padding: 20px; }
.me-detail { width: min(640px, 100%); max-height: 90vh; overflow-y: auto; background: var(--paper-card); border-radius: 14px; }
.me-detail-cover { aspect-ratio: 21 / 9; background: var(--bg-sub); display: flex; align-items: center; justify-content: center; overflow: hidden; }
.me-detail-cover img { width: 100%; height: 100%; object-fit: cover; }
.me-detail-body { padding: 20px 24px 24px; display: grid; gap: 12px; }
.me-detail-head { display: flex; align-items: center; gap: 10px; justify-content: space-between; }
.me-detail-head h2 { margin: 0; font-size: 1.25rem; }
.me-detail-desc { margin: 0; font-size: .9rem; color: var(--text-secondary); line-height: 1.7; }
/* 打分 */
.rate-box { border: 1px solid var(--border-line); border-radius: 10px; padding: 14px; display: grid; gap: 8px; }
.rate-head { display: flex; align-items: baseline; gap: 8px; font-size: .85rem; color: var(--text-secondary); }
.rate-head strong { font-size: 1.1rem; color: var(--text-primary); }
.rate-stars { display: flex; align-items: center; gap: 4px; }
.star-btn { background: none; border: none; font-size: 1.5rem; color: var(--border-line); cursor: pointer; padding: 0 2px; line-height: 1; }
.star-btn.on { color: #f0a500; }
.star-btn:disabled { opacity: .5; cursor: default; }
.my-score { margin-left: 10px; font-size: .78rem; color: var(--text-secondary); }
.my-score.muted { opacity: .7; }
/* 评论 */
.comment-box { border-top: 1px dashed var(--border-line); padding-top: 14px; display: grid; gap: 10px; }
.comment-title { margin: 0; font-size: .95rem; }
.comment-input { display: grid; gap: 8px; }
.comment-input textarea { padding: 10px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .85rem; font-family: inherit; resize: vertical; }
.comment-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.comment-item { padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); }
.comment-item.reply { margin-left: 24px; }
.comment-meta { display: flex; align-items: center; gap: 10px; font-size: .78rem; color: var(--text-secondary); }
.comment-meta strong { color: var(--text-primary); }
.comment-content { margin: 6px 0; font-size: .85rem; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.reply-btn { background: none; border: none; color: var(--accent-color); font-size: .75rem; cursor: pointer; padding: 0; }
.reply-hint { margin: 0; font-size: .78rem; color: var(--text-secondary); }
/* 编辑器 */
.me-editor { width: min(560px, 100%); max-height: 90vh; overflow-y: auto; background: var(--paper-card); border-radius: 12px; padding: 24px; display: grid; gap: 14px; }
.me-editor-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .8rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; font-family: inherit; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.cover-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.link-row { display: flex; gap: 8px; align-items: center; }
.link-row input { flex: 1; min-width: 0; }
.cover-preview { width: 120px; height: 68px; object-fit: cover; border-radius: 6px; border: 1px solid var(--border-line); }
.check { display: flex; align-items: center; gap: 6px; font-size: .85rem; cursor: pointer; }
.me-editor-actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
@media (max-width: 560px) { .me-grid { grid-template-columns: 1fr; } }
</style>
