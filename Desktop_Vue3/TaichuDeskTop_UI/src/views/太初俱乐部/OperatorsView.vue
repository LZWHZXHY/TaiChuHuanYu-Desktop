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
          <!-- ✅ 不再传 operators，子组件自己去拉 -->
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
import Navbar            from './Navbar.vue'
import Footer            from './Footer.vue'
import SectionHeading    from './SectionHeading.vue'
import OperatorsSection  from './OperatorsSection.vue'
// ❌ 删掉这一行：import { operators } from './mock'

export default {
  name: 'OperatorsView',
  components: { Navbar, Footer, SectionHeading, OperatorsSection },
  // ❌ 删掉 data() { return { operators } }
  methods: {
    handleView(uid)  { console.log('查看档案:', uid) },
    handleOrder(uid) { console.log('指定下单:', uid) }
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }
@media (max-width: 768px) { .page-hero { padding: 80px 0 40px; } }
</style>