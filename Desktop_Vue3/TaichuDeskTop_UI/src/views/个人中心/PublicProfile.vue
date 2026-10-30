<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import ProfileGuestbook from './ProfileGuestbook.vue'
import ActivityPanel from './ActivityPanel.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import { TradeApi } from '@/api/trade'
import UserTitle from '@/components/UserTitle.vue'
import ReportButton from '@/components/ReportButton.vue'
import { profileDecorApi, type PublicDecor } from '@/api/appearance'
import { useSiteBackground } from '@/composables/useSiteBackground'
import { profileApi, type PublicProfile } from '@/api/profile'
import { notePublishApi } from '@/api/NotePublish'
import { formatPostCard, relativeTime, type PostCard } from '@/utils/postCard'
import { boardOf, FORUM_NAME } from '@/utils/forumBoards'

const props = defineProps<{ id: string; isSelf?: boolean }>()
const route = useRoute()
const guestbookCount = ref(0)

const profile = ref<PublicProfile | null>(null)
const loading = ref(true), notFound = ref(false), error = ref('')
// 对方在交易行佩戴的头像框
const frame = ref<string | null>(null)
// 主页装扮：横幅 + 整页背景（整页背景临时盖住访客自己的网站背景，离开主页恢复）
const decor = ref<PublicDecor>({ banner: null, page: null })
const siteBg = useSiteBackground()
const FOCUS_POS: Record<string, string> = { center: 'center', top: 'center top', bottom: 'center bottom', left: 'left center', right: 'right center' }
const bannerStyle = computed(() => {
  const b = decor.value.banner
  if (!b || b.kind === 'video') return undefined
  return { backgroundImage: `url(${JSON.stringify(b.url)})`, backgroundPosition: FOCUS_POS[b.focus] ?? 'center' }
})
async function loadDecor(userId: string) {
  try {
    decor.value = await profileDecorApi.of(userId)
    siteBg.setVisit(decor.value.page)
  } catch { decor.value = { banner: null, page: null }; siteBg.setVisit(null) }
}
onBeforeUnmount(() => siteBg.setVisit(null))

type Tab = 'posts' | 'artworks' | 'worlds' | 'novels' | 'characters' | 'chai' | 'guestbook'
const tab = ref<Tab>('posts')
const tabs = computed(() => {
  const c = profile.value?.counts
  return [
    { id: 'posts' as Tab, label: '帖子与博客', count: c?.posts ?? 0 },
    { id: 'artworks' as Tab, label: '画作', count: c?.artworks ?? 0 },
    { id: 'worlds' as Tab, label: '世界观', count: c?.worlds ?? 0 },
    { id: 'novels' as Tab, label: '小说', count: c?.novels ?? 0 },
    { id: 'characters' as Tab, label: '人设', count: c?.characters ?? 0 },
    { id: 'chai' as Tab, label: '柴圈作品', count: c?.chaiWorks ?? 0 },
    { id: 'guestbook' as Tab, label: '留言板', count: guestbookCount.value },
  // 小说、柴圈作品没有的时候不占位置（旧后端也没有这两项）
  ].filter(t => !(t.id === 'novels' || t.id === 'chai') || t.count > 0)
})

async function load() {
  loading.value = true; notFound.value = false; error.value = ''
  try {
    profile.value = await profileApi.getPublicProfile(props.id)
    frame.value = null
    TradeApi.appearance(profile.value.id).then(a => { frame.value = a.frame }).catch(() => null)
    void loadDecor(profile.value.id)
    // 从通知点进来时直接打开留言板；否则默认打开第一个有内容的分区。
    tab.value = route.query.tab === 'guestbook' ? 'guestbook' : tabs.value.find(t => t.id !== 'guestbook' && t.count > 0)?.id ?? 'posts'
    if (profile.value.counts.posts > 0) loadPosts(true)
  } catch (e: any) {
    if (e?.response?.status === 404 || e?.response?.status === 400) notFound.value = true
    else error.value = e?.friendlyMessage || '主页加载失败，请重试。'
  } finally { loading.value = false }
}
onMounted(load)

// 帖子与博客：分页读取该用户发布的内容。
const posts = ref<PostCard[]>([])
const postsLoading = ref(false), postsMore = ref(false), postsError = ref('')
let postPage = 0
const POST_PAGE = 10
async function loadPosts(reset = false) {
  if (postsLoading.value) return
  postsLoading.value = true; postsError.value = ''
  const next = reset ? 1 : postPage + 1
  try {
    const rows = (await notePublishApi.getPublicStream({ author: props.id, page: next, pageSize: POST_PAGE }) || []).map(formatPostCard)
    posts.value = reset ? rows : posts.value.concat(rows)
    postPage = next; postsMore.value = rows.length === POST_PAGE
  } catch { postsError.value = '帖子加载失败，请重试。' }
  finally { postsLoading.value = false }
}

const joined = computed(() => {
  const d = profile.value?.joinedAt ? new Date(profile.value.joinedAt) : null
  return d && !isNaN(d.getTime()) ? `${d.getFullYear()} 年 ${d.getMonth() + 1} 月` : ''
})
const memberNo = computed(() => String(props.id).replace(/-/g, '').slice(0, 8).toUpperCase())
const place = (post: PostCard) => {
  const { board, sub } = boardOf(post.tags)
  return board ? [board.tag, sub?.tag].filter(Boolean).join(' · ') : FORUM_NAME
}
const hide = (event: Event) => { (event.target as HTMLImageElement).hidden = true }
</script>

<template>
  <article class="public-profile community-surface tone-profile">
    <ParticleState v-if="loading" class="profile-state" text="正在读取成员档案" />

    <div v-else-if="notFound" class="profile-state">
      <h1>没有找到这位用户</h1>
      <p>对方可能已注销，或者链接有误。</p>
      <RouterLink to="/" class="btn-line">回到首页</RouterLink>
    </div>

    <div v-else-if="error" class="profile-state" role="alert">
      <p>{{ error }}</p>
      <button class="btn-line" @click="load">重新加载</button>
    </div>

    <template v-else-if="profile">
      <header class="user-header profile-header" :class="{ 'with-banner': !!decor.banner }">
        <div v-if="decor.banner" class="profile-banner" :style="bannerStyle" aria-hidden="true">
          <video v-if="decor.banner.kind === 'video'" :src="decor.banner.url" :poster="decor.banner.poster ?? undefined" :style="{ objectPosition: FOCUS_POS[decor.banner.focus] ?? 'center' }" autoplay muted loop playsinline disablepictureinpicture></video>
        </div>
        <SectionTrace />
        <AvatarFrame :frame="frame" class="profile-avatar-slot">
          <div class="profile-avatar">
            <span aria-hidden="true">{{ profile.username?.[0] || '?' }}</span>
            <img v-if="profile.avatar" :src="profile.avatar" alt="" @error="hide" />
          </div>
        </AvatarFrame>
        <div class="profile-info">
          <span class="eyebrow">MEMBER FILE · No.{{ memberNo }}</span>
          <h1>{{ profile.username }}</h1>
          <p class="profile-tags">
            <b>Lv.{{ profile.level }}</b>
            <span v-if="!profile.title">太初散修</span><UserTitle v-else :title="profile.title" class="profile-title-badge" />
          </p>
          <p class="profile-bio">{{ profile.bio || profile.mood || '这位成员还没有写简介。' }}</p>
          <p class="profile-meta">
            <span v-if="joined">加入于 {{ joined }}</span>
            <span>最长连续签到 {{ profile.maxSignStreak }} 天</span>
            <span>获赞 {{ profile.likesReceived }}</span>
          </p>
        </div>
        <RouterLink v-if="isSelf" class="btn-line self-link" :to="{ name: 'UserCenter', params: { id } }">返回个人中心</RouterLink>
        <ReportButton v-else class="profile-report" target-type="user" :target-id="profile.id" :target-title="profile.username" :target-url="`/user/${profile.id}`" text="举报这位用户" />
      </header>
      <p v-if="isSelf" class="self-note" role="status">这是别人看到的你的主页。</p>

      <!-- 社区活跃：活跃日历、活跃天数、各类活动次数 -->
      <ActivityPanel :user-id="profile.id" />

      <nav class="profile-tabs" aria-label="主页分区">
        <button v-for="item in tabs" :key="item.id" type="button" :class="{ active: tab === item.id }" :aria-pressed="tab === item.id" @click="tab = item.id">
          <strong>{{ item.count }}</strong><span>{{ item.label }}</span>
        </button>
      </nav>

      <!-- 帖子与博客 -->
      <section v-if="tab === 'posts'" class="profile-panel" aria-label="帖子与博客">
        <ParticleState v-if="!profile.counts.posts" class="panel-empty" kind="empty" preset="forum" text="还没有公开的帖子或博客。" />
        <template v-else>
          <ol class="post-list">
            <li v-for="post in posts" :key="post.id">
              <RouterLink :to="{ name: 'CommunityStream', query: { post: post.id } }" class="post-item">
                <span v-if="post.cardCover" class="post-thumb"><img :src="post.cardCover" alt="" loading="lazy" @error="hide" /></span>
                <span class="post-body">
                  <span class="post-meta"><b :class="'type-' + post.type">{{ post.type === 'blog' ? '博客' : '帖子' }}</b>{{ place(post) }}</span>
                  <strong>{{ post.title || '无标题' }}</strong>
                  <small v-if="post.content">{{ post.content }}</small>
                </span>
                <span class="post-side">
                  <time :datetime="post.publishedAt">{{ relativeTime(post.publishedAt || post.createdAt) }}</time>
                  <span v-if="post.commentsCount">{{ post.commentsCount }} 评论</span>
                </span>
              </RouterLink>
            </li>
          </ol>
          <p v-if="postsLoading" class="panel-empty" role="status">正在加载…</p>
          <p v-if="postsError" class="panel-empty" role="alert">{{ postsError }} <button class="btn-line" @click="loadPosts(!posts.length)">重试</button></p>
          <button v-if="postsMore && !postsLoading" class="btn-line load-more" @click="loadPosts()">加载更多</button>
        </template>
      </section>

      <!-- 画作 -->
      <section v-else-if="tab === 'artworks'" class="profile-panel" aria-label="画作">
        <ParticleState v-if="!profile.artworks.length" class="panel-empty" kind="empty" preset="gallery" text="还没有公开的画作。" />
        <template v-else>
          <div class="tile-grid">
            <RouterLink v-for="work in profile.artworks" :key="work.id" class="art-tile" data-depth :to="{ path: '/gallery', query: { workId: String(work.id) } }" :aria-label="`查看画作：${work.title}`">
              <img v-if="work.coverImageUrl" :src="work.coverImageUrl" alt="" loading="lazy" @error="hide" />
              <span>{{ work.title || '无标题' }}</span>
            </RouterLink>
          </div>
          <p v-if="profile.counts.artworks > profile.artworks.length" class="panel-note">显示最近 {{ profile.artworks.length }} 件，共 {{ profile.counts.artworks }} 件。</p>
        </template>
      </section>

      <!-- 世界观 -->
      <section v-else-if="tab === 'worlds'" class="profile-panel" aria-label="世界观">
        <ParticleState v-if="!profile.worlds.length" class="panel-empty" kind="empty" preset="world" text="还没有公开的世界观。" />
        <div v-else class="card-grid">
          <RouterLink v-for="world in profile.worlds" :key="world.id" class="info-card" :to="`/world/project/${world.id}`">
            <span class="info-cover" data-depth><img v-if="world.coverImage" :src="world.coverImage" alt="" loading="lazy" @error="hide" /></span>
            <strong>{{ world.name }}</strong>
            <small>{{ world.description || '暂无简介' }}</small>
          </RouterLink>
        </div>
      </section>

      <!-- 人设 -->
      <section v-else-if="tab === 'characters'" class="profile-panel" aria-label="人设">
        <ParticleState v-if="!profile.characters.length" class="panel-empty" kind="empty" preset="chai" text="还没有公开的人设。" />
        <div v-else class="card-grid">
          <RouterLink v-for="oc in profile.characters" :key="oc.id" class="info-card" :to="`/ocs/${oc.id}`">
            <span class="info-cover" data-depth><img v-if="oc.coverUrl" :src="oc.coverUrl" alt="" loading="lazy" @error="hide" /></span>
            <strong>{{ oc.title }}</strong>
            <small>{{ oc.description || '暂无简介' }}</small>
          </RouterLink>
        </div>
      </section>

      <!-- 小说 -->
      <section v-else-if="tab === 'novels'" class="profile-panel" aria-label="小说">
        <div class="card-grid">
          <RouterLink v-for="n in profile.novels ?? []" :key="n.id" class="info-card" :to="`/novels/${n.id}`">
            <span class="info-cover" data-depth><img v-if="n.coverUrl" :src="n.coverUrl" alt="" loading="lazy" @error="hide" /></span>
            <strong>{{ n.title }}</strong>
            <small>{{ n.wordCount.toLocaleString() }} 字 · {{ n.status === 'completed' ? '已完结' : '连载中' }}</small>
          </RouterLink>
        </div>
        <p v-if="(profile.counts.novels ?? 0) > (profile.novels?.length ?? 0)" class="panel-note">显示最近更新的 {{ profile.novels?.length }} 部，共 {{ profile.counts.novels }} 部。</p>
      </section>

      <!-- 柴圈作品 -->
      <section v-else-if="tab === 'chai'" class="profile-panel" aria-label="柴圈作品">
        <div class="card-grid">
          <RouterLink v-for="w in profile.chaiWorks ?? []" :key="w.id" class="info-card" :to="`/works/${w.id}`">
            <span class="info-cover" data-depth><img v-if="w.coverUrl" :src="w.coverUrl" alt="" loading="lazy" @error="hide" /></span>
            <strong>{{ w.title }}</strong>
            <small>{{ w.description || '暂无简介' }}</small>
          </RouterLink>
        </div>
        <p v-if="(profile.counts.chaiWorks ?? 0) > (profile.chaiWorks?.length ?? 0)" class="panel-note">显示最近 {{ profile.chaiWorks?.length }} 件，共 {{ profile.counts.chaiWorks }} 件。</p>
      </section>

      <p v-if="profile.counts.history" class="history-link">📖 在岁月史书里记了 <b>{{ profile.counts.history }}</b> 段 <RouterLink :to="{ path: '/history', query: { author: profile.id } }">去看看 →</RouterLink></p>

      <!-- 留言板：始终挂载以便统计留言数，只在选中时显示 -->
      <section v-show="tab === 'guestbook'" class="profile-panel" aria-label="留言板">
        <ProfileGuestbook :owner-id="profile.id" :owner-name="profile.username" @count="guestbookCount = $event" />
      </section>
    </template>
  </article>
</template>

<style scoped>
.public-profile { max-width: 1100px; margin: 0 auto; color: var(--text-primary); }
.profile-state { padding: 60px 24px; text-align: center; color: var(--text-secondary); background: var(--paper-card); border: 1px solid var(--border-line); }
.profile-state h1 { color: var(--text-primary); font-size: 1.4rem; margin: 0 0 8px; }
.profile-state .btn-line { margin-top: 16px; }

.profile-header { position: relative; display: flex; align-items: center; gap: 28px; }
/* 主页横幅：铺在卡片顶部，头像压在横幅下沿 */
.profile-header.with-banner { align-items: flex-start; padding-top: calc(clamp(110px, 20vw, 220px) + 18px); overflow: hidden; }
.profile-header.with-banner > .profile-banner { position: absolute; z-index: 0; left: 0; right: 0; top: 0; height: clamp(110px, 20vw, 220px); overflow: hidden; background: var(--bg-sub) center / cover no-repeat; border-bottom: 1px solid var(--border-line); }
.profile-banner video { width: 100%; height: 100%; object-fit: cover; }
.with-banner .profile-avatar-slot { margin-top: -64px; }
.with-banner .profile-avatar { box-shadow: 0 0 0 4px var(--paper-card); }
.profile-title-badge { margin-left: 0; }
.profile-avatar-slot { flex: 0 0 auto; margin: 6px; }
.profile-avatar { position: relative; flex: 0 0 104px; width: 104px; height: 104px; display: grid; place-items: center; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint)); color: #fff; font-size: 40px; font-weight: 700; clip-path: polygon(0 0, 100% 0, 100% calc(100% - 16px), calc(100% - 16px) 100%, 0 100%); }
.profile-avatar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
.profile-avatar::after { content: ''; position: absolute; inset: auto 0 0; height: 3px; background: var(--signal-mint); }
.profile-info { flex: 1; min-width: 0; display: grid; gap: 8px; }
.profile-info h1 { margin: 2px 0 0; font-size: clamp(1.6rem, 3vw, 2.2rem); letter-spacing: -.02em; overflow-wrap: anywhere; }
.profile-tags { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 0; }
.profile-tags b { padding: 2px 8px; background: var(--accent-color); color: var(--on-accent); font: 600 12px/1.5 var(--font-mono); }
.profile-tags span { padding: 2px 8px; border: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.profile-tags .profile-title-badge { border-color: var(--accent-color); color: var(--accent-color); font-weight: 600; line-height: 1.5; }
.profile-bio { margin: 0; color: var(--text-primary); line-height: 1.7; max-width: 640px; overflow-wrap: anywhere; }
.profile-meta { display: flex; flex-wrap: wrap; gap: 6px 18px; margin: 0; font: 12px var(--font-mono); color: var(--text-secondary); }
.self-link { flex-shrink: 0; align-self: flex-start; }
.self-note { margin: 12px 0 0; padding: 8px 12px; border-left: 3px solid var(--signal-mint); background: var(--accent-soft); font-size: 13px; color: var(--text-secondary); }

.profile-tabs { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: 0; margin: 24px 0 20px; border: 1px solid var(--border-line); background: var(--paper-card); }
.profile-tabs button { position: relative; display: grid; gap: 2px; justify-items: start; padding: 14px 18px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); cursor: pointer; text-align: left; font: inherit; }
.profile-tabs button:last-child { border-right: 0; }
.profile-tabs button strong { font: 600 22px/1.2 var(--font-mono); color: var(--text-primary); }
.profile-tabs button span { font-size: 12px; }
.profile-tabs button::after { content: ''; position: absolute; inset: auto 0 0; height: 3px; background: var(--signal-mint); transform: scaleX(0); transform-origin: left; transition: transform .3s ease; }
.profile-tabs button:hover { background: var(--bg-sub); }
.profile-tabs button.active { color: var(--accent-color); background: linear-gradient(135deg, var(--paper-card) 50%, var(--accent-soft)); }
.profile-tabs button.active strong { color: var(--accent-color); }
.profile-tabs button.active::after { transform: scaleX(1); }

.profile-panel { min-width: 0; }
.panel-empty, .panel-note { padding: 28px 0; margin: 0; text-align: center; color: var(--text-secondary); font-size: 14px; }
.panel-note { padding: 14px 0 0; font-size: 12px; }
.load-more { display: block; margin: 16px auto 0; }

.post-list { list-style: none; margin: 0; padding: 0; border-top: 1px solid var(--border-line); }
.post-list li { border-bottom: 1px solid var(--border-line); }
.post-item { position: relative; display: flex; align-items: center; gap: 16px; padding: 14px 6px 14px 12px; color: var(--text-primary); text-decoration: none; transition: background .2s; }
.post-item::before { content: ''; position: absolute; left: 0; top: 12px; bottom: 12px; width: 2px; background: var(--signal-mint); transform: scaleY(0); transition: transform .25s; }
.post-item:is(:hover, :focus-visible) { background: linear-gradient(90deg, var(--accent-soft), transparent 70%); }
.post-item:is(:hover, :focus-visible)::before { transform: scaleY(1); }
.post-item:is(:hover, :focus-visible) strong { color: var(--accent-color); }
.post-thumb { flex: 0 0 96px; height: 64px; overflow: hidden; background: var(--paper-sub); clip-path: polygon(8px 0, 100% 0, 100% 100%, 0 100%, 0 8px); }
.post-thumb img { width: 100%; height: 100%; object-fit: cover; }
.post-body { flex: 1; min-width: 0; display: grid; gap: 3px; }
.post-meta { display: flex; align-items: center; gap: 8px; font: 10px/1.4 var(--font-mono); color: var(--text-secondary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.post-meta b { font-weight: 600; padding: 1px 5px; background: var(--bg-sub); }
.post-meta b.type-post { background: var(--accent-soft); color: var(--accent-color); }
.post-body strong { font-size: 15px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; transition: color .2s; }
.post-body small { font-size: 12px; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.post-side { display: grid; justify-items: end; gap: 3px; font: 11px var(--font-mono); color: var(--text-secondary); white-space: nowrap; }

.tile-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 12px; }
.art-tile { position: relative; display: block; aspect-ratio: 4 / 5; overflow: hidden; background: linear-gradient(135deg, var(--inverse-bg), var(--accent-color)); color: #fff; text-decoration: none; clip-path: polygon(14px 0, 100% 0, 100% calc(100% - 14px), calc(100% - 14px) 100%, 0 100%, 0 14px); }
.art-tile img { width: 100%; height: 100%; object-fit: cover; transition: transform .5s cubic-bezier(.2,.7,.2,1); }
.art-tile::after { content: ''; position: absolute; inset: 50% 0 0; background: linear-gradient(transparent, rgb(4 14 24 / .85)); }
.art-tile span { position: absolute; inset: auto 14px 12px; z-index: 1; font-size: 14px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.art-tile:is(:hover, :focus-visible) img { transform: scale(1.05); }
.art-tile:is(:hover, :focus-visible) { box-shadow: inset 0 -3px var(--signal-mint); }

.card-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 14px; }
.info-card { display: grid; gap: 6px; padding-bottom: 14px; border: 1px solid var(--border-line); border-top: 2px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); text-decoration: none; transition: border-color .2s, translate .2s; }
.info-card:is(:hover, :focus-visible) { border-top-color: var(--signal-mint); translate: 0 -2px; }
.info-cover { display: block; aspect-ratio: 16 / 9; overflow: hidden; background: linear-gradient(135deg, var(--paper-sub), var(--accent-soft)); }
.info-cover img { width: 100%; height: 100%; object-fit: cover; }
.info-card strong, .info-card small { padding: 0 14px; }
.info-card strong { font-size: 15px; }
.info-card small { color: var(--text-secondary); font-size: 12px; line-height: 1.6; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }

@media (max-width: 720px) {
  .profile-header { flex-wrap: wrap; gap: 16px; }
  .profile-avatar { flex-basis: 76px; width: 76px; height: 76px; font-size: 30px; }
  .profile-info { flex: 1 1 100%; order: 2; }
  .self-link { order: 1; align-self: center; margin-left: auto; }
  .profile-tabs { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .profile-tabs button:nth-child(2n) { border-right: 0; }
  .profile-tabs button:nth-child(-n + 4) { border-bottom: 1px solid var(--border-line); }
  .profile-tabs button:last-child { grid-column: span 2; border-right: 0; }
  .post-thumb { flex-basis: 72px; height: 52px; }
  .post-side span { display: none; }
  .tile-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (prefers-reduced-motion: reduce) { .profile-tabs button::after, .post-item::before, .art-tile img, .info-card { transition: none; } }
.history-link { margin: 0 0 16px; padding: 10px 14px; background: var(--paper-card); border: 1px solid var(--border-line); font-size: 13px; color: var(--text-secondary); }
.history-link b { color: var(--text-primary); }
.history-link a { margin-left: 6px; color: var(--accent-color); text-decoration: none; }
.profile-report { align-self: flex-start; margin-left: auto; }
</style>
