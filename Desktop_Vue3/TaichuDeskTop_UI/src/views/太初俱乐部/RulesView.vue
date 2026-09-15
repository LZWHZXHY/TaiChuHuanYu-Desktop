<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="DIVIDEND" subtitle="分红规则 / REVENUE SHARING" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">
          <DividendRules :tiers="dividendTiers" />
        </div>
      </section>
    </main>

    <Footer />
  </div>
</template>

<script>
import Navbar        from './Navbar.vue'
import Footer        from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import DividendRules from './DividendRules.vue'
import { dividendTiers } from './mock'

export default {
  name: 'RulesView',
  components: { Navbar, Footer, SectionHeading, DividendRules },
  data() { return { dividendTiers } }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }
@media (max-width: 768px) { .page-hero { padding: 80px 0 40px; } }
</style>