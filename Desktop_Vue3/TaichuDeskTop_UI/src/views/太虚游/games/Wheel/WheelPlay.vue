<!-- 转盘页：转一个转盘。
     · 用户转盘：结果在浏览器里随机（纯娱乐，不发奖励），转完后台只记一次次数；可以开「抽签模式」把转到的拿掉。
     · 官方转盘：点「转」后由服务器决定结果并发奖，前端只负责把动画转到服务器给的扇区；每天每人 1 次。 -->
<template>
  <div class="wheel-play">
    <div class="play-top">
      <button type="button" class="btn ghost" @click="emit('back')">← 转盘大厅</button>
      <span v-if="wheel" class="kind" :class="wheel.kind">{{ wheel.kind === 'official' ? '官方抽奖' : wheel.visibility === 'private' ? '仅自己可见' : '玩家转盘' }}</span>
    </div>

    <div v-if="loading" class="state">正在打开转盘…</div>
    <div v-else-if="loadError" class="state error">
      <p>{{ loadError }}</p>
      <button type="button" class="btn" @click="emit('back')">回转盘大厅</button>
    </div>

    <template v-else-if="wheel">
      <p v-if="wheel.status === 'hidden'" class="banner danger">
        这个转盘已经{{ wheel.kind === 'official' ? '下架' : '被管理员下架' }}{{ wheel.hiddenReason ? `（${wheel.hiddenReason}）` : '' }}，现在只有{{ wheel.kind === 'official' ? '管理员' : '你自己和管理员' }}能看到。
      </p>
      <p v-else-if="wheel.isMine && wheel.visibility === 'private'" class="banner">
        这是「仅自己可见」的转盘：不会出现在大厅。把下面的链接发给朋友，他们也能打开来转。
      </p>

      <!-- 全站最近中奖（官方转盘） -->
      <div v-if="isOfficial && feed.length" class="ticker" aria-label="最近中奖">
        <span class="ticker-tag">🎉 最近中奖</span>
        <div class="ticker-window">
          <div class="ticker-track" :style="{ animationDuration: `${Math.max(16, feed.length * 4)}s` }">
            <span v-for="(f, i) in [...feed, ...feed]" :key="i" class="ticker-item"><b>{{ f.name }}</b> 抽中 {{ f.prize }}</span>
          </div>
        </div>
      </div>

      <div class="play-grid">
        <section class="stage">
          <WheelCanvas ref="canvas" :segments="live" :size="500" :highlight="highlight">
            <template #center>
              <button type="button" class="go" :disabled="!canClickSpin" :aria-label="spinLabel" @click="spin">{{ spinning ? '…' : '转' }}</button>
            </template>
          </WheelCanvas>
          <button type="button" class="btn primary big" :disabled="!canClickSpin" @click="spin">{{ spinLabel }}</button>
          <p v-if="notice" class="notice" role="status">{{ notice }}</p>

          <!-- 用户转盘：抽签模式 -->
          <div v-if="!isOfficial" class="draw-tools">
            <label class="check"><input v-model="removeMode" type="checkbox" :disabled="spinning" /> 抽签模式：转到的从盘上拿掉（只在这个页面，刷新就恢复）</label>
            <button v-if="removedCount > 0" type="button" class="btn small" :disabled="spinning" @click="resetLive">放回全部（拿掉了 {{ removedCount }} 个）</button>
          </div>
          <div v-if="!isOfficial && draws.length" class="draws">
            <span>这次转到：</span>
            <ol><li v-for="(d, i) in draws" :key="i">{{ d }}</li></ol>
            <button type="button" class="link" @click="draws = []">清空</button>
          </div>
        </section>

        <aside class="info">
          <div class="info-head">
            <h2>{{ wheel.title }}</h2>
            <p class="meta">
              <template v-if="wheel.owner">by <RouterLink :to="`/user/${wheel.owner.id}`">{{ wheel.owner.name }}</RouterLink> · </template>
              <template v-else>太初寰宇官方 · </template>
              {{ wheel.segments.length }} 个扇区 · 转了 {{ wheel.spinCount.toLocaleString() }} 次
            </p>
          </div>
          <p v-if="wheel.intro" class="intro">{{ wheel.intro }}</p>

          <!-- 官方转盘：今天的状态、概率表、我的记录 -->
          <template v-if="isOfficial">
            <div class="today" :class="{ done: !!today }">
              <template v-if="!user.isLoggedIn">登录后每天可以在这里<b>免费转 1 次</b>，奖品直接发到账号。</template>
              <template v-else-if="today">
                今天已经转过了，转到「{{ today.text }}」{{ today.prizeLabel ? `，获得 ${today.prizeLabel}` : '' }}。<b>明天再来</b>（北京时间 0 点刷新）。
              </template>
              <template v-else>今天还能<b>免费转 1 次</b>，结果由服务器决定，奖品自动发放。</template>
            </div>
            <table class="odds">
              <caption>奖品和概率</caption>
              <thead><tr><th>扇区</th><th>奖品</th><th>概率</th></tr></thead>
              <tbody>
                <tr v-for="(s, i) in wheel.segments" :key="i">
                  <td><i class="dot" :style="{ background: s.color }"></i>{{ s.text }}</td>
                  <td>{{ prizeText(s) || '—' }}</td>
                  <td class="num">{{ percent(officialProbs[i] ?? 0) }}</td>
                </tr>
              </tbody>
            </table>
            <div v-if="user.isLoggedIn" class="records">
              <h3>我最近的抽奖</h3>
              <p v-if="!records.length" class="muted">还没有记录</p>
              <ul v-else>
                <li v-for="r in records" :key="r.id" :class="{ here: r.wheelId === wheel.id }">
                  <span class="r-date">{{ r.spinDate.slice(5) }}</span>
                  <span class="r-wheel">{{ r.wheelTitle }}</span>
                  <span class="r-prize" :class="{ none: !r.prizeLabel }">{{ r.prizeLabel || r.text }}</span>
                </li>
              </ul>
            </div>
          </template>

          <!-- 用户转盘：扇区和概率 -->
          <details v-else class="seg-odds">
            <summary>看看每个扇区的概率</summary>
            <ul>
              <li v-for="(s, i) in wheel.segments" :key="i"><i class="dot" :style="{ background: s.color }"></i>{{ s.text }}<span class="num">{{ percent(userProbs[i] ?? 0) }}</span></li>
            </ul>
          </details>

          <div class="actions">
            <button v-if="wheel.kind === 'user' && wheel.status === 'active'" type="button" class="btn small" @click="copyLink">复制链接</button>
            <button v-if="wheel.isMine" type="button" class="btn small" @click="emit('edit', wheel)">编辑</button>
            <ReportButton v-if="wheel.kind === 'user' && !wheel.isMine && wheel.status === 'active'" :target-type="REPORT_TYPE" :target-id="`wheel-${wheel.id}`" :target-title="`转盘「${wheel.title}」`" text="举报" />
            <template v-if="canModerate">
              <button v-if="wheel.status === 'active'" type="button" class="btn small danger" @click="moderate('hidden')">{{ isOfficial ? '下架' : '管理员下架' }}</button>
              <button v-else type="button" class="btn small" @click="moderate('active')">{{ isOfficial ? '重新上架' : '恢复' }}</button>
            </template>
          </div>
          <p v-if="wheel.kind === 'user'" class="fine">玩家转盘只是娱乐，转到什么都不会发奖励。</p>
        </aside>
      </div>
    </template>

    <!-- 结果弹窗 -->
    <Teleport to="body">
      <div v-if="result" class="wr-mask" @mousedown.self="closeResult">
        <div class="wr" role="dialog" aria-modal="true" aria-label="转盘结果" @keydown.esc="closeResult">
          <div class="wr-color" :style="{ background: result.segment.color }"></div>
          <p class="wr-eyebrow">{{ isOfficial ? (result.segment.prizeType !== 'none' ? '恭喜中奖' : '转盘结果') : '转到了' }}</p>
          <h3 class="wr-text">{{ result.segment.text }}</h3>
          <p v-if="isOfficial && result.segment.prizeType !== 'none' && !result.segment.text.includes(prizeText(result.segment))" class="wr-prize">+ {{ prizeText(result.segment) }}</p>
          <p v-if="result.message" class="wr-msg">{{ result.message }}</p>
          <p v-else-if="!isOfficial && removeMode" class="wr-msg">关掉后会把「{{ result.segment.text }}」从盘上拿掉</p>
          <div class="wr-foot">
            <button ref="okBtn" type="button" class="btn primary" @click="closeResult">好的</button>
            <button v-if="!isOfficial && live.length - (removeMode ? 1 : 0) >= 2" type="button" class="btn" @click="closeResult(); spin()">再转一次</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import ReportButton from '@/components/ReportButton.vue'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import { notifyPotatoChanged } from '@/api/potato'
import { uiSound } from '@/utils/uiSound'
import { wheelApi, wheelError, type WheelDetail, type WheelSegment, type WheelSpinRecord, type WheelSpinResponse, type WheelStatus } from '@/api/wheel'
import WheelCanvas from './WheelCanvas.vue'
import { absoluteUrl, percent, pickWeighted, prizeText, probabilities, wheelQuery } from './wheelShared'

const props = defineProps<{ id: number; shareKey?: string }>()
const emit = defineEmits<{ back: []; edit: [wheel: WheelDetail]; changed: [] }>()

/**
 * 举报的类型。举报系统现在还没有「转盘」这一类，先用 other（其他），标题和链接里写清楚是哪个转盘；
 * 等后端 ContentReport.TargetTypes 和前端 reports.ts 加上 'wheel' 以后，把这里改成 'wheel'。
 */
const REPORT_TYPE = 'other' as const

const user = useUserStore()
const route = useRoute()
const router = useRouter()

const wheel = ref<WheelDetail | null>(null)
const today = ref<WheelSpinRecord | null>(null)
const canModerate = ref(false)
const loading = ref(true)
const loadError = ref('')
const isOfficial = computed(() => wheel.value?.kind === 'official')

/** 现在盘上的扇区（抽签模式下会越转越少） */
const live = ref<WheelSegment[]>([])
const removeMode = ref(false)
const removedCount = computed(() => (wheel.value ? wheel.value.segments.length - live.value.length : 0))
const draws = ref<string[]>([])

const canvas = ref<InstanceType<typeof WheelCanvas> | null>(null)
const spinning = ref(false)
const highlight = ref<number | null>(null)
const notice = ref('')
const result = ref<{ index: number; segment: WheelSegment; message: string } | null>(null)
const okBtn = ref<HTMLButtonElement | null>(null)

const records = ref<WheelSpinRecord[]>([])
const feed = ref<{ id: number; name: string; prize: string }[]>([])

const officialProbs = computed(() => probabilities(wheel.value?.segments ?? []))
const userProbs = computed(() => probabilities(wheel.value?.segments ?? []))

const canClickSpin = computed(() => {
  if (!wheel.value || spinning.value || wheel.value.status !== 'active') return false
  if (isOfficial.value) return !today.value
  return live.value.length >= 2
})
const spinLabel = computed(() => {
  if (spinning.value) return '转动中…'
  if (!wheel.value) return '转'
  if (wheel.value.status !== 'active') return '已下架'
  if (isOfficial.value) {
    if (!user.isLoggedIn) return '登录后免费转一次'
    return today.value ? '今天已转过，明天再来' : '免费转一次'
  }
  if (live.value.length === 1) return `只剩「${live.value[0]?.text ?? ''}」了`
  return '开始转'
})

async function load() {
  loading.value = true
  loadError.value = ''
  result.value = null
  highlight.value = null
  try {
    const r = await wheelApi.get(props.id, props.shareKey)
    wheel.value = r.wheel
    today.value = r.today
    canModerate.value = r.canModerate
    live.value = r.wheel.segments.slice()
    removeMode.value = r.wheel.kind === 'user' && r.wheel.removeOnHit
    draws.value = []
    if (r.wheel.kind === 'official') void loadOfficialExtras()
  } catch (e) {
    loadError.value = wheelError(e, '这个转盘打不开，可能已经被删掉了')
  } finally {
    loading.value = false
  }
}

async function loadOfficialExtras() {
  try { feed.value = await wheelApi.feed() } catch { feed.value = [] }
  if (!user.isLoggedIn) return
  try {
    const me = await wheelApi.officialMe()
    records.value = me.records
    const t = me.today.find(x => x.wheelId === props.id)
    if (t) today.value = t
  } catch { /* 记录读不到不影响转 */ }
}

function goLogin() {
  ElMessage.info('登录后才能转官方转盘')
  void router.push({ path: LOGIN_PATH, query: { redirect: route.fullPath } })
}

async function spin() {
  if (!wheel.value || spinning.value) return
  if (isOfficial.value && !user.isLoggedIn) { goLogin(); return }
  if (!canClickSpin.value) return
  notice.value = ''
  highlight.value = null
  spinning.value = true
  try {
    if (isOfficial.value) await spinOfficial()
    else await spinUser()
  } finally {
    spinning.value = false
  }
}

/** 用户转盘：浏览器里按权重随机，转完给后台记一次次数 */
async function spinUser() {
  const w = wheel.value
  if (!w || live.value.length < 2) return
  const idx = pickWeighted(live.value)
  await canvas.value?.spinTo(idx)
  const seg = live.value[idx]
  if (!seg) return
  highlight.value = idx
  draws.value.unshift(seg.text)
  if (draws.value.length > 50) draws.value.length = 50
  showResult(idx, seg, '')
  void wheelApi.countSpin(w.id).then(r => { if (r.counted && wheel.value) wheel.value.spinCount++ }).catch(() => { /* 计数失败无所谓 */ })
}

/** 官方转盘：服务器决定结果并发奖，动画转到服务器给的扇区 */
async function spinOfficial() {
  const w = wheel.value
  if (!w) return
  let r: WheelSpinResponse
  try {
    r = await wheelApi.spinOfficial(w.id)
  } catch (e) {
    notice.value = wheelError(e, '没转成，稍后再试')
    const t = (e as { response?: { data?: { today?: WheelSpinRecord | null } } })?.response?.data?.today
    if (t) today.value = t
    return
  }
  // 管理员刚好改过扇区的话，先换成服务器这一刻的扇区，保证停的位置和结果一致
  const same = r.segments.length === live.value.length && r.segments.every((s, i) => s.text === live.value[i]?.text && s.weight === live.value[i]?.weight)
  if (!same) {
    w.segments = r.segments
    live.value = r.segments.slice()
    await nextTick()
  }
  await canvas.value?.spinTo(r.segmentIndex)
  highlight.value = r.segmentIndex
  w.spinCount++
  today.value = {
    id: 0, wheelId: w.id, wheelTitle: w.title, segmentIndex: r.segmentIndex, text: r.segment.text,
    prizeType: r.segment.prizeType, prizeAmount: r.segment.prizeAmount, prizeLabel: r.segment.prizeLabel ?? prizeText(r.segment),
    spinDate: '', createdAt: new Date().toISOString(),
  }
  if (r.segment.prizeType === 'potato') notifyPotatoChanged()
  if (r.segment.prizeType !== 'none') uiSound.confirm()
  showResult(r.segmentIndex, r.segment, r.message)
  void loadOfficialExtras()
}

function showResult(index: number, segment: WheelSegment, message: string) {
  result.value = { index, segment, message }
  void nextTick(() => okBtn.value?.focus())
}

function closeResult() {
  const r = result.value
  result.value = null
  if (!r) return
  // 抽签模式：把刚转到的拿掉
  if (!isOfficial.value && removeMode.value && live.value.length > 1) {
    live.value = live.value.filter((_, i) => i !== r.index)
    highlight.value = null
  }
}

function resetLive() {
  if (!wheel.value) return
  live.value = wheel.value.segments.slice()
  highlight.value = null
}

const link = computed(() => {
  const w = wheel.value
  if (!w) return ''
  const code = w.visibility === 'private' ? (w.shareCode ?? props.shareKey ?? null) : null
  return absoluteUrl(router.resolve({ path: route.path, query: wheelQuery(w.id, code) }).href)
})
async function copyLink() {
  try {
    await navigator.clipboard.writeText(link.value)
    ElMessage.success(wheel.value?.visibility === 'private' ? '链接已复制（带分享码，拿到的人都能转）' : '链接已复制')
  } catch {
    window.prompt('复制这个链接', link.value)
  }
}

async function moderate(status: WheelStatus) {
  const w = wheel.value
  if (!w) return
  let reason = ''
  if (status === 'hidden') {
    try {
      const r = await ElMessageBox.prompt(w.kind === 'user' ? '下架原因（会通知转盘主人）' : '下架后大家就看不到这个官方转盘了', '下架转盘', {
        confirmButtonText: '下架', cancelButtonText: '取消', inputPlaceholder: '例如：内容不适合公开',
      })
      reason = (r as unknown as { value?: string }).value ?? ''
    } catch { return }
  }
  try {
    const r = await wheelApi.adminSetStatus(w.id, status, reason)
    ElMessage.success(r.message)
    emit('changed')
    await load()
  } catch (e) {
    ElMessage.error(wheelError(e, '操作失败'))
  }
}

// 抽签模式关掉时，把拿掉的放回去，免得用户以为盘坏了
watch(removeMode, on => { if (!on) resetLive() })
watch(() => [props.id, props.shareKey], () => { void load() })
onMounted(load)
</script>

<style scoped>
.wheel-play { display: grid; gap: 14px; color: var(--text-primary); }
.play-top { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.kind { padding: 3px 10px; border: 1px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.kind.official { border-color: #d97706; color: #b45309; background: color-mix(in srgb, #f59e0b 12%, transparent); }
.state { padding: 48px 16px; text-align: center; color: var(--text-secondary); }
.state.error p { color: var(--danger, #dc2626); }
.banner { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; line-height: 1.6; }
.banner.danger { border-left-color: var(--danger, #dc2626); background: color-mix(in srgb, var(--danger, #dc2626) 8%, var(--paper-card)); }

.ticker { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); font-size: 13px; overflow: hidden; }
.ticker-tag { flex: none; font-weight: 600; color: #b45309; }
.ticker-window { flex: 1; overflow: hidden; mask-image: linear-gradient(90deg, transparent, #000 6%, #000 94%, transparent); }
.ticker-track { display: inline-flex; gap: 28px; white-space: nowrap; animation: ticker linear infinite; }
.ticker-item b { color: var(--accent-color); font-weight: 600; }
@keyframes ticker { from { transform: translateX(0); } to { transform: translateX(-50%); } }

.play-grid { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(260px, 1fr); gap: 24px; align-items: start; }
.stage { display: grid; gap: 14px; justify-items: center; padding: 22px 16px 18px; border: 1px solid var(--border-line); background: radial-gradient(circle at 50% 40%, color-mix(in srgb, var(--accent-color) 10%, var(--paper-card)), var(--paper-card) 70%); }
.go { width: clamp(56px, 16%, 84px); aspect-ratio: 1; border: 4px solid #fff; border-radius: 50%; background: #20252b; color: #fff; font: inherit; font-size: clamp(16px, 4vw, 22px); font-weight: 800; letter-spacing: .1em; cursor: pointer; box-shadow: 0 3px 12px rgba(0, 0, 0, .35); transition: transform .15s; }
.go:not(:disabled):hover { transform: scale(1.06); }
.go:disabled { cursor: default; background: #4b5563; }
.big { min-width: 220px; padding: 12px 24px; font-size: 16px; }
.notice { margin: 0; color: #b45309; font-size: 13px; text-align: center; }
.draw-tools { display: flex; flex-wrap: wrap; justify-content: center; align-items: center; gap: 8px 14px; }
.check { display: inline-flex; gap: 6px; align-items: center; font-size: 13px; color: var(--text-secondary); cursor: pointer; }
.draws { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px; width: 100%; font-size: 13px; color: var(--text-secondary); }
.draws ol { display: flex; flex-wrap: wrap; gap: 4px; margin: 0; padding: 0; list-style: none; counter-reset: d; }
.draws li { padding: 2px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); }
.link { border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }

.info { display: grid; gap: 14px; padding: 18px; border: 1px solid var(--border-line); background: var(--paper-card); }
.info-head h2 { margin: 0; font-size: 22px; overflow-wrap: anywhere; }
.meta { margin: 6px 0 0; color: var(--text-secondary); font-size: 12px; }
.meta a { color: var(--accent-color); text-decoration: none; }
.intro { margin: 0; white-space: pre-wrap; font-size: 14px; line-height: 1.7; color: var(--text-secondary); overflow-wrap: anywhere; }
.today { padding: 10px 12px; border: 1px dashed #d97706; background: color-mix(in srgb, #f59e0b 10%, transparent); font-size: 13px; line-height: 1.7; }
.today.done { border-color: var(--border-line); background: var(--bg-sub); }
.today b { color: #b45309; }
.odds { width: 100%; border-collapse: collapse; font-size: 13px; }
.odds caption { text-align: left; font-weight: 600; margin-bottom: 6px; }
.odds th { padding: 6px 4px; border-bottom: 1px solid var(--border-line); color: var(--text-secondary); font-weight: 500; text-align: left; font-size: 12px; }
.odds td { padding: 6px 4px; border-bottom: 1px dashed var(--border-line); }
.num { font-family: var(--font-mono, monospace); text-align: right; color: var(--accent-color); }
.odds th:last-child { text-align: right; }
.dot { display: inline-block; width: 10px; height: 10px; margin-right: 6px; vertical-align: -1px; }
.records h3 { margin: 0 0 6px; font-size: 14px; }
.records ul { display: grid; gap: 4px; margin: 0; padding: 0; list-style: none; max-height: 220px; overflow-y: auto; }
.records li { display: grid; grid-template-columns: 44px minmax(0, 1fr) auto; gap: 8px; padding: 4px 6px; font-size: 12px; }
.records li.here { background: var(--accent-soft); }
.r-date { font-family: var(--font-mono, monospace); color: var(--text-secondary); }
.r-wheel { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.r-prize { color: #b45309; font-weight: 600; }
.r-prize.none { color: var(--text-secondary); font-weight: 400; }
.seg-odds summary { cursor: pointer; font-size: 13px; color: var(--accent-color); }
.seg-odds ul { display: grid; gap: 4px; margin: 8px 0 0; padding: 0; list-style: none; font-size: 13px; }
.seg-odds li { display: flex; align-items: center; }
.seg-odds li .num { margin-left: auto; }
.actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding-top: 10px; border-top: 1px solid var(--border-line); }
.fine { margin: 0; font-size: 12px; color: var(--text-secondary); }
.muted { margin: 0; color: var(--text-secondary); font-size: 13px; }

.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 4px 10px; font-size: 12px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.ghost { color: var(--text-secondary); }
.btn.danger { color: var(--danger, #dc2626); }
.btn:disabled { opacity: .5; cursor: not-allowed; }

.wr-mask { position: fixed; inset: 0; z-index: 3000; display: grid; place-items: center; padding: 16px; background: rgba(0, 0, 0, .45); animation: fade .2s; }
.wr { position: relative; width: min(380px, 100%); display: grid; justify-items: center; gap: 8px; padding: 0 22px 20px; background: var(--paper-card); color: var(--text-primary); box-shadow: 0 24px 60px rgba(0, 0, 0, .35); text-align: center; animation: pop .28s cubic-bezier(.2, 1.4, .4, 1); }
.wr-color { width: calc(100% + 44px); height: 10px; margin-bottom: 10px; }
.wr-eyebrow { margin: 0; font: 11px var(--font-mono, monospace); letter-spacing: .2em; color: var(--text-secondary); }
.wr-text { margin: 0; font-size: 30px; line-height: 1.3; overflow-wrap: anywhere; }
.wr-prize { margin: 0; font-size: 20px; font-weight: 700; color: #b45309; }
.wr-msg { margin: 0; font-size: 13px; color: var(--text-secondary); line-height: 1.6; }
.wr-foot { display: flex; gap: 8px; margin-top: 8px; }
@keyframes fade { from { opacity: 0; } }
@keyframes pop { from { transform: scale(.85); opacity: 0; } }

@media (max-width: 860px) {
  .play-grid { grid-template-columns: minmax(0, 1fr); }
  .stage { padding: 16px 8px; }
  .big { width: 100%; }
}
@media (prefers-reduced-motion: reduce) {
  .ticker-track { animation: none; flex-wrap: wrap; white-space: normal; }
  .wr, .wr-mask { animation: none; }
}
</style>
