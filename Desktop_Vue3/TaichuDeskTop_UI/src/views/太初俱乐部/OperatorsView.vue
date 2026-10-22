<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="OPERATORS" subtitle="打手阵列 / ACTIVE ROSTER" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">
          <OperatorsSection
            @view="handleView"
            @order="handleOrder"
          />
        </div>
      </section>
    </main>

    <Footer />
  </div>
</template>

<script>
import { useUserStore } from '@/stores/user'
import Navbar            from './Navbar.vue'
import Footer            from './Footer.vue'
import SectionHeading    from './SectionHeading.vue'
import OperatorsSection  from './OperatorsSection.vue'

export default {
  name: 'OperatorsView',
  components: { Navbar, Footer, SectionHeading, OperatorsSection },

  methods: {
    handleView(uid) {
      // 目前 OperatorsSection 内部自己处理弹窗，外部不用管
      console.log('查看档案:', uid)
    },

    /** ⭐ 指定下单：跳到定价页，带上打手 ID */
    handleOrder(uid) {
      if (!useUserStore().token) {
        alert('请先登录')
        return
      }
      this.$router.push({
        path: '/taichu/pricing',
        query: { operatorUserId: uid }
      })
    }
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }
@media (max-width: 768px) { .page-hero { padding: 80px 0 40px; } }
</style>