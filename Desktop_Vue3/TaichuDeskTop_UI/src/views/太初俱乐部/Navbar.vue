<template>
  <header class="navbar">
    <div class="nav-container">

      <router-link to="/" class="logo-area">
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

      <router-link to="/taichu/profile" class="btn-apply">
        <span>我的</span><i>↗</i>
      </router-link>

    </div>
  </header>
</template>

<script>
export default { name: 'Navbar' }
</script>

<style scoped>
.navbar {
  height: 76px;
  position: sticky;
  top: 0;
  z-index: 100;
  background: rgba(7, 10, 15, 0.82);
  backdrop-filter: blur(18px);
  border-bottom: 1px solid var(--line);
}
.nav-container {
  width: min(1320px, calc(100% - 56px));
  height: 100%;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.logo-area { display: flex; align-items: center; gap: 12px; text-decoration: none; }
.logo-mark {
  width: 27px; height: 27px;
  display: flex; align-items: center; justify-content: center;
  gap: 3px; transform: skew(-12deg);
}
.logo-mark span {
  display: block; width: 5px; height: 22px;
  background: var(--orange); transform: skew(-8deg);
}
.logo-mark span:nth-child(2) { height: 17px; opacity: 0.7; }
.logo-mark span:nth-child(3) { height: 12px; opacity: 0.45; }
.logo-text { display: flex; align-items: baseline; gap: 8px; }
.logo-title { font-size: 18px; font-weight: 800; letter-spacing: 4px; color: var(--text); }
.logo-divider { color: var(--text-muted); }
.logo-subtitle { color: var(--text-soft); font-size: 12px; letter-spacing: 2px; }

.nav-links { display: flex; align-items: center; gap: 34px; }
.nav-item {
  position: relative;
  color: var(--text-soft);
  text-decoration: none;
  font-size: 13px;
  letter-spacing: 1px;
  transition: color 0.25s ease;
}
.nav-index {
  color: var(--text-muted);
  font-family: monospace;
  font-size: 9px;
  margin-right: 7px;
  transition: color 0.25s ease;
}
.nav-item:hover { color: var(--text); }
.nav-item.active { color: var(--orange); }
.nav-item.active .nav-index { color: var(--orange); }
.nav-item.active::after {
  content: "";
  position: absolute;
  left: 0; right: 0; bottom: -29px;
  height: 2px;
  background: var(--orange);
  box-shadow: 0 0 12px rgba(255, 107, 26, 0.7);
}

.btn-apply {
  height: 38px;
  display: flex; align-items: center; gap: 16px;
  padding: 0 16px;
  border: 1px solid rgba(255, 107, 26, 0.35);
  background: rgba(255, 107, 26, 0.06);
  color: var(--orange);
  font-size: 12px; letter-spacing: 1px;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.25s ease;
}
.btn-apply i { font-style: normal; font-size: 16px; }
.btn-apply:hover {
  background: var(--orange); color: #fff;
  border-color: var(--orange);
  box-shadow: 0 0 24px rgba(255, 107, 26, 0.22);
}

@media (max-width: 1000px) {
  .nav-links { gap: 18px; }
  .nav-item { font-size: 12px; }
  .nav-index { display: none; }
}
@media (max-width: 768px) {
  .navbar { height: 64px; }
  .nav-container { width: calc(100% - 32px); }
  .logo-subtitle, .logo-divider { display: none; }
  .nav-links { display: none; }
  .btn-apply { height: 34px; padding: 0 12px; }
}
</style>