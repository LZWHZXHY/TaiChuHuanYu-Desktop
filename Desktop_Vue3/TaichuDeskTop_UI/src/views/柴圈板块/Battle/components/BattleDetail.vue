<template>
  <div class="battle-detail">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载约战中...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!battle" class="empty-state">
      <p>约战不存在</p>
      <router-link to="/battles" class="empty-link">返回约战大厅</router-link>
    </div>

    <!-- 详情内容 -->
    <template v-else>
      <!-- 页面头部 -->
      <div class="page-header">
        <div class="header-left">
          <button class="back-btn" @click="goBack">← 返回</button>
          <div>
            <h1 class="page-title">{{ battle.title }}</h1>
            <div class="page-meta">
              <span class="meta-tag status" :class="battle.status">
                {{ statusLabel(battle.status) }}
              </span>
              <span class="meta-tag judgment">
                {{ battle.judgmentType === 'vote' ? '📊 投票制' : '🤝 内定制' }}
              </span>
              <span class="meta-tag type">{{ battle.battleType || '自定义' }}</span>
              <span class="meta-tag" :class="battle.isPublic ? 'public' : 'private'">
                {{ battle.isPublic ? '🌐 公开约战' : '🔒 指定对手' }}
              </span>
            </div>
          </div>
        </div>
        <div class="header-right">
          <button
            v-if="isInitiator && battle.status !== 'finished' && battle.status !== 'cancelled'"
            class="btn-line danger"
            @click="handleCancel"
          >
            取消约战
          </button>
        </div>
      </div>

      <!-- 对战参与者 -->
      <div class="combatants">
        <div
          v-for="(p, index) in battle.participants"
          :key="p.id"
          class="combatant"
          :class="{ challenger: index === 0, opponent: index > 0 }"
        >
          <span class="role">{{ index === 0 ? '发起方' : `参与者 ${index + 1}` }}</span>
          <strong>{{ p.userName }}</strong>
          <span class="oc-name">（{{ p.ocNames?.join('、') || '未知OC' }}）</span>
        </div>
        <div v-if="battle.participants.length < 2" class="combatant open-slot">
          <span class="role">等待报名</span>
          <span class="open-text">—</span>
        </div>
      </div>

      <!-- 规则 -->
      <div class="detail-section rules-section">
        <h3>📋 约战规则</h3>
        <div class="rules-content">{{ battle.rules }}</div>
      </div>

      <!-- 参与者列表 -->
      <div class="detail-section participants-section">
        <div class="section-header">
          <h3>👥 参与者</h3>
          <span class="section-count">{{ battle.participants?.length || 0 }} 人</span>
        </div>

        <div class="participant-actions">
          <!-- ⭐ 报名/应战/拒绝按钮 -->
          <template v-if="!isParticipant && !isInitiator && canJoin">
            <!-- 公开约战：报名参加 -->
            <button
              v-if="battle.isPublic"
              class="btn-action btn-join"
              :disabled="joining"
              @click="handleJoin"
            >
              {{ joining ? '报名中...' : '报名参加' }}
            </button>

            <!-- 指定对手且当前用户是对手：应战 + 拒绝 -->
            <template v-else-if="isSpecifiedOpponent">
              <button
                class="btn-action btn-join"
                :disabled="joining"
                @click="handleJoin"
              >
                {{ joining ? '应战中...' : '⚔️ 应战' }}
              </button>
              <button
                class="btn-action btn-reject"
                :disabled="rejecting"
                @click="handleReject"
              >
                {{ rejecting ? '处理中...' : '✕ 拒绝' }}
              </button>
            </template>

            <!-- 指定对手但不是当前用户 -->
            <span v-else class="locked-hint">
              🔒 此约战已指定对手
            </span>
          </template>

          <!-- 已报名：取消报名 -->
          <button
            v-if="isParticipant && !isInitiator"
            class="btn-action btn-cancel"
            :disabled="cancelling"
            @click="handleCancelJoin"
          >
            {{ cancelling ? '取消中...' : '取消报名' }}
          </button>

          <!-- 已拒绝提示 -->
          <span v-if="isRejected" class="rejected-hint">
            ❌ 你已拒绝此约战
          </span>

          <!-- 发起人：结束报名 -->
          <button
            v-if="isInitiator && battle.status === 'open'"
            class="btn-action btn-primary"
            @click="handleCloseRegistration"
          >
            结束报名 → 进入创作期
          </button>

          <!-- 发起人：结束创作 -->
          <button
            v-if="isInitiator && battle.status === 'ongoing'"
            class="btn-action btn-primary"
            @click="handleCloseCreation"
          >
            结束创作 → 进入判定期
          </button>

          <!-- 发起人：录入内定结果 -->
          <button
            v-if="isInitiator && battle.status === 'judging' && battle.judgmentType === 'internal'"
            class="btn-action btn-primary"
            @click="showInternalResultModal = true"
          >
            录入内定结果
          </button>

          <!-- 发起人：发布结果 -->
          <button
            v-if="isInitiator && battle.status === 'judging'"
            class="btn-action btn-publish"
            @click="handlePublishResult"
          >
            发布结果 → 完成约战
          </button>

          <!-- 投票制提示 -->
          <span v-if="battle.status === 'judging' && battle.judgmentType === 'vote'" class="vote-hint">
            📊 投票进行中，等待问卷结果
          </span>

          <span v-if="isInitiator" class="organizer-tip">（你发起的约战）</span>
        </div>

        <!-- 参与者列表 -->
        <div v-if="battle.participants?.length" class="participant-list">
          <div
            v-for="p in battle.participants"
            :key="p.id"
            class="participant-item"
          >
            <span class="participant-name">{{ p.userName }}</span>
            <span class="participant-oc">{{ p.ocNames?.join('、') || '无OC' }}</span>
            <span class="participant-status" :class="p.status">
              {{ participantStatusLabel(p.status) }}
            </span>
            <span v-if="p.result" class="participant-result" :class="p.result">
              {{ p.result === 'win' ? '🏆 胜' : p.result === 'lose' ? '💔 败' : '🤝 平' }}
            </span>
          </div>
        </div>
        <div v-else class="participant-empty">暂无参与者</div>
      </div>

      <!-- 作品提交区 -->
      <div v-if="battle.status !== 'open' && battle.status !== 'cancelled'" class="detail-section submissions-section">
        <div class="section-header">
          <h3>📝 作品提交</h3>
          <button
            v-if="isParticipant && battle.status === 'ongoing' && !hasSubmitted"
            class="btn-action btn-submit-work"
            @click="showSubmitModal = true"
          >
            提交作品
          </button>
          <span v-if="hasSubmitted" class="submitted-hint">✅ 已提交作品</span>
        </div>

        <div v-if="battle.submissions?.length" class="submissions-list">
          <div
            v-for="sub in battle.submissions"
            :key="sub.id"
            class="submission-item"
          >
            <div class="submission-info">
              <span class="sub-title">{{ sub.title }}</span>
              <span class="sub-author">by {{ sub.participant?.userName || '未知' }}</span>
            </div>
            <a :href="sub.contentUrl" target="_blank" class="sub-link">查看作品 →</a>
          </div>
        </div>
        <div v-else class="submissions-empty">暂无作品提交</div>
      </div>

      <!-- 结果展示 -->
      <div v-if="battle.status === 'finished'" class="detail-section result-section">
        <h3>🏆 最终结果</h3>
        <div class="result-display">
          <span class="result-text">{{ getResultText() }}</span>
        </div>
        <div class="participants-result">
          <div
            v-for="p in battle.participants"
            :key="p.id"
            class="result-item"
          >
            <span>{{ p.userName }}（{{ p.ocNames?.join('、') || '无OC' }}）</span>
            <span class="result-badge" :class="p.result">
              {{ p.result === 'win' ? '🏆 胜' : p.result === 'lose' ? '💔 败' : p.result === 'draw' ? '🤝 平' : '—' }}
            </span>
          </div>
        </div>
      </div>

      <!-- ===== 报名弹窗 ===== -->
      <el-dialog v-model="showRegisterModal" title="选择出战 OC" width="500px">
        <div class="modal-body">
          <p class="modal-hint">选择你用于此约战的 OC（可多选）：</p>
          <div class="oc-select-wrapper">
            <select v-model="selectedRegisterOcIds" multiple class="oc-select" style="height: auto; min-height: 100px; padding: 8px;">
              <option
                v-for="oc in myOcList"
                :key="oc.id"
                :value="oc.id"
                :disabled="oc.status !== 'published' || !oc.isBattleEnabled"
              >
                {{ oc.title }}
                <span v-if="oc.status !== 'published'" class="oc-disabled-tag">（未发布）</span>
                <span v-if="!oc.isBattleEnabled" class="oc-disabled-tag">（不可约战）</span>
              </option>
            </select>
          </div>
          <div v-if="selectedRegisterOcIds.length" class="selected-ocs" style="margin-top: 8px;">
            <span
              v-for="id in selectedRegisterOcIds"
              :key="id"
              class="selected-tag"
            >
              {{ getOcTitle(myOcList, id) }}
            </span>
          </div>
          <p class="hint">按住 Ctrl 键可多选</p>
        </div>
        <template #footer>
          <el-button @click="showRegisterModal = false">取消</el-button>
          <el-button type="primary" @click="handleRegisterSubmit" :disabled="!selectedRegisterOcIds.length">
            确认报名
          </el-button>
        </template>
      </el-dialog>

      <!-- 提交作品弹窗 -->
      <el-dialog v-model="showSubmitModal" title="提交作品" width="500px">
        <div class="modal-body">
          <div class="form-group">
            <label>作品标题 <span class="required">*</span></label>
            <input v-model="submitForm.title" placeholder="给作品起个名字" />
          </div>
          <div class="form-group">
            <label>作品链接 <span class="required">*</span></label>
            <input v-model="submitForm.contentUrl" placeholder="输入作品链接（视频/漫画/图文等）" />
            <p class="hint">支持任何链接形式，用户点击即可查看</p>
          </div>
          <div class="form-group">
            <label>简介</label>
            <textarea v-model="submitForm.description" rows="3" placeholder="简要描述作品内容"></textarea>
          </div>
        </div>
        <template #footer>
          <el-button @click="showSubmitModal = false">取消</el-button>
          <el-button type="primary" @click="handleSubmitWork" :disabled="!submitForm.title || !submitForm.contentUrl">
            提交
          </el-button>
        </template>
      </el-dialog>

      <!-- 内定结果弹窗 -->
      <el-dialog v-model="showInternalResultModal" title="录入内定结果" width="500px">
        <div class="modal-body">
          <p class="modal-hint">选择胜利的参与者（可多选，支持平局）：</p>
          <div class="winner-checkboxes">
            <label v-for="p in battle.participants" :key="p.id" class="winner-option">
              <input type="checkbox" v-model="internalResult.winnerIds" :value="p.id" />
              {{ p.userName }}（{{ p.ocNames?.join('、') || '无OC' }}）
            </label>
          </div>
          <div class="form-group">
            <label>结果说明</label>
            <input v-model="internalResult.resultDescription" placeholder="可简单描述结果原因" />
          </div>
        </div>
        <template #footer>
          <el-button @click="showInternalResultModal = false">取消</el-button>
          <el-button type="primary" @click="handleSetInternalResult" :disabled="!internalResult.winnerIds.length">
            确认结果
          </el-button>
        </template>
      </el-dialog>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useBattleStore } from '../battle_Store'
import { battleApi } from '../battle_api'
import { stickmanApi } from '../../OCs/stickman'
import { ElMessage, ElMessageBox } from 'element-plus'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const battleStore = useBattleStore()

const battle = computed(() => battleStore.currentBattle)
const loading = computed(() => battleStore.loading)

const joining = ref(false)
const cancelling = ref(false)
const rejecting = ref(false)
const showSubmitModal = ref(false)
const showInternalResultModal = ref(false)





// 报名弹窗相关
const showRegisterModal = ref(false)
const selectedRegisterOcIds = ref<string[]>([])
const myOcList = ref<any[]>([])

const submitForm = ref({
  title: '',
  description: '',
  contentUrl: '',
})

const internalResult = ref({
  winnerIds: [] as string[],
  resultDescription: '',
})

// ===== 身份判断 =====
const isInitiator = computed(() =>
  battle.value?.participants?.[0]?.userId === userStore.userInfo?.id
)

const isParticipant = computed(() =>
  battle.value?.participants?.some(p => p.userId === userStore.userInfo?.id) || false
)

const hasSubmitted = computed(() =>
  battle.value?.submissions?.some(s => s.participant?.userId === userStore.userInfo?.id) || false
)

const isSpecifiedOpponent = computed(() => {
  if (!battle.value || battle.value.isPublic) return false
  if (isParticipant.value) return false
  if (isRejected.value) return false
  const userId = userStore.userInfo?.id
  if (!userId) return false
  return !!battle.value.opponentOcIds?.[userId]
})

const isRejected = ref(false)

const canJoin = computed(() => {
  if (!battle.value) return false
  if (battle.value.status !== 'open') return false
  if (battle.value.participants?.some(p => p.userId === userStore.userInfo?.id)) return false
  if (isRejected.value) return false
  if (!battle.value.isPublic && !isSpecifiedOpponent.value) return false
  return true
})

// ===== 获取详情 =====
const fetchDetail = async () => {
  const id = route.params.id as string
  await battleStore.fetchDetail(id)
}

// ===== 获取我的OC列表 =====
const fetchMyOcs = async () => {
  try {
    const res = await stickmanApi.getMyCharacters({ status: 'published' })
    myOcList.value = Array.isArray(res) ? res : []
  } catch (error) {
    console.error('获取OC列表失败:', error)
  }
}

// ===== 工具函数 =====
function statusLabel(status: string): string {
  const map: Record<string, string> = {
    open: '待应战',
    ongoing: '创作中',
    judging: '定夺中',
    finished: '已了结',
    cancelled: '已罢战',
  }
  return map[status] || status
}

function participantStatusLabel(status: string): string {
  const map: Record<string, string> = {
    registered: '已报名',
    submitted: '已提交',
    eliminated: '已淘汰',
    finished: '已完成',
  }
  return map[status] || status
}

function getResultText(): string {
  if (!battle.value) return ''
  if (battle.value.result === 'draw') return '🤝 平局'
  const winner = battle.value.participants?.find(p => p.result === 'win')
  if (winner) return `🏆 ${winner.userName} 获胜`
  return '结果已记录'
}

function getOcTitle(list: any[], id: string): string {
  const oc = list.find((o: any) => o.id === id)
  return oc ? oc.title : id
}

function goBack() {
  router.push('/battles')
}

// ===== 报名相关 =====
const handleJoin = () => {
  if (!battle.value) return

  const userId = userStore.userInfo?.id
  if (!userId) {
    ElMessage.warning('请先登录')
    return
  }

  // 指定对手：直接使用发起者指定的OC
  if (!battle.value.isPublic && battle.value.opponentOcIds?.[userId]) {
    const ocIds = battle.value.opponentOcIds[userId]
    if (!ocIds.length) {
      ElMessage.warning('没有可用的OC')
      return
    }
    // 直接报名，不弹窗
    doRegister(ocIds)
    return
  }

  // 公开约战：弹窗选择
  selectedRegisterOcIds.value = []
  showRegisterModal.value = true
}

// ⭐ 抽取报名逻辑
const doRegister = async (ocIds: string[]) => {
  if (!battle.value) return
  joining.value = true
  try {
    await battleApi.register(battle.value.id, { ocIds, remark: '' })
    await fetchDetail()
    ElMessage.success('报名成功！')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '报名失败')
  } finally {
    joining.value = false
  }
}

const handleRegisterSubmit = async () => {
  if (!battle.value || !selectedRegisterOcIds.value.length) return
  await doRegister(selectedRegisterOcIds.value)
  showRegisterModal.value = false
  selectedRegisterOcIds.value = []
}

// ===== 拒绝约战 =====
const handleReject = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm(
      '确定拒绝此约战吗？拒绝后你将无法参与，该约战将变为公开约战。',
      '确认拒绝',
      { type: 'warning' }
    )
    rejecting.value = true
    await battleApi.reject(battle.value.id)
    isRejected.value = true
    await fetchDetail()
    ElMessage.success('已拒绝此约战')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    rejecting.value = false
  }
}

// ===== 取消报名 =====
const handleCancelJoin = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm('确定要取消报名吗？', '确认')
    await battleApi.unregister(battle.value.id)
    await fetchDetail()
    ElMessage.success('已取消报名')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

// ===== 结束报名 =====
const handleCloseRegistration = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm('确定结束报名吗？之后不可再报名。', '确认')
    await battleApi.closeRegistration(battle.value.id)
    await fetchDetail()
    ElMessage.success('已进入创作期')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

// ===== 结束创作 =====
const handleCloseCreation = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm('确定结束创作吗？参与者不可再修改作品。', '确认')
    await battleApi.closeCreation(battle.value.id)
    await fetchDetail()
    ElMessage.success('已进入判定期')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

// ===== 提交作品 =====
const handleSubmitWork = async () => {
  if (!battle.value) return
  if (!submitForm.value.title || !submitForm.value.contentUrl) {
    ElMessage.warning('请填写作品标题和链接')
    return
  }
  try {
    await battleApi.submit(battle.value.id, {
      title: submitForm.value.title,
      description: submitForm.value.description || undefined,
      contentUrl: submitForm.value.contentUrl,
    })
    showSubmitModal.value = false
    submitForm.value = { title: '', description: '', contentUrl: '' }
    await fetchDetail()
    ElMessage.success('作品提交成功！')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '提交失败')
  }
}

// ===== 录入内定结果 =====
const handleSetInternalResult = async () => {
  if (!battle.value) return
  if (!internalResult.value.winnerIds.length) {
    ElMessage.warning('请至少选择一位胜者')
    return
  }
  try {
    await battleApi.setInternalResult(battle.value.id, {
      winnerIds: internalResult.value.winnerIds,
      resultDescription: internalResult.value.resultDescription || undefined,
    })
    showInternalResultModal.value = false
    internalResult.value = { winnerIds: [], resultDescription: '' }
    await fetchDetail()
    ElMessage.success('结果录入成功！')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '操作失败')
  }
}

// ===== 发布结果 =====
const handlePublishResult = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm('确定发布最终结果吗？发布后不可修改。', '确认')
    await battleApi.publishResult(battle.value.id)
    await fetchDetail()
    ElMessage.success('约战完成！')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

// ===== 取消约战 =====
const handleCancel = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm('确定取消约战吗？', '确认')
    await battleApi.cancel(battle.value.id)
    await fetchDetail()
    ElMessage.success('已取消')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

onMounted(() => {
  isRejected.value = false
  fetchMyOcs()
  fetchDetail()
})

onUnmounted(() => {
  battleStore.clear()
})
</script>

<style scoped>
.battle-detail {
  max-width: 960px;
  margin: 0 auto;
  padding: 32px 20px 60px;
  background: #f5f0eb;
  min-height: 100vh;
  color: #2c2a29;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  gap: 16px;
  color: #999;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid #d8d0c4;
  border-top-color: #2c2a29;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.empty-state {
  padding: 80px 0;
  text-align: center;
  color: #999;
}

.empty-link {
  color: #9e2a2b;
  text-decoration: none;
  border-bottom: 1px solid #d8d0c4;
}

.empty-link:hover {
  border-color: #9e2a2b;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 2px solid #d8d0c4;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 12px;
}

.header-left {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.back-btn {
  background: none;
  border: none;
  font-size: 15px;
  color: #999;
  cursor: pointer;
  font-family: inherit;
  padding: 4px 8px 4px 0;
  transition: color 0.25s;
}
.back-btn:hover {
  color: #2c2a29;
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 8px 0;
  color: #2c2a29;
}

.page-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.meta-tag {
  font-size: 12px;
  padding: 2px 12px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  letter-spacing: 0.08em;
}

.meta-tag.status.open { border-color: #4CAF50; color: #4CAF50; }
.meta-tag.status.ongoing { border-color: #FF9800; color: #FF9800; }
.meta-tag.status.judging { border-color: #FF9800; color: #FF9800; }
.meta-tag.status.finished { border-color: #9E9E9E; color: #9E9E9E; }
.meta-tag.status.cancelled { border-color: #F44336; color: #F44336; }

.meta-tag.judgment {
  border-color: #9e2a2b;
  color: #9e2a2b;
}

.meta-tag.type {
  border-color: #d8d0c4;
  color: #666;
}

.meta-tag.public {
  border-color: #4CAF50;
  color: #4CAF50;
}
.meta-tag.private {
  border-color: #FF9800;
  color: #FF9800;
}

.header-right {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.btn-line {
  background: none;
  border: 1px solid #d8d0c4;
  color: #2c2a29;
  padding: 6px 16px;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-line:hover {
  border-color: #9e2a2b;
  color: #9e2a2b;
}
.btn-line.danger {
  border-color: #F44336;
  color: #F44336;
}
.btn-line.danger:hover {
  background: #F44336;
  color: #fff;
}

.combatants {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px 0;
  border-bottom: 1px solid #d8d0c4;
  margin-bottom: 24px;
  gap: 20px;
  flex-wrap: wrap;
}

.combatant {
  text-align: center;
  padding: 8px 16px;
}
.combatant .role {
  display: block;
  font-size: 12px;
  color: #999;
  letter-spacing: 0.08em;
}
.combatant strong {
  font-size: 20px;
  color: #2c2a29;
}
.combatant .oc-name {
  font-size: 14px;
  color: #888;
  margin-left: 4px;
}
.combatant .open-text {
  font-size: 16px;
  color: #999;
  font-style: italic;
}

.vs {
  font-size: 24px;
  color: #9e2a2b;
}

.detail-section {
  border-bottom: 1px solid #d8d0c4;
  padding-bottom: 20px;
  margin-bottom: 24px;
}
.detail-section:last-of-type {
  border-bottom: none;
}

.detail-section h3 {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.12em;
  margin: 0 0 12px 0;
  color: #2c2a29;
}

.rules-content {
  padding: 12px 16px;
  background: #fcfaf7;
  border-left: 3px solid #9e2a2b;
  white-space: pre-wrap;
  font-size: 14px;
  line-height: 1.8;
  color: #555;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}
.section-header h3 {
  margin: 0;
}
.section-count {
  font-size: 13px;
  color: #999;
}

.participant-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
  padding: 12px 0;
  border-bottom: 1px solid #d8d0c4;
  margin-bottom: 12px;
}

.btn-action {
  padding: 8px 20px;
  border: 1px solid #d8d0c4;
  background: transparent;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-action:hover:not(:disabled) {
  border-color: #9e2a2b;
  color: #9e2a2b;
}
.btn-action:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-join {
  border-color: #9e2a2b;
  color: #9e2a2b;
}
.btn-join:hover:not(:disabled) {
  background: #9e2a2b;
  color: #fff;
}

.btn-reject {
  border-color: #F44336;
  color: #F44336;
}
.btn-reject:hover:not(:disabled) {
  background: #F44336;
  color: #fff;
}

.btn-cancel {
  border-color: #999;
  color: #999;
}
.btn-cancel:hover:not(:disabled) {
  border-color: #F44336;
  color: #F44336;
}

.btn-primary {
  background: #2c2a29;
  color: #f5f0eb;
  border-color: #2c2a29;
}
.btn-primary:hover:not(:disabled) {
  background: transparent;
  color: #2c2a29;
}

.btn-publish {
  background: #4CAF50;
  color: #fff;
  border-color: #4CAF50;
}
.btn-publish:hover:not(:disabled) {
  background: #388E3C;
  border-color: #388E3C;
  color: #fff;
}

.btn-submit-work {
  border-color: #2196F3;
  color: #2196F3;
}
.btn-submit-work:hover:not(:disabled) {
  background: #2196F3;
  color: #fff;
}

.organizer-tip {
  font-size: 13px;
  color: #999;
}

.vote-hint {
  font-size: 13px;
  color: #FF9800;
  padding: 6px 14px;
  background: #fff3e0;
  border: 1px solid #FF9800;
}

.locked-hint {
  font-size: 13px;
  color: #999;
  padding: 6px 14px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
}

.rejected-hint {
  font-size: 13px;
  color: #F44336;
  padding: 6px 14px;
  border: 1px solid #F44336;
  background: #fff5f5;
}

.participant-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.participant-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  flex-wrap: wrap;
}

.participant-name {
  font-size: 14px;
  color: #2c2a29;
}
.participant-oc {
  font-size: 13px;
  color: #888;
}
.participant-status {
  font-size: 12px;
  padding: 1px 10px;
  border: 1px solid #d8d0c4;
}
.participant-status.registered { border-color: #FF9800; color: #FF9800; }
.participant-status.submitted { border-color: #4CAF50; color: #4CAF50; }
.participant-status.eliminated { border-color: #F44336; color: #F44336; }
.participant-status.finished { border-color: #9E9E9E; color: #9E9E9E; }

.participant-result {
  font-size: 13px;
  margin-left: auto;
}
.participant-result.win { color: #4CAF50; }
.participant-result.lose { color: #F44336; }
.participant-result.draw { color: #FF9800; }

.participant-empty {
  padding: 12px 0;
  text-align: center;
  color: #999;
  font-size: 14px;
}

.submitted-hint {
  font-size: 13px;
  color: #4CAF50;
}

.submissions-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.submission-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  flex-wrap: wrap;
  gap: 8px;
}

.sub-title {
  font-weight: 500;
  color: #2c2a29;
}
.sub-author {
  font-size: 13px;
  color: #888;
  margin-left: 8px;
}
.sub-link {
  color: #9e2a2b;
  text-decoration: none;
}
.sub-link:hover {
  text-decoration: underline;
}

.submissions-empty {
  padding: 12px 0;
  text-align: center;
  color: #999;
  font-size: 14px;
}

.result-display {
  padding: 12px 16px;
  background: #fcfaf7;
  border: 2px solid #2c2a29;
  margin-bottom: 12px;
}
.result-text {
  font-size: 20px;
  font-weight: 500;
  color: #2c2a29;
}

.participants-result {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 8px;
}
.result-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 12px;
  background: #fcfaf7;
  border: 1px solid #d8d0c4;
}
.result-badge.win { color: #4CAF50; }
.result-badge.lose { color: #F44336; }
.result-badge.draw { color: #FF9800; }

.modal-body .form-group {
  margin-bottom: 14px;
}
.modal-body .form-group label {
  display: block;
  font-size: 13px;
  margin-bottom: 4px;
  color: #2c2a29;
}
.modal-body .form-group input,
.modal-body .form-group textarea {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid #d8d0c4;
  background: #fff;
  font-family: inherit;
  font-size: 14px;
  outline: none;
}
.modal-body .form-group input:focus,
.modal-body .form-group textarea:focus {
  border-color: #2c2a29;
}
.modal-body .hint {
  font-size: 12px;
  color: #bbb;
  margin: 4px 0 0;
}
.modal-hint {
  margin-bottom: 12px;
  color: #666;
}
.winner-checkboxes {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 12px 0;
}
.winner-option {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border: 1px solid #d8d0c4;
  cursor: pointer;
}
.winner-option:hover {
  background: #f5f0eb;
}
.winner-option input[type="checkbox"] {
  width: 16px;
  height: 16px;
  cursor: pointer;
}

/* 多选下拉样式 */
.oc-select-wrapper select[multiple] {
  height: auto;
  min-height: 100px;
  padding: 8px;
}
.oc-select-wrapper select[multiple] option {
  padding: 4px 8px;
}
.oc-select-wrapper select[multiple] option:checked {
  background: #9e2a2b;
  color: #fff;
}

.oc-disabled-tag {
  color: #ccc;
  font-size: 12px;
}

.selected-ocs {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
}

.selected-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 10px;
  background: #ede8e2;
  border: 1px solid #d8d0c4;
  border-radius: 4px;
  font-size: 12px;
  color: #2c2a29;
}

@media (max-width: 640px) {
  .battle-detail { padding: 20px 12px 40px; }
  .page-header { flex-direction: column; align-items: flex-start; }
  .header-left { flex-wrap: wrap; }
  .combatants { flex-direction: column; gap: 8px; }
  .vs { transform: rotate(90deg); }
  .participants-result { grid-template-columns: 1fr; }
  .participant-actions { flex-direction: column; align-items: stretch; }
  .btn-action { text-align: center; }
}
</style>