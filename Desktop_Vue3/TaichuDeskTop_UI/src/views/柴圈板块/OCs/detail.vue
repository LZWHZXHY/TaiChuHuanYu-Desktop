<template>
  <div class="detail-page">
    <button class="back-btn" @click="router.back()">← 返回</button>

    <div v-if="loading" class="loading-state">
      <span>加载角色卷宗...</span>
    </div>

    <div v-else-if="!character" class="empty-state">
      <p>角色不存在</p>
      <router-link to="/ocs" class="empty-link">返回 OC 画阁</router-link>
    </div>

    <div v-else class="detail-content">
      <!-- 左侧：头像 + 基本信息 -->
      <div class="detail-left">
        <div class="avatar-container">
          <img :src="character.avatar || defaultAvatar" :alt="character.name" />
          <span v-if="character.status === 'draft'" class="badge-draft">草稿</span>
        </div>

        <div class="info-section">
          <h1 class="char-name">{{ character.name }}</h1>
          <p v-if="character.nickname" class="char-nickname">“{{ character.nickname }}”</p>
          <p class="char-author">作者：{{ character.authorName }}</p>

          <div class="info-grid">
            <div class="info-item">
              <span class="label">性别</span>
              <span class="value">{{ character.gender }}</span>
            </div>
            <div v-if="character.age" class="info-item">
              <span class="label">年龄</span>
              <span class="value">{{ character.age }}岁</span>
            </div>
            <div v-if="character.height" class="info-item">
              <span class="label">身高</span>
              <span class="value">{{ character.height }}</span>
            </div>
            <div v-if="character.weight" class="info-item">
              <span class="label">体重</span>
              <span class="value">{{ character.weight }}</span>
            </div>
          </div>

          <div class="tag-list">
            <span v-for="tag in character.tags" :key="tag" class="tag-item">
              #{{ tag }}
            </span>
          </div>

          <!-- 作者操作 -->
          <div v-if="isOwner" class="owner-actions">
            <router-link :to="`/ocs/edit/${character.id}`" class="btn-line">✎ 编辑</router-link>
            <button class="btn-line" @click="handleDelete">✕ 删除</button>
          </div>
        </div>
      </div>

      <!-- 右侧：详细描述 -->
      <div class="detail-right">
        <div class="desc-section">
          <h3 class="desc-title">外貌描述</h3>
          <p class="desc-text">{{ character.appearance }}</p>
        </div>

        <div v-if="character.outfit" class="desc-section">
          <h3 class="desc-title">服装</h3>
          <p class="desc-text">{{ character.outfit }}</p>
        </div>

        <div class="desc-section">
          <h3 class="desc-title">性格特征</h3>
          <p class="desc-text">{{ character.personality }}</p>
        </div>

        <div class="desc-section">
          <h3 class="desc-title">背景故事</h3>
          <p class="desc-text">{{ character.background }}</p>
        </div>

        <div v-if="character.abilities" class="desc-section">
          <h3 class="desc-title">能力/技能</h3>
          <p class="desc-text">{{ character.abilities }}</p>
        </div>

        <div v-if="character.gallery?.length" class="desc-section">
          <h3 class="desc-title">图集</h3>
          <div class="gallery-grid">
            <img v-for="(img, idx) in character.gallery" :key="idx" :src="img" alt="图集" />
          </div>
        </div>

        <!-- 底部操作 -->
        <div class="detail-actions">
          <div class="action-stats">
            <span class="stat">👁 {{ character.views }}</span>
            <button class="stat-btn" @click="handleLike">
              <span :class="{ liked: isLiked }">❤</span> {{ character.likes }}
            </button>
            <button class="stat-btn" @click="handleFavorite">
              <span :class="{ favorited: isFavorited }">⭐</span> {{ character.favorites }}
            </button>
          </div>
          <button class="btn-line" @click="goBattle">⚔ 发起约战</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useStickmanStore } from '../stickman_store'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const route = useRoute()
const store = useStickmanStore()
const userStore = useUserStore()

const defaultAvatar = 'https://api.dicebear.com/7.x/avataaars/svg?seed=stickman'
const isLiked = ref(false)
const isFavorited = ref(false)

const character = computed(() => store.currentCharacter)
const loading = computed(() => store.loading)
const isOwner = computed(() => character.value?.authorId === userStore.userInfo?.id)

onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})

function handleLike() {
  if (!character.value) return
  isLiked.value = !isLiked.value
  character.value.likes += isLiked.value ? 1 : -1
  store.toggleLike(character.value.id)
}

function handleFavorite() {
  if (!character.value) return
  isFavorited.value = !isFavorited.value
  character.value.favorites += isFavorited.value ? 1 : -1
  store.toggleFavorite(character.value.id)
}

async function handleDelete() {
  if (!character.value) return
  if (!confirm(`确定要删除「${character.value.name}」吗？`)) return
  await store.deleteCharacter(character.value.id)
  router.push('/ocs')
}

function goBattle() {
  if (!character.value) return
  router.push(`/battles/create?ocId=${character.value.id}`)
}
</script>

<style scoped>
.detail-page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 24px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
}

.back-btn {
  background: none;
  border: none;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  padding: 8px 0;
  margin-bottom: 24px;
  font-family: var(--font-family);
  transition: color 0.3s;
}

.back-btn:hover {
  color: var(--ink-black);
}

.loading-state,
.empty-state {
  padding: 80px 0;
  text-align: center;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
}

.empty-link {
  color: var(--cinnabar);
  text-decoration: none;
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 2px;
}

.empty-link:hover {
  border-color: var(--cinnabar);
}

/* ===== 主布局 ===== */
.detail-content {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 40px;
}

/* ===== 左侧 ===== */
.detail-left {
  position: sticky;
  top: 24px;
  align-self: start;
}

.avatar-container {
  position: relative;
  border: 1px solid var(--line-raw);
  overflow: hidden;
  aspect-ratio: 1/1;
  background: var(--paper-sub);
}

.avatar-container img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.badge-draft {
  position: absolute;
  top: 10px;
  right: 10px;
  padding: 3px 14px;
  font-size: 12px;
  color: #fff;
  background: rgba(44, 42, 41, 0.75);
  letter-spacing: 0.15em;
}

.info-section {
  margin-top: 20px;
}

.char-name {
  font-size: 22px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 2px 0;
  color: var(--ink-black);
}

.char-nickname {
  color: var(--ink-gray);
  font-style: italic;
  letter-spacing: 0.1em;
  margin: 0 0 4px 0;
}

.char-author {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
  margin: 0 0 16px 0;
}

.info-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px 16px;
  padding: 12px 16px;
  border: 1px solid var(--line-raw);
  margin-bottom: 14px;
  background: var(--paper-card);
}

.info-item .label {
  display: block;
  font-size: 11px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.info-item .value {
  font-size: 14px;
  color: var(--ink-black);
  letter-spacing: 0.1em;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 16px;
}

.tag-item {
  font-size: 12px;
  color: var(--ink-gray);
  border: 1px solid var(--line-raw);
  padding: 2px 12px;
  letter-spacing: 0.1em;
}

.owner-actions {
  display: flex;
  gap: 12px;
}

.owner-actions .btn-line {
  padding: 6px 20px;
  font-size: 13px;
}

/* ===== 右侧 ===== */
.detail-right {
  display: flex;
  flex-direction: column;
  gap: 28px;
}

.desc-section {
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 20px;
}

.desc-section:last-of-type {
  border-bottom: none;
}

.desc-title {
  font-size: 15px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 8px 0;
  color: var(--ink-black);
}

.desc-text {
  font-size: 14px;
  line-height: 2;
  color: var(--ink-gray);
  letter-spacing: 0.08em;
  margin: 0;
  white-space: pre-wrap;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 8px;
  margin-top: 8px;
}

.gallery-grid img {
  width: 100%;
  aspect-ratio: 1/1;
  object-fit: cover;
  border: 1px solid var(--line-raw);
}

/* ===== 底部操作 ===== */
.detail-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 20px;
  border-top: 1px solid var(--line-raw);
  flex-wrap: wrap;
  gap: 16px;
}

.action-stats {
  display: flex;
  gap: 20px;
  font-size: 14px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.stat-btn {
  background: none;
  border: none;
  font-family: var(--font-family);
  font-size: 14px;
  color: var(--ink-gray);
  cursor: pointer;
  transition: color 0.3s;
  display: flex;
  align-items: center;
  gap: 4px;
}

.stat-btn:hover {
  color: var(--ink-black);
}

.stat-btn .liked,
.stat-btn .favorited {
  color: var(--cinnabar);
}

/* ===== 响应式 ===== */
@media (max-width: 860px) {
  .detail-content {
    grid-template-columns: 1fr;
  }

  .detail-left {
    position: static;
    max-width: 320px;
  }

  .avatar-container {
    max-width: 280px;
    margin: 0 auto;
  }

  .info-section {
    margin-top: 16px;
  }
}

@media (max-width: 480px) {
  .detail-page {
    padding: 16px 12px 40px;
  }

  .detail-left {
    max-width: 100%;
  }

  .info-grid {
    grid-template-columns: 1fr 1fr;
    gap: 4px 12px;
    padding: 10px 14px;
  }
}
</style>