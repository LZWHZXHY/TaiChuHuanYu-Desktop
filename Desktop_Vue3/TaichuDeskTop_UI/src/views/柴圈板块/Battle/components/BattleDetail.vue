<template>
  <div class="community-detail battle-detail">
    <div class="bd-wrap">
      <!-- 加载状态（已经有内容时刷新不再整页闪成加载圈，比如审核一件作品之后） -->
      <div v-if="loading && !battle" class="bd-state">
        <div class="spinner"></div>
        <span>加载约战中...</span>
      </div>

      <!-- 空状态 -->
      <div v-else-if="!battle" class="bd-state">
        <p>{{ loadError || '约战不存在，可能已经被删除' }}</p>
        <router-link to="/battles" class="bd-link">返回约战广场</router-link>
      </div>

      <!-- 详情内容 -->
      <template v-else>
        <!-- ===== 标题 ===== -->
        <header class="bd-top">
          <button class="bd-back" @click="goBack">← 约战广场</button>
          <div class="bd-titlebar">
            <h1 class="bd-title">{{ battle.title }}</h1>
            <span class="bd-status" :class="'st-' + battle.status">{{ statusLabel(battle.status) }}</span>
          </div>
          <ul class="bd-meta">
            <li>{{ battle.judgmentType === 'vote' ? '📊 投票制：路人投票定胜负' : '🤝 内定制：参与者协商定胜负' }}</li>
            <li>{{ battle.battleType || '自定义' }}</li>
            <li>{{ battle.isPublic ? '🌐 公开报名' : '🔒 指定对手' }}</li>
            <li v-if="isInitiator" class="bd-meta-mine">你发起的约战</li>
          </ul>
        </header>

        <!-- ===== 对阵图 ===== -->
        <BattleVersus :battle="battle" :ocs="ocMap" />

        <!-- ===== 结果横幅（已结束） ===== -->
        <section v-if="battle.status === 'finished'" class="bd-result" :class="'o-' + (headline.outcome || 'none')">
          <div class="bd-result-main">
            <span class="bd-result-icon" aria-hidden="true">{{ headline.icon }}</span>
            <div>
              <p class="bd-result-text">{{ headline.text }}</p>
              <p class="bd-result-rule">{{ outcomeMeta(headline.outcome)?.record || '战绩已记录' }}</p>
            </div>
          </div>
          <p v-if="battle.resultDescription" class="bd-result-desc">{{ battle.resultDescription }}</p>
          <footer class="bd-result-foot">
            <span v-if="battle.finishedAt">结束于 {{ fmtTime(battle.finishedAt) }}</span>
            <!-- 管理员修正以前录错的结果（不显眼，只有审核员看得到） -->
            <button v-if="battle.canReview" type="button" class="bd-admin-link" :disabled="actionBusy" @click="openResultModal('correct')">修正结果（管理员）</button>
          </footer>
        </section>

        <!-- ===== 进度 ===== -->
        <BattleStageBar :battle="battle" class="bd-stages" />

        <!-- ===== 操作区：我的参战（参与者 / 想报名的人）+ 发起人操作 ===== -->
        <div v-if="showMine || showHost" class="bd-actions" :class="{ two: showMine && showHost }">
          <!-- 我的参战 -->
          <section v-if="showMine" class="bd-card bd-mine">
            <h2 class="bd-card-title">{{ isParticipant ? '我的参战' : '参加这场约战' }}</h2>

            <!-- 报名期 -->
            <template v-if="battle.status === 'open'">
              <template v-if="!isParticipant && !isInitiator && canJoin">
                <!-- 公开约战：报名参加 -->
                <template v-if="battle.isPublic">
                  <p class="bd-tip">公开约战，谁都可以报名：选一个开启了约战的 OC 就行。</p>
                  <div class="bd-btns">
                    <button class="bd-btn main" :disabled="joining" @click="handleJoin">{{ joining ? '报名中...' : '报名参加' }}</button>
                  </div>
                </template>
                <!-- 指定对手且当前用户是对手：应战 + 拒绝 -->
                <template v-else-if="isSpecifiedOpponent">
                  <p class="bd-tip">发起人点名要和你打，出战 OC 已经由发起人选好。应战后等发起人开始创作期。</p>
                  <div class="bd-btns">
                    <button class="bd-btn main" :disabled="joining" @click="handleJoin">{{ joining ? '应战中...' : '⚔️ 应战' }}</button>
                    <button class="bd-btn danger" :disabled="rejecting" @click="handleReject">{{ rejecting ? '处理中...' : '拒绝' }}</button>
                  </div>
                  <!-- 发起人指定的我方 OC 没填约战面板：提醒一下（不拦着应战） -->
                  <BattlePanelReminder :oc-ids="battle.opponentOcIds?.[userStore.userId || ''] ?? []" own />
                </template>
              </template>

              <!-- 已报名：取消报名 -->
              <template v-if="isParticipant && !isInitiator">
                <p class="bd-tip">你已经报名了，等发起人结束报名后进入创作期，到时候会通知你交作品。</p>
                <div class="bd-btns">
                  <button class="bd-btn" :disabled="cancelling" @click="handleCancelJoin">{{ cancelling ? '取消中...' : '取消报名' }}</button>
                </div>
              </template>

              <!-- 已拒绝提示 -->
              <p v-if="isRejected" class="bd-note danger">❌ 你已拒绝此约战</p>

              <!-- 指定对手、但不是我：只能围观 -->
              <p v-if="lockedForMe" class="bd-note">🔒 这场约战指定了对手，你可以围观，在下面的讨论区聊聊。</p>
            </template>

            <!-- 创作期 / 判定期：交作品、看审核状态、改作品 -->
            <template v-else-if="isParticipant">
              <template v-if="mySubmission">
                <div class="bd-mywork" :class="'rv-' + (mySubmission.reviewStatus || 'none')">
                  <div class="bd-mywork-head">
                    <b>{{ mySubmission.title }}</b>
                    <span v-if="mySubmission.reviewStatus" class="bd-chip" :class="mySubmission.reviewStatus">{{ reviewLabel(mySubmission.reviewStatus) }}</span>
                  </div>
                  <a v-if="isWebLink(mySubmission.contentUrl)" :href="mySubmission.contentUrl" target="_blank" rel="noopener noreferrer" class="bd-mywork-link">{{ mySubmission.contentUrl }}</a>
                  <p class="bd-tip">{{ reviewTip(mySubmission) }}</p>
                </div>
                <div v-if="canEditMine" class="bd-btns">
                  <button class="bd-btn" :class="{ main: myReview === 'rejected' }" :disabled="actionBusy" @click="openEditSubmission">修改作品</button>
                  <small class="bd-tip">{{ battle.status === 'ongoing' ? '创作期结束前都能改，改了要重新审核' : '被打回的作品判定期也能改，改好重新审核' }}</small>
                </div>
              </template>
              <template v-else-if="battle.status === 'ongoing'">
                <p class="bd-tip">创作期进行中：作品做好后，把作品的网址交上来（视频、漫画、图文都行）。发起人点「结束创作」后就不能再交了，抓紧哦。</p>
                <div class="bd-btns">
                  <button class="bd-btn main" @click="showSubmitModal = true">提交作品</button>
                </div>
              </template>
              <p v-else class="bd-tip">创作期已经结束，你这场没有交作品。</p>
            </template>
          </section>

          <!-- 发起人操作：只有发起人看得到，按阶段只给「下一步」该点的按钮 -->
          <section v-if="showHost" class="bd-card bd-host">
            <header class="bd-host-head">
              <h2 class="bd-card-title">发起人操作</h2>
              <small>只有你看得到</small>
            </header>

            <!-- 报名期 -->
            <template v-if="battle.status === 'open'">
              <p class="bd-tip">
                已有 <b>{{ battle.participants.length }}</b> 人报名<template v-if="battle.participants.length < 2">，至少要 2 人才能开始</template>。
                结束报名后进入创作期，大家开始交作品；之后不能再报名。
              </p>
              <div class="bd-btns">
                <button class="bd-btn main" :disabled="actionBusy || battle.participants.length < 2" @click="handleCloseRegistration">结束报名，开始创作期</button>
              </div>
            </template>

            <!-- 创作期：先看交了几份，按钮放在进度下面（和「结束报名」不在同一个位置），刚开始的几秒还点不了，防止连点 -->
            <template v-else-if="battle.status === 'ongoing'">
              <div class="bd-progress">
                <div class="bd-progress-num"><b>{{ progress.done }}</b> / {{ progress.total }} 人已交作品</div>
                <div class="bd-progress-bar" aria-hidden="true"><span :style="{ width: progress.total ? `${(progress.done / progress.total) * 100}%` : '0%' }"></span></div>
                <ul class="bd-progress-list">
                  <li v-for="p in battle.participants" :key="p.id" :class="{ ok: submittedIds.has(p.id) }">{{ submittedIds.has(p.id) ? '✓' : '○' }} {{ p.userName }}</li>
                </ul>
              </div>
              <p class="bd-tip">等大家都交了再结束创作。结束后进入判定期，不能再交、也不能再改作品。</p>
              <div class="bd-btns end">
                <button class="bd-btn main" :disabled="actionBusy || creationLocked" @click="handleCloseCreation">
                  {{ creationLocked ? `结束创作（${creationWait} 秒后可点）` : '结束创作，进入判定' }}
                </button>
              </div>
            </template>

            <!-- 判定期：录入 → 公布 -->
            <template v-else-if="battle.status === 'judging'">
              <p v-if="recorded" class="bd-recorded">
                已录入：<b>{{ headline.icon }} {{ headline.text }}</b>
                <small>还没公布，大家看到的还是「判定中」；公布前可以再改。</small>
              </p>
              <p v-else class="bd-tip">还没录入结果。先「录入结果」，确认无误再「公布结果」。</p>
              <!-- 投票制提示 -->
              <p v-if="battle.judgmentType === 'vote'" class="bd-note">📊 路人投票中，公布结果时按票数定胜负（没人投票时才用「录入结果」）</p>
              <div class="bd-btns">
                <button class="bd-btn" :disabled="actionBusy" @click="openResultModal('record')">{{ recorded ? '修改录入的结果' : '录入结果' }}</button>
                <button class="bd-btn main" :disabled="actionBusy || (battle.judgmentType === 'internal' && !recorded)" @click="handlePublishResult">公布结果，结束约战</button>
              </div>
            </template>

            <footer class="bd-host-foot">
              <button class="bd-text-btn danger" :disabled="actionBusy" @click="handleCancel">取消这场约战</button>
            </footer>
          </section>
        </div>

        <!-- ===== 规则 ===== -->
        <section class="bd-sec">
          <h2 class="bd-h">约战规则</h2>
          <div class="bd-rules">{{ battle.rules || '发起人没有写规则' }}</div>
        </section>

        <!-- ===== 出战人设：双方派出的 OC 的详细设定（以前只有名字，路人没法讨论） ===== -->
        <section class="bd-sec">
          <BattleOcCards :participants="battle.participants" @loaded="ocMap = $event" />
        </section>

        <!-- ===== 作品 ===== -->
        <section v-if="battle.status !== 'open' && battle.status !== 'cancelled'" class="bd-sec">
          <div class="bd-sec-head">
            <h2 class="bd-h">作品</h2>
            <span class="bd-count">{{ progress.done }} / {{ progress.total }} 人已交</span>
          </div>
          <ul v-if="battle.submissions?.length" class="bd-works">
            <li v-for="sub in battle.submissions" :key="sub.id" class="bd-work" :class="'side-' + (sideOf(battle, ownerOf(sub)?.id) || 'A').toLowerCase()">
              <div class="bd-work-main">
                <b class="bd-work-title">{{ sub.title }}</b>
                <span class="bd-work-by">
                  <UserLink :id="ownerOf(sub)?.userId" :name="ownerOf(sub)?.userName || '未知'" />
                  <template v-if="ownerOf(sub)?.ocNames?.length">（{{ ownerOf(sub)!.ocNames.join('、') }}）</template>
                </span>
                <p v-if="sub.description" class="bd-work-desc">{{ sub.description }}</p>
                <!-- 作品链接要管理员审核通过后，别人才看得到 -->
                <span v-if="sub.reviewStatus && sub.reviewStatus !== 'approved'" class="bd-chip" :class="sub.reviewStatus">
                  {{ reviewLabel(sub.reviewStatus) }}<template v-if="sub.reviewNote">：{{ sub.reviewNote }}</template>
                </span>
              </div>
              <div class="bd-work-side">
                <!-- 只把 http(s) 链接做成可点的，防止 javascript: 之类的链接 -->
                <a v-if="isWebLink(sub.contentUrl)" :href="sub.contentUrl" target="_blank" rel="noopener noreferrer" class="bd-btn">打开作品 ↗</a>
                <span v-else-if="sub.contentUrl" class="bd-work-url">{{ sub.contentUrl }}</span>
                <span v-else class="bd-work-url muted">审核通过后可以查看</span>
                <!-- 审核员：通过 / 打回 -->
                <span v-if="battle.canReview && sub.reviewStatus !== 'approved'" class="bd-review">
                  <button type="button" class="bd-text-btn" :disabled="actionBusy" @click="reviewWork(sub.id, true)">通过</button>
                  <button type="button" class="bd-text-btn danger" :disabled="actionBusy" @click="reviewWork(sub.id, false)">打回</button>
                </span>
              </div>
            </li>
          </ul>
          <p v-else class="bd-empty">{{ battle.status === 'ongoing' ? '还没有人交作品。' : '这场没有人交作品。' }}</p>
        </section>

        <!-- ===== 投票制：路人投票（用问卷系统） ===== -->
        <section v-if="battle.judgmentType === 'vote' && (battle.status === 'judging' || battle.status === 'finished')" class="bd-sec">
          <BattleVote :battle-id="battle.id" :status="battle.status" :is-initiator="isInitiator" />
        </section>

        <!-- ===== 战后记录（已结束才有；组件自己带标题、自己读数据，读不到时什么都不渲染，空盒子用 :empty 藏掉） ===== -->
        <section v-if="battle.status === 'finished'" class="bd-sec bd-aftermath">
          <BattleAftermath :battle-id="battle.id" :status="battle.status" />
        </section>

        <!-- ===== 参战名单 ===== -->
        <section class="bd-sec">
          <div class="bd-sec-head">
            <h2 class="bd-h">参战名单</h2>
            <span class="bd-count">{{ battle.participants?.length || 0 }} 人</span>
          </div>
          <ul v-if="battle.participants?.length" class="bd-roster">
            <li v-for="p in battle.participants" :key="p.id" :class="'side-' + (sideOf(battle, p.id) || 'A').toLowerCase()">
              <span class="bd-roster-side">{{ sideOf(battle, p.id) === 'B' ? sides[1].label : sides[0].label }}</span>
              <UserLink class="bd-roster-name" :id="p.userId" :name="p.userName" />
              <span class="bd-roster-oc">{{ p.ocNames?.join('、') || '无OC' }}</span>
              <span class="bd-roster-status" :class="p.status">{{ participantStatusLabel(p.status) }}</span>
              <span v-if="battle.status === 'finished'" class="bd-roster-result" :class="p.result || 'none'">{{ resultLabel(p.result, headline.outcome) }}</span>
            </li>
          </ul>
          <p v-else class="bd-empty">暂无参与者</p>
        </section>

        <!-- 讨论区：所有登录的人都能评论（参赛的、路人都可以），没登录能看不能发；以前约战页没有评论区 -->
        <section class="bd-sec bd-comments">
          <ChaiComments target-type="battle" :target-id="battle.id" />
        </section>

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
            <div v-if="selectedRegisterOcIds.length" class="selected-ocs">
              <span v-for="id in selectedRegisterOcIds" :key="id" class="selected-tag">{{ getOcTitle(myOcList, id) }}</span>
            </div>
            <p class="hint">已选择 {{ selectedRegisterOcIds.length }} 个 OC，点击条目可多选。</p>
            <!-- 所选 OC 没填约战面板：提醒一下（不拦着报名） -->
            <BattlePanelReminder :oc-ids="selectedRegisterOcIds" :names="selectedRegisterOcIds.map(id => getOcTitle(myOcList, id))" own />
          </div>
          <template #footer>
            <el-button :disabled="actionBusy || joining" @click="showRegisterModal = false">取消</el-button>
            <el-button type="primary" @click="handleRegisterSubmit" :loading="joining" :disabled="!selectedRegisterOcIds.length">确认报名</el-button>
          </template>
        </el-dialog>

        <!-- ===== 提交 / 修改作品弹窗 ===== -->
        <el-dialog v-model="showSubmitModal" :title="editingSubmission ? '修改作品' : '提交作品'" width="min(520px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy && !joining" :close-on-press-escape="!actionBusy && !joining" :show-close="!actionBusy && !joining">
          <div class="modal-body">
            <p class="modal-hint">交上来的作品要管理员审核，通过后其他人才能看到链接{{ editingSubmission ? '；改过的作品会重新审核' : '' }}。</p>
            <div class="form-group">
              <label for="bd-work-title">作品标题 <span class="required">*</span></label>
              <input id="bd-work-title" v-model="submitForm.title" maxlength="100" placeholder="给作品起个名字" />
            </div>
            <div class="form-group">
              <label for="bd-work-url">作品链接 <span class="required">*</span></label>
              <input id="bd-work-url" v-model="submitForm.contentUrl" maxlength="500" inputmode="url" placeholder="https://…（视频 / 漫画 / 图文等的网址）" :aria-invalid="!!urlError" aria-describedby="bd-work-url-hint" />
              <p v-if="urlError" id="bd-work-url-hint" class="hint error" role="alert">{{ urlError }}</p>
              <p v-else id="bd-work-url-hint" class="hint">填网址（以 http:// 或 https:// 开头），大家点击就能查看；创作期结束前都可以再改</p>
            </div>
            <div class="form-group">
              <label for="bd-work-desc">简介</label>
              <textarea id="bd-work-desc" v-model="submitForm.description" rows="3" maxlength="2000" placeholder="简要描述作品内容（可不填）"></textarea>
            </div>
          </div>
          <template #footer>
            <el-button :disabled="actionBusy || joining" @click="showSubmitModal = false">取消</el-button>
            <el-button type="primary" @click="handleSubmitWork" :loading="actionBusy" :disabled="!submitForm.title.trim() || !submitForm.contentUrl.trim() || !!urlError">
              {{ editingSubmission ? '保存修改' : '提交作品' }}
            </el-button>
          </template>
        </el-dialog>

        <!-- ===== 录入结果 / 管理员修正结果弹窗（同一个选择器） ===== -->
        <el-dialog v-model="showInternalResultModal" :title="resultMode === 'correct' ? '修正结果（管理员）' : '录入结果'" width="min(580px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy" :close-on-press-escape="!actionBusy" :show-close="!actionBusy">
          <div class="modal-body">
            <p v-if="resultMode === 'correct'" class="modal-hint warn">修正会立刻生效：自动调整双方 OC 的战绩（先减掉旧结果再记新结果），通知所有参与者，并留下修正记录。</p>
            <p v-else class="modal-hint">录入后还要点「公布结果」才算结束，公布前可以再改。</p>
            <BattleOutcomePicker
              v-model:outcome="internalResult.outcome"
              v-model:winnerId="internalResult.winnerId"
              :participants="battle.participants"
              :disabled="actionBusy"
              :preview-title="resultMode === 'correct' ? '确认后战绩会改成（每个出战 OC 各记一次）' : '公布后战绩会这样记（每个出战 OC 各记一次）'"
            />
            <div class="form-group" style="margin-top: 14px;">
              <label for="bd-result-desc">结果说明</label>
              <textarea id="bd-result-desc" v-model="internalResult.resultDescription" rows="2" :placeholder="resultMode === 'correct' ? '写一下为什么修正（可不填，填了会替换原来的说明）' : '可简单描述结果原因（可不填）'"></textarea>
            </div>
          </div>
          <template #footer>
            <el-button :disabled="actionBusy" @click="showInternalResultModal = false">取消</el-button>
            <el-button type="primary" @click="handleSetInternalResult" :loading="actionBusy" :disabled="!resultReady">
              {{ resultMode === 'correct' ? '确认修正' : '确认录入' }}
            </el-button>
          </template>
        </el-dialog>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import ChaiComments from '../../components/ChaiComments.vue'
import BattleOcCards from './BattleOcCards.vue'
import BattleVote from './BattleVote.vue'
import BattlePanelReminder from './BattlePanelReminder.vue'
import BattleVersus from './BattleVersus.vue'
import BattleStageBar from './BattleStageBar.vue'
import BattleOutcomePicker from './BattleOutcomePicker.vue'
import BattleAftermath from './BattleAftermath.vue'
import { ref, computed, watch, onUnmounted, h } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useBattleStore } from '../battle_Store'
import { battleApi, type BattleOutcome, type BattleSubmission } from '../battle_api'
import { RECORD_TEXT, fmtDuration, fmtTime, outcomeMeta, outcomeOf, recordPreview, resultHeadline, resultLabel, sideOf, splitSides, statusLabel } from '../battleView'
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
// 修改已提交的作品：打开同一个弹窗，内容预先填好
const editingSubmission = ref(false)
const isWebLink = (url?: string | null) => /^https?:\/\/\S+$/i.test((url ?? '').trim())
watch(showSubmitModal, open => { if (!open) editingSubmission.value = false })
function openEditSubmission() {
  const mine = mySubmission.value
  if (!mine) return
  submitForm.value = { title: mine.title, description: mine.description ?? '', contentUrl: mine.contentUrl }
  editingSubmission.value = true
  showSubmitModal.value = true
}
const showInternalResultModal = ref(false)
/** 结果弹窗的用途：record = 发起人录入（判定期）；correct = 管理员修正（已结束） */
const resultMode = ref<'record' | 'correct'>('record')
/** 出战 OC 的名字、封面（「出战人设」读到后传上来，给顶部对阵图用） */
const ocMap = ref<Record<string, { title?: string; coverUrl?: string } | undefined>>({})





// 报名弹窗相关
const showRegisterModal = ref(false)
const selectedRegisterOcIds = ref<string[]>([])
const myOcList = ref<any[]>([])

const submitForm = ref({
  title: '',
  description: '',
  contentUrl: '',
})
/** 作品链接当场校验：后端只收 http(s) 开头的网址，以前要点了提交才弹提示 */
const urlError = computed(() => {
  const u = submitForm.value.contentUrl.trim()
  if (!u || isWebLink(u)) return ''
  return '链接要以 http:// 或 https:// 开头，比如 https://www.bilibili.com/video/…（直接从浏览器地址栏复制）'
})

// 结果改成单选：先选结果种类，一方胜 / 惨胜再选一个胜者（以前是多选框，两边都能勾成胜）
const internalResult = ref({
  outcome: '' as BattleOutcome | '',
  winnerId: '',
  resultDescription: '',
})

// ===== 身份判断 =====
// 「我」统一用 userStore.userId（登录信息里没有 id 时从登录凭证里取），比较时忽略大小写。
// 以前有的地方用 userInfo.id、有的用 userId，个别情况下参赛者会看不到「提交作品」按钮
const me = computed(() => (userStore.userId || '').toLowerCase())
const sameUser = (id?: string | null) => !!me.value && (id || '').toLowerCase() === me.value
const isInitiator = computed(() =>
  (battle.value?.initiatorId || battle.value?.participants?.[0]?.userId)?.toLowerCase() === userStore.userId?.toLowerCase()
)

const isParticipant = computed(() =>
  battle.value?.participants?.some(p => sameUser(p.userId)) || false
)

/** 作品是谁交的：接口里 participant 有时没带（比如部分操作返回的数据），就按 participantId 到参与者列表里找 */
function ownerOf(sub: BattleSubmission) {
  return sub.participant ?? battle.value?.participants?.find(p => p.id === sub.participantId)
}
const mySubmission = computed(() => battle.value?.submissions?.find(s => sameUser(ownerOf(s)?.userId)))
const hasSubmitted = computed(() => !!mySubmission.value)

const isSpecifiedOpponent = computed(() => {
  if (!battle.value || battle.value.isPublic) return false
  if (isParticipant.value) return false
  if (isRejected.value) return false
  const userId = me.value
  if (!userId) return false
  return Object.keys(battle.value.opponentOcIds ?? {}).some(k => k.toLowerCase() === userId)
})

const isRejected = ref(false)

const canJoin = computed(() => {
  if (!battle.value) return false
  if (battle.value.status !== 'open') return false
  if (battle.value.participants?.some(p => sameUser(p.userId))) return false
  if (isRejected.value) return false
  if (!battle.value.isPublic && !isSpecifiedOpponent.value) return false
  return true
})

// ===== 作品审核 =====
// 我自己作品的审核状态（被打回时创作期结束后也能改）
const myReview = computed(() => mySubmission.value?.reviewStatus)
/** 我能不能改作品：创作期随时能改；判定期只有被打回的能改（和后端一致） */
const canEditMine = computed(() => hasSubmitted.value && (battle.value?.status === 'ongoing' || (battle.value?.status === 'judging' && myReview.value === 'rejected')))
const reviewLabel = (s?: string) => (s === 'approved' ? '已通过' : s === 'rejected' ? '被打回' : '审核中')
function reviewTip(sub: BattleSubmission): string {
  if (sub.reviewStatus === 'approved') return '已通过审核，大家都能看到你的作品了。'
  if (sub.reviewStatus === 'rejected') return `没通过审核${sub.reviewNote ? `：${sub.reviewNote}` : ''}。改好后重新提交，会再审核一次。`
  return '等管理员审核，通过后其他人才能看到链接（你自己一直看得到）。'
}

// ===== 页面上用到的派生数据 =====
const sides = computed(() => (battle.value ? splitSides(battle.value) : splitSides({ participants: [] })))
const headline = computed(() => (battle.value ? resultHeadline(battle.value) : { icon: '', text: '', outcome: '' as const }))
/** 判定期：发起人是不是已经录入过结果了 */
const recorded = computed(() => !!outcomeOf(battle.value))
/** 交作品的进度（发起人结束创作前要看） */
const submittedIds = computed(() => new Set((battle.value?.submissions ?? []).map(s => ownerOf(s)?.id).filter((x): x is string => !!x)))
const progress = computed(() => {
  const ps = battle.value?.participants ?? []
  return {
    done: ps.filter(p => submittedIds.value.has(p.id)).length,
    total: ps.length,
    missing: ps.filter(p => !submittedIds.value.has(p.id)).map(p => p.userName),
  }
})
/** 发起人操作区：报名期、创作期、判定期才有 */
const showHost = computed(() => isInitiator.value && ['open', 'ongoing', 'judging'].includes(battle.value?.status ?? ''))
/** 指定了对手、但不是我：报名期给路人一句提示 */
const lockedForMe = computed(() => {
  const b = battle.value
  return !!b && b.status === 'open' && !b.isPublic && !!me.value && !isParticipant.value && !isInitiator.value && !isSpecifiedOpponent.value && !isRejected.value
})
/** 「我的参战 / 参加这场约战」卡片：报名期给能报名的人、已报名的人；创作期、判定期给参赛的人 */
const showMine = computed(() => {
  const b = battle.value
  if (!b) return false
  if (b.status === 'open') return (!isInitiator.value && (canJoin.value || isParticipant.value)) || isRejected.value || lockedForMe.value
  if (b.status === 'ongoing' || b.status === 'judging') return isParticipant.value
  return false
})

// ===== 防止创作期一闪而过 =====
// 线上有一场「结束报名」和「结束创作」只隔了 3 秒：结束报名后，「结束创作」按钮先锁 5 秒
const creationUnlockAt = ref(0)
const now = ref(Date.now())
let tick: ReturnType<typeof setInterval> | undefined
function lockCreationButton(ms: number) {
  creationUnlockAt.value = Date.now() + ms
  now.value = Date.now()
  clearInterval(tick)
  tick = setInterval(() => {
    now.value = Date.now()
    if (now.value >= creationUnlockAt.value) clearInterval(tick)
  }, 500)
}
const creationLocked = computed(() => now.value < creationUnlockAt.value)
const creationWait = computed(() => Math.max(1, Math.ceil((creationUnlockAt.value - now.value) / 1000)))
const reviewWork = async (submissionId: string, approve: boolean) => {
  if (actionBusy.value) return
  let reason: string | undefined
  if (!approve) {
    try {
      const res = await ElMessageBox.prompt('写一下打回原因（会通知作者）', '打回作品', { confirmButtonText: '打回', cancelButtonText: '取消', inputValidator: (v: string) => !!v?.trim() || '请写原因' })
      reason = (res as { value: string }).value
    } catch { return }
  }
  actionBusy.value = true
  try {
    await battleApi.reviewSubmission(submissionId, approve, reason)
    ElMessage.success(approve ? '已通过' : '已打回')
    await fetchDetail()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '操作失败')
  } finally {
    actionBusy.value = false
  }
}

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
function participantStatusLabel(status: string): string {
  const map: Record<string, string> = {
    registered: '已报名',
    submitted: '已提交',
    eliminated: '已淘汰',
    finished: '已完成',
  }
  return map[status] || status
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

  const userId = userStore.userId
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
    const names = battle.value.participants.map(p => p.userName).join('、')
    await ElMessageBox.confirm(
      `现在有 ${battle.value.participants.length} 人参战：${names}。结束报名后进入创作期，大家开始交作品，之后不能再报名。`,
      '结束报名，开始创作期？',
      { ...confirmOptions, confirmButtonText: '开始创作期' }
    )
    const updated = await battleApi.closeRegistration(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    // 「结束创作」按钮先锁几秒，防止手快连点把创作期直接结束掉
    lockCreationButton(5000)
    ElMessage.success('已进入创作期，参赛的人现在可以交作品了')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  } finally {
    actionBusy.value = false
  }
}

// ===== 结束创作 =====
/** 结束创作的确认框：写清楚交了几份、谁还没交；一份都没交、创作期才刚开始时用醒目的警告 */
function closeCreationMessage() {
  const b = battle.value!
  const p = progress.value
  const started = b.registrationDeadline ? new Date(b.registrationDeadline).getTime() : NaN
  const justStarted = Number.isFinite(started) && Date.now() - started < 120000
  // 确认框挂在页面外面，这里的样式要写成行内的
  const warn = 'margin:8px 0 0;padding:8px 10px;border-left:4px solid var(--danger);background:var(--danger-soft);color:var(--danger);font-weight:600;'
  return {
    risky: p.done === 0 || justStarted,
    vnode: h('div', { style: 'line-height:1.8' }, [
      h('p', { style: 'margin:0' }, `已交作品：${p.done} / ${p.total} 人`),
      h('p', { style: 'margin:0' }, p.missing.length ? `还没交：${p.missing.join('、')}` : '所有人都交了 ✓'),
      p.done === 0 ? h('p', { style: warn }, '⚠ 还没有任何人交作品，结束后就不能再交了！') : null,
      justStarted ? h('p', { style: warn }, `⚠ 创作期才刚开始（${fmtDuration(Date.now() - started)}前），确定不是点错了？`) : null,
      h('p', { style: 'margin:8px 0 0;color:var(--text-secondary)' }, '结束后进入判定期：不能再交作品，也不能再改（被管理员打回的除外）。'),
    ]),
  }
}

const handleCloseCreation = async () => {
  if (!battle.value || actionBusy.value || creationLocked.value) return
  actionBusy.value = true
  try {
    const msg = closeCreationMessage()
    await ElMessageBox.confirm(msg.vnode, '结束创作，进入判定期？', {
      ...confirmOptions,
      type: msg.risky ? 'warning' : 'info',
      confirmButtonText: msg.risky ? '仍然结束创作' : '结束创作',
    })
    const updated = await battleApi.closeCreation(battle.value.id)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    ElMessage.success('已进入判定期，可以录入结果了')
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
  if (!isWebLink(submitForm.value.contentUrl)) {
    ElMessage.warning('作品链接要以 http:// 或 https:// 开头')
    return
  }
  // 先记下是不是在改：关弹窗会把 editingSubmission 清掉
  const wasEditing = editingSubmission.value
  actionBusy.value = true
  try {
    const data = {
      title: submitForm.value.title.trim(),
      description: submitForm.value.description || undefined,
      contentUrl: submitForm.value.contentUrl.trim(),
    }
    const updated = wasEditing
      ? await battleApi.updateSubmission(battle.value.id, data)
      : await battleApi.submit(battle.value.id, data)
    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
    showSubmitModal.value = false
    submitForm.value = { title: '', description: '', contentUrl: '' }
    ElMessage.success(wasEditing ? '作品已修改，等管理员重新审核' : '作品提交成功！管理员审核通过后大家就能看到')
    editingSubmission.value = false
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '提交失败')
  } finally {
    actionBusy.value = false
  }
}

// ===== 录入结果 / 管理员修正结果 =====
/** 打开结果弹窗；已经有结果的（改录入、修正）把原来的结果带进去 */
function openResultModal(mode: 'record' | 'correct') {
  const b = battle.value
  if (!b) return
  resultMode.value = mode
  internalResult.value = {
    outcome: outcomeOf(b),
    winnerId: b.participants.find(p => p.result === 'win')?.id ?? '',
    resultDescription: b.resultDescription ?? '',
  }
  showInternalResultModal.value = true
}
/** 选好了没有：要有结果种类；一方胜 / 惨胜还要有胜者 */
const resultReady = computed(() => {
  const { outcome, winnerId } = internalResult.value
  if (!outcome) return false
  return !outcomeMeta(outcome)?.needsWinner || !!winnerId
})

const handleSetInternalResult = async () => {
  if (!battle.value || actionBusy.value) return
  const { outcome, winnerId, resultDescription } = internalResult.value
  if (!outcome) {
    ElMessage.warning('先选一下结果是哪一种')
    return
  }
  const meta = outcomeMeta(outcome)!
  if (meta.needsWinner && !winnerId) {
    ElMessage.warning('请选出胜者')
    return
  }
  const correcting = resultMode.value === 'correct'
  // 确认框里把「战绩会怎么记」逐个人写出来
  const lines = recordPreview(battle.value.participants, outcome, meta.needsWinner ? winnerId : '')
  try {
    await ElMessageBox.confirm(
      h('div', { style: 'line-height:1.8' }, [
        h('p', { style: 'margin:0 0 4px;font-weight:600' }, `结果：${meta.icon} ${meta.label}`),
        h('p', { style: 'margin:0 0 4px' }, correcting ? '确认后战绩会改成：' : '公布结果后，战绩会这样记：'),
        h('ul', { style: 'margin:0;padding-left:18px' }, lines.map(l => h('li', `${l.participant.userName}${l.participant.ocNames?.length ? `（${l.participant.ocNames.join('、')}）` : ''}：${RECORD_TEXT[l.result]}`))),
        h('p', { style: 'margin:6px 0 0;color:var(--text-secondary)' }, correcting
          ? '会自动调整双方 OC 的战绩、通知所有参与者，并留下修正记录。'
          : '现在只是录入，点「公布结果」才算真正结束；公布前还能改。'),
      ]),
      correcting ? '确认修正结果？' : '确认录入结果？',
      { ...confirmOptions, confirmButtonText: correcting ? '确认修正' : '确认录入' }
    )
  } catch { return }
  actionBusy.value = true
  try {
    const data = {
      outcome,
      winnerIds: meta.needsWinner && winnerId ? [winnerId] : [],
      resultDescription: resultDescription.trim() || undefined,
    }
    if (correcting) {
      await battleApi.correctResult(battle.value.id, data)
      showInternalResultModal.value = false
      await fetchDetail()
      ElMessage.success('结果已修正，双方 OC 的战绩已自动调整')
    } else {
      const updated = await battleApi.setInternalResult(battle.value.id, data)
      if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated
      showInternalResultModal.value = false
      ElMessage.success('结果已录入，确认无误后点「公布结果」')
    }
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
    await ElMessageBox.confirm(
      battle.value.judgmentType === 'vote'
        ? '有人投票就按票数定胜负，没人投票就用你录入的结果。公布后约战结束、OC 战绩生效，不能再改。'
        : `公布「${headline.value.icon} ${headline.value.text}」？公布后约战结束、OC 战绩生效，不能再改。`,
      '公布结果，结束约战？',
      { ...confirmOptions, confirmButtonText: '公布结果' }
    )
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
    await ElMessageBox.confirm('取消后约战就作废了，参与者会收到通知，不能恢复。', '取消这场约战？', { ...confirmOptions, type: 'warning', confirmButtonText: '取消约战', cancelButtonText: '再想想' })
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
  ocMap.value = {}
  creationUnlockAt.value = 0
  showRegisterModal.value = showSubmitModal.value = showInternalResultModal.value = false
  loadError.value = ''
  fetchMyOcs()
  try { await fetchDetail() }
  catch { loadError.value = '约战详情加载失败，请刷新重试' }
}, { immediate: true })

onUnmounted(() => {
  clearInterval(tick)
  battleStore.clear()
})
</script>

<style scoped>
/* ============================================================
   约战详情。外层 .community-detail 的公共样式（detail.css）优先级很高，
   这里的类名都用 bd- 开头，避免和它的 .page-header / .detail-section / .btn-primary 撞上
   ============================================================ */
.bd-wrap { --side-a: var(--accent-color); --side-b: #d97706; max-width: 1040px; margin: 0 auto; padding: 24px 20px 60px; display: grid; gap: 20px; color: var(--text-primary); }

.bd-state { display: flex; flex-direction: column; align-items: center; gap: 16px; padding: 80px 0; color: var(--text-secondary); text-align: center; }
.spinner { width: 32px; height: 32px; border: 2px solid var(--border-line); border-top-color: var(--text-primary); border-radius: 50%; animation: spin .8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.bd-link { color: var(--accent-color); }

/* ===== 标题 ===== */
.bd-top { display: grid; gap: 8px; }
.bd-back { justify-self: start; padding: 0; min-height: 0 !important; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.bd-back:hover { color: var(--accent-color); }
.bd-titlebar { display: flex; align-items: flex-start; gap: 12px; flex-wrap: wrap; }
.bd-title { flex: 1 1 320px; margin: 0 !important; font-size: clamp(24px, 3vw, 32px) !important; line-height: 1.3 !important; overflow-wrap: anywhere; }
.bd-status { flex-shrink: 0; margin-top: 6px; padding: 3px 10px; font-size: 13px; font-weight: 600; color: var(--paper-card); background: var(--text-secondary); }
.bd-status.st-open { background: var(--success); }
.bd-status.st-ongoing { background: var(--accent-color); color: var(--on-accent); }
.bd-status.st-judging { background: var(--caution); color: var(--caution-ink); }
.bd-status.st-finished { background: var(--text-primary); }
.bd-meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; font-size: 13px; color: var(--text-secondary); }
.bd-meta li { padding: 2px 8px; background: var(--bg-sub); }
.bd-meta .bd-meta-mine { color: var(--accent-color); background: var(--accent-soft); }

/* ===== 结果横幅 ===== */
.bd-result { display: grid; gap: 8px; padding: 18px 20px; border: 1px solid var(--border-line); border-left: 6px solid var(--text-primary); background: var(--paper-card); }
.bd-result-main { display: flex; align-items: center; gap: 16px; }
.bd-result-icon { font-size: 40px; line-height: 1; }
.bd-result-text { margin: 0; font: 700 clamp(22px, 3vw, 30px)/1.25 var(--font-display); overflow-wrap: anywhere; }
.bd-result-rule { margin: 2px 0 0; font-size: 13px; color: var(--text-secondary); }
.bd-result-desc { margin: 0; padding-top: 8px; border-top: 1px dashed var(--border-line); font-size: 14px; line-height: 1.8; white-space: pre-wrap; overflow-wrap: anywhere; }
.bd-result-foot { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; font-size: 12px; color: var(--text-secondary); }
.bd-result.o-win { border-left-color: var(--success); background: linear-gradient(100deg, var(--success-soft), var(--paper-card) 70%); }
.bd-result.o-pyrrhic { border-left-color: var(--danger); background: linear-gradient(100deg, var(--danger-soft), var(--paper-card) 70%); }
.bd-result.o-pyrrhic .bd-result-text { color: var(--danger); }
.bd-result.o-draw { border-left-color: var(--info); background: linear-gradient(100deg, var(--info-soft), var(--paper-card) 70%); }
.bd-result.o-mutual { border-left-color: var(--inverse-bg); background: var(--inverse-bg); color: var(--inverse-text); }
.bd-result.o-mutual :is(.bd-result-rule, .bd-result-foot) { color: var(--inverse-text); opacity: .8; }
.bd-result.o-mutual .bd-result-desc { border-top-color: color-mix(in srgb, var(--inverse-text) 30%, transparent); }
.bd-result.o-nocontest { border-left: 6px solid transparent; border-image: var(--hazard-stripe) 1; border-image-width: 0 0 0 6px; background: var(--bg-sub); }
.bd-result.o-nocontest .bd-result-text { color: var(--text-secondary); }
/* 管理员入口：故意做得不显眼 */
.bd-admin-link { min-height: 0 !important; padding: 0; border: 0; background: none; color: inherit; font: inherit; font-size: 12px; text-decoration: underline dotted; text-underline-offset: 3px; cursor: pointer; opacity: .75; }
.bd-admin-link:hover { opacity: 1; color: var(--accent-color); }

.bd-stages { padding: 0 2px; }

/* ===== 操作区 ===== */
.bd-actions { display: grid; gap: 16px; }
.bd-actions.two { grid-template-columns: repeat(auto-fit, minmax(min(100%, 340px), 1fr)); align-items: start; }
.bd-card { display: grid; gap: 10px; padding: 16px 18px; border: 1px solid var(--border-line); background: var(--paper-card); min-width: 0; }
.bd-mine { border-top: 3px solid var(--accent-color); }
.bd-host { border-top: 3px solid var(--caution); background: linear-gradient(170deg, color-mix(in srgb, var(--caution) 7%, var(--paper-card)), var(--paper-card) 120px); }
.bd-card-title { margin: 0; font-size: 16px; font-weight: 600; }
.bd-host-head { display: flex; align-items: baseline; gap: 8px; }
.bd-host-head small { font-size: 12px; color: var(--text-secondary); }
.bd-host-foot { padding-top: 10px; border-top: 1px dashed var(--border-line); }
.bd-tip { margin: 0; font-size: 13px; line-height: 1.75; color: var(--text-secondary); }
.bd-tip b { color: var(--text-primary); font-weight: 700; }
.bd-note { margin: 0; padding: 6px 10px; font-size: 13px; color: var(--text-secondary); background: var(--bg-sub); border-left: 3px solid var(--border-line); }
.bd-note.danger { color: var(--danger); background: var(--danger-soft); border-left-color: var(--danger); }
.bd-btns { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 10px; }
.bd-btns.end { justify-content: flex-end; }
.bd-btn { display: inline-flex; align-items: center; justify-content: center; padding: 7px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; text-decoration: none; cursor: pointer; transition: border-color .15s, background-color .15s, color .15s; }
.bd-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.bd-btn.main { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); font-weight: 600; }
.bd-btn.main:hover:not(:disabled) { color: var(--on-accent); filter: brightness(1.08); }
.bd-btn.danger { border-color: var(--danger); color: var(--danger); }
.bd-btn.danger:hover:not(:disabled) { background: var(--danger); color: var(--paper-card); }
.bd-btn:disabled { opacity: .5; cursor: not-allowed; }
.bd-text-btn { min-height: 0 !important; padding: 2px 4px; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; text-decoration: underline; text-underline-offset: 3px; }
.bd-text-btn:hover:not(:disabled) { color: var(--accent-color); }
.bd-text-btn.danger:hover:not(:disabled) { color: var(--danger); }
.bd-text-btn:disabled { opacity: .5; cursor: not-allowed; }

/* 我的作品 */
.bd-mywork { display: grid; gap: 4px; padding: 10px 12px; border: 1px solid var(--border-line); border-left: 4px solid var(--warning); background: var(--bg-sub); min-width: 0; }
.bd-mywork.rv-approved { border-left-color: var(--success); }
.bd-mywork.rv-rejected { border-left-color: var(--danger); background: var(--danger-soft); }
.bd-mywork-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.bd-mywork-link { font-size: 13px; color: var(--accent-color); overflow-wrap: anywhere; }
.bd-chip { display: inline-block; padding: 1px 8px; font-size: 12px; font-weight: 600; color: var(--warning); background: var(--warning-soft); }
.bd-chip.approved { color: var(--success); background: var(--success-soft); }
.bd-chip.rejected { color: var(--danger); background: var(--danger-soft); }

/* 交作品进度 */
.bd-progress { display: grid; gap: 6px; }
.bd-progress-num { font-size: 14px; }
.bd-progress-num b { font: 700 26px/1 var(--font-display); }
.bd-progress-bar { height: 6px; background: var(--bg-sub); }
.bd-progress-bar span { display: block; height: 100%; background: var(--accent-color); transition: width .3s; }
.bd-progress-list { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 4px 12px; font-size: 13px; color: var(--danger); }
.bd-progress-list li.ok { color: var(--success); }
.bd-recorded { margin: 0; display: grid; gap: 2px; padding: 8px 10px; background: var(--bg-sub); font-size: 14px; }
.bd-recorded small { font-size: 12px; color: var(--text-secondary); }

/* ===== 内容区块 ===== */
.bd-sec { padding: 18px 20px; border: 1px solid var(--border-line); background: var(--paper-card); min-width: 0; }
.bd-sec-head { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
.bd-sec-head .bd-h { margin: 0; }
.bd-h { margin: 0 0 12px; padding-left: 10px; border-left: 3px solid var(--text-primary); font-size: 17px; font-weight: 600; line-height: 1.3; }
.bd-count { font-size: 13px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.bd-rules { white-space: pre-wrap; font-size: 14px; line-height: 1.85; color: var(--text-primary); overflow-wrap: anywhere; max-width: 72ch; }
.bd-aftermath:empty { display: none; }
.bd-empty { margin: 0; padding: 16px 0; text-align: center; font-size: 14px; color: var(--text-secondary); }

/* 作品列表 */
.bd-works { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.bd-work { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; padding: 12px 14px; border: 1px solid var(--border-line); box-shadow: inset 4px 0 0 var(--side-a); }
.bd-work.side-b { box-shadow: inset 4px 0 0 var(--side-b); }
.bd-work-main { display: grid; gap: 3px; min-width: 0; justify-items: start; }
.bd-work-title { font-size: 15px; overflow-wrap: anywhere; }
.bd-work-by { font-size: 13px; color: var(--text-secondary); }
.bd-work-desc { margin: 2px 0 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); white-space: pre-wrap; overflow-wrap: anywhere; }
.bd-work-side { display: grid; gap: 6px; justify-items: end; flex-shrink: 0; max-width: 45%; }
.bd-work-url { font-size: 13px; color: var(--text-secondary); overflow-wrap: anywhere; text-align: right; }
.bd-work-url.muted { opacity: .8; }
.bd-review { display: inline-flex; gap: 6px; padding: 2px 6px; border: 1px dashed var(--caution); }

/* 参战名单 */
.bd-roster { list-style: none; margin: 0; padding: 0; display: grid; }
.bd-roster li { display: flex; align-items: center; gap: 8px 12px; flex-wrap: wrap; padding: 10px 0 10px 12px; border-bottom: 1px solid var(--border-line); box-shadow: inset 3px 0 0 var(--side-a); }
.bd-roster li.side-b { box-shadow: inset 3px 0 0 var(--side-b); }
.bd-roster li:last-child { border-bottom: 0; }
.bd-roster-side { font-size: 12px; color: var(--text-secondary); min-width: 3.5em; }
.bd-roster-name { font-weight: 600; }
.bd-roster-oc { font-size: 13px; color: var(--text-secondary); overflow-wrap: anywhere; }
.bd-roster-status { font-size: 12px; padding: 0 8px; border: 1px solid var(--border-line); color: var(--text-secondary); }
.bd-roster-status.submitted { border-color: var(--success); color: var(--success); }
.bd-roster-status.eliminated { border-color: var(--danger); color: var(--danger); }
.bd-roster-result { margin-left: auto; padding: 1px 10px; font-size: 13px; font-weight: 700; background: var(--bg-sub); color: var(--text-secondary); }
.bd-roster-result.win { color: var(--success); background: var(--success-soft); }
.bd-roster-result.lose { color: var(--danger); background: var(--danger-soft); }
.bd-roster-result.draw { color: var(--info); background: var(--info-soft); }

/* ===== 弹窗（el-dialog 渲染到页面外，但内容仍带本组件的作用域，这些样式照样生效） ===== */
.register-oc { display: flex; gap: 12px; align-items: center; padding: 12px 14px; margin-bottom: 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.register-oc.selected { border-color: var(--accent-color); background: var(--accent-soft); }
.register-oc input { width: 18px; height: 18px; accent-color: var(--accent-color); }
.register-oc span { overflow-wrap: anywhere; }
.modal-body .form-group { margin-bottom: 14px; }
.modal-body .form-group label { display: block; font-size: 13px; margin-bottom: 4px; color: var(--text-primary); }
.modal-body .form-group input,
.modal-body .form-group textarea { width: 100%; box-sizing: border-box; padding: 7px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font-family: inherit; font-size: 14px; outline: none; }
.modal-body .form-group input:focus,
.modal-body .form-group textarea:focus { border-color: var(--text-primary); }
.modal-body .form-group input[aria-invalid='true'] { border-color: var(--danger); }
.modal-body .hint { font-size: 12px; color: var(--text-secondary); margin: 4px 0 0; }
.modal-body .hint.error { color: var(--danger); }
.required { color: var(--danger); }
.modal-hint { margin: 0 0 12px; font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.modal-hint.warn { padding: 8px 10px; color: var(--text-primary); background: var(--warning-soft); border-left: 3px solid var(--warning); }
.selected-ocs { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 8px; }
.selected-tag { display: inline-flex; align-items: center; gap: 4px; padding: 2px 10px; background: var(--bg-sub); border: 1px solid var(--border-line); font-size: 12px; color: var(--text-primary); }

@media (max-width: 640px) {
  .bd-wrap { padding: 16px 12px 40px; gap: 14px; }
  .bd-sec, .bd-card { padding: 14px; }
  .bd-result { padding: 14px; }
  .bd-result-icon { font-size: 32px; }
  .bd-work { flex-direction: column; }
  .bd-work-side { max-width: none; justify-items: start; }
  .bd-work-url { text-align: left; }
  .bd-btns .bd-btn { flex: 1 1 auto; }
  .bd-btns.end { justify-content: stretch; }
  .bd-roster-result { margin-left: 0; }
}
@media (prefers-reduced-motion: reduce) { .spinner { animation-duration: 2s; } .bd-btn, .bd-progress-bar span { transition: none; } }
</style>
