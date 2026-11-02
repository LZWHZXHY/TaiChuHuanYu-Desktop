<!-- 开发进度的门：详情（研发报告列表、单个项目的报告）只给管理员和赞助会员看。
     先问后端 /DevProject/access：能看就显示原来的页面；不能看显示「赞助会员专享」，下面附上简单进度（和首页那块一样）。
     赞助到期没续费的，会提示「已于某天到期，续费后马上恢复」。后端也会拦（没权限时接口只给简单进度），这里只是界面。 -->
<template>
  <div v-if="state === 'loading'" class="dg-wait content-page">正在确认访问权限…</div>
  <component :is="page === 'detail' ? ReportDetail : DevList" v-else-if="state === 'ok'" />
  <div v-else class="dg content-page community-surface tone-collab">
    <section class="dg-lock" aria-labelledby="dg-title">
      <span class="dg-icon" aria-hidden="true">🔒</span>
      <h1 id="dg-title">开发进度详情 · 赞助会员专享</h1>
      <p v-if="!access?.loggedIn">研发报告、开发日志、参考资料只对<b>{{ need }}及以上的赞助会员</b>和管理员开放。已经是赞助会员的话，请先登录。</p>
      <p v-else-if="access?.tierTooLow">开发进度详情需要<b>{{ need }}及以上</b>的赞助会员。你现在是{{ access?.tier || '觉醒者' }}，升级到{{ need }}后就能查看研发报告和开发日志。</p>
      <p v-else-if="access?.expired">你的赞助会员已于 <b>{{ untilText }}</b> 到期，续费后马上就能继续查看研发报告和开发日志。</p>
      <p v-else>研发报告、开发日志、参考资料只对<b>{{ need }}及以上的赞助会员</b>和管理员开放。赞助{{ need }}档位后就能查看全部内容（还附带自定义网站背景、个人主页装扮等特权）。</p>
      <div class="dg-actions">
        <RouterLink v-if="!access?.loggedIn" :to="{ path: LOGIN_PATH, query: { redirect: route.fullPath } }" class="dg-btn primary">登录</RouterLink>
        <RouterLink to="/sponsors" class="dg-btn" :class="{ primary: access?.loggedIn }">{{ access?.tierTooLow ? '升级赞助' : access?.expired ? '去续费' : '了解赞助' }}</RouterLink>
        <RouterLink to="/" class="dg-btn">回首页</RouterLink>
      </div>
      <p class="dg-note">在爱赞助赞助后，私信告诉我们你的网站用户名，管理员核对后就会开通。</p>
    </section>
    <!-- 简单进度：所有人都能看 -->
    <DevProgress class="dg-simple" :hide-link="true" />
  </div>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { devProjectApi, type DevAccess } from '@/api/devProject'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import DevProgress from '@/views/推送首页/components/DevProgress.vue'

defineProps<{ page: 'list' | 'detail' }>()

const DevList = defineAsyncComponent(() => import('./index.vue'))
const ReportDetail = defineAsyncComponent(() => import('./ReportDetail.vue'))

const route = useRoute()
const user = useUserStore()
const access = ref<DevAccess | null>(null)
const state = ref<'loading' | 'ok' | 'locked'>('loading')

async function check() {
  try {
    access.value = await devProjectApi.access()
    state.value = access.value.canView ? 'ok' : 'locked'
  } catch {
    // 读不到权限（比如后端还是旧版本、没有这个接口）：先放行，后端照样会按权限给数据
    access.value = null
    state.value = 'ok'
  }
}
onMounted(check)
// 在这个页面登录 / 退出后重新判断
watch(() => user.isLoggedIn, () => { state.value = 'loading'; void check() })

const need = computed(() => access.value?.needTier || '超凡者')
const untilText = computed(() => {
  const u = access.value?.sponsorUntil
  if (!u) return ''
  const d = new Date(u)
  return Number.isNaN(d.getTime()) ? '' : `${d.getFullYear()} 年 ${d.getMonth() + 1} 月 ${d.getDate()} 日`
})
</script>

<style scoped>
.dg-wait { padding: 60px 20px; text-align: center; color: var(--text-secondary); }
.dg { max-width: 760px; margin: 0 auto; padding: 32px 20px 48px; display: grid; gap: 20px; }
.dg-lock { padding: 32px 28px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); text-align: center; }
.dg-icon { font-size: 2.2rem; }
.dg-lock h1 { margin: 10px 0 12px; font-size: 1.35rem; }
.dg-lock p { margin: 0 auto; max-width: 520px; color: var(--text-secondary); line-height: 1.8; font-size: .92rem; }
.dg-lock p b { color: var(--text-primary); }
.dg-actions { display: flex; justify-content: center; flex-wrap: wrap; gap: 10px; margin: 22px 0 14px; }
.dg-btn { padding: 9px 20px; border: 1px solid var(--border-line); color: var(--text-primary); text-decoration: none; font-size: .9rem; }
.dg-btn:hover { border-color: var(--accent-color); color: var(--accent-color); }
.dg-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.dg-btn.primary:hover { color: var(--on-accent, #fff); filter: brightness(1.08); }
.dg-note { font-size: .78rem !important; }
@media (max-width: 520px) { .dg-lock { padding: 24px 16px; } }
</style>
