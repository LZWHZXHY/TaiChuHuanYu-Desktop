<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="EVENTS" subtitle="赛事活动 / TOURNAMENTS" />
          <p class="placeholder">赛事活动页面建设中…</p>
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

export default {
  name: 'EventsView',
  components: { Navbar, Footer, SectionHeading }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; }
.placeholder {
  margin-top: 40px;
  color: var(--text-muted);
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
@media (max-width: 768px) { .page-hero { padding: 80px 0 40px; } }
</style>