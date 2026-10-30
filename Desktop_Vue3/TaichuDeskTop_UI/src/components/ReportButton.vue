<!-- 举报按钮：点了弹出「为什么举报」，提交给管理员。没登录时提示先登录。
     用法：<ReportButton target-type="artwork" :target-id="id" :target-title="title" />（地址默认用当前页面） -->
<template>
  <span class="report-wrap">
  <button type="button" class="report-btn" :class="{ plain }" :title="`举报这个${label}`" @click.stop="openDialog">{{ text ?? '举报' }}</button>
  <Teleport to="body">
    <div v-if="open" class="rp-mask" @mousedown.self="open = false">
      <form class="rp" role="dialog" aria-modal="true" :aria-label="`举报${label}`" @submit.prevent="submit" @keydown.esc="open = false">
        <header class="rp-head">
          <h3>举报{{ label }}</h3>
          <button type="button" class="rp-x" aria-label="关闭" @click="open = false">×</button>
        </header>
        <p v-if="targetTitle" class="rp-target">「{{ targetTitle.length > 60 ? targetTitle.slice(0, 60) + '…' : targetTitle }}」</p>
        <fieldset class="rp-reasons">
          <legend>哪里有问题</legend>
          <label v-for="(text, key) in REASON_LABELS" :key="key" :class="{ on: reason === key }"><input v-model="reason" type="radio" :value="key" /> {{ text }}</label>
        </fieldset>
        <label class="rp-detail">
          <span>补充说明 <small>{{ reason === 'other' ? '（必填）' : '（可选）' }}哪一句、哪张图、为什么</small></span>
          <textarea v-model="detail" rows="3" maxlength="500" placeholder="说清楚一点，管理员处理得更快"></textarea>
        </label>
        <p class="rp-hint">举报只有管理员看得到，不会告诉被举报的人是谁举报的。处理后会通知你结果。</p>
        <p v-if="error" class="rp-error" role="alert">{{ error }}</p>
        <footer class="rp-foot">
          <button type="button" class="rp-btn" @click="open = false">取消</button>
          <button type="submit" class="rp-btn primary" :disabled="busy || !reason">{{ busy ? '提交中…' : '提交举报' }}</button>
        </footer>
      </form>
    </div>
  </Teleport>
  </span>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { reportApi, REASON_LABELS, type ReportReason, type ReportTargetType } from '@/api/reports'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'

const props = defineProps<{ targetType: ReportTargetType; targetId: string | number; targetTitle?: string | null; targetUrl?: string; text?: string; plain?: boolean }>()
const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const LABELS: Record<ReportTargetType, string> = {
  post: '帖子', blog: '博客', artwork: '画作', comment: '评论', history: '内容', 'history-comment': '评论', work: '作品', character: '角色', user: '用户', world: '世界观', novel: '小说', other: '内容',
}
const label = computed(() => LABELS[props.targetType])
const open = ref(false)
const reason = ref<ReportReason | ''>('')
const detail = ref('')
const busy = ref(false)
const error = ref('')

function openDialog() {
  if (!userStore.isLoggedIn) {
    ElMessage.info('登录后才能举报')
    void router.push({ path: LOGIN_PATH, query: { redirect: route.fullPath } })
    return
  }
  reason.value = ''; detail.value = ''; error.value = ''
  open.value = true
}
async function submit() {
  if (!reason.value) return
  if (reason.value === 'other' && !detail.value.trim()) { error.value = '选了「其他」的话，写一下是什么问题吧'; return }
  busy.value = true; error.value = ''
  try {
    const r = await reportApi.create({
      targetType: props.targetType, targetId: String(props.targetId), targetTitle: props.targetTitle ?? undefined,
      targetUrl: props.targetUrl ?? route.fullPath, reason: reason.value, detail: detail.value.trim() || undefined,
    })
    ElMessage.success(r.message)
    open.value = false
  } catch (e: any) { error.value = e?.response?.data?.message || '没提交成功，稍后再试' }
  finally { busy.value = false }
}
</script>

<style scoped>
.report-wrap { display: inline-flex; }
.report-btn { padding: 3px 8px; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.report-btn:hover { color: #dc2626; }
.rp-mask { position: fixed; inset: 0; z-index: 3000; display: flex; align-items: flex-start; justify-content: center; padding: 10vh 16px; background: rgba(0, 0, 0, .4); overflow-y: auto; }
.rp { width: min(440px, 100%); display: grid; gap: 12px; padding: 0 0 4px; background: var(--paper-card); color: var(--text-primary); border-top: 3px solid #dc2626; box-shadow: 0 20px 50px rgba(0, 0, 0, .25); }
.rp-head { display: flex; align-items: center; padding: 14px 18px; border-bottom: 1px solid var(--border-line); }
.rp-head h3 { flex: 1; margin: 0; font-size: 16px; }
.rp-x { width: 30px; height: 30px; border: 0; background: none; color: var(--text-secondary); font-size: 20px; cursor: pointer; }
.rp-target { margin: 0 18px; font-size: 13px; color: var(--text-secondary); overflow-wrap: anywhere; }
.rp-reasons { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 18px; padding: 0; border: 0; }
.rp-reasons legend { margin-bottom: 6px; font-size: 13px; font-weight: 600; }
.rp-reasons label { display: inline-flex; align-items: center; gap: 4px; padding: 5px 10px; border: 1px solid var(--border-line); font-size: 13px; cursor: pointer; }
.rp-reasons label.on { border-color: #dc2626; color: #dc2626; }
.rp-reasons input { margin: 0; }
.rp-detail { display: grid; gap: 4px; margin: 0 18px; }
.rp-detail span { font-size: 13px; font-weight: 600; }
.rp-detail small { font-weight: 400; color: var(--text-secondary); }
.rp-detail textarea { box-sizing: border-box; width: 100%; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; resize: vertical; }
.rp-hint { margin: 0 18px; font-size: 12px; color: var(--text-secondary); }
.rp-error { margin: 0 18px; color: #dc2626; font-size: 13px; }
.rp-foot { display: flex; justify-content: flex-end; gap: 8px; padding: 10px 18px; border-top: 1px solid var(--border-line); }
.rp-btn { padding: 7px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.rp-btn.primary { border-color: #dc2626; background: #dc2626; color: #fff; }
.rp-btn:disabled { opacity: .5; cursor: default; }
</style>
