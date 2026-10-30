<!-- 世界任务：我的世界服务器全服一起完成目标，完成后公布新 mod / 新内容。
     玩家：看进度、提交贡献（数量 + 截图），看贡献榜和自己的记录。
     管理员（超级管理员 / 太初官方）：在同一页审核贡献、直接记录进度、管理任务链。 -->
<template>
  <article class="quest-page" :class="embedded ? 'embedded' : 'content-page'">
    <p v-if="embedded" class="qp-lead embedded-lead">全服一起完成目标。每完成一个任务，整个服务器就会解锁新的 mod 和内容，所有人都能玩到。</p>
    <header v-else class="qp-head">
      <p class="qp-kicker">MINECRAFT SERVER · WORLD QUEST</p>
      <h1>世界任务</h1>
      <p class="qp-lead">全服一起完成目标。每完成一个任务，整个服务器就会解锁新的 mod 和内容，所有人都能玩到。</p>
    </header>

    <ParticleState v-if="loading && !overview" text="正在读取世界任务" compact />
    <p v-else-if="loadError && !overview" class="qp-error" role="alert">{{ loadError }} <button type="button" @click="load">重试</button></p>

    <template v-else-if="overview">
      <!-- 同时有主线和支线在进行时，切换看哪一个 -->
      <nav v-if="actives.length > 1" class="qp-switch" aria-label="进行中的任务">
        <button v-for="q in actives" :key="q.id" type="button" :aria-pressed="active?.id === q.id" @click="selectedId = q.id">
          <span class="qp-line" :class="q.line ?? 'main'">{{ lineName(q) }}</span>{{ q.title }}
        </button>
      </nav>
      <!-- 进行中的任务 -->
      <section v-if="active" class="qp-active" aria-label="进行中的任务">
        <img v-if="active.coverImage" class="qp-cover" :src="active.coverImage" :alt="active.title" />
        <div class="qp-active-body">
          <span class="qp-tag">{{ lineName(active) }} · 进行中</span>
          <h2>{{ active.title }}</h2>
          <p v-if="active.description" class="qp-desc">{{ active.description }}</p>
          <p v-if="isGoal(active)" class="qp-goal"><span>🎯 一次性目标</span>做到就算完成，不用凑数量。管理员在服务器里确认后就会标记完成，你也可以在下面报告一声。</p>
          <div v-else class="qp-progress" role="progressbar" :aria-valuenow="active.progress" :aria-valuemax="active.target" :aria-label="`${active.title} 进度`">
            <div class="qp-bar"><span :style="{ width: pct(active) + '%' }"></span></div>
            <div class="qp-nums"><b>{{ fmt(active.progress) }}</b> / {{ fmt(active.target) }} {{ active.unit }}<span>{{ pct(active).toFixed(pct(active) < 10 ? 1 : 0) }}%</span></div>
          </div>
          <dl class="qp-facts">
            <div><dt>参与人数</dt><dd>{{ active.contributors }}</dd></div>
            <div v-if="active.rewardExp || active.rewardPotato"><dt>参与者额外奖励</dt><dd>{{ rewardText(active) }}</dd></div>
            <div v-if="active.startedAt"><dt>开始于</dt><dd>{{ day(active.startedAt) }}</dd></div>
          </dl>
          <p v-if="active.rewardTeaser" class="qp-teaser"><span>完成后全服解锁</span>{{ active.rewardTeaser }}</p>
          <p v-if="overview.canManage && active.followUps?.length" class="qp-chain"><span>完成后开启</span>{{ refs(active.followUps) }}</p>
          <p v-else-if="active.followUpCount" class="qp-chain"><span>完成后开启</span>{{ active.followUpCount }} 个新任务，到时候揭晓</p>
          <div v-if="overview.canManage" class="qp-admin-quick">
            <button type="button" class="qp-primary" :disabled="busy === 'complete'" @click="forceComplete">✓ 标记完成</button>
            <small>在服务器里确认做好了就点这里：公布解锁内容{{ active.rewardExp || active.rewardPotato ? '、给参与者发奖励' : '' }}{{ active.followUps?.length ? '，开启后续任务' : isSide(active) ? '' : '，开始下一个主线任务' }}。</small>
          </div>
          <p v-if="overview.canManage && active.rewardText" class="qp-secret"><span>解锁内容（仅管理员可见，完成后公开）</span>{{ active.rewardText }}</p>
        </div>
      </section>
      <section v-else class="qp-empty">
        <p>现在没有进行中的任务{{ overview.upcomingCount ? '，下一个任务很快开始' : '' }}。</p>
        <p v-if="overview.canManage && !overview.upcomingCount && !overview.completed.length" class="qp-hint">还没有建过任务：在下面「任务管理」里新建一个，点「开始」后大家就能看到了。</p>
      </section>

      <div class="qp-grid">
        <div class="qp-main">
          <!-- 提交贡献 -->
          <section v-if="active" class="qp-card qp-report" aria-label="发帖报告">
            <h3>{{ isGoal(active) ? '做好了？发帖报告' : '有进展？发帖报告' }}</h3>
            <p v-if="isGoal(active)" class="qp-hint">这个任务完成后是全服解锁新内容。在服务器里做好了，就在论坛「我的世界 · 任务报告」发一篇帖子：截图、坐标、怎么做的都可以写，大家也能看到你的作品。管理员看帖确认后就会标记完成，你的名字也会记在完成者里{{ active.rewardExp || active.rewardPotato ? `，还能拿 ${rewardText(active)}` : '' }}。</p>
            <p v-else class="qp-hint">在服务器里完成了一部分，就在论坛「我的世界 · 任务报告」发一篇帖子，写清楚数量、附上截图，管理员看帖核对后计入进度。</p>
            <div class="qp-row">
              <RouterLink class="qp-primary qp-btn" :to="questReportRoute(active.id)">✎ 发帖报告</RouterLink>
              <RouterLink class="qp-link" :to="questReportsRoute()">看看大家的任务报告 →</RouterLink>
            </div>
            <p v-if="overview.myPendingCount" class="qp-hint">你有 {{ overview.myPendingCount }} 篇报告在等管理员确认。</p>
          </section>

          <!-- 管理员 -->
          <section v-if="overview.canManage" class="qp-card qp-admin" aria-label="管理">
            <div class="qp-tabs" role="tablist">
              <button v-for="t in ADMIN_TABS" :key="t.id" type="button" role="tab" :aria-selected="adminTab === t.id" @click="adminTab = t.id">
                {{ t.label }}<b v-if="t.id === 'review' && overview.pendingCount">{{ overview.pendingCount }}</b>
              </button>
            </div>

            <!-- 待审核 -->
            <div v-if="adminTab === 'review'">
              <p v-if="!pending.length" class="qp-hint">没有待审核的贡献。</p>
              <article v-for="c in pending" :key="c.id" class="qp-review">
                <header>
                  <b>{{ c.username || '—' }}</b><span v-if="c.playerName" class="qp-player">游戏名：{{ c.playerName }}</span>
                  <span class="qp-muted">{{ c.questTitle }}{{ c.questStatus === 'completed' ? '（已完成）' : '' }} · {{ timeAgo(c.createdAt) }}</span>
                </header>
                <p v-if="c.questKind === 'goal'" class="qp-amount">报告<b>已经做好了</b>{{ c.questStatus === 'completed' ? '（任务已经完成，通过的话补发奖励）' : '（通过就算任务完成）' }}</p>
                <p v-else class="qp-amount">申报 <b>{{ fmt(c.amount) }}</b> {{ c.unit }}</p>
                <p v-if="c.note" class="qp-note">{{ c.note }}</p>
                <RouterLink v-if="c.postId" class="qp-link" :to="postRoute(c.postId)">查看报告帖 →</RouterLink>
                <div v-if="c.images.length" class="qp-thumbs"><a v-for="img in c.images" :key="img" :href="img" target="_blank" rel="noopener" class="qp-thumb"><img :src="img" alt="截图" /></a></div>
                <div class="qp-review-actions">
                  <label v-if="c.questKind !== 'goal'">计入数量<input v-model.number="reviewAmount[c.id]" type="number" min="1" :placeholder="String(c.amount)" /></label>
                  <input v-model="reviewNote[c.id]" class="qp-grow" maxlength="300" placeholder="备注（驳回时会告诉提交的人）" />
                  <button type="button" class="qp-primary" :disabled="busy === c.id" @click="review(c, true)">{{ c.questKind === 'goal' && c.questStatus !== 'completed' ? '确认完成' : '通过' }}</button>
                  <button type="button" class="qp-danger" :disabled="busy === c.id" @click="review(c, false)">驳回</button>
                </div>
              </article>
            </div>

            <!-- 记录进度 -->
            <div v-else-if="adminTab === 'record'">
              <p v-if="!active" class="qp-hint">没有进行中的任务。</p>
              <template v-else-if="isGoal(active)">
                <p class="qp-hint">在服务器里确认已经做好了，就在这里记上完成的人，任务马上完成（不用等玩家提交）。几个人一起做的，用逗号隔开，每人都拿奖励。</p>
                <form class="qp-form" @submit.prevent="recordGoal">
                  <label class="wide">网站用户名（几个人用逗号隔开，没有账号可以空着）<input v-model.trim="rec.username" maxlength="300" placeholder="比如：小明, 阿紫" /></label>
                  <label class="wide">游戏名（可选，同样用逗号隔开，和上面一一对应）<input v-model.trim="rec.playerName" maxlength="300" /></label>
                  <label class="wide">备注<input v-model="rec.note" maxlength="300" placeholder="比如：刷石机在主城东边" /></label>
                  <button type="submit" class="qp-primary wide" :disabled="busy === 'record' || !(rec.username || rec.playerName)">记上并完成任务</button>
                </form>
                <form class="qp-form qp-fix" @submit.prevent>
                  <button type="button" class="qp-danger" :disabled="busy === 'complete'" @click="forceComplete">不记名字，直接标记完成</button>
                </form>
              </template>
              <template v-else>
                <p class="qp-hint">在服务器里看到的贡献直接记上（不用审核）。填了网站用户名的，完成时这个人也会拿到奖励。</p>
                <form class="qp-form" @submit.prevent="record">
                  <label>数量（{{ active.unit || '个' }}）<input v-model.number="rec.amount" type="number" min="1" required /></label>
                  <label>网站用户名（可选）<input v-model.trim="rec.username" maxlength="50" /></label>
                  <label>游戏名（可选）<input v-model.trim="rec.playerName" maxlength="50" /></label>
                  <label>备注<input v-model="rec.note" maxlength="300" /></label>
                  <button type="submit" class="qp-primary wide" :disabled="busy === 'record' || !((rec.amount ?? 0) > 0)">记录</button>
                </form>
                <form class="qp-form qp-fix" @submit.prevent="fixProgress">
                  <label>直接改进度（纠正用）<input v-model.number="fixValue" type="number" min="0" :placeholder="String(active.progress)" /></label>
                  <button type="submit" :disabled="busy === 'fix' || fixValue === null || fixValue === ''">改成这个数</button>
                  <button type="button" class="qp-danger" :disabled="busy === 'complete'" @click="forceComplete">直接完成这个任务</button>
                </form>
              </template>
            </div>

            <!-- 任务管理 -->
            <div v-else>
              <p class="qp-hint">主线一次只进行一个：没设前置的主线按顺序排队，上一个主线完成后开始排在最前面的。设了「前置任务」的（主线或支线）在前置完成后自动开启——这样一个任务的奖励就可以是「解锁一条支线」或「开启下一章」。支线可以和主线同时进行。玩家只能看到「后面还有几个任务」，看不到内容。</p>
              <ol class="qp-queue">
                <li v-for="q in actives" :key="q.id" class="is-active"><span class="qp-tag">{{ lineName(q) }} · 进行中</span>{{ q.title }}<button type="button" @click="edit(q)">编辑</button></li>
                <li v-for="(q, i) in overview.upcoming ?? []" :key="q.id">
                  <span class="qp-order">{{ i + 1 }}</span><span class="qp-line" :class="q.line ?? 'main'">{{ lineName(q) }}</span>{{ q.title }}<small>{{ isGoal(q) ? '一次性目标' : `${fmt(q.target)} ${q.unit}` }} · {{ q.prereqTitle ? `《${q.prereqTitle}》完成后开启` : isSide(q) ? '手动开始' : '主线排队' }}</small>
                  <button type="button" :disabled="i === 0" aria-label="上移" @click="move(i, -1)">↑</button>
                  <button type="button" :disabled="i === (overview.upcoming?.length ?? 0) - 1" aria-label="下移" @click="move(i, 1)">↓</button>
                  <button v-if="isSide(q) || !hasActiveMain" type="button" @click="start(q)">开始</button>
                  <button type="button" @click="edit(q)">编辑</button>
                  <button type="button" class="qp-danger" @click="remove(q)">删除</button>
                </li>
              </ol>
              <button v-if="!editing" type="button" class="qp-primary" @click="edit(null)">＋ 新任务</button>
              <form v-else class="qp-form qp-editor" @submit.prevent="saveQuest">
                <h4 class="wide">{{ editing.id ? '编辑任务' : '新任务' }}</h4>
                <label class="wide">任务名称<input v-model="editing.title" maxlength="100" required placeholder="比如：做出第一台刷石机" /></label>
                <div class="wide qp-kind" role="radiogroup" aria-label="任务类型">
                  <span>任务类型</span>
                  <label><input v-model="editing.kind" type="radio" value="count" :disabled="kindLocked" /> 数量目标<small>大家一起凑数，比如收集 10000 块木头</small></label>
                  <label><input v-model="editing.kind" type="radio" value="goal" :disabled="kindLocked" /> 一次性目标<small>做到就完成，比如做出一台刷石机、打败末影龙</small></label>
                  <small v-if="kindLocked" class="qp-muted">这个任务已经有进度了，不能再改类型</small>
                </div>
                <div class="wide qp-kind" role="radiogroup" aria-label="主线还是支线">
                  <span>主线 / 支线</span>
                  <label><input v-model="editing.line" type="radio" value="main" /> 主线<small>一次只进行一个，一个接一个</small></label>
                  <label><input v-model="editing.line" type="radio" value="side" /> 支线<small>可以和主线同时进行</small></label>
                </div>
                <label class="wide">前置任务（它完成后自动开启这个任务）
                  <select v-model="editing.prereqId">
                    <option :value="null">{{ editing.line === 'side' ? '无（管理员手动开始）' : '无（按主线顺序排队）' }}</option>
                    <option v-for="q in prereqOptions" :key="q.id" :value="q.id">{{ lineName(q) }} · {{ q.title }}{{ q.status === 'completed' ? '（已完成）' : q.status === 'active' ? '（进行中）' : '（还没开始）' }}</option>
                  </select>
                  <small class="qp-muted">{{ prereqHint }}</small>
                </label>
                <template v-if="editing.kind !== 'goal'">
                  <label>目标数量<input v-model.number="editing.target" type="number" min="1" required /></label>
                  <label>单位<input v-model="editing.unit" maxlength="20" placeholder="块石头 / 只末影龙 / 座建筑" /></label>
                </template>
                <label class="wide">任务说明<textarea v-model="editing.description" rows="3" placeholder="要做什么、怎么算数、去哪里交"></textarea></label>
                <label class="wide">完成后全服解锁什么（预告，公开）<input v-model="editing.rewardTeaser" maxlength="500" placeholder="比如：一个全新的科技类 mod" /></label>
                <label class="wide">解锁内容（完成后公布，之前只有管理员看得到）<textarea v-model="editing.rewardText" rows="4" placeholder="新 mod 名称、版本、介绍、下载或更新说明……"></textarea></label>
                <p class="wide qp-muted qp-reward-hint">任务的奖励就是上面的「全服解锁」。下面两项是给参与者（被通过 / 记上名字的人）的额外经验和土豆，可以不填，填 0 就不发。</p>
                <label>参与者额外经验（可选）<input v-model.number="editing.rewardExp" type="number" min="0" placeholder="0" /></label>
                <label>参与者额外土豆（可选）<input v-model.number="editing.rewardPotato" type="number" min="0" placeholder="0" /></label>
                <div class="wide qp-shots">
                  <span>封面（可选）</span>
                  <div class="qp-thumbs">
                    <span v-if="editing.coverImage" class="qp-thumb wide-thumb"><img :src="editing.coverImage" alt="封面" /><button type="button" aria-label="移除封面" @click="editing.coverImage = ''">×</button></span>
                    <button v-else type="button" class="qp-add-shot" :disabled="uploading" @click="coverInput?.click()">{{ uploading ? '上传中…' : '＋ 封面' }}</button>
                  </div>
                  <input ref="coverInput" type="file" accept="image/*" hidden @change="onCover" />
                </div>
                <div class="wide qp-row">
                  <button type="submit" class="qp-primary" :disabled="busy === 'save'">保存</button>
                  <button type="button" @click="editing = null">取消</button>
                </div>
              </form>
            </div>
          </section>

          <!-- 已完成 -->
          <section class="qp-card" aria-label="已完成的任务">
            <h3>已完成的任务 · 解锁记录</h3>
            <p v-if="!overview.completed.length" class="qp-hint">还没有完成的任务，第一个解锁就看大家的了。</p>
            <article v-for="q in overview.completed" :key="q.id" class="qp-done">
              <header><h4>{{ q.title }}</h4><span class="qp-muted">{{ q.completedAt ? day(q.completedAt) : '' }} 完成 · {{ q.contributors }} 人参与{{ isGoal(q) ? '' : ` · ${fmt(q.progress)} ${q.unit}` }}</span></header>
              <p v-if="q.rewardText" class="qp-unlock"><span>解锁</span>{{ q.rewardText }}</p>
              <p v-if="q.unlocked?.length" class="qp-chain"><span>开启了</span>{{ refs(q.unlocked) }}</p>
              <button type="button" class="qp-link" @click="toggleBoard(q.id)">{{ boardOpen[q.id] ? '收起' : isGoal(q) ? '看完成者' : '看贡献榜' }}</button>
              <ol v-if="boardOpen[q.id]" class="qp-board small">
                <li v-for="(r, i) in boards[q.id] ?? []" :key="i"><span>{{ i + 1 }}</span><b>{{ who(r) }}</b><em v-if="!isGoal(q)">{{ fmt(r.amount) }}</em></li>
              </ol>
            </article>
          </section>
        </div>

        <aside class="qp-side">
          <section v-if="active && !isGoal(active)" class="qp-card" aria-label="贡献榜">
            <h3>贡献榜</h3>
            <p v-if="!(boards[active.id] ?? []).length" class="qp-hint">还没有人上榜，来当第一个吧。</p>
            <ol class="qp-board">
              <li v-for="(r, i) in boards[active.id] ?? []" :key="i" :class="{ top: i < 3 }"><span>{{ i + 1 }}</span><b>{{ who(r) }}</b><em>{{ fmt(r.amount) }}</em></li>
            </ol>
          </section>
          <section v-if="overview.upcomingCount" class="qp-card qp-next">
            <h3>后面还有 {{ overview.upcomingCount }} 个任务</h3>
            <p class="qp-hint">完成当前任务后自动开始，内容到时候揭晓。</p>
          </section>
          <section v-if="mine.length" class="qp-card" aria-label="我的贡献">
            <h3>我的贡献</h3>
            <ul class="qp-mine">
              <li v-for="c in mine" :key="c.id">
                <span class="qp-status" :class="c.status">{{ STATUS[c.status ?? 'pending'] }}</span>
                <div><b>{{ c.questKind === 'goal' ? '完成证明' : `${fmt(c.amount)} ${c.unit ?? ''}` }}</b><small>{{ c.questTitle }} · {{ timeAgo(c.createdAt) }}<RouterLink v-if="c.postId" class="qp-mine-post" :to="postRoute(c.postId)">报告帖</RouterLink></small><small v-if="c.reviewNote" class="qp-review-note">管理员：{{ c.reviewNote }}</small></div>
              </li>
            </ul>
          </section>
        </aside>
      </div>
    </template>
  </article>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import ParticleState from '@/components/ParticleState.vue'
import { useUserStore } from '@/stores/user'
import { useCos } from '@/composables/useCos'
import { getErrorMessage } from '@/utils/apiError'
import { useRoute } from 'vue-router'
import { questReportRoute, questReportsRoute } from '@/utils/forumBoards'
import { questApi, type QuestBoardRow, type QuestContribution, type QuestLine, type QuestOverview, type QuestPayload, type QuestRef, type ServerQuest } from '@/api/quests'

const userStore = useUserStore()
const { uploadFile } = useCos()
const overview = ref<QuestOverview | null>(null)
const loading = ref(false)
const loadError = ref('')
// 进行中的任务可能有好几个（主线 + 支线），selectedId 是现在看的那个
const actives = computed<ServerQuest[]>(() => overview.value?.actives ?? (overview.value?.active ? [overview.value.active] : []))
const selectedId = ref<number | null>(null)
const active = computed(() => actives.value.find(q => q.id === selectedId.value) ?? actives.value[0] ?? null)
const hasActiveMain = computed(() => actives.value.some(q => !isSide(q)))
watch(() => active.value?.id, id => { if (id && !boards[id]) void loadBoard(id) })
const mine = ref<QuestContribution[]>([])
const pending = ref<QuestContribution[]>([])
const boards = reactive<Record<number, QuestBoardRow[]>>({})
const boardOpen = reactive<Record<number, boolean>>({})
const STATUS: Record<string, string> = { pending: '审核中', approved: '已计入', rejected: '未通过' }
const ADMIN_TABS = [{ id: 'review', label: '待审核' }, { id: 'record', label: '记录进度' }, { id: 'manage', label: '任务管理' }] as const
defineProps<{ /** 放在论坛「我的世界」版块里时不显示页头 */ embedded?: boolean }>()
const route = useRoute()
// 首页「去创建第一个」带 ?tab=manage 过来，直接打开任务管理
const initialTab = String(route.query.tab ?? '')
const adminTab = ref<'review' | 'record' | 'manage'>(initialTab === 'manage' ? 'manage' : initialTab === 'record' ? 'record' : 'review')

const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
const isGoal = (q: ServerQuest | null | undefined) => q?.kind === 'goal'
const isSide = (q: { line?: QuestLine } | null | undefined) => q?.line === 'side'
const lineName = (q: { line?: QuestLine } | null | undefined) => isSide(q) ? '支线' : '主线'
const refs = (list: QuestRef[]) => list.map(r => `${lineName(r)}《${r.title}》`).join('、')
const pct = (q: ServerQuest) => q.target > 0 ? Math.min(100, (q.progress / q.target) * 100) : 0
const day = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' })
const who = (r: QuestBoardRow) => r.username ? (r.playerName && r.playerName !== r.username ? `${r.username}（${r.playerName}）` : r.username) : (r.playerName || '神秘玩家')
function rewardText(q: ServerQuest) {
  const parts: string[] = []
  if (q.rewardExp) parts.push(`${fmt(q.rewardExp)} 经验`)
  if (q.rewardPotato) parts.push(`${fmt(q.rewardPotato)} 土豆`)
  return parts.join(' + ')
}
function timeAgo(v: string) {
  const diff = Date.now() - new Date(v).getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  return new Date(v).toLocaleDateString('zh-CN')
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    overview.value = await questApi.overview()
    const tasks: Promise<unknown>[] = []
    if (active.value) tasks.push(loadBoard(active.value.id))
    if (userStore.isLoggedIn) tasks.push(questApi.mine().then(rows => { mine.value = rows }).catch(() => { mine.value = [] }))
    if (overview.value.canManage) tasks.push(questApi.pending().then(rows => { pending.value = rows }).catch(() => { pending.value = [] }))
    await Promise.all(tasks)
  } catch (error) {
    loadError.value = getErrorMessage(error, '世界任务加载失败')
  } finally { loading.value = false }
}
async function loadBoard(id: number) {
  try { boards[id] = await questApi.leaderboard(id) } catch { boards[id] = [] }
}
function toggleBoard(id: number) {
  boardOpen[id] = !boardOpen[id]
  if (boardOpen[id] && !boards[id]) void loadBoard(id)
}
onMounted(load)
watch(() => userStore.token, () => { void load() })

// ---------- 图片上传（任务封面） ----------
const uploading = ref(false)
const coverInput = ref<HTMLInputElement | null>(null)
const postRoute = (id: string) => ({ path: '/stream', query: { tag: '我的世界', post: id } })
async function upload(files: File[], folder: string) {
  uploading.value = true
  try {
    const out: string[] = []
    for (const file of files) {
      if (!file.type.startsWith('image/')) continue
      if (file.size > 10 * 1024 * 1024) { ElMessage.warning(`${file.name} 超过 10MB，跳过了`); continue }
      out.push((await uploadFile(file, folder)).url)
    }
    return out
  } catch { ElMessage.error('图片上传失败'); return [] } finally { uploading.value = false }
}
// ---------- 管理：审核 ----------
const reviewAmount = reactive<Record<number, number | ''>>({})
const reviewNote = reactive<Record<number, string>>({})
const busy = ref<number | string | null>(null)
async function review(c: QuestContribution, approve: boolean) {
  busy.value = c.id
  try {
    const amount = reviewAmount[c.id]
    const res = await questApi.review(c.id, { approve, amount: approve && amount ? Number(amount) : undefined, note: reviewNote[c.id] || undefined })
    ElMessage.success(res.message)
    if (res.completed) ElMessage({ type: 'success', message: '🎉 任务完成！解锁内容已经公布，下一个任务已开始', duration: 6000 })
    await load()
  } catch (error) { ElMessage.error(getErrorMessage(error, '操作失败')) } finally { busy.value = null }
}

// ---------- 管理：记录进度 ----------
const rec = reactive({ amount: null as number | null, username: '', playerName: '', note: '' })
const fixValue = ref<number | '' | null>(null)
async function record() {
  if (!active.value) return
  busy.value = 'record'
  try {
    const res = await questApi.record(active.value.id, { amount: rec.amount ?? 0, username: rec.username || undefined, playerName: rec.playerName || undefined, note: rec.note || undefined })
    ElMessage.success(res.message)
    rec.amount = null; rec.username = ''; rec.playerName = ''; rec.note = ''
    await load()
  } catch (error) { ElMessage.error(getErrorMessage(error, '记录失败')) } finally { busy.value = null }
}
/** 一次性目标：记上完成的人（几个人用逗号隔开），第一笔记上任务就完成，后面的人补发奖励 */
async function recordGoal() {
  if (!active.value) return
  const split = (v: string) => v.split(/[,，、;；]+/).map(s => s.trim()).filter(Boolean)
  const users = split(rec.username), players = split(rec.playerName)
  const count = Math.max(users.length, players.length)
  if (!count) return
  busy.value = 'record'
  const questId = active.value.id
  let done = 0, completed = false
  try {
    for (let i = 0; i < count; i++) {
      const res = await questApi.record(questId, { amount: 1, username: users[i] || undefined, playerName: players[i] || undefined, note: rec.note || undefined })
      done++
      if (res.completed) completed = true
    }
    if (completed) ElMessage({ type: 'success', message: '🎉 任务完成！解锁内容已经公布，下一个任务已开始', duration: 6000 })
    else ElMessage.success('已记录')
    rec.amount = null; rec.username = ''; rec.playerName = ''; rec.note = ''
  } catch (error) {
    ElMessage.error(getErrorMessage(error, done ? `前 ${done} 个人已经记上了，后面的没记上` : '记录失败'))
  } finally { busy.value = null; await load() }
}
async function fixProgress() {
  if (!active.value || fixValue.value === '' || fixValue.value === null) return
  busy.value = 'fix'
  try {
    const res = await questApi.setProgress(active.value.id, Number(fixValue.value))
    ElMessage.success(res.completed ? '进度已改，任务完成了！' : '进度已改')
    fixValue.value = null
    await load()
  } catch (error) { ElMessage.error(getErrorMessage(error, '修改失败')) } finally { busy.value = null }
}
async function forceComplete() {
  if (!active.value) return
  try {
    const q = active.value
    await ElMessageBox.confirm(`确定「${q.title}」已经完成了吗？会公布解锁内容${q.rewardExp || q.rewardPotato ? '、给参与者发额外奖励' : ''}，并开始下一个任务。`, '标记完成', { type: 'warning', confirmButtonText: '已完成', cancelButtonText: '取消' })
  } catch { return }
  busy.value = 'complete'
  try { ElMessage.success((await questApi.complete(active.value.id)).message); await load() }
  catch (error) { ElMessage.error(getErrorMessage(error, '操作失败')) } finally { busy.value = null }
}

// ---------- 管理：任务链 ----------
type Editing = QuestPayload & { id?: number; description: string; rewardTeaser: string; rewardText: string; coverImage: string }
const editing = ref<Editing | null>(null)
const editingSource = ref<ServerQuest | null>(null)
// 前置任务可选：除了自己和自己的后续任务（避免绕圈）
const prereqOptions = computed(() => {
  const all = [...actives.value, ...(overview.value?.upcoming ?? []), ...(overview.value?.completed ?? [])]
  const selfId = editing.value?.id
  if (!selfId) return all
  const descendants = new Set<number>([selfId])
  let grew = true
  while (grew) {
    grew = false
    for (const q of all) if (q.prereqId != null && descendants.has(q.prereqId) && !descendants.has(q.id)) { descendants.add(q.id); grew = true }
  }
  return all.filter(q => !descendants.has(q.id))
})
const prereqHint = computed(() => {
  const e = editing.value
  if (!e) return ''
  const p = prereqOptions.value.find(q => q.id === e.prereqId)
  if (!p) return e.line === 'side' ? '支线没设前置的话，保存后在列表里点「开始」' : '没设前置的主线按顺序排队，上一个主线完成后开始'
  if (p.status === 'completed') return '前置已经完成了，保存后马上开始' + (e.line === 'main' && hasActiveMain.value && !(e.id && actives.value.some(a => a.id === e.id)) ? '（不过现在有主线在进行，要等它完成或手动开始）' : '')
  return `《${p.title}》完成后自动开启。可以在《${p.title}》的「完成后全服解锁」里写个预告，比如「解锁一条支线任务」`
})
// 已经有进度的任务不能再改类型（后端也会拦）
const kindLocked = computed(() => !!editingSource.value && (editingSource.value.progress > 0 || editingSource.value.status === 'completed'))
function edit(q: ServerQuest | null) {
  editingSource.value = q
  editing.value = q
    ? { id: q.id, title: q.title, kind: q.kind ?? 'count', line: q.line ?? 'main', prereqId: q.prereqId ?? null, description: q.description ?? '', unit: q.unit, target: q.target, rewardTeaser: q.rewardTeaser ?? '', rewardText: q.rewardText ?? '', rewardExp: q.rewardExp, rewardPotato: q.rewardPotato, coverImage: q.coverImage ?? '' }
    : { title: '', kind: 'goal', line: 'main', prereqId: null, description: '', unit: '', target: 1, rewardTeaser: '', rewardText: '', rewardExp: 0, rewardPotato: 0, coverImage: '' }
  adminTab.value = 'manage'
}
async function onCover(e: Event) {
  const input = e.target as HTMLInputElement
  const [url] = await upload(Array.from(input.files ?? []).slice(0, 1), 'quests/covers')
  input.value = ''
  if (url && editing.value) editing.value.coverImage = url
}
async function saveQuest() {
  const q = editing.value
  if (!q) return
  busy.value = 'save'
  try {
    const { id, ...body } = q
    if (id) await questApi.update(id, body)
    else await questApi.create(body)
    ElMessage.success('已保存')
    editing.value = null
    await load()
  } catch (error) { ElMessage.error(getErrorMessage(error, '保存失败')) } finally { busy.value = null }
}
async function move(index: number, dir: -1 | 1) {
  const list = [...(overview.value?.upcoming ?? [])]
  const [item] = list.splice(index, 1)
  list.splice(index + dir, 0, item!)
  if (overview.value) overview.value.upcoming = list
  try { await questApi.reorder(list.map(q => q.id)) } catch (error) { ElMessage.error(getErrorMessage(error, '调整顺序失败')); await load() }
}
async function start(q: ServerQuest) {
  try { ElMessage.success((await questApi.start(q.id)).message); await load() } catch (error) { ElMessage.error(getErrorMessage(error, '操作失败')) }
}
async function remove(q: ServerQuest) {
  try { await ElMessageBox.confirm(`删除任务「${q.title}」？它的贡献记录也会一起删掉。`, '删除任务', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' }) } catch { return }
  try { await questApi.remove(q.id); ElMessage.success('已删除'); await load() } catch (error) { ElMessage.error(getErrorMessage(error, '删除失败')) }
}
</script>

<style scoped>
.quest-page { max-width: 1120px; margin: 0 auto; padding: 32px 20px 64px; color: var(--text-primary); }
.quest-page.embedded { max-width: none; margin: 0; padding: 4px 0 32px; }
.embedded-lead { margin: 0 0 18px; font-size: 14px; }
.qp-head { margin-bottom: 24px; }
.qp-kicker { margin: 0 0 6px; font-size: 11px; letter-spacing: .14em; color: var(--accent-color); }
.qp-head h1 { margin: 0 0 8px; font-size: clamp(26px, 4vw, 36px); }
.qp-lead { margin: 0; color: var(--text-secondary); max-width: 680px; line-height: 1.7; }
.qp-error { color: var(--danger); }
.qp-error button { border: 0; background: none; color: var(--accent-color); text-decoration: underline; cursor: pointer; font: inherit; }
.qp-active { display: grid; grid-template-columns: minmax(0, 1fr); gap: 0; border: 1px solid var(--border-line); border-top: 4px solid var(--accent-color); background: var(--paper-card); margin-bottom: 24px; }
.qp-active:has(.qp-cover) { grid-template-columns: minmax(180px, 320px) minmax(0, 1fr); }
.qp-cover { width: 100%; height: 100%; min-height: 200px; object-fit: cover; display: block; }
.qp-active-body { padding: 22px 24px; display: grid; gap: 12px; min-width: 0; }
.qp-btn { display: inline-block; padding: 9px 18px; background: var(--accent-color); color: var(--on-accent); font-size: 14px; font-weight: 600; text-decoration: none; }
.qp-btn:hover { filter: brightness(1.08); }
.qp-report .qp-row { display: flex; flex-wrap: wrap; align-items: center; gap: 14px; }
.qp-mine-post { margin-left: 6px; color: var(--accent-color); }
.qp-switch { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; }
.qp-switch button { display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.qp-switch button[aria-pressed=true] { border-color: var(--accent-color); box-shadow: inset 0 -2px 0 var(--accent-color); }
.qp-line { padding: 0 6px; font-size: 11px; font-weight: 600; color: var(--on-accent); background: var(--accent-color); }
.qp-line.side { background: var(--accent-mint, #3a9d8f); }
.qp-chain { margin: 0; font-size: 13px; line-height: 1.7; }
.qp-chain span { margin-right: 8px; font-size: 11px; font-weight: 600; letter-spacing: .06em; color: var(--accent-color); }
.qp-admin-quick { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding-top: 10px; border-top: 1px dashed var(--border-line); }
.qp-admin-quick button { padding: 8px 18px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; }
.qp-admin-quick button:disabled { opacity: .6; cursor: default; }
.qp-admin-quick small { flex: 1; min-width: 200px; font-size: 12px; color: var(--text-secondary); }
.qp-reward-hint { margin: 0; font-size: 12px; line-height: 1.6; }
.qp-tag { justify-self: start; padding: 1px 8px; font-size: 11px; font-weight: 600; color: var(--on-accent); background: var(--accent-color); }
.qp-active h2 { margin: 0; font-size: 22px; }
.qp-desc { margin: 0; white-space: pre-wrap; line-height: 1.7; color: var(--text-secondary); }
.qp-progress { display: grid; gap: 6px; }
.qp-goal { margin: 0; padding: 10px 12px; font-size: 13px; line-height: 1.7; color: var(--text-secondary); background: var(--bg-sub); border-left: 3px solid var(--accent-color); }
.qp-goal span { margin-right: 8px; font-weight: 600; color: var(--accent-color); }
.qp-kind { display: grid; gap: 6px; font-size: 13px; }
.qp-kind > span { color: var(--text-secondary); }
.qp-kind label { display: flex !important; flex-direction: row !important; align-items: baseline; gap: 6px; flex-wrap: wrap; cursor: pointer; }
.qp-kind label input { width: auto !important; padding: 0; accent-color: var(--accent-color); }
.qp-kind small { color: var(--text-secondary); font-size: 12px; }
.qp-bar { height: 16px; background: var(--bg-sub); border: 1px solid var(--border-line); overflow: hidden; }
.qp-bar span { display: block; height: 100%; background: repeating-linear-gradient(45deg, var(--accent-color) 0 10px, color-mix(in srgb, var(--accent-color) 80%, #fff) 10px 20px); transition: width .6s ease; }
.qp-nums { display: flex; align-items: baseline; gap: 6px; font-size: 14px; color: var(--text-secondary); }
.qp-nums b { font-size: 20px; color: var(--text-primary); }
.qp-nums span { margin-left: auto; font-weight: 600; color: var(--accent-color); }
.qp-facts { display: flex; flex-wrap: wrap; gap: 8px 28px; margin: 0; }
.qp-facts dt { font-size: 11px; color: var(--text-secondary); }
.qp-facts dd { margin: 0; font-weight: 600; }
.qp-teaser, .qp-secret, .qp-unlock { margin: 0; padding: 10px 12px; white-space: pre-wrap; line-height: 1.7; overflow-wrap: anywhere; }
.qp-teaser { background: var(--accent-soft); }
.qp-secret { border: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 13px; }
.qp-unlock { background: var(--bg-sub); border-left: 3px solid var(--accent-color); }
:is(.qp-teaser, .qp-secret, .qp-unlock) > span { display: block; font-size: 11px; font-weight: 600; letter-spacing: .06em; color: var(--accent-color); margin-bottom: 2px; }
.qp-empty { padding: 28px; border: 1px dashed var(--border-line); text-align: center; color: var(--text-secondary); margin-bottom: 24px; }
.qp-grid { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 24px; align-items: start; }
.qp-main, .qp-side { display: grid; gap: 20px; min-width: 0; }
.qp-card { padding: 18px 20px; border: 1px solid var(--border-line); background: var(--paper-card); min-width: 0; }
.qp-card h3 { margin: 0 0 12px; font-size: 16px; }
.qp-hint { margin: 0 0 12px; font-size: 13px; color: var(--text-secondary); line-height: 1.7; }
.qp-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.qp-form label { display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); min-width: 0; }
.qp-form .wide { grid-column: 1 / -1; }
.qp-form :is(input, textarea, select), .qp-review-actions input { box-sizing: border-box; width: 100%; min-width: 0; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 14px; padding: 7px 10px; }
.qp-form :is(input, textarea, select):focus, .qp-review-actions input:focus { outline: none; border-color: var(--accent-color); }
.qp-shots { display: grid; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.qp-thumbs { display: flex; flex-wrap: wrap; gap: 8px; }
.qp-thumb { position: relative; width: 72px; height: 72px; background: var(--bg-sub); }
.qp-thumb.wide-thumb { width: 160px; height: 90px; }
.qp-thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
.qp-thumb button { position: absolute; top: 2px; right: 2px; width: 20px; height: 20px; border: 0; background: #000a; color: #fff; cursor: pointer; }
.qp-add-shot { width: 72px; height: 72px; border: 1px dashed var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.qp-add-shot:hover { color: var(--accent-color); border-color: var(--accent-color); }
button { font: inherit; }
.qp-card button:not(.qp-add-shot):not(.qp-link):not([role=tab]), .qp-queue button { padding: 7px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font-size: 13px; cursor: pointer; }
.qp-card button.qp-primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
.qp-card button.qp-danger { color: var(--danger); }
.qp-card button:disabled { opacity: .5; cursor: not-allowed; }
.qp-link { border: 0; background: none; padding: 0; color: var(--accent-color); cursor: pointer; font-size: 13px; }
.qp-tabs { display: flex; gap: 4px; margin: -4px 0 14px; border-bottom: 1px solid var(--border-line); }
.qp-tabs button { padding: 8px 14px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); cursor: pointer; font-size: 14px; }
.qp-tabs button[aria-selected=true] { color: var(--accent-color); border-bottom-color: var(--accent-color); font-weight: 600; }
.qp-tabs b { margin-left: 6px; padding: 0 6px; font-size: 11px; color: var(--on-accent); background: var(--danger); }
.qp-review { padding: 12px 0; border-bottom: 1px solid var(--border-line); display: grid; gap: 8px; }
.qp-review header { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 10px; }
.qp-player { font-size: 12px; padding: 0 6px; background: var(--bg-sub); }
.qp-muted { font-size: 12px; color: var(--text-secondary); }
.qp-amount, .qp-note { margin: 0; }
.qp-note { white-space: pre-wrap; font-size: 13px; color: var(--text-secondary); }
.qp-review-actions { display: flex; flex-wrap: wrap; align-items: end; gap: 8px; }
.qp-review-actions label { display: grid; gap: 2px; font-size: 11px; color: var(--text-secondary); width: 110px; }
.qp-grow { flex: 1 1 180px; width: auto !important; }
.qp-fix { margin-top: 18px; padding-top: 14px; border-top: 1px dashed var(--border-line); align-items: end; }
.qp-queue { list-style: none; margin: 0 0 14px; padding: 0; display: grid; gap: 6px; }
.qp-queue li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 10px; border: 1px solid var(--border-line); }
.qp-queue li.is-active { border-color: var(--accent-color); }
.qp-queue small { color: var(--text-secondary); margin-right: auto; }
.qp-queue li.is-active button { margin-left: auto; }
.qp-queue button { padding: 3px 10px !important; font-size: 12px !important; }
.qp-order { width: 22px; height: 22px; display: grid; place-items: center; font-size: 12px; background: var(--bg-sub); }
.qp-editor { margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--border-line); }
.qp-editor h4 { margin: 0; font-size: 14px; }
.qp-row { display: flex; gap: 8px; }
.qp-done { padding: 14px 0; border-bottom: 1px solid var(--border-line); display: grid; gap: 8px; }
.qp-done:last-child { border-bottom: 0; }
.qp-done header { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; }
.qp-done h4 { margin: 0; font-size: 15px; }
.qp-board { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.qp-board li { display: grid; grid-template-columns: 24px minmax(0, 1fr) auto; align-items: center; gap: 8px; padding: 6px 8px; font-size: 13px; background: var(--bg-sub); }
.qp-board li.top span { color: var(--on-accent); background: var(--accent-color); }
.qp-board span { width: 22px; height: 22px; display: grid; place-items: center; font-size: 11px; background: var(--paper-card); }
.qp-board b { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.qp-board em { font-style: normal; font-weight: 600; color: var(--accent-color); }
.qp-board.small li { padding: 4px 8px; }
.qp-next { border-style: dashed; }
.qp-mine { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.qp-mine li { display: flex; gap: 10px; align-items: flex-start; font-size: 13px; }
.qp-mine div { display: grid; min-width: 0; }
.qp-mine small { color: var(--text-secondary); font-size: 11px; }
.qp-review-note { color: var(--danger) !important; }
.qp-status { flex-shrink: 0; padding: 1px 6px; font-size: 11px; background: var(--bg-sub); color: var(--text-secondary); }
.qp-status.approved { background: var(--accent-soft); color: var(--accent-color); }
.qp-status.rejected { color: var(--danger); }
@media (max-width: 900px) { .qp-grid { grid-template-columns: minmax(0, 1fr); } }
@media (max-width: 640px) {
  .quest-page { padding: 20px 16px 48px; }
  .qp-active:has(.qp-cover) { grid-template-columns: minmax(0, 1fr); }
  .qp-cover { min-height: 140px; max-height: 200px; }
  .qp-active-body { padding: 16px; }
  .qp-form { grid-template-columns: minmax(0, 1fr); }
}
</style>
