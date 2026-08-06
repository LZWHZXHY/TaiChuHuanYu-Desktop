<template>
  <div class="detail-page">
    <button class="back-btn" @click="goBack">← 返回</button>

    <div v-if="loading" class="loading-state">
      <span>加载角色卷宗...</span>
    </div>

    <div v-else-if="!character" class="empty-state">
      <p>角色不存在</p>
      <router-link to="/ocs" class="empty-link">返回 OC 画阁</router-link>
    </div>

    <div v-else class="detail-content">
      <!-- ===== 左侧：封面 + 短属性 ===== -->
      <div class="detail-left">
        <div class="avatar-container">
          <img :src="character.coverUrl || defaultAvatar" :alt="character.title" />
          <span v-if="character.status === 'draft'" class="badge-draft">草稿</span>
        </div>

        <div class="info-section">
          <h1 class="char-name">{{ character.title }}</h1>
          <p v-if="getAttr('昵称') || getAttr('绰号')" class="char-nickname">
            “{{ getAttr('昵称') || getAttr('绰号') }}”
          </p>
          <p class="char-author">作者：{{ character.authorName }}</p>

          <!-- 短属性网格（只显示短文本属性） -->
          <div v-if="shortAttributes.length" class="attr-grid">
            <div v-for="attr in shortAttributes" :key="attr.id" class="attr-item">
              <span class="attr-key">{{ attr.key }}</span>
              <span class="attr-value">{{ attr.value }}</span>
            </div>
          </div>

          <!-- 作者操作 -->
          <div v-if="isOwner" class="owner-actions">
            <router-link :to="`/ocs/edit/${character.id}`" class="btn-line">✎ 编辑</router-link>
            <button class="btn-line" @click="handleDelete">✕ 删除</button>
          </div>
        </div>
      </div>

      <!-- ===== 右侧：长文本属性 + 图库 ===== -->
      <div class="detail-right">
        <!-- 长文本属性区块 -->
        <div v-if="longAttributes.length">
          <div v-for="attr in longAttributes" :key="attr.id" class="desc-section">
            <h3 class="desc-title">{{ attr.key }}</h3>
            <div class="desc-text long-text">{{ attr.value }}</div>
          </div>
        </div>

        <!-- 图库 -->
        <div v-if="character.images?.length" class="desc-section">
          <h3 class="desc-title">🖼️ 图库</h3>
          <div class="gallery-grid">
            <div v-for="(img, idx) in character.images" :key="idx" class="gallery-item">
              <img :src="img.url" :alt="img.alt || character.title" />
              <p v-if="img.alt" class="gallery-alt">{{ img.alt }}</p>
            </div>
          </div>
        </div>

        <div v-else class="desc-section">
          <h3 class="desc-title">🖼️ 图库</h3>
          <p class="desc-text">暂无图库图片</p>
        </div>

        <!-- 约战战绩 -->
        <div class="battle-stats-section">
          <h4 class="stats-title">⚔️ 约战战绩</h4>
          <div class="battle-stats">
            <span class="stat-win">🏆 胜 {{ character.battleWins || 0 }}</span>
            <span class="stat-lose">💔 负 {{ character.battleLosses || 0 }}</span>
            <span class="stat-draw">🤝 平 {{ character.battleDraws || 0 }}</span>
          </div>
        </div>

        <!-- 底部操作 -->
        <div class="detail-actions">
          <div class="action-stats">
            <span class="stat">👁 {{ character.views }}</span>
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

const character = computed(() => store.currentCharacter)
const loading = computed(() => store.loading)
const isOwner = computed(() => character.value?.authorId === userStore.userInfo?.id)
// ===== 返回 OC 画阁列表页 =====
function goBack() {
  router.push('/ocs')
}
// ===== 从 attributes 中获取指定 key 的值 =====
function getAttr(key: string): string | null {
  if (!character.value?.attributes) return null
  const attr = character.value.attributes.find(a => a.key === key)
  return attr?.value ?? null
}

// ===== 判断是否为长文本（长度 > 100 或包含换行） =====
function isLongText(value?: string): boolean {
  if (!value) return false
  return value.length > 100 || value.includes('\n')
}

// ✅ 直接使用后端的 type 字段
const shortAttributes = computed(() => {
  if (!character.value?.attributes) return []
  const hiddenKeys = ['昵称', '绰号', '标签']
  return character.value.attributes
    .filter(a => !hiddenKeys.includes(a.key) && a.type === 'short')
    .sort((a, b) => a.sortOrder - b.sortOrder)
})

const longAttributes = computed(() => {
  if (!character.value?.attributes) return []
  const hiddenKeys = ['昵称', '绰号', '标签']
  return character.value.attributes
    .filter(a => !hiddenKeys.includes(a.key) && a.type === 'long')
    .sort((a, b) => a.sortOrder - b.sortOrder)
})
// ===== 获取标签列表 =====
const tags = computed(() => {
  const tagStr = getAttr('标签')
  if (!tagStr) return []
  return tagStr.split(/[,，、\s]+/).filter(t => t.trim())
})

onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})

async function handleDelete() {
  if (!character.value) return
  if (!confirm(`确定要删除「${character.value.title}」吗？`)) return
  await store.deleteCharacter(character.value.id)
  router.push('/ocs')
}

function goBattle() {
  if (!character.value) return
  router.push(`/battles/create?ocId=${character.value.id}`)
}
</script>

<style scoped>
.battle-stats-section {
  margin: 12px 0;
  padding: 12px 0;
  border-top: 1px solid var(--line-raw);
}

.stats-title {
  font-size: 13px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 8px 0;
  color: var(--ink-gray);
}

.battle-stats {
  display: flex;
  gap: 16px;
  font-size: 14px;
}

.stat-win { color: #4CAF50; font-weight: 500; }
.stat-lose { color: #F44336; font-weight: 500; }
.stat-draw { color: #FF9800; font-weight: 500; }
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

/* ===== 短属性网格 ===== */
.attr-grid {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 0;
  border-top: 1px solid var(--line-raw);
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 14px;
}

.attr-item {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  letter-spacing: 0.1em;
  padding: 2px 0;
}

.attr-key {
  color: var(--ink-gray);
}

.attr-value {
  color: var(--ink-black);
  text-align: right;
  max-width: 60%;
  word-break: break-word;
}

/* ===== 标签 ===== */
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

/* ===== 长文本专用样式 ===== */
.desc-text.long-text {
  font-size: 14px;
  line-height: 1.8;
  color: var(--text-primary);
  background: var(--paper-sub);
  padding: 16px 20px;
  border-left: 3px solid var(--accent-color);
  border-radius: 0 4px 4px 0;
  white-space: pre-wrap;
  word-break: break-word;
}

/* ===== 图库 ===== */
.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
  margin-top: 8px;
}

.gallery-item {
  border: 1px solid var(--line-raw);
  overflow: hidden;
  background: var(--paper-sub);
}

.gallery-item img {
  width: 100%;
  aspect-ratio: 1/1;
  object-fit: cover;
}

.gallery-alt {
  font-size: 11px;
  color: var(--ink-light);
  text-align: center;
  padding: 4px 6px;
  letter-spacing: 0.1em;
  margin: 0;
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

.stat {
  display: flex;
  align-items: center;
  gap: 4px;
}

/* ===== 响应式 ===== */
@media (max-width: 860px) {
  .detail-content {
    grid-template-columns: 1fr;
  }

  .detail-left {
    position: static;
    max-width: 320px;
    margin: 0 auto;
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

  .attr-item {
    font-size: 12px;
    flex-wrap: wrap;
  }

  .attr-value {
    max-width: 100%;
    text-align: left;
  }

  .gallery-grid {
    grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
    gap: 6px;
  }

  .desc-text.long-text {
    padding: 12px 16px;
    font-size: 13px;
  }
}
</style>