<!-- 心性测试答题页（替换原来的 components/TestGame.vue）。
     开始页 → 一题一页（单选 / 是非 / 量表点一下自动下一题，多选勾完点「下一题」，可以回上一题）→ 交卷由服务器算分、记成绩 → 结果页。
     服务器交卷失败（比如后端还没更新）时，在本地按同样规则算出结果照常显示，只提示「成绩没保存上」。 -->
<template>
  <div class="qp">
    <!-- 开始页 -->
    <section v-if="stage === 'intro'" class="qp-card qp-intro">
      <span class="qp-icon" aria-hidden="true">{{ game.icon || '📋' }}</span>
      <h2>{{ game.title }}</h2>
      <p v-if="game.description" class="qp-desc">{{ game.description }}</p>
      <p class="qp-meta">共 {{ questions.length }} 题<template v-if="creatorName"> · {{ creatorName }} 出题</template><template v-if="game.playCount"> · {{ game.playCount }} 人次测过</template></p>
      <div class="qp-actions">
        <button type="button" class="qp-btn primary" :disabled="!questions.length" @click="start">开始测试</button>
        <button v-if="showBack" type="button" class="qp-btn" @click="emit('back')">返回</button>
      </div>
      <p v-if="!questions.length" class="qp-tip">这个测试还没有题目。</p>
    </section>

    <!-- 答题 -->
    <section v-else-if="stage === 'answer' && current" class="qp-card">
      <div class="qp-progress" role="progressbar" :aria-valuenow="index + 1" :aria-valuemin="1" :aria-valuemax="questions.length">
        <span :style="{ width: `${((index + 1) / questions.length) * 100}%` }"></span>
      </div>
      <p class="qp-count">第 {{ index + 1 }} / {{ questions.length }} 题<template v-if="current.type === 'multiple'"> · 可多选</template></p>
      <h3 class="qp-question">{{ current.text }}</h3>
      <img v-if="current.image" :src="current.image" alt="" class="qp-qimg" loading="lazy" />

      <div class="qp-options" :class="{ likert: current.type === 'likert' }" role="group" :aria-label="current.text">
        <button v-for="o in current.options" :key="o.id" type="button" class="qp-opt" :class="{ on: picked[index]?.includes(o.id) }"
          :aria-pressed="picked[index]?.includes(o.id) ?? false" @click="choose(o.id)">
          <img v-if="o.image" :src="o.image" alt="" loading="lazy" />
          <span>{{ o.label }}</span>
        </button>
      </div>

      <div class="qp-nav">
        <button type="button" class="qp-btn" :disabled="index === 0" @click="index--">← 上一题</button>
        <button v-if="current.type === 'multiple' || picked[index]?.length" type="button" class="qp-btn primary"
          :disabled="!picked[index]?.length || submitting" @click="next">
          {{ index === questions.length - 1 ? (submitting ? '正在出结果…' : '看结果') : '下一题 →' }}
        </button>
      </div>
    </section>

    <!-- 结果 -->
    <section v-else-if="stage === 'result'" class="qp-card qp-result">
      <p class="qp-label">你的测试结果</p>
      <span class="qp-icon big" aria-hidden="true">{{ result?.icon || '🏷️' }}</span>
      <h2>{{ result?.title || '没有对应的结果' }}</h2>
      <img v-if="result?.image" :src="result.image" alt="" class="qp-rimg" loading="lazy" />
      <p v-if="result?.description" class="qp-rdesc">{{ result.description }}</p>
      <p class="qp-meta">得分 {{ score }}<template v-if="game.questionnaire?.scoring === 'average'">（平均分）</template></p>
      <p v-if="saveNote" class="qp-tip">{{ saveNote }}</p>
      <div class="qp-actions">
        <button type="button" class="qp-btn primary" @click="restart">再测一次</button>
        <button type="button" class="qp-btn" @click="copyResult">{{ copied ? '已复制 ✓' : '复制结果' }}</button>
        <button v-if="showBack" type="button" class="qp-btn" @click="emit('back')">返回列表</button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Game } from '../../game_types'
import { submitSession } from '../../games_api'

const props = withDefaults(defineProps<{ game: Game; showBack?: boolean }>(), { showBack: true })
const emit = defineEmits<{ back: []; completed: [data: { totalScore: number; resultId: number | null }] }>()

type Result = { id: number; title: string; description?: string; icon?: string; image?: string; min: number; max: number }

const questions = computed(() => [...(props.game.questionnaire?.questions ?? [])].sort((a, b) => a.order - b.order))
const results = computed<Result[]>(() => [...(props.game.questionnaire?.results ?? [])].sort((a, b) => a.order - b.order) as Result[])
const creatorName = computed(() => props.game.creator?.username || (props.game as Game & { creatorName?: string }).creatorName || '')

const stage = ref<'intro' | 'answer' | 'result'>('intro')
const index = ref(0)
/** 每道题选的选项 id */
const picked = ref<number[][]>([])
const current = computed(() => questions.value[index.value])
const submitting = ref(false)
const score = ref(0)
const result = ref<Result | null>(null)
const saveNote = ref('')
const copied = ref(false)
let advanceTimer = 0

function start() {
  picked.value = questions.value.map(() => [])
  index.value = 0
  stage.value = 'answer'
}
function restart() { result.value = null; saveNote.value = ''; copied.value = false; start() }

function choose(optionId: number) {
  const q = current.value
  if (!q || submitting.value) return
  const list = picked.value[index.value] ?? []
  if (q.type === 'multiple') {
    picked.value[index.value] = list.includes(optionId) ? list.filter(x => x !== optionId) : [...list, optionId]
    return
  }
  picked.value[index.value] = [optionId]
  // 单选类：点完稍停一下自动下一题（让人看到选中了哪个）；最后一题不自动交卷，让人点「看结果」
  if (index.value >= questions.value.length - 1) return
  window.clearTimeout(advanceTimer)
  advanceTimer = window.setTimeout(() => { if (stage.value === 'answer' && current.value === q) void next() }, 260)
}

async function next() {
  if (!picked.value[index.value]?.length) return
  if (index.value < questions.value.length - 1) { index.value++; return }
  await finish()
}

/** 本地算分（和服务器同一规则）：用于服务器交卷失败时 */
function localScore() {
  let total = 0
  questions.value.forEach((q, i) => {
    const ids = picked.value[i] ?? []
    total += q.options.filter(o => ids.includes(o.id)).reduce((s, o) => s + (o.value || 0), 0)
  })
  return props.game.questionnaire?.scoring === 'average' ? Math.round(total / Math.max(1, questions.value.length)) : total
}
function localResult(s: number): Result | null {
  const list = results.value
  if (!list.length) return null
  return list.find(r => s >= r.min && s <= r.max)
    ?? [...list].sort((a, b) => (s < a.min ? a.min - s : s - a.max) - (s < b.min ? b.min - s : s - b.max))[0] ?? null
}

async function finish() {
  submitting.value = true
  saveNote.value = ''
  try {
    const res = await submitSession({ gameId: props.game.id, optionIds: picked.value })
    score.value = res.totalScore
    result.value = res.result ?? localResult(res.totalScore)
  } catch {
    score.value = localScore()
    result.value = localResult(score.value)
    saveNote.value = '结果出来了，不过成绩没保存上（网络或服务器问题），不影响查看。'
  } finally {
    submitting.value = false
    stage.value = 'result'
    emit('completed', { totalScore: score.value, resultId: result.value?.id ?? null })
  }
}

async function copyResult() {
  const text = `我在太初寰宇做了「${props.game.title}」，结果是：${result.value?.icon ?? ''}${result.value?.title ?? ''}${result.value?.description ? `——${result.value.description}` : ''}`
  try { await navigator.clipboard.writeText(text); copied.value = true; window.setTimeout(() => { copied.value = false }, 1500) } catch { /* 复制不了就算了 */ }
}
</script>

<style scoped>
.qp { width: 100%; }
.qp-card { padding: 28px 24px; border: 1px solid var(--border-line); background: var(--paper-card); }
.qp-intro, .qp-result { text-align: center; }
.qp-icon { display: block; font-size: 44px; line-height: 1.2; }
.qp-icon.big { font-size: 60px; }
.qp-intro h2, .qp-result h2 { margin: 10px 0 8px; font-size: 1.4rem; color: var(--text-primary); }
.qp-desc, .qp-rdesc { margin: 0 auto 10px; max-width: 560px; color: var(--text-secondary); line-height: 1.8; white-space: pre-wrap; }
.qp-rdesc { color: var(--text-primary); }
.qp-meta { margin: 6px 0 0; color: var(--text-secondary); font-size: 13px; }
.qp-label { margin: 0 0 8px; color: var(--text-secondary); font-size: 12px; letter-spacing: .2em; }
.qp-tip { margin: 10px 0 0; color: var(--text-secondary); font-size: 12px; }
.qp-actions { display: flex; justify-content: center; flex-wrap: wrap; gap: 10px; margin-top: 20px; }
.qp-btn { padding: 9px 20px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.qp-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.qp-btn:disabled { opacity: .45; cursor: not-allowed; }
.qp-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.qp-btn.primary:hover:not(:disabled) { color: var(--on-accent, #fff); filter: brightness(1.08); }

.qp-progress { height: 4px; background: var(--bg-sub); margin-bottom: 14px; }
.qp-progress span { display: block; height: 100%; background: var(--accent-color); transition: width .25s; }
.qp-count { margin: 0 0 6px; color: var(--text-secondary); font-size: 12px; }
.qp-question { margin: 0 0 14px; font-size: 1.15rem; line-height: 1.6; color: var(--text-primary); }
.qp-qimg, .qp-rimg { display: block; max-width: 100%; max-height: 260px; margin: 0 auto 14px; object-fit: contain; }
.qp-options { display: grid; gap: 8px; }
.qp-options.likert { grid-template-columns: repeat(auto-fit, minmax(90px, 1fr)); }
.qp-opt { display: flex; align-items: center; gap: 10px; padding: 12px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 15px; text-align: left; cursor: pointer; transition: border-color .15s, background .15s; }
.qp-options.likert .qp-opt { justify-content: center; text-align: center; font-size: 13px; }
.qp-opt img { width: 48px; height: 48px; object-fit: cover; flex-shrink: 0; }
.qp-opt:hover { border-color: var(--accent-color); }
.qp-opt.on { border-color: var(--accent-color); background: var(--accent-soft, color-mix(in srgb, var(--accent-color) 10%, transparent)); color: var(--accent-color); font-weight: 600; }
.qp-nav { display: flex; justify-content: space-between; gap: 10px; margin-top: 18px; }
@media (max-width: 520px) { .qp-card { padding: 20px 14px; } }
</style>
