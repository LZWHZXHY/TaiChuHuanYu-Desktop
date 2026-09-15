<template>
  <footer class="footer">
    <div class="footer-container">
      <div class="broadcast">
        <span class="broadcast-status"><i></i> SYSTEM BROADCAST</span>
        <span class="broadcast-divider"></span>
        <span class="broadcast-text">恭喜太初突击队在周末跨服联赛中斩获桂冠！</span>
      </div>
      <div class="footer-right">
        <span>TAICHU ESPORTS CLUB</span>
        <span>© 2026</span>
      </div>
    </div>
  </footer>
</template>

<script>
export default { name: 'Footer' }
</script>

<style scoped>
.footer {
  background: #05080c;
  color: var(--text-muted);
  border-top: 1px solid var(--line);
}
.footer-container {
  width: min(1200px, calc(100% - 56px));
  min-height: 70px;
  margin: 0 auto;
  display: flex; align-items: center;
  justify-content: space-between; gap: 20px;
}
.broadcast { display: flex; align-items: center; gap: 14px; }
.broadcast-status {
  display: flex; align-items: center; gap: 7px;
  color: var(--orange);
  font-family: monospace; font-size: 8px; letter-spacing: 1px;
  white-space: nowrap;
}
.broadcast-status i {
  width: 5px; height: 5px; border-radius: 50%;
  background: var(--orange);
  box-shadow: 0 0 8px rgba(255, 107, 26, 0.7);
}
.broadcast-divider { width: 1px; height: 15px; background: var(--line-bright); }
.broadcast-text { font-size: 11px; }
.footer-right {
  display: flex; gap: 20px;
  font-family: monospace; font-size: 8px; letter-spacing: 1px;
  white-space: nowrap;
}
@media (max-width: 768px) {
  .footer-container {
    width: calc(100% - 40px);
    flex-direction: column; align-items: flex-start;
    padding: 20px 0; gap: 12px;
  }
}
</style>