<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, onMounted, watch, onUnmounted } from 'vue'
import { useUserStore } from '../../stores/user' 
import { useCos } from '../../composables/useCos'
import request from '../../utils/request'
import { useRouter } from 'vue-router'
const router = useRouter()
import ProfileCard from './ProfileCard.vue'
import PreferenceSettings from './PreferenceSettings.vue' // 🌟 引入偏好设置组件
import UserManualViewer from './UserManualViewer.vue' // 🌟 引入用户手册组件
import AiSettings from './AiSettings.vue'
import ClubCard from './ClubCard.vue'
import ArtworkInfiniteGrid from '../画廊展厅/ArtworkInfiniteGrid.vue'
const userStore = useUserStore()
const { uploadFile, isUploading } = useCos()

const experienceLogs = ref<{id:number;change:number;reason:string;createdAt:string}[]>([])
const experienceLoading = ref(false), experienceError = ref(''), experienceLoaded = ref(false), experienceMore = ref(false)
let experienceVersion = 0
async function loadExperience(more = false) {
  if (experienceLoading.value) return
  const version = ++experienceVersion
  experienceLoading.value = true; experienceError.value = ''
  try {
    const result = await request.get<{items:typeof experienceLogs.value;hasMore:boolean}>('/User/experience-history', { params: { offset: more ? experienceLogs.value.length : 0, limit: 20 } })
    if (version !== experienceVersion) return
    experienceLogs.value = more ? [...experienceLogs.value,...result.items] : result.items
    experienceMore.value = result.hasMore; experienceLoaded.value = true
  } catch { if (version === experienceVersion) experienceError.value = '经验记录加载失败，请重试。' }
  finally { if (version === experienceVersion) experienceLoading.value = false }
}
watch(() => userStore.token, () => { experienceVersion++; experienceLogs.value = []; experienceLoaded.value = false; experienceMore.value = false; experienceLoading.value = false; experienceError.value = '' })
onUnmounted(() => { experienceVersion++ })
const fileInput = ref<HTMLInputElement | null>(null)
const defaultAvatar = 'https://api.dicebear.com/7.x/avataaars/svg?seed=Felix'

// 🌟 新增 Tab 状态控制 (增加 'manual')
const activeTab = ref('overview') // 'overview' | 'favorites' | 'settings' | 'manual'

// 打开画廊作品详情
const openArtwork = (id: number | string) => router.push({ path: '/gallery', query: { workId: String(id) } })

// 经验条计算
const expPercentage = computed(() => {
  const exp = userStore.userInfo?.experience || 0
  if (exp <= 0) return 0
  const currentLevel = Math.floor(Math.sqrt(exp / 100))
  const currentLevelStartExp = Math.pow(currentLevel, 2) * 100
  const nextLevelStartExp = Math.pow(currentLevel + 1, 2) * 100
  const progressInLevel = exp - currentLevelStartExp
  const levelExpRange = nextLevelStartExp - currentLevelStartExp
  const percentage = (progressInLevel / levelExpRange) * 100
  return Math.min(Math.max(percentage, 0), 100)
})

// ---------- 强制刷新：完全替换 userInfo ----------
const fetchFullProfile = async () => {
  await userStore.fetchUserInfo(true)
}

// ---------- 页面加载 ----------
onMounted(() => {
  // 不管有没有数据，都强制刷新一次（保证最新）
  fetchFullProfile()
})

// ---------- 头像上传 ----------
const triggerUpload = () => {
  if (isUploading.value) return
  fileInput.value?.click()
}

const handleFileChange = async (e: Event) => {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) {
    alert('请选择有效的图片文件')
    return
  }
  try {
    const result = await uploadFile(file, 'avatars')
    const newAvatarUrl = result.url
    await request.patch('/User/update-profile', { avatar: newAvatarUrl })
    if (userStore.userInfo) {
      userStore.setUser({ ...userStore.userInfo, avatar: newAvatarUrl })
    }
  } catch (error: any) {
    console.error('头像处理失败:', error)
    alert(error.friendlyMessage || '上传失败，请稍后再试')
  } finally {
    target.value = ''
  }
}

// ---------- 退出登录 ----------
const handleLogout = () => {
  userStore.logout()
  void router.push('/')
}


type ContentType = 'artwork' | 'blog' | 'post' | 'character' | 'world'
interface ContentItem { id: string; title: string; status: string; updatedAt: string }
const contentTypes: { id: ContentType; label: string; create: string }[] = [
  { id: 'artwork', label: '画作', create: '/gallery?publish=1' },
  { id: 'blog', label: '博客', create: '/write/blog' },
  { id: 'post', label: '帖子', create: '/write/post' },
  { id: 'character', label: '人设', create: '/ocs/create' },
  { id: 'world', label: '世界观', create: '/world/create' },
]
const contentType = ref<ContentType>('artwork'), page = ref(1), total = ref(0)
const items = ref<ContentItem[]>([]), contentLoading = ref(false), contentError = ref('')
const currentType = computed(() => contentTypes.find(t => t.id === contentType.value)!)
let loadVersion = 0
async function loadContent() {
  const version = ++loadVersion
  items.value = []; total.value = 0; contentError.value = ''
  if (!userStore.isLoggedIn) { contentLoading.value = false; return }
  contentLoading.value = true
  try {
    const result = await request.get<{ items: ContentItem[]; total: number }>('/User/content', {
      params: { type: contentType.value, page: page.value }, silentError: true,
    })
    if (version !== loadVersion) return
    items.value = result.items; total.value = result.total
  } catch { if (version === loadVersion) contentError.value = '内容读取失败，请重试。' }
  finally { if (version === loadVersion) contentLoading.value = false }
}
function selectContent(type: ContentType) { contentType.value = type; page.value = 1 }
function contentLink(item: ContentItem) {
  switch (contentType.value) {
    case 'artwork': return { path: '/gallery', query: { workId: item.id } }
    case 'blog': case 'post': return { path: `/write/${contentType.value}`, query: { draft: item.id } }
    case 'character': return { path: `/ocs/edit/${item.id}` }
    case 'world': return { path: `/world/project/${item.id}` }
  }
}
watch([contentType, page, () => userStore.token], loadContent, { immediate: true })
onUnmounted(() => loadVersion++)
</script>

<template>
  <article class="user-center community-surface tone-profile" v-if="userStore.userInfo">
    <header class="user-header">
      <SectionTrace />
      <button class="avatar-wrapper" type="button" aria-label="更换头像" :disabled="isUploading" @click="triggerUpload">
        <img :src="userStore.userInfo.avatar || defaultAvatar" alt="我的头像" class="avatar" />
        <span class="avatar-hint">{{ isUploading ? '上传中…' : '更换头像' }}</span>
      </button>
      <input type="file" ref="fileInput" hidden accept="image/*" @change="handleFileChange" />
      <div class="user-info">
        <span class="eyebrow">个人中心 · 我的内容与社区生活</span>
        <h1>{{ userStore.userInfo.username }}</h1>
        <p>{{ userStore.userInfo.bio || userStore.userInfo.mood || '记录创作，参与社区，继续你的故事。' }}</p>
      </div>

    </header>
    <div class="stats-grid">
      <div class="stat-item"><RouterLink v-if="router.hasRoute('交易行')" :to="{ name: '交易行' }">经验兑换与配额 →</RouterLink><span>当前等级</span><strong>Lv.{{ userStore.userInfo.level }}</strong><div class="exp-bar"><div :style="{ width: expPercentage + '%' }"></div></div><small>{{ userStore.userInfo.experience }} 经验</small></div>
      <div class="stat-item"><span>佩戴头衔</span><strong>{{ userStore.userInfo.title || '太初散修' }}</strong><small>我的社区身份</small></div>
      <div class="stat-item"><span>最长连续签到</span><strong>{{ userStore.userInfo.maxSignStreak }} <small>天</small></strong><RouterLink to="/">去首页签到 →</RouterLink></div>
    </div>
    <details class="experience-history content-panel" @toggle="($event.target as HTMLDetailsElement).open && !experienceLoaded && loadExperience()">
      <summary>经验变动记录</summary><p class="history-note">查看已记录的收支原因。早期未记录的变动无法补回。</p>
      <p v-if="experienceError" role="alert">{{ experienceError }} <button class="btn-line" @click="loadExperience(experienceLogs.length > 0)">重试</button></p>
      <ol><li v-for="log in experienceLogs" :key="log.id"><div><strong>{{ log.reason || '经验变动' }}</strong><time>{{ new Date(log.createdAt).toLocaleString() }}</time></div><span :class="{ income: log.change > 0 }">{{ log.change > 0 ? '+' : '' }}{{ log.change }}</span></li></ol>
      <p v-if="experienceLoading" role="status">正在读取记录…</p><p v-else-if="experienceLoaded && !experienceLogs.length">暂无经验记录。</p>
      <button v-if="experienceMore" class="btn-line" :disabled="experienceLoading" @click="loadExperience(true)">加载更多</button>
    </details>
    <div class="content-layout">
      <section class="main-content">
        <nav class="content-tabs" aria-label="个人中心分区">
          <button v-for="tab in [{id:'overview',name:'我的内容'}, {id:'favorites',name:'我的收藏'}, {id:'community',name:'社区事务'}, {id:'settings',name:'偏好设置'}, {id:'ai',name:'AI 助手'}, {id:'manual',name:'操作手册'}]" :key="tab.id" :class="{active:activeTab===tab.id}" :aria-pressed="activeTab===tab.id" @click="activeTab=tab.id">{{ tab.name }}</button>
        </nav>
        <section v-if="activeTab === 'overview'" class="content-panel library-panel">
          <div class="section-heading"><div><h2>我的创作</h2><p>查看已发布内容，或继续未完成的草稿。仅你自己可见。</p></div><RouterLink class="btn-line" :to="currentType.create">新建{{ currentType.label }}</RouterLink></div>
          <nav class="content-filters" aria-label="内容类型"><button v-for="type in contentTypes" :key="type.id" :class="{active:contentType===type.id}" :aria-pressed="contentType===type.id" @click="selectContent(type.id)">{{ type.label }}</button></nav>
          <div v-if="contentLoading" class="content-state" role="status">正在读取{{ currentType.label }}…</div>
          <div v-else-if="contentError" class="content-state" role="alert">{{ contentError }} <button class="btn-line" @click="loadContent">重试</button></div>
          <template v-else>
            <div class="library-count">共 {{ total }} 项</div>
            <RouterLink v-for="item in items" :key="item.id" :to="contentLink(item)" class="library-item">
              <div><h3>{{ item.title || '无标题' }}</h3><time>{{ new Date(item.updatedAt).toLocaleDateString() }}</time></div><span class="content-status">{{ item.status }}</span><span class="item-action">{{ contentType === 'artwork' ? '查看' : '继续编辑' }} →</span>
            </RouterLink>
            <div v-if="!items.length" class="content-state"><p>还没有{{ currentType.label }}，从第一份创作开始。</p></div>
            <footer v-if="total > 12" class="pagination"><button class="btn-line" :disabled="page===1" @click="page--">上一页</button><span>{{ page }} / {{ Math.ceil(total / 12) }}</span><button class="btn-line" :disabled="page * 12 >= total" @click="page++">下一页</button></footer>
          </template>
        </section>
        <section v-else-if="activeTab === 'favorites'" class="content-panel library-panel">
          <div class="section-heading"><div><h2>我的收藏</h2><p>你收藏过的画廊作品，按收藏时间倒序。</p></div></div>
          <ArtworkInfiniteGrid favorites @on-click="openArtwork" />
        </section>
        <section v-else-if="activeTab === 'community'" class="community-content">
          <div class="community-links"><RouterLink to="/Project">多人协作 <span>项目、任务与文档 →</span></RouterLink><RouterLink to="/joint/my">我的联合 <span>参与和管理活动 →</span></RouterLink><RouterLink to="/battles/my">我的约战 <span>查看约战进展 →</span></RouterLink><RouterLink to="/activity/checkin/my">我的打卡 <span>回顾参与记录 →</span></RouterLink></div>
          <ClubCard />
        </section>
        <PreferenceSettings v-else-if="activeTab === 'settings'" />
        <AiSettings v-else-if="activeTab === 'ai'" />
        <UserManualViewer v-else-if="activeTab === 'manual'" />
      </section>
      <aside class="side-widgets"><ProfileCard :userInfo="userStore.userInfo" @update-success="fetchFullProfile" @logout="handleLogout" /></aside>
    </div>
  </article>
  <div v-else class="content-state"><p>个人资料暂未读取成功。</p><button class="btn-line" @click="fetchFullProfile">重新读取</button></div>
</template>

<style scoped>
.experience-history { margin: 0 0 24px; padding: 18px 22px; }
.experience-history summary { cursor: pointer; font-weight: 600; }
.history-note, .experience-history time { display: block; color: var(--text-secondary); font-size: .8rem; margin-top: 6px; }
.experience-history ol { list-style: none; padding: 0; }
.experience-history li { display: flex; justify-content: space-between; gap: 20px; padding: 14px 0; border-bottom: 1px solid var(--border-line); }
.experience-history strong { font-size: .9rem; font-weight: 500; }
.experience-history li > span { font-variant-numeric: tabular-nums; white-space: nowrap; }
.experience-history .income { color: var(--accent-color); }
.user-center { color: var(--text-primary); }
.user-header { display: flex; align-items: center; gap: 24px; }
.avatar-wrapper { position: relative; flex: 0 0 88px; width: 88px; height: 88px; border: 0; border-radius: 20px; overflow: hidden; padding: 0; cursor: pointer; }
.avatar { width: 100%; height: 100%; object-fit: cover; }
.avatar-hint { position: absolute; inset: auto 0 0; font-size: 11px; padding: 3px; background: var(--overlay); color: var(--on-overlay); }
.user-info { flex: 1; min-width: 0; }
.eyebrow { font-size: 12px; color: var(--accent-color); }
h1 { margin: 8px 0; font-size: clamp(24px, 3vw, 34px); overflow-wrap: anywhere; }
.user-info p { color: var(--text-secondary); line-height: 1.7; margin: 0; white-space: pre-wrap; }
.stats-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; margin: 24px 0; }
.stat-item { display: grid; gap: 10px; padding: 20px; border: 1px solid var(--border-line); border-radius: var(--radius-md); background: var(--paper-card); }
.stat-item > span, small, time { color: var(--text-secondary); font-size: 13px; }
.stat-item strong { font-size: 22px; }
.exp-bar { height: 4px; background: var(--bg-sub); border-radius: 4px; overflow: hidden; }
.exp-bar > div { height: 100%; background: var(--accent-color); }
.content-layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 24px; align-items: start; }
.main-content, .side-widgets { min-width: 0; }
.side-widgets:has(.edit-container) { grid-column: 1 / -1; grid-row: 1; }
.side-widgets :deep(.edit-container) { max-width: 760px; margin: 0 auto; }
.content-tabs, .content-filters { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 12px; }
.content-tabs { border-bottom: 1px solid var(--border-line); margin-bottom: 20px; }
.content-tabs button, .content-filters button { white-space: nowrap; padding: 10px 14px; border: 0; border-radius: 8px; color: var(--text-secondary); background: transparent; cursor: pointer; }
button.active { color: var(--accent-color); background: var(--accent-soft); font-weight: 600; }
.library-panel { padding: 24px; }
.section-heading { display: flex; justify-content: space-between; align-items: start; gap: 16px; margin-bottom: 20px; }
h2 { margin: 0 0 8px; font-size: 20px; }
.section-heading p { font-size: 13px; color: var(--text-secondary); line-height: 1.6; margin: 0; }
.section-heading > a { white-space: nowrap; }
.library-count { padding: 12px 0; color: var(--text-secondary); font-size: 12px; }
.library-item { display: flex; align-items: center; gap: 12px; padding: 18px 4px; border-top: 1px solid var(--border-line); color: var(--text-primary); text-decoration: none; }
.library-item:hover { background: var(--bg-sub); }
.library-item > div { flex: 1; min-width: 0; }
h3 { font-size: 15px; margin: 0 0 8px; overflow-wrap: anywhere; }
.content-status { font-size: 12px; background: var(--bg-sub); color: var(--text-secondary); padding: 4px 8px; border-radius: 6px; }
.item-action { color: var(--accent-color); font-size: 13px; }
.content-state { text-align: center; padding: 44px 16px; color: var(--text-secondary); }
a { color: var(--accent-color); }
.pagination { display: flex; justify-content: space-between; align-items: center; margin-top: 20px; }
.community-content { display: grid; gap: 24px; }
.community-links { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 12px; }
.community-links a { display: grid; gap: 8px; padding: 20px; border: 1px solid var(--border-line); border-radius: var(--radius-md); background: var(--paper-card); text-decoration: none; }
.community-links span { color: var(--text-secondary); font-size: 13px; }
@media(max-width: 1100px) { .content-layout { grid-template-columns: minmax(0,1fr); } }
@media(max-width: 600px) { .user-header { flex-wrap: wrap; padding: 20px; gap: 16px; } .stats-grid { gap: 8px; grid-template-columns: repeat(2, minmax(0, 1fr)); } .stat-item:first-child { grid-column: 1 / -1; } .stat-item { padding: 12px; } .stat-item strong { font-size: 17px; } .library-panel { padding: 16px; } .section-heading { flex-direction: column; } .item-action { display: none; } .community-links { grid-template-columns: 1fr; } }
</style>
