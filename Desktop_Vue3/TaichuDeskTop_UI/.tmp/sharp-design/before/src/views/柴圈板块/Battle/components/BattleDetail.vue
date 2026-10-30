<template>
  <div class="community-detail battle-detail">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载约战中...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!battle" class="empty-state">
      <p>{{ loadError || '约战不存在' }}</p>
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
            :disabled="actionBusy" @click="handleCancel"
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
          <span class="role">{{ p.teamNumber ? `队伍 ${p.teamNumber === 1 ? 'A' : 'B'}` : (index === 0 ? '发起方' : `参与者 ${index + 1}`) }}</span>
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
            :disabled="actionBusy || battle.participants.length < 2" @click="handleCloseRegistration"
          >
            结束报名 → 进入创作期
          </button>

          <!-- 发起人：结束创作 -->
          <button
            v-if="isInitiator && battle.status === 'ongoing'"
            class="btn-action btn-primary"
            :disabled="actionBusy" @click="handleCloseCreation"
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
            :disabled="actionBusy" @click="handlePublishResult"
          >
            发布结果 → 完成约战
          </button>

          <!-- 投票制提示 -->
          <span v-if="battle.status === 'judging' && battle.judgmentType === 'vote'" class="vote-hint">
            📊 投票判定暂不可用，此约战尚未产生投票结果
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
      <el-dialog v-model="showRegisterModal" title="选择出战 OC" width="min(500px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy && !joining" :close-on-press-escape="!actionBusy && !joining" :show-close="!actionBusy && !joining">
        <div class="modal-body">
          <p class="modal-hint">选择你用于此约战的 OC（可多选）：</p>
          <div class="oc-select-wrapper">
            <label v-for="oc in myOcList" :key="oc.id" class="register-oc" :class="{ selected: selectedRegisterOcIds.includes(oc.id) }">
              <input type="checkbox" v-model="selectedRegisterOcIds" :value="oc.id" :disabled="joining" />
              <span>{{ oc.title }}</span>
            </label>
            <p v-if="!myOcList.length" class="hint">暂无可用 OC，请先发布人设并开启约战。</p>
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
          <p class="hint">已选择 {{ selectedRegisterOcIds.length }} 个 OC，点击条目可多选。</p>
        </div>
        <template #footer>
          <el-button :disabled="actionBusy || joining" @click="showRegisterModal = false">取消</el-button>
          <el-button type="primary" @click="handleRegisterSubmit" :loading="joining" :disabled="!selectedRegisterOcIds.length">
            确认报名
          </el-button>
        </template>
      </el-dialog>

      <!-- 提交作品弹窗 -->
      <el-dialog v-model="showSubmitModal" title="提交作品" width="min(500px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy && !joining" :close-on-press-escape="!actionBusy && !joining" :show-close="!actionBusy && !joining">
        <div class="modal-body">
          <div class="form-group">
            <label>作品标题 <span class="required">*</span></label>
            <input aria-label="作品标题" v-model="submitForm.title" placeholder="给作品起个名字" />
          </div>
          <div class="form-group">
            <label>作品链接 <span class="required">*</span></label>
            <input aria-label="作品链接" v-model="submitForm.contentUrl" placeholder="输入作品链接（视频/漫画/图文等）" />
            <p class="hint">支持任何链接形式，用户点击即可查看</p>
          </div>
          <div class="form-group">
            <label>简介</label>
            <textarea aria-label="简介" v-model="submitForm.description" rows="3" placeholder="简要描述作品内容"></textarea>
          </div>
        </div>
        <template #footer>
          <el-button :disabled="actionBusy || joining" @click="showSubmitModal = false">取消</el-button>
          <el-button type="primary" @click="handleSubmitWork" :loading="actionBusy" :disabled="!submitForm.title.trim() || !submitForm.contentUrl.trim()">
            提交
          </el-button>
        </template>
      </el-dialog>

      <!-- 内定结果弹窗 -->
      <el-dialog v-model="showInternalResultModal" title="录入内定结果" width="min(500px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy && !joining" :close-on-press-escape="!actionBusy && !joining" :show-close="!actionBusy && !joining">
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
            <input aria-label="结果说明" v-model="internalResult.resultDescription" placeholder="可简单描述结果原因" />
          </div>
        </div>
        <template #footer>
          <el-button :disabled="actionBusy || joining" @click="showInternalResultModal = false">取消</el-button>
          <el-button type="primary" @click="handleSetInternalResult" :loading="actionBusy" :disabled="!internalResult.winnerIds.length">
            确认结果
          </el-button>
        </template>
      </el-dialog>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
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
const confirmOptions = { confirmButtonText: '确定', cancelButtonText: '取消' }

const loadError = ref('')
const battle = computed(() => battleStore.currentBattle)
const loading = computed(() => battleStore.loading)

const joining = ref(false)
const actionBusy = ref(false)
const cancelling = actionBusy
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
  (battle.value?.initiatorId || battle.value?.participants?.[0]?.userId)?.toLowerCase() === userStore.userId?.toLowerCase()
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
    myOcList.value = Array.isArray(res) ? res.filter(oc => oc.isBattleEnabled) : []
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
  if (!battle.value || joining.value) return false
  joining.value = true
  try {
    const updated = await battleApi.register(battle.value.id, { ocIds, remark: '' })
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('报名成功！')
    return true
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '报名失败')
    return false
  } finally {
    joining.value = false
  }
}

const handleRegisterSubmit = async () => {
  if (!battle.value || !selectedRegisterOcIds.value.length) return
  if (await doRegister([...selectedRegisterOcIds.value])) {
    showRegisterModal.value = false
    selectedRegisterOcIds.value = []
  }
}

// ===== 拒绝约战 =====
const handleReject = async () => {
  if (!battle.value) return
  try {
    await ElMessageBox.confirm(
      '确定拒绝此邀请吗？其他人的邀请会保留；无人受邀时约战将转为公开报名。',
      '确认拒绝',
      { ...confirmOptions, type: 'warning' }
    )
    rejecting.value = true
    const updated = await battleApi.reject(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    isRejected.value = true
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
  if (!battle.value || actionBusy.value) return
  actionBusy.value = true
  try {
    await ElMessageBox.confirm('确定要取消报名吗？', '确认', confirmOptions)
    const updated = await battleApi.unregister(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('已取消报名')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

// ===== 结束报名 =====
const handleCloseRegistration = async () => {
  if (!battle.value || actionBusy.value) return
  actionBusy.value = true
  try {
    await ElMessageBox.confirm('确定结束报名吗？之后不可再报名。', '确认', confirmOptions)
    const updated = await battleApi.closeRegistration(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('已进入创作期')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

// ===== 结束创作 =====
const handleCloseCreation = async () => {
  if (!battle.value || actionBusy.value) return
  actionBusy.value = true
  try {
    await ElMessageBox.confirm('确定结束创作吗？参与者不可再修改作品。', '确认', confirmOptions)
    const updated = await battleApi.closeCreation(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('已进入判定期')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

// ===== 提交作品 =====
const handleSubmitWork = async () => {
  if (!battle.value || actionBusy.value) return
  if (!submitForm.value.title.trim() || !submitForm.value.contentUrl.trim()) {
    ElMessage.warning('请填写作品标题和链接')
    return
  }
  actionBusy.value = true
  try {
    const updated = await battleApi.submit(battle.value.id, {
      title: submitForm.value.title.trim(),
      description: submitForm.value.description || undefined,
      contentUrl: submitForm.value.contentUrl.trim(),
    })
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    showSubmitModal.value = false
    submitForm.value = { title: '', description: '', contentUrl: '' }
    ElMessage.success('作品提交成功！')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '提交失败')
  } finally {
    actionBusy.value = false
  }
}

// ===== 录入内定结果 =====
const handleSetInternalResult = async () => {
  if (!battle.value || actionBusy.value) return
  if (!internalResult.value.winnerIds.length) {
    ElMessage.warning('请至少选择一位胜者')
    return
  }
  actionBusy.value = true
  try {
    const updated = await battleApi.setInternalResult(battle.value.id, {
      winnerIds: internalResult.value.winnerIds,
      resultDescription: internalResult.value.resultDescription || undefined,
    })
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    showInternalResultModal.value = false
    internalResult.value = { winnerIds: [], resultDescription: '' }
    ElMessage.success('结果录入成功！')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '操作失败')
  } finally {
    actionBusy.value = false
  }
}

// ===== 发布结果 =====
const handlePublishResult = async () => {
  if (!battle.value || actionBusy.value) return
  actionBusy.value = true
  try {
    await ElMessageBox.confirm('确定发布最终结果吗？发布后不可修改。', '确认', confirmOptions)
    const updated = await battleApi.publishResult(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('约战完成！')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

// ===== 取消约战 =====
const handleCancel = async () => {
  if (!battle.value || actionBusy.value) return
  actionBusy.value = true
  try {
    await ElMessageBox.confirm('确定取消约战吗？', '确认', confirmOptions)
    const updated = await battleApi.cancel(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('已取消')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

watch(() => route.params.id, async () => {
  isRejected.value = false
  showRegisterModal.value = showSubmitModal.value = showInternalResultModal.value = false
  loadError.value = ''
  fetchMyOcs()
  try { await fetchDetail() }
  catch { loadError.value = '约战详情加载失败，请刷新重试' }
}, { immediate: true })

onUnmounted(() => {
  battleStore.clear()
})
</script>

<style scoped>
.register-oc { display: flex; gap: 12px; align-items: center; padding: 14px; margin-bottom: 8px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.register-oc.selected { border-color: var(--accent-color); background: var(--bg-soft); }
.register-oc input { width: 18px; height: 18px; accent-color: var(--accent-color); }
.register-oc span { overflow-wrap: anywhere; }
.battle-detail {
  max-width: 960px;
  margin: 0 auto;
  padding: 32px 20px 60px;
  background: var(--bg-sub);
  min-height: 100vh;
  color: var(--text-primary);
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  gap: 16px;
  color: var(--text-secondary);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid var(--border-line);
  border-top-color: var(--text-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.empty-state {
  padding: 80px 0;
  text-align: center;
  color: var(--text-secondary);
}

.empty-link {
  color: var(--accent-color);
  text-decoration: none;
  border-bottom: 1px solid var(--border-line);
}

.empty-link:hover {
  border-color: var(--accent-color);
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 2px solid var(--border-line);
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
  color: var(--text-secondary);
  cursor: pointer;
  font-family: inherit;
  padding: 4px 8px 4px 0;
  transition: color 0.25s;
}
.back-btn:hover {
  color: var(--text-primary);
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 8px 0;
  color: var(--text-primary);
}

.page-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.meta-tag {
  font-size: 12px;
  padding: 2px 12px;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  letter-spacing: 0.08em;
}

.meta-tag.status.open { border-color: var(--success); color: var(--success); }
.meta-tag.status.ongoing { border-color: var(--warning); color: var(--warning); }
.meta-tag.status.judging { border-color: var(--warning); color: var(--warning); }
.meta-tag.status.finished { border-color: var(--text-secondary); color: var(--text-secondary); }
.meta-tag.status.cancelled { border-color: var(--danger); color: var(--danger); }

.meta-tag.judgment {
  border-color: var(--accent-color);
  color: var(--accent-color);
}

.meta-tag.type {
  border-color: var(--border-line);
  color: var(--text-secondary);
}

.meta-tag.public {
  border-color: var(--success);
  color: var(--success);
}
.meta-tag.private {
  border-color: var(--warning);
  color: var(--warning);
}

.header-right {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.btn-line {
  background: none;
  border: 1px solid var(--border-line);
  color: var(--text-primary);
  padding: 6px 16px;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-line:hover {
  border-color: var(--accent-color);
  color: var(--accent-color);
}
.btn-line.danger {
  border-color: var(--danger);
  color: var(--danger);
}
.btn-line.danger:hover {
  background: var(--danger);
  color: var(--on-accent);
}

.combatants {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px 0;
  border-bottom: 1px solid var(--border-line);
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
  color: var(--text-secondary);
  letter-spacing: 0.08em;
}
.combatant strong {
  font-size: 20px;
  color: var(--text-primary);
}
.combatant .oc-name {
  font-size: 14px;
  color: var(--text-secondary);
  margin-left: 4px;
}
.combatant .open-text {
  font-size: 16px;
  color: var(--text-secondary);
  font-style: italic;
}

.vs {
  font-size: 24px;
  color: var(--accent-color);
}

.detail-section {
  border-bottom: 1px solid var(--border-line);
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
  color: var(--text-primary);
}

.rules-content {
  padding: 12px 16px;
  background: var(--paper-card);
  border-left: 3px solid var(--accent-color);
  white-space: pre-wrap;
  font-size: 14px;
  line-height: 1.8;
  color: var(--text-secondary);
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
  color: var(--text-secondary);
}

.participant-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
  padding: 12px 0;
  border-bottom: 1px solid var(--border-line);
  margin-bottom: 12px;
}

.btn-action {
  padding: 8px 20px;
  border: 1px solid var(--border-line);
  background: transparent;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-action:hover:not(:disabled) {
  border-color: var(--accent-color);
  color: var(--accent-color);
}
.btn-action:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-join {
  border-color: var(--accent-color);
  color: var(--accent-color);
}
.btn-join:hover:not(:disabled) {
  background: var(--accent-color);
  color: var(--on-accent);
}

.btn-reject {
  border-color: var(--danger);
  color: var(--danger);
}
.btn-reject:hover:not(:disabled) {
  background: var(--danger);
  color: var(--on-accent);
}

.btn-cancel {
  border-color: var(--text-secondary);
  color: var(--text-secondary);
}
.btn-cancel:hover:not(:disabled) {
  border-color: var(--danger);
  color: var(--danger);
}

.btn-primary {
  background: var(--text-primary);
  color: var(--bg-sub);
  border-color: var(--text-primary);
}
.btn-primary:hover:not(:disabled) {
  background: transparent;
  color: var(--text-primary);
}

.btn-publish {
  background: var(--success);
  color: var(--on-accent);
  border-color: var(--success);
}
.btn-publish:hover:not(:disabled) {
  background: var(--success);
  border-color: var(--success);
  color: var(--on-accent);
}

.btn-submit-work {
  border-color: var(--info);
  color: var(--info);
}
.btn-submit-work:hover:not(:disabled) {
  background: var(--info);
  color: var(--on-accent);
}

.organizer-tip {
  font-size: 13px;
  color: var(--text-secondary);
}

.vote-hint {
  font-size: 13px;
  color: var(--warning);
  padding: 6px 14px;
  background: var(--accent-soft);
  border: 1px solid var(--warning);
}

.locked-hint {
  font-size: 13px;
  color: var(--text-secondary);
  padding: 6px 14px;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
}

.rejected-hint {
  font-size: 13px;
  color: var(--danger);
  padding: 6px 14px;
  border: 1px solid var(--danger);
  background: var(--accent-soft);
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
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  flex-wrap: wrap;
}

.participant-name {
  font-size: 14px;
  color: var(--text-primary);
}
.participant-oc {
  font-size: 13px;
  color: var(--text-secondary);
}
.participant-status {
  font-size: 12px;
  padding: 1px 10px;
  border: 1px solid var(--border-line);
}
.participant-status.registered { border-color: var(--warning); color: var(--warning); }
.participant-status.submitted { border-color: var(--success); color: var(--success); }
.participant-status.eliminated { border-color: var(--danger); color: var(--danger); }
.participant-status.finished { border-color: var(--text-secondary); color: var(--text-secondary); }

.participant-result {
  font-size: 13px;
  margin-left: auto;
}
.participant-result.win { color: var(--success); }
.participant-result.lose { color: var(--danger); }
.participant-result.draw { color: var(--warning); }

.participant-empty {
  padding: 12px 0;
  text-align: center;
  color: var(--text-secondary);
  font-size: 14px;
}

.submitted-hint {
  font-size: 13px;
  color: var(--success);
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
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  flex-wrap: wrap;
  gap: 8px;
}

.sub-title {
  font-weight: 500;
  color: var(--text-primary);
}
.sub-author {
  font-size: 13px;
  color: var(--text-secondary);
  margin-left: 8px;
}
.sub-link {
  color: var(--accent-color);
  text-decoration: none;
}
.sub-link:hover {
  text-decoration: underline;
}

.submissions-empty {
  padding: 12px 0;
  text-align: center;
  color: var(--text-secondary);
  font-size: 14px;
}

.result-display {
  padding: 12px 16px;
  background: var(--paper-card);
  border: 2px solid var(--text-primary);
  margin-bottom: 12px;
}
.result-text {
  font-size: 20px;
  font-weight: 500;
  color: var(--text-primary);
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
  background: var(--paper-card);
  border: 1px solid var(--border-line);
}
.result-badge.win { color: var(--success); }
.result-badge.lose { color: var(--danger); }
.result-badge.draw { color: var(--warning); }

.modal-body .form-group {
  margin-bottom: 14px;
}
.modal-body .form-group label {
  display: block;
  font-size: 13px;
  margin-bottom: 4px;
  color: var(--text-primary);
}
.modal-body .form-group input,
.modal-body .form-group textarea {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  font-family: inherit;
  font-size: 14px;
  outline: none;
}
.modal-body .form-group input:focus,
.modal-body .form-group textarea:focus {
  border-color: var(--text-primary);
}
.modal-body .hint {
  font-size: 12px;
  color: var(--text-secondary);
  margin: 4px 0 0;
}
.modal-hint {
  margin-bottom: 12px;
  color: var(--text-secondary);
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
  border: 1px solid var(--border-line);
  cursor: pointer;
}
.winner-option:hover {
  background: var(--bg-sub);
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
  background: var(--accent-color);
  color: var(--on-accent);
}

.oc-disabled-tag {
  color: var(--border-line);
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
  background: var(--bg-sub);
  border: 1px solid var(--border-line);
  border-radius: 4px;
  font-size: 12px;
  color: var(--text-primary);
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
