<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="PROFILE" subtitle="我的 / OPERATOR PROFILE" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">
          <div class="profile-grid">
            <div class="profile-card">
              <div class="pc-label">身份</div>
              <div class="pc-value">老板 / 打手</div>
            </div>
            <div class="profile-card">
              <div class="pc-label">我的订单</div>
              <div class="pc-value">0</div>
            </div>
            <div class="profile-card">
              <div class="pc-label">我的评价</div>
              <div class="pc-value">—</div>
            </div>
            <div class="profile-card">
              <div class="pc-label">收益 / 消费</div>
              <div class="pc-value">¥0</div>
            </div>
          </div>
          <p class="placeholder">更多个人中心功能建设中…</p>
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
  name: 'MyProfile',
  components: { Navbar, Footer, SectionHeading }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }
.profile-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.profile-card {
  padding: 24px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.5);
}
.pc-label {
  color: var(--text-muted);
  font-family: monospace; font-size: 9px; letter-spacing: 1px;
  margin-bottom: 12px;
}
.pc-value { color: var(--text); font-size: 22px; font-weight: 700; }
.placeholder {
  margin-top: 40px;
  color: var(--text-muted);
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
@media (max-width: 768px) {
  .page-hero { padding: 80px 0 40px; }
  .profile-grid { grid-template-columns: repeat(2, 1fr); }
}
@media (max-width: 520px) {
  .profile-grid { grid-template-columns: 1fr; }
}
</style>