<!-- 顶部工具栏的「N 人在线」：每分钟更新一次（页面在后台时不更新），点了打开社区成员页（在线的排在最前面）。 -->
<template>
  <RouterLink v-if="count" to="/members" class="online-ind" :title="`现在 ${count.total} 人在线：${count.members} 位成员、${count.guests} 位游客。点击查看社区成员`">
    <i class="online-ind-dot" aria-hidden="true"></i>
    <span class="online-ind-num">{{ count.total }}</span><span class="online-ind-label"> 人在线</span>
  </RouterLink>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { membersApi, type OnlineCount } from '@/api/members'

const count = ref<OnlineCount | null>(null)
let timer = 0
let last = 0
async function refresh() {
  if (document.hidden) return
  last = Date.now()
  try { count.value = await membersApi.onlineCount() } catch { /* 后端还没更新时不显示 */ }
}
const onVisible = () => { if (!document.hidden && Date.now() - last > 20_000) void refresh() }
onMounted(() => {
  void refresh()
  timer = window.setInterval(refresh, 60_000)
  document.addEventListener('visibilitychange', onVisible)
})
onBeforeUnmount(() => { window.clearInterval(timer); document.removeEventListener('visibilitychange', onVisible) })
</script>

<style scoped>
.online-ind { display: inline-flex; align-items: center; gap: 6px; height: 32px; padding: 0 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font-size: 12px; text-decoration: none; white-space: nowrap; }
.online-ind:hover { border-color: var(--accent-color); color: var(--accent-color); }
.online-ind-dot { width: 7px; height: 7px; border-radius: 50%; background: #22c55e; box-shadow: 0 0 0 3px color-mix(in srgb, #22c55e 22%, transparent); }
.online-ind-num { font-weight: 700; color: var(--text-primary); font-variant-numeric: tabular-nums; }
@media (max-width: 420px) { .online-ind-label { display: none; } }
</style>
