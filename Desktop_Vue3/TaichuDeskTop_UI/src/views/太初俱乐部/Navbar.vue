<template>
  <header class="navbar">
    <div class="nav-container">

      <router-link to="/Club" class="logo-area">
        <div class="logo-mark"><span></span><span></span><span></span></div>
        <div class="logo-text">
          <span class="logo-title">太初</span>
          <span class="logo-divider">/</span>
          <span class="logo-subtitle">三角洲分部</span>
        </div>
      </router-link>

      <nav class="nav-links">
        <router-link to="/Club"        class="nav-item" active-class="active">
          <span class="nav-index">01</span> 首页
        </router-link>
        <router-link to="/taichu/operators" class="nav-item" active-class="active">
          <span class="nav-index">02</span> 打手阵列
        </router-link>
        <router-link to="/taichu/pricing"   class="nav-item" active-class="active">
          <span class="nav-index">03</span> 价目单
        </router-link>
        <router-link to="/taichu/rules"     class="nav-item" active-class="active">
          <span class="nav-index">04</span> 分红规则
        </router-link>
        <router-link to="/taichu/events"    class="nav-item" active-class="active">
          <span class="nav-index">05</span> 赛事活动
        </router-link>
      </nav>

      <div class="nav-actions">
        <!-- ⭐ 客服工作台入口（仅审核通过的客服显示） -->
        <router-link
          v-if="isApprovedCs"
          to="/taichu/cs/dashboard"
          class="btn-cs"
          active-class="active"
        >
          <span>客服工作台</span>
          <i>🎧</i>
        </router-link>

        <router-link to="/taichu/profile" class="btn-apply">
          <span>我的</span><i>↗</i>
        </router-link>
      </div>

    </div>
  </header>
</template>

<script>
import { useUserStore } from '@/stores/user'
import request from '@/utils/request'

export default {
  name: 'Navbar',

  data() {
    return {
      isApprovedCs: false
    }
  },

  async mounted() {
    await this.detectCsIdentity()
  },

  methods: {
    /** ⭐ 检测当前用户是否为审核通过的客服 */
    async detectCsIdentity() {
      const token = useUserStore().token
      if (!token) return

      try {
        const res = await request.get('/club/cs/me')
        const data = res?.data ?? res
        this.isApprovedCs = !!data?.hasApplied && data?.auditStatus === 'approved'
      } catch (e) {
        // 静默失败：用户不是客服或网络问题，不显示入口
        this.isApprovedCs = false
      }
    }
  }
}
</script>

<style scoped>
.navbar { padding: 16px 0; border-bottom: 1px solid var(--line); background: var(--bg); }
.nav-container { display: flex; align-items: center; flex-wrap: wrap; gap: 16px; }
.logo-area { display: flex; text-decoration: none; color: var(--text); margin-right: auto; }
.logo-text { display: flex; gap: 8px; align-items: center; }
.logo-title { font-size: 18px; font-weight: 700; }
.logo-subtitle, .logo-divider { font-size: 12px; color: var(--text-soft); }
.logo-mark, .nav-index { display: none; }
.nav-links, .nav-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; }
.nav-item, .btn-apply, .btn-cs { display: inline-flex; align-items: center; gap: 6px; padding: 9px 12px; border-radius: 8px; color: var(--text-soft); text-decoration: none; font-size: 13px; }
.nav-item:hover, .nav-item.active, .btn-apply, .btn-cs { background: var(--accent-soft); color: var(--orange); }
.nav-actions i { font-style: normal; }
@media(max-width: 1100px) { .nav-links { order: 3; width: 100%; } }
</style>
