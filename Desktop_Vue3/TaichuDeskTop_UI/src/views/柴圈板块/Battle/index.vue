<template>
  <div class="battle-page">
    <!-- ============ 顶部 ============ -->
    <header class="page-header">
      <div class="header-left">
        <div class="title-group">
          <span class="title-icon">⚔</span>
          <h1>TCV</h1>
          <span class="title-sub">太初论战</span>
        </div>
        <p class="subtitle">画师以创作为刃，一决高下</p>
      </div>
      <div class="header-actions">
        <button class="btn-ghost" @click="goMyBattles">我的约战</button>
        <button class="btn-primary" @click="goCreate">+ 发起约战</button>
      </div>
    </header>

    <!-- ============ 核心：规则说明 ============ -->
    <section class="rules-section">
      <div class="rules-inner">
        <div class="rules-header">
          <span class="rules-badge">· 规则 ·</span>
          <h2>约战流程</h2>
        </div>

        <div class="rules-body">
          <!-- 左：约战是什么 -->
          <div class="rules-intro">
            <p>
              约战是画师之间以<strong>创作作品</strong>为形式的对决。
              每位参与者提交作品链接，通过<strong>投票</strong>或<strong>内定</strong>判定胜负，
              结果将影响 OC 的状态与战绩。
            </p>
          </div>

          <!-- 右：五步流程 -->
          <div class="rules-flow">
            <div class="flow-item">
              <span class="flow-num">1</span>
              <span class="flow-label">发起</span>
              <span class="flow-desc">设定类型、规则、判定方式</span>
            </div>
            <div class="flow-line"></div>
            <div class="flow-item">
              <span class="flow-num">2</span>
              <span class="flow-label">报名</span>
              <span class="flow-desc">选择 OC 加入</span>
            </div>
            <div class="flow-line"></div>
            <div class="flow-item">
              <span class="flow-num">3</span>
              <span class="flow-label">提交</span>
              <span class="flow-desc">上传作品链接</span>
            </div>
            <div class="flow-line"></div>
            <div class="flow-item">
              <span class="flow-num">4</span>
              <span class="flow-label">判定</span>
              <span class="flow-desc">投票制 / 内定制</span>
            </div>
            <div class="flow-line"></div>
            <div class="flow-item">
              <span class="flow-num">5</span>
              <span class="flow-label">更新</span>
              <span class="flow-desc">记录战绩，更新 OC 状态</span>
            </div>
          </div>
        </div>

        <!-- 底部信息 -->
        <div class="rules-footer">
          <div class="rule-tag">
            <span class="tag-mark">◆</span>
            <span><strong>投票制</strong> — 社区投票决定胜负</span>
          </div>
          <div class="rule-tag">
            <span class="tag-mark">◆</span>
            <span><strong>内定制</strong> — 参与者协商决定胜负</span>
          </div>
          <div class="rule-tag">
            <span class="tag-mark">◆</span>
            <span>须提交<strong>作品链接</strong>，作为判定依据</span>
          </div>
        </div>
      </div>
    </section>

    <!-- ============ 筛选栏 ============ -->
    <div class="filter-bar">
      <div class="filter-left">
        <select v-model="filterStatus" @change="fetchList">
          <option value="">全部状态</option>
          <option value="open">报名中</option>
          <option value="ongoing">创作中</option>
          <option value="judging">评审中</option>
          <option value="finished">已完成</option>
          <option value="cancelled">已取消</option>
        </select>
        <input
          v-model="keyword"
          placeholder="搜索约战..."
          @input="fetchList"
        />
      </div>
      <span class="result-count">共 {{ total }} 场</span>
    </div>

    <!-- ============ 约战列表 ============ -->
    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="!list.length" class="empty">
      <p>还没有约战，<button class="link-btn" @click="goCreate">发起第一场</button></p>
    </div>

    <div v-else class="battle-list">
      <div
        v-for="item in list"
        :key="item.id"
        class="battle-card"
        @click="goDetail(item.id)"
      >
        <!-- 封面 -->
        <div class="card-cover">
          <img v-if="item.coverUrl" :src="item.coverUrl" alt="" />
          <span v-else class="cover-placeholder">⚔</span>
          <span class="card-status" :class="item.status">
            {{ statusMap[item.status] || item.status }}
          </span>
        </div>

        <!-- 内容 -->
        <div class="card-body">
          <div class="card-head">
            <span class="type-tag">{{ item.battleType || '自定义' }}</span>
            <span class="judgment-tag">
              {{ item.judgmentType === 'vote' ? '投票制' : '内定制' }}
            </span>
          </div>
          <h3 class="card-title">{{ item.title }}</h3>

          <!-- 规则 -->
          <div class="card-rules">
            <span class="rules-label">规则</span>
            <span class="rules-text">{{ item.rules }}</span>
          </div>

          <div class="card-meta">
            <span>◈ {{ item.participantCount || 0 }} 人</span>
            <span>◈ {{ item.submissionCount || 0 }} 作品</span>
           <span>◈ 发起人：{{ item.participants?.[0]?.userName || '未知' }}</span>
            <span class="meta-time">◈ {{ formatTime(item.createdAt) }}</span>
          </div>
        </div>

        <div class="card-action">
          <span>查看 →</span>
        </div>
      </div>
    </div>

    <!-- 分页 -->
    <div v-if="total > pageSize" class="pagination">
      <button @click="page--" :disabled="page <= 1">上一页</button>
      <span>{{ page }} / {{ Math.ceil(total / pageSize) }}</span>
      <button @click="page++" :disabled="page >= Math.ceil(total / pageSize)">下一页</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { battleApi, type Battle } from './battle_api'

const router = useRouter()

const list = ref<Battle[]>([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const filterStatus = ref('')
const keyword = ref('')

const statusMap: Record<string, string> = {
  open: '报名中',
  ongoing: '创作中',
  judging: '评审中',
  finished: '已完成',
  cancelled: '已取消',
}

const formatTime = (iso: string) => {
  const d = new Date(iso)
  const now = new Date()
  const diff = Math.floor((now.getTime() - d.getTime()) / 86400000)
  if (diff === 0) return '今天'
  if (diff === 1) return '昨天'
  if (diff < 7) return `${diff} 天前`
  return d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
}

const fetchList = async () => {
  loading.value = true
  try {
    const params: any = { page: page.value, pageSize: pageSize.value }
    if (filterStatus.value) params.status = filterStatus.value
    if (keyword.value) params.keyword = keyword.value
    const res = await battleApi.list(params)
    list.value = res.items
    total.value = res.total
  } catch {
    // ignore
  } finally {
    loading.value = false
  }
}

const goCreate = () => router.push('/battles/create')
const goMyBattles = () => router.push('/battles/my')
const goDetail = (id: string) => router.push(`/battles/${id}`)

onMounted(fetchList)
</script>

<style scoped>
/* ============================================================
   整体容器 - 宣纸底色
   ============================================================ */
.battle-page {
  max-width: 960px;
  margin: 0 auto;
  padding: 32px 20px 60px;
  background: #f5f0eb;
  min-height: 100vh;
  color: #2c2a29;
}

/* ============================================================
   顶部
   ============================================================ */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 20px;
  border-bottom: 2px solid #d8d0c4;
  margin-bottom: 32px;
  flex-wrap: wrap;
  gap: 12px;
}

.header-left {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.title-group {
  display: flex;
  align-items: baseline;
  gap: 12px;
}

.title-icon {
  font-size: 22px;
  color: #9e2a2b;
}

.title-group h1 {
  font-size: 28px;
  font-weight: 400;
  letter-spacing: 0.25em;
  margin: 0;
  color: #2c2a29;
}

.title-sub {
  font-size: 13px;
  color: #9e2a2b;
  letter-spacing: 0.15em;
  font-weight: 300;
}

.subtitle {
  font-size: 14px;
  color: #999;
  margin: 0;
  letter-spacing: 0.1em;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.btn-primary,
.btn-ghost {
  padding: 8px 22px;
  border: 1px solid #d8d0c4;
  background: transparent;
  cursor: pointer;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.1em;
  transition: all 0.3s;
}

.btn-primary {
  background: #2c2a29;
  color: #f5f0eb;
  border-color: #2c2a29;
}
.btn-primary:hover {
  background: #f5f0eb;
  color: #2c2a29;
}

.btn-ghost:hover {
  border-color: #9e2a2b;
  color: #9e2a2b;
}

/* ============================================================
   规则区
   ============================================================ */
.rules-section {
  background: #fcfaf7;
  border: 1px solid #d8d0c4;
  padding: 28px 32px;
  margin-bottom: 32px;
  box-shadow: 0 2px 12px rgba(44, 42, 41, 0.04);
}

.rules-inner {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.rules-header {
  display: flex;
  align-items: center;
  gap: 14px;
  padding-bottom: 14px;
  border-bottom: 1px dashed #d8d0c4;
}

.rules-badge {
  font-size: 11px;
  color: #9e2a2b;
  letter-spacing: 0.2em;
}

.rules-header h2 {
  font-size: 17px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0;
  color: #2c2a29;
}

.rules-body {
  display: grid;
  grid-template-columns: 1fr 1.8fr;
  gap: 28px;
}

.rules-intro p {
  font-size: 14px;
  line-height: 2;
  color: #555;
  margin: 0;
  text-align: justify;
}

.rules-intro p strong {
  color: #2c2a29;
}

.rules-flow {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.flow-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 6px 0;
}

.flow-num {
  width: 24px;
  height: 24px;
  border: 1px solid #9e2a2b;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  color: #9e2a2b;
  flex-shrink: 0;
}

.flow-label {
  font-size: 14px;
  font-weight: 500;
  color: #2c2a29;
  letter-spacing: 0.08em;
  min-width: 44px;
}

.flow-desc {
  font-size: 13px;
  color: #888;
  letter-spacing: 0.05em;
}

.flow-line {
  width: 1px;
  height: 10px;
  background: #d8d0c4;
  margin-left: 11.5px;
}

.rules-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 24px;
  padding-top: 14px;
  border-top: 1px solid #eee;
}

.rule-tag {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #666;
}

.tag-mark {
  color: #9e2a2b;
  font-size: 10px;
}

.rule-tag strong {
  color: #2c2a29;
}

/* ============================================================
   筛选栏
   ============================================================ */
.filter-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 22px;
  flex-wrap: wrap;
  gap: 10px;
}

.filter-left {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  flex: 1;
}

.filter-left select,
.filter-left input {
  padding: 8px 14px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  border-radius: 2px;
  font-family: inherit;
  font-size: 13px;
  outline: none;
  letter-spacing: 0.05em;
  transition: border-color 0.25s;
  color: #2c2a29;
}

.filter-left select:focus,
.filter-left input:focus {
  border-color: #2c2a29;
}

.filter-left select {
  min-width: 120px;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='6' viewBox='0 0 10 6'%3E%3Cpath fill='%23999' d='M5 6L0 0h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  padding-right: 32px;
  cursor: pointer;
}

.filter-left input {
  flex: 1;
  min-width: 140px;
}

.result-count {
  font-size: 13px;
  color: #aaa;
  letter-spacing: 0.05em;
}

/* ============================================================
   加载 / 空
   ============================================================ */
.loading {
  padding: 40px 0;
  text-align: center;
  color: #aaa;
  font-size: 14px;
}

.empty {
  padding: 40px 0;
  text-align: center;
  color: #aaa;
  font-size: 14px;
}

.link-btn {
  background: none;
  border: none;
  color: #9e2a2b;
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
  border-bottom: 1px solid #9e2a2b;
  padding-bottom: 1px;
}
.link-btn:hover {
  color: #2c2a29;
  border-color: #2c2a29;
}

/* ============================================================
   卡片
   ============================================================ */
.battle-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.battle-card {
  display: flex;
  gap: 18px;
  padding: 18px 22px;
  background: #fcfaf7;
  border: 1px solid #d8d0c4;
  cursor: pointer;
  transition: all 0.25s;
}

.battle-card:hover {
  border-color: #2c2a29;
  transform: translateY(-2px);
  box-shadow: 0 4px 16px rgba(44, 42, 41, 0.06);
}

.card-cover {
  flex-shrink: 0;
  width: 72px;
  height: 72px;
  border: 1px solid #d8d0c4;
  overflow: hidden;
  background: #ede8e2;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.card-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.cover-placeholder {
  font-size: 26px;
  color: #9e2a2b;
}

.card-status {
  position: absolute;
  top: 4px;
  left: 4px;
  font-size: 9px;
  padding: 1px 8px;
  background: rgba(44, 42, 41, 0.8);
  color: #f5f0eb;
  letter-spacing: 0.05em;
}
.card-status.open {
  background: #8b7a6b;
}
.card-status.ongoing {
  background: #b8956a;
}
.card-status.judging {
  background: #b8956a;
}
.card-status.finished {
  background: #7a8a7a;
}
.card-status.cancelled {
  background: #999;
}

.card-body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.card-head {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.type-tag {
  font-size: 12px;
  color: #666;
  background: #ede8e2;
  padding: 0 12px;
  line-height: 22px;
  letter-spacing: 0.05em;
}

.judgment-tag {
  font-size: 12px;
  color: #666;
  background: #ede8e2;
  padding: 0 12px;
  line-height: 22px;
  letter-spacing: 0.05em;
}

.card-title {
  font-size: 17px;
  font-weight: 400;
  letter-spacing: 0.08em;
  margin: 0;
  color: #2c2a29;
}

.card-rules {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  background: #f5f0eb;
  padding: 6px 12px;
  border-left: 3px solid #9e2a2b;
}

.rules-label {
  font-size: 11px;
  color: #9e2a2b;
  flex-shrink: 0;
  letter-spacing: 0.1em;
}

.rules-text {
  font-size: 13px;
  color: #555;
  line-height: 1.6;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  font-size: 12px;
  color: #aaa;
  letter-spacing: 0.05em;
}

.meta-time {
  margin-left: auto;
}

.card-action {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding-left: 12px;
  font-size: 13px;
  color: #ccc;
  letter-spacing: 0.05em;
  transition: color 0.25s;
}

.battle-card:hover .card-action {
  color: #2c2a29;
}

/* ============================================================
   分页
   ============================================================ */
.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 14px;
  margin-top: 28px;
  padding-top: 18px;
  border-top: 1px solid #d8d0c4;
}

.pagination button {
  padding: 6px 18px;
  border: 1px solid #d8d0c4;
  background: transparent;
  cursor: pointer;
  font-family: inherit;
  font-size: 13px;
  transition: all 0.25s;
  color: #2c2a29;
}

.pagination button:hover:not(:disabled) {
  border-color: #2c2a29;
}

.pagination button:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.pagination span {
  font-size: 13px;
  color: #aaa;
  min-width: 60px;
  text-align: center;
}

/* ============================================================
   响应式
   ============================================================ */
@media (max-width: 768px) {
  .rules-body {
    grid-template-columns: 1fr;
    gap: 16px;
  }

  .flow-line {
    display: none;
  }

  .battle-card {
    flex-direction: column;
  }

  .card-cover {
    width: 100%;
    height: 100px;
  }

  .card-action {
    padding-left: 0;
    padding-top: 8px;
    border-top: 1px solid #eee;
    justify-content: flex-end;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions button {
    flex: 1;
    text-align: center;
  }

  .filter-left {
    flex-direction: column;
  }

  .filter-left select,
  .filter-left input {
    width: 100%;
  }

  .result-count {
    width: 100%;
    text-align: center;
  }

  .meta-time {
    margin-left: 0;
  }

  .rules-section {
    padding: 20px 18px;
  }

  .rules-footer {
    flex-direction: column;
    gap: 6px;
  }
}
</style>