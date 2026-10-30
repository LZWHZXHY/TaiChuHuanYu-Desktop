<template>
  <div class="work-detail content-page">
    <header class="content-header">
      <RouterLink to="/works">← 返回作品</RouterLink>
      <div class="header-actions" v-if="isAuthor">
        <button class="btn-line danger" @click="remove">删除</button>
      </div>
    </header>

    <p v-if="loading" class="state">正在加载作品…</p>
    <p v-else-if="error" class="state" role="alert">{{ error }} <button @click="load">重试</button></p>

    <div v-else-if="work" class="detail-layout">
      <main class="player-col">
        <video v-if="work.mediaType === 'video'" :src="work.mediaUrl" :poster="withThumbnail(work.coverUrl, 800) || undefined" controls class="player" />
        <img v-else :src="work.mediaUrl" class="player img" :alt="work.title" />

        <h1 class="title">{{ work.title }}</h1>
        <div class="meta">
          <UserLink :id="work.authorId" :name="work.authorName">@{{ work.authorName }}</UserLink>
          <span>·</span>
          <time>{{ formatDate(work.createdAt) }}</time>
          <span>·</span>
          <span>{{ work.views }} 次观看</span>
        </div>

        <p v-if="work.description" class="description">{{ work.description }}</p>

        <div v-if="work.tags?.length" class="tags">
          <span v-for="tag in work.tags" :key="tag" class="tag">#{{ tag }}</span>
        </div>

        <div class="interactions">
          <button class="interact-btn" :class="{ active: liked }" @click="toggleLike">❤ {{ likeCount }}</button>
          <button class="interact-btn" :class="{ active: favorited }" @click="toggleFavorite">⭐ {{ favoriteCount }}</button>
          <span class="interact-count">💬 {{ work.commentCount || 0 }}</span>
        </div>

        <ChaiComments :target-type="'work'" :target-id="workId" />
      </main>

      <aside class="side-col">
        <div class="side-card">
          <h3>关于作者</h3>
          <RouterLink :to="`/user/${work.authorId}`">@{{ work.authorName }}</RouterLink>
          <button v-if="!isAuthor && user.isLoggedIn" class="follow-btn" :class="{ active: following }" @click="toggleFollow">{{ following ? '已关注' : '+ 关注' }}</button>
          <span class="follower-count">{{ followerCount }} 粉丝</span>
        </div>
        <div v-if="work.relatedCharacterIds?.length" class="side-card">
          <h3>相关 OC</h3>
          <RouterLink v-for="cid in work.relatedCharacterIds" :key="cid" :to="`/ocs/${cid}`">OC 角色</RouterLink>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { chaiWorkApi, type ChaiWork } from '@/api/chaiWork'
import { useUserStore } from '@/stores/user'
import ChaiComments from '../components/ChaiComments.vue'
import { withThumbnail } from '@/utils/imageThumb'
import { followApi } from '@/api/follow'

const route = useRoute()
const router = useRouter()
const user = useUserStore()
const workId = computed(() => String(route.params.id))
const work = ref<ChaiWork | null>(null)
const loading = ref(false)
const error = ref('')
const liked = ref(false)
const favorited = ref(false)
const likeCount = ref(0)
const favoriteCount = ref(0)
const following = ref(false)
const followerCount = ref(0)

const isAuthor = computed(() => Boolean(user.userId && work.value && work.value.authorId === user.userId))

async function load() {
  loading.value = true
  error.value = ''
  try {
    const detail = await chaiWorkApi.getDetail(workId.value)
    work.value = detail
    likeCount.value = detail.likeCount
    favoriteCount.value = detail.favoriteCount
    if (detail.authorId) {
      try {
        const st = await followApi.status(detail.authorId)
        following.value = st.following
        followerCount.value = st.followerCount
      } catch { /* 关注状态拿不到不影响浏览 */ }
    }
  } catch (e: any) {
    error.value = e.friendlyMessage || '作品加载失败'
  } finally {
    loading.value = false
  }
}

async function toggleFollow() {
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  if (!work.value) return
  try {
    const res = await followApi.toggle(work.value.authorId)
    following.value = res.following
    followerCount.value = res.followerCount
  } catch (e: any) { error.value = e.friendlyMessage || '关注失败' }
}

async function toggleLike() {
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  const res = await chaiWorkApi.toggle(workId.value, 'like')
  liked.value = res.active; likeCount.value = res.count
}
async function toggleFavorite() {
  if (!user.isLoggedIn) { error.value = '请先登录'; return }
  const res = await chaiWorkApi.toggle(workId.value, 'favorite')
  favorited.value = res.active; favoriteCount.value = res.count
}

async function remove() {
  if (!confirm('确认删除这个作品？此操作不可恢复。')) return
  await chaiWorkApi.remove(workId.value)
  router.replace('/works')
}

const formatDate = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'short', day: 'numeric' })
onMounted(load)
</script>

<style scoped>
.content-header { display: flex; justify-content: space-between; align-items: center; }
.content-header a { color: var(--accent-color); text-decoration: none; font-size: .85rem; }
.header-actions { display: flex; gap: 10px; }
.btn-line { padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; text-decoration: none; font-size: .85rem; }
.btn-line.danger { color: var(--danger); border-color: var(--danger); }
.state { padding: 40px 0; text-align: center; color: var(--text-secondary); }
.detail-layout { display: grid; grid-template-columns: minmax(0, 1fr) 240px; gap: 24px; margin-top: 16px; }
.player { width: 100%; max-height: 70vh; background: #000; border-radius: 0; }
.player.img { object-fit: contain; }
.title { margin: 16px 0 8px; font-size: 1.4rem; }
.meta { display: flex; gap: 8px; align-items: center; font-size: .85rem; color: var(--text-secondary); }
.description { margin: 16px 0; line-height: 1.8; white-space: pre-wrap; }
.tags { display: flex; flex-wrap: wrap; gap: 8px; }
.tag { font-size: .8rem; color: var(--accent-color); background: var(--accent-soft); padding: 2px 10px; border-radius: 0; }
.interactions { display: flex; gap: 12px; margin-top: 20px; }
.interact-btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; }
.interact-btn.active { color: var(--accent-color); border-color: var(--accent-color); }
.interact-count { display: inline-flex; align-items: center; padding: 0 8px; font-size: .85rem; color: var(--text-secondary); }
.follow-btn { padding: 6px 12px; border: 1px solid var(--accent-color); border-radius: 0; background: transparent; color: var(--accent-color); cursor: pointer; font-size: .8rem; justify-self: start; }
.follow-btn.active { background: var(--accent-soft); }
.follower-count { font-size: .75rem; color: var(--text-secondary); }
.side-col { display: grid; gap: 16px; align-content: start; }
.side-card { border: 1px solid var(--border-line); border-radius: 0; padding: 16px; background: var(--paper-card); display: grid; gap: 8px; }
.side-card h3 { margin: 0 0 4px; font-size: .9rem; }
.side-card a { color: var(--accent-color); text-decoration: none; font-size: .85rem; }
@media (max-width: 820px) { .detail-layout { grid-template-columns: 1fr; } }
</style>
