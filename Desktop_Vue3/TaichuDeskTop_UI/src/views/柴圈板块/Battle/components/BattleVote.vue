<!-- 投票制约战的投票区：用问卷系统存票（后端 /Battle/{id}/votes、/vote）。
     判定期内路人投票（参赛者不能投、每人一票），投过之后能看到票数；发起人发布结果时按票数定胜负。 -->
<template>
  <div class="bv">
    <h3>📊 路人投票</h3>
    <p v-if="loading" class="bv-tip">正在加载投票…</p>
    <template v-else-if="data && data.options.length">
      <p class="bv-tip">
        共 {{ data.total }} 票。<template v-if="status === 'judging'">参赛的人不能投，每人一票；发起人发布结果时按票数定胜负。</template>
        <template v-else>投票已结束。</template>
      </p>
      <ul class="bv-list">
        <li v-for="o in data.options" :key="o.id" class="bv-item" :class="{ mine: data.myOptionId === o.id }">
          <div class="bv-row">
            <span class="bv-text">{{ o.text }}<em v-if="data.myOptionId === o.id">我投的</em></span>
            <span v-if="showCounts" class="bv-count">{{ o.count }} 票</span>
            <button v-if="data.canVote" type="button" class="bv-btn" :disabled="busy" @click="vote(o.id)">投这个</button>
          </div>
          <div v-if="showCounts" class="bv-bar" aria-hidden="true"><span :style="{ width: `${data.total ? (o.count / data.total) * 100 : 0}%` }"></span></div>
        </li>
      </ul>
      <p v-if="!data.canVote && data.reason && status === 'judging'" class="bv-tip">{{ data.reason }}</p>
      <p v-if="!showCounts" class="bv-tip">投票后可以看到当前票数</p>
    </template>
    <template v-else>
      <p class="bv-tip">{{ status === 'judging' ? '这场约战还没开始投票。' : '没有投票记录。' }}</p>
      <button v-if="isInitiator && status === 'judging'" type="button" class="bv-btn" :disabled="busy" @click="start">开启投票</button>
    </template>
    <p v-if="error" class="bv-err" role="alert">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { battleApi, type BattleVotes } from '../battle_api'
import { getErrorMessage } from '@/utils/apiError'

const props = defineProps<{ battleId: string; status: string; isInitiator: boolean }>()

const data = ref<BattleVotes | null>(null)
const loading = ref(false)
const busy = ref(false)
const error = ref('')
// 票数：投过的人、发起人、投票结束后才显示（避免还没投的人跟风）
const showCounts = computed(() => !!data.value && (data.value.myOptionId != null || props.isInitiator || props.status !== 'judging'))

async function load() {
  loading.value = true
  try { data.value = await battleApi.votes(props.battleId) } catch { data.value = null } finally { loading.value = false }
}
async function vote(optionId: number) {
  busy.value = true; error.value = ''
  try { await battleApi.vote(props.battleId, optionId); await load() }
  catch (e) { error.value = getErrorMessage(e, '投票失败，请稍后再试') }
  finally { busy.value = false }
}
async function start() {
  busy.value = true; error.value = ''
  try { await battleApi.startVote(props.battleId); await load() }
  catch (e) { error.value = getErrorMessage(e, '开启投票失败') }
  finally { busy.value = false }
}
watch(() => [props.battleId, props.status], () => { void load() }, { immediate: true })
</script>

<style scoped>
/* 标题、按钮和约战详情页统一：直角、左竖线标题、主题色按钮 */
.bv h3 { margin: 0 0 10px; padding-left: 10px; border-left: 3px solid var(--text-primary); font-size: 17px; font-weight: 600; line-height: 1.3; color: var(--text-primary); }
.bv-tip { margin: 0 0 10px; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.bv-err { margin: 8px 0 0; color: var(--danger); font-size: 13px; }
.bv-list { list-style: none; margin: 0 0 8px; padding: 0; display: grid; gap: 8px; }
.bv-item { padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.bv-item.mine { border-color: var(--accent-color); box-shadow: inset 4px 0 0 var(--accent-color); }
.bv-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.bv-text { flex: 1 1 160px; min-width: 0; font-size: 14px; color: var(--text-primary); overflow-wrap: anywhere; }
.bv-text em { margin-left: 6px; padding: 0 5px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; font-style: normal; }
.bv-count { font: 600 16px/1 var(--font-display); color: var(--text-primary); font-variant-numeric: tabular-nums; }
.bv-btn { min-height: 0 !important; padding: 5px 14px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font: inherit; font-size: 13px; cursor: pointer; }
.bv-btn:hover:not(:disabled) { filter: brightness(1.08); }
.bv-btn:disabled { opacity: .5; cursor: not-allowed; }
.bv-bar { height: 8px; margin-top: 8px; background: var(--bg-sub); }
.bv-bar span { display: block; height: 100%; background: var(--accent-color); transition: width .3s; }
@media (prefers-reduced-motion: reduce) { .bv-bar span { transition: none; } }
</style>
