<template>
  <div class="home" :class="{ 'theme-aged': isAged }">
    <!-- 顶部导航 -->
    <nav class="navbar">
      <div class="nav-brand">柴圈 · 墨划集所</div>
      <ul class="nav-links">
        <li class="nav-item active">首页总览</li>
        <li class="nav-item" @click="goTo('/ocs')">OC画阁</li>
        <li class="nav-item" @click="goTo('/joint')">合演大作</li>
        <li class="nav-item" @click="goTo('/battles')">擂台约战</li>
      </ul>
      <div class="nav-controls">
        <button class="btn-line" @click="togglePaper">
          {{ isAged ? '新纸' : '旧纸' }}
        </button>
      </div>
    </nav>

    <!-- 主内容 -->
    <div class="container">
      <main>
        <!-- 板块1：OC展厅 -->
        <section>
          <div class="section-header">
            <h2 class="section-title">OC 角色画阁</h2>
            <router-link to="/ocs" class="section-more">
              收录 {{ ocCount }} 卷 · 浏览全部 ＞
            </router-link>
          </div>

          <div v-if="ocLoading" class="oc-grid">
            <div v-for="i in 3" :key="i" class="oc-card skeleton">
              <div class="oc-canvas-box shimmer"></div>
              <div class="skeleton-line"></div>
              <div class="skeleton-line short"></div>
            </div>
          </div>

          <div v-else-if="!latestList.length" class="empty-state">
            <p>尚无 OC 卷宗</p>
            <router-link to="/ocs/create" class="empty-link">创建第一个角色</router-link>
          </div>

          <div v-else class="oc-grid">
            <CharacterCard
              v-for="char in latestList"
              :key="char.id"
              :character="char"
              @click="goDetail"
            />
          </div>
        </section>

        <!-- 板块2：约战系统 -->
        <section class="battle-section">
          <div class="section-header">
            <h2 class="section-title">擂台拆招 · 约战榜</h2>
            <button class="btn-line" @click="goTo('/battles/create')">+ 下达战书</button>
          </div>
          <div class="battle-list">
            <div class="battle-item">
              <div class="battle-versus">
                <span class="fighter">【残月剑·影】</span>
                <span class="vs-sign">与</span>
                <span class="fighter">【断水流】</span>
              </div>
              <span class="battle-info">限时 30 秒 · 帧数不限</span>
              <span class="tag-status active">拆招中</span>
            </div>
            <div class="battle-item">
              <div class="battle-versus">
                <span class="fighter">【赤拳·狂徒】</span>
                <span class="vs-sign">与</span>
                <span class="fighter">【虚空法者】</span>
              </div>
              <span class="battle-info">传统 24 帧 · 兵刃限制</span>
              <span class="tag-status">待应战</span>
            </div>
          </div>
        </section>

        <!-- 板块3：联合活动（改为真实数据） -->
        <section>
          <div class="section-header">
            <h2 class="section-title">大型联合大作 · 招募</h2>
            <router-link to="/joint" class="section-more">
              共 {{ jointTotal }} 场 · 查看全部 ＞
            </router-link>
          </div>

          <!-- 加载中 -->
          <div v-if="jointLoading" class="joint-skeleton">
            <div class="skeleton-card">
              <div class="skeleton-line"></div>
              <div class="skeleton-line short"></div>
            </div>
            <div class="skeleton-card">
              <div class="skeleton-line"></div>
              <div class="skeleton-line short"></div>
            </div>
          </div>

          <!-- 空状态 -->
          <div v-else-if="!jointList.length" class="empty-state">
            <p>暂无联合活动</p>
            <router-link to="/joint/create" class="empty-link">发起第一个联合</router-link>
          </div>

          <!-- 联合列表 -->
          <div v-else class="joint-home-list">
            <div
              v-for="item in jointList.slice(0, 3)"
              :key="item.id"
              class="joint-home-item"
              @click="goTo(`/joint/${item.id}`)"
            >
              <div class="joint-home-info">
                <h3 class="joint-home-title">{{ item.title }}</h3>
                <p class="joint-home-desc">{{ truncateText(item.description, 60) }}</p>
                <div class="joint-home-meta">
                  <span class="joint-home-type">{{ typeLabel(item.type) }}</span>
                  <span class="joint-home-status" :class="statusClass(item.status)">
                    {{ statusLabel(item.status) }}
                  </span>
                  <span class="joint-home-count">{{ item.participantCount }} 人参与</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>

      <!-- 侧栏 -->
      <aside class="sidebar">
        <div class="notice-board">
          <div class="section-header" style="margin-bottom: 12px;">
            <h3 class="section-title" style="font-size: 15px;">集所告示</h3>
          </div>
          <ul class="notice-list">
            <li>墨划系统 UI 设计规范 1.0 发布</li>
            <li>关于柴圈约战系统判分规则调整</li>
            <li>喜讯：第三届柴圈联合大作获奖公布</li>
            <li>新手作画教程：如何保持线条柔顺</li>
          </ul>
        </div>

        <div class="notice-board">
          <div class="section-header" style="margin-bottom: 12px;">
            <h3 class="section-title" style="font-size: 15px;">集所数据</h3>
          </div>
          <div class="stats-data">
            <div>在线柴友：1,280 人</div>
            <div>归档 OC：{{ ocCount || 0 }} 位</div>
            <div>累计战书：892 份</div>
          </div>
        </div>
      </aside>
    </div>

    <footer>
      <div>墨划 · 纸张极简设计 — 柴圈社区</div>
      <div class="footer-sub">线条无界 · 呼吸自然</div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useStickmanStore } from './stickman_store'
import { useJointStore } from './Joint/joint_store'
import CharacterCard from './components/CharacterCard.vue'

const router = useRouter()
const store = useStickmanStore()
const jointStore = useJointStore()

const isAged = ref(false)

// ===== OC 数据 =====
const ocLoading = computed(() => store.loading)
const latestList = computed(() => store.characters.slice(0, 3))
const ocCount = computed(() => store.total)

// ===== 联合数据 =====
const jointList = computed(() => jointStore.activities)
const jointLoading = computed(() => jointStore.loading)
const jointTotal = computed(() => jointStore.total)

// ===== 工具函数 =====
function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text
  return text.slice(0, maxLength) + '...'
}

function statusLabel(status: string): string {
  const map: Record<string, string> = {
    open: '报名中',
    closed: '已截止',
    ended: '已结束',
    banned: '已封禁',
    abandoned: '暴毙',
  }
  return map[status] || status
}

function statusClass(status: string): string {
  const map: Record<string, string> = {
    open: 'status-open',
    closed: 'status-closed',
    ended: 'status-ended',
    banned: 'status-banned',
    abandoned: 'status-abandoned',
  }
  return map[status] || ''
}

function typeLabel(type: string): string {
  const map: Record<string, string> = {
    joint: '联合',
    relay: '接力',
    project: '企划',
    free: '自由',
    other: '其他',
  }
  return map[type] || type
}

// ===== 导航 =====
function goTo(path: string) {
  router.push(path)
}

function goDetail(id: string) {
  router.push(`/ocs/${id}`)
}

// ===== 纸张切换 =====
function togglePaper() {
  isAged.value = !isAged.value
  const root = document.documentElement

  if (isAged.value) {
    root.style.setProperty('--paper-bg', '#EAE4D6')
    root.style.setProperty('--paper-card', '#F0EBDF')
    root.style.setProperty('--paper-sub', '#E5DFD0')
    root.style.setProperty('--ink-black', '#2A2826')
    root.style.setProperty('--ink-gray', '#7A7570')
    root.style.setProperty('--line-raw', '#C8BFB3')
  } else {
    root.style.setProperty('--paper-bg', '#F7F4EE')
    root.style.setProperty('--paper-card', '#FCFAF7')
    root.style.setProperty('--paper-sub', '#F0EBE1')
    root.style.setProperty('--ink-black', '#2A2826')
    root.style.setProperty('--ink-gray', '#7A7570')
    root.style.setProperty('--line-raw', '#D5CEC5')
  }
}

onMounted(() => {
  store.fetchList({ page: 1, pageSize: 3 })
  jointStore.fetchList({ page: 1, pageSize: 3 })
})
</script>

<style scoped>
.home {
  /* 参考样式：宣纸白、沉底灰、徽墨、烟灰、远山灰线、朱砂红 */
  --bg-main: #F4F1EA;          /* 宣纸白 */
  --bg-sub: #ECE8E0;           /* 沉底灰 */
  --text-primary: #2C2A29;     /* 徽墨 */
  --text-secondary: #7A7571;   /* 烟灰 */
  --border-line: #D8D2C7;      /* 远山灰线 */
  --accent-color: #9E2A2B;     /* 朱砂红 */
  --font-family: 'Noto Serif SC', 'Source Han Serif SC', 'Songti SC', 'SimSun', serif;

  --paper-bg: var(--bg-main);
  --paper-card: #FCFAF7;
  --paper-sub: var(--bg-sub);
  --ink-black: var(--text-primary);
  --ink-gray: var(--text-secondary);
  --line-raw: var(--border-line);
  --cinnabar: var(--accent-color);
  --font-family: var(--font-family);

  background-color: var(--paper-bg);
  color: var(--ink-black);
  font-family: var(--font-family);
  min-height: 100vh;
  transition: all 0.5s ease;
}

/* 夜墨模式 */
.home.theme-night {
  --bg-main: #181818;
  --bg-sub: #121212;
  --text-primary: #C5C0B6;
  --text-secondary: #66625D;
  --border-line: #2C2B29;
  --accent-color: #A63B3B;

  --paper-bg: var(--bg-main);
  --paper-sub: var(--bg-sub);
  --ink-black: var(--text-primary);
  --ink-gray: var(--text-secondary);
  --line-raw: var(--border-line);
  --cinnabar: var(--accent-color);
}

/* ====== 顶部导航 ====== */
.navbar {
  height: 64px;
  border-bottom: 1px solid var(--line-raw);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 60px;
  background: var(--paper-bg);
  transition: background 0.6s ease, border-color 0.6s ease;
}

.nav-brand {
  font-size: 20px;
  letter-spacing: 0.3em;
  font-weight: 400;
  color: var(--ink-black);
}

.nav-links {
  display: flex;
  gap: 30px;
  list-style: none;
  padding: 0;
  margin: 0;
}

.nav-item {
  font-size: 14px;
  letter-spacing: 0.15em;
  color: var(--ink-gray);
  cursor: pointer;
  padding: 4px 0;
  transition: color 0.3s;
  border-bottom: 2px solid transparent;
}

.nav-item:hover,
.nav-item.active {
  color: var(--ink-black);
  border-bottom-color: var(--cinnabar);
}

.nav-controls {
  display: flex;
  gap: 10px;
  align-items: center;
}

/* ====== 通用组件 ====== */
.btn-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 6px 16px;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.btn-line.active {
  border-color: var(--cinnabar);
  background: rgba(158, 42, 43, 0.05);
  color: var(--cinnabar);
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 12px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--line-raw);
}

.section-title {
  font-size: 18px;
  font-weight: 400;
  letter-spacing: 0.2em;
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  color: var(--ink-black);
}

.section-title::before {
  content: "丨";
  color: var(--cinnabar);
  font-weight: 700;
}

.section-more {
  font-size: 13px;
  letter-spacing: 0.15em;
  color: var(--ink-gray);
  cursor: pointer;
  text-decoration: none;
  transition: color 0.3s;
}

.section-more:hover {
  color: var(--cinnabar);
}

.tag-status {
  font-size: 12px;
  padding: 2px 10px;
  border: 1px solid var(--line-raw);
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.tag-status.active {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* ====== 主布局 ====== */
.container {
  max-width: 1280px;
  margin: 0 auto;
  padding: 40px;
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 40px;
}

/* ====== OC 展厅 ====== */
.oc-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
  margin-bottom: 50px;
}

.oc-card {
  border: 1px solid var(--line-raw);
  padding: 16px;
  transition: all 0.3s ease;
  cursor: pointer;
  background: var(--paper-card);
}

.oc-card:hover {
  border-color: var(--ink-black);
  transform: translateY(-2px);
}

.oc-canvas-box {
  width: 100%;
  height: 140px;
  border: 1px dashed var(--line-raw);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 12px;
  background: var(--paper-sub);
  transition: border-color 0.3s;
}

.oc-card:hover .oc-canvas-box {
  border-color: var(--ink-black);
}

.oc-name {
  font-size: 16px;
  letter-spacing: 0.15em;
  margin-bottom: 6px;
  color: var(--ink-black);
}

.oc-meta {
  font-size: 12px;
  color: var(--ink-gray);
  display: flex;
  justify-content: space-between;
  letter-spacing: 0.1em;
}

.shimmer {
  animation: shimmer 1.8s ease-in-out infinite;
  background: var(--paper-sub);
}

@keyframes shimmer {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

.skeleton-line {
  height: 14px;
  background: var(--paper-sub);
  margin: 6px 0;
  animation: shimmer 1.8s ease-in-out infinite;
}

.skeleton-line.short {
  width: 60%;
}

/* ====== 约战系统 ====== */
.battle-section {
  margin-bottom: 50px;
}

.battle-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.battle-item {
  border: 1px solid var(--line-raw);
  padding: 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  transition: border-color 0.3s;
  flex-wrap: wrap;
  gap: 12px;
  background: var(--paper-card);
}

.battle-item:hover {
  border-color: var(--ink-black);
}

.battle-versus {
  display: flex;
  align-items: center;
  gap: 16px;
}

.fighter {
  font-size: 15px;
  letter-spacing: 0.15em;
  color: var(--ink-black);
}

.vs-sign {
  font-size: 13px;
  color: var(--cinnabar);
  letter-spacing: 0.2em;
  padding: 0 10px;
  border-left: 1px solid var(--line-raw);
  border-right: 1px solid var(--line-raw);
}

.battle-info {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

/* ====== 联合活动（真实数据） ====== */
.joint-home-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.joint-home-item {
  border: 1px solid var(--line-raw);
  padding: 16px 20px;
  cursor: pointer;
  transition: all 0.3s ease;
  background: var(--paper-card);
}

.joint-home-item:hover {
  border-color: var(--ink-black);
  transform: translateX(4px);
}

.joint-home-title {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 6px 0;
  color: var(--ink-black);
}

.joint-home-desc {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  margin: 0 0 10px 0;
  line-height: 1.6;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.joint-home-meta {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.joint-home-type {
  padding: 1px 10px;
  border: 1px solid var(--line-raw);
}

.joint-home-status {
  padding: 1px 10px;
  border: 1px solid var(--line-raw);
}

.status-open {
  border-color: #4CAF50;
  color: #4CAF50;
}
.status-closed {
  border-color: #FF9800;
  color: #FF9800;
}
.status-ended {
  border-color: #9E9E9E;
  color: #9E9E9E;
}
.status-banned {
  border-color: #F44336;
  color: #F44336;
}
.status-abandoned {
  border-color: #795548;
  color: #795548;
}

.joint-home-count {
  color: var(--ink-light);
}

.joint-skeleton {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.joint-skeleton .skeleton-card {
  padding: 16px 20px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
}

.joint-skeleton .skeleton-line {
  height: 14px;
  background: var(--paper-sub);
  margin: 4px 0;
  animation: shimmer 1.8s ease-in-out infinite;
}

.joint-skeleton .skeleton-line.short {
  width: 60%;
}

/* ====== 空状态 ====== */
.empty-state {
  padding: 40px 0;
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
  transition: border-color 0.3s;
}

.empty-link:hover {
  border-color: var(--cinnabar);
}

/* ====== 侧栏 ====== */
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 40px;
}

.notice-board {
  border: 1px solid var(--line-raw);
  padding: 20px;
  background: var(--paper-card);
}

.notice-list {
  list-style: none;
  padding: 0;
  margin: 0;
  font-size: 13px;
  line-height: 2.2;
  letter-spacing: 0.1em;
  color: var(--ink-gray);
}

.notice-list li {
  border-bottom: 1px dashed var(--line-raw);
  padding: 6px 0;
  cursor: pointer;
  transition: color 0.3s;
}

.notice-list li:hover {
  color: var(--ink-black);
}

.notice-list li:last-child {
  border-bottom: none;
}

.stats-data {
  font-size: 13px;
  color: var(--ink-gray);
  line-height: 2.2;
  letter-spacing: 0.15em;
}

/* ====== 页脚 ====== */
footer {
  border-top: 1px solid var(--line-raw);
  padding: 30px 60px;
  text-align: center;
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.2em;
  background: var(--paper-bg);
  transition: background 0.6s ease, border-color 0.6s ease;
}

.footer-sub {
  margin-top: 8px;
  opacity: 0.6;
}

/* ====== 响应式 ====== */
@media (max-width: 1024px) {
  .container {
    grid-template-columns: 1fr;
  }

  .oc-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 768px) {
  .navbar {
    padding: 0 20px;
    flex-wrap: wrap;
    height: auto;
    padding: 12px 20px;
    gap: 12px;
  }

  .nav-links {
    gap: 16px;
    flex-wrap: wrap;
  }

  .nav-item {
    font-size: 13px;
  }

  .container {
    padding: 20px 16px;
  }

  .oc-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 14px;
  }

  .battle-item {
    flex-direction: column;
    align-items: flex-start;
  }

  .battle-versus {
    flex-wrap: wrap;
  }

  .joint-home-item:hover {
    transform: none;
  }

  footer {
    padding: 20px;
  }
}

@media (max-width: 480px) {
  .oc-grid {
    grid-template-columns: 1fr 1fr;
    gap: 12px;
  }

  .oc-canvas-box {
    height: 100px;
  }

  .section-title {
    font-size: 16px;
  }

  .joint-home-meta {
    gap: 8px;
  }
}
</style>