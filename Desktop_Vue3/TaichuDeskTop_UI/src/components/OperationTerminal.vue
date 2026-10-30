<template>
  <aside class="terminal-wrapper" @keydown.esc="closeMenu">
    <div class="terminal-brand">
      <RouterLink to="/" class="brand-link" @click="isExpanded = false"><img class="brand-mark" src="/taichu-logo.png" alt="" width="40" height="40" /><span>太初寰宇<small>创作 · 相遇 · 共建</small></span></RouterLink>
      <button ref="trigger" class="menu-trigger" :aria-expanded="isExpanded" aria-controls="community-menu" @click="isExpanded = !isExpanded">{{ isExpanded ? '收起' : '菜单' }}</button>
    </div>
    <div id="community-menu" class="side-panel" :class="{ expanded: isExpanded }">
      <nav aria-label="全站导航" class="menu-list">
        <div v-for="(group, index) in groups" :key="group.title" class="nav-group">
          <p class="index-label"><span aria-hidden="true">SEC.0{{ index + 1 }}</span>{{ group.title }}</p>
          <a v-for="item in group.items" :key="item.name" :href="menuHref(item)" class="menu-item" :class="{ active: isActive(item) }" :aria-current="isActive(item) ? 'page' : undefined" @click="handleSelect($event, item)">
            <span class="menu-symbol" aria-hidden="true">{{ symbols[item.name] || '·' }}</span><span>{{ labels[item.name] || item.name }}</span>
          </a>
        </div>
        <details v-if="secondary.length" class="nav-more" :open="secondary.some(isActive)">
          <summary>更多</summary>
          <a v-for="item in secondary" :key="item.name" :href="menuHref(item)" class="menu-item" :class="{ active: isActive(item) }" :aria-current="isActive(item) ? 'page' : undefined" @click="handleSelect($event, item)">{{ labels[item.name] || item.name }}</a>
        </details>
      </nav>
      <div class="terminal-spec" aria-hidden="true"><span class="spec-barcode"></span><span>TC-HY / NODE 01</span></div>
      <div class="terminal-footer">
        <label class="theme-label" for="site-theme">显示模式</label>
        <select id="site-theme" :value="mode" @change="setTheme(($event.target as HTMLSelectElement).value as ThemeMode)">
          <option value="light">白天模式</option><option value="dark">夜间模式</option><option value="system">跟随系统</option>
        </select>
        <button type="button" class="sound-toggle" :class="{ on: uiSoundOn }" title="点击切换：关 → 轻 → 标准 → 强" @click="cycleUiSoundLevel"><span class="sound-bars" aria-hidden="true"><i v-for="n in 3" :key="n" :class="{ lit: uiSoundLevel >= n }"></i></span>界面音效 · {{ SOUND_LEVEL_NAMES[uiSoundLevel] }}</button>
        <button v-if="userStore.isLoggedIn" type="button" class="bg-link" @click="openBackground">自定义背景图 →</button>
        <button class="user-profile" @click="handleAuthClick"><span class="avatar">{{ userName?.[0]?.toUpperCase() || '客' }}</span><span class="user-info"><strong>{{ userName || '访客' }}</strong><small>{{ userStore.isLoggedIn ? '打开个人中心' : '登录，加入社区' }}</small></span><span>→</span></button>
      </div>
    </div>
  </aside>
</template>
<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { canOpenAdmin } from '@/utils/adminAccess'
import { SOUND_LEVEL_NAMES, cycleUiSoundLevel, uiSoundLevel, uiSoundOn } from '@/utils/uiSound'
import { LOGIN_PATH } from '@/router/auth'
import { useTheme, type ThemeMode } from '@/composables/useTheme'
interface MenuItem { name: string; url: string; isAdmin?: boolean }
const props = defineProps<{ menuItems: MenuItem[]; activeName: string }>()
const emit = defineEmits<{ navigate: [item: MenuItem] }>()
const userStore = useUserStore()
const route = useRoute()
const router = useRouter()
const { mode, setTheme } = useTheme()
const isExpanded = ref(false)
const trigger = ref<HTMLButtonElement>()
const userName = computed(() => userStore.userInfo?.username)
const labels: Record<string, string> = { 推送首页: '首页', 太初随笔: '论坛', 太初协作: '协作项目', 荣誉墙: '排行榜 · 荣誉墙' }
const symbols: Record<string, string> = { 推送首页: '⌂', 画廊展厅: '▧', 年度最佳: '✦', 太初随笔: '≡', 知识库: '▤', 开发进度: '▣', 赞助墙: '♡', 必吃榜: '≋', 版本更新: '◷', 聊天室: '◍', 活动中心: '◒', 社区规则: '§', 太初俱乐部: '◇', 柴圈板块: '◎', 荣誉墙: '☆', 太虚游: '⊞', 太初灵脉: '⌁', 世界观管理: '◈', 太初协作: '⊕', 交易行: '⇄' }
const sections = [
  { title: '内容', names: ['推送首页', '画廊展厅', '年度最佳', '太初随笔', '知识库', '活动中心', '必吃榜', '聊天室'] },
  { title: '创作', names: ['太初灵脉', '世界观管理', '太初协作'] },
  { title: '社区', names: ['柴圈板块', '太虚游', '交易行', '太初俱乐部', '赞助墙', '社区规则'] },
]
const availableItems = computed(() => props.menuItems.filter(item =>
  !['', '身份认证', '个人中心', '灵脉百科'].includes(item.name) &&
  (!(item.isAdmin || ['管理面板', '用户治理中枢'].includes(item.name)) || canOpenAdmin(userStore.permissions))))
const groups = computed(() => sections.map(section => ({ title: section.title,
  items: section.names.flatMap(name => availableItems.value.find(item => item.name === name) || [])
})).filter(group => group.items.length))
const secondary = computed(() => availableItems.value.filter(item => {
  if (sections.some(section => section.names.includes(item.name))) return false
  if (item.name === '任务栏' && availableItems.value.some(i => i.name === '太初协作') && router.hasRoute('任务栏')) return false
  return true
}))
const menuHref = (item: MenuItem) => /^https?:\/\//.test(item.url) ? item.url : router.resolve(item.url).href
function isActive(item: MenuItem) {
  if (item.name === '太初协作' && route.name === '任务栏') return true
  if (item.url === '/') return route.path === '/'
  const related: Record<string,string[]> = { 太虚游: ['/games'], 画廊展厅: ['/gallery'], 柴圈板块: ['/ocs','/joint','/battles','/community'], 太初俱乐部: ['/Club','/taichu'], 太初随笔: ['/stream','/posts','/write'], 知识库: ['/knowledge','/codex'] }
  return [item.url, ...(related[item.name] || [])].some(path => route.path === path || route.path.startsWith(path + '/'))
}
function closeMenu() { isExpanded.value = false; trigger.value?.focus() }
function openBackground() {
  emit('navigate', { name: '网站背景', url: `/user/${userStore.userId}?tab=appearance` })
  isExpanded.value = false
}
function handleAuthClick() {
  emit('navigate', { name: userStore.isLoggedIn ? '个人中心' : '身份认证', url: userStore.isLoggedIn ? `/user/${userStore.userId}` : LOGIN_PATH })
  isExpanded.value = false
}
function handleSelect(event: MouseEvent, item: MenuItem) {
  if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return
  event.preventDefault()
  emit('navigate', item)
  isExpanded.value = false
}
watch(() => route.fullPath, () => { isExpanded.value = false })
</script>
<style scoped>
/* 半透明 + 毛玻璃：能透出后面的网站背景，又和内容区有个简单的区分。
   底色用伪元素加透明度，不用 color-mix，旧内核手机浏览器（荣耀等）也能显示。
   不透明度由用户在「个人中心 → 网站背景 → 导航栏」里调（--nav-opacity，默认 .8）。 */
.terminal-wrapper { position: relative; z-index: 2; width: 220px; flex-shrink: 0; display: flex; flex-direction: column; border-right: 1px solid var(--border-line); background: transparent; -webkit-backdrop-filter: blur(14px) saturate(1.1); backdrop-filter: blur(14px) saturate(1.1); }
.terminal-wrapper::before { content: ''; position: absolute; inset: 0; z-index: -1; background: var(--paper-card); opacity: var(--nav-opacity, .8); pointer-events: none; }
.terminal-brand { position: relative; padding: 25px 20px; display: flex; align-items: center; justify-content: space-between; border-top: 4px solid var(--signal-color); }
.terminal-brand::after { content: ''; position: absolute; left: 20px; right: 20px; bottom: 0; height: 4px; background: repeating-linear-gradient(90deg, var(--text-primary) 0 1px, transparent 1px 6px); opacity: .28; }
.brand-link { display: flex; gap: 14px; align-items: center; text-decoration: none; color: var(--text-primary); font-size: 17px; font-weight: 750; letter-spacing: .04em; }
.brand-link small { display: block; margin-top: 5px; color: var(--text-secondary); font-size: 10px; font-weight: 400; letter-spacing: .08em; }
/* 标识放进铭牌框：内白边 + 外细框，替代原来的偏移投影。 */
.brand-mark { display: block; width: 40px; height: 40px; object-fit: contain; padding: 2px; background: #000; box-shadow: 0 0 0 3px var(--paper-card), 0 0 0 4px var(--text-primary), 4px 4px 0 4px var(--signal-color); }
.side-panel { display: flex; flex-direction: column; flex: 1; min-height: 0; }
.menu-list { padding: 0 14px; overflow: auto; flex: 1; }
.nav-group { margin-bottom: 18px; }
.index-label { display: flex; align-items: center; gap: 9px; padding: 16px 8px 8px; margin: 0 0 4px; font-size: 11px; color: var(--text-secondary); letter-spacing: .15em; }
.index-label span { padding: 2px 5px; font-family: var(--font-mono); font-size: 9px; letter-spacing: .08em; color: var(--inverse-text); background: var(--inverse-bg); }
.index-label::after { content: ''; flex: 1; height: 1px; background: var(--border-line); }
.menu-item { position: relative; width: 100%; display: flex; align-items: center; text-align: left; gap: 10px; padding: 10px 12px; border: 0; border-radius: 0; font-size: 13px; line-height: 1.4; color: var(--text-secondary); background: transparent; text-decoration: none; transition: color .2s, background .2s; }
.menu-symbol { transition: transform .2s; }
.menu-item:is(:hover, :focus-visible) .menu-symbol { transform: translateX(3px); }
.menu-item { isolation: isolate; overflow: hidden; }
.menu-item:not(.active)::after { content: ''; position: absolute; inset: 0; background: var(--bg-sub); z-index: -1; transform: scaleX(0); transform-origin: left; transition: transform .2s ease-out; }
.menu-item:not(.active):is(:hover, :focus-visible)::after { transform: scaleX(1); }
.menu-item:hover { color: var(--text-primary); }
/* 选中项为切角铭牌：右上斜切 + 左侧信号条 + 右端刻度。 */
.menu-item.active { background: var(--inverse-bg); color: var(--inverse-text); font-weight: 600; clip-path: polygon(0 0, calc(100% - var(--chamfer)) 0, 100% var(--chamfer), 100% 100%, 0 100%); }
.menu-item.active::before { content: ''; position: absolute; top: 10px; bottom: 10px; left: 0; width: 3px; background: var(--signal-color); }
.menu-item.active::after { content: ''; position: absolute; right: 12px; top: 50%; width: 14px; height: 5px; translate: 0 -50%; background: repeating-linear-gradient(90deg, var(--signal-color) 0 2px, transparent 2px 4px); }
.menu-item.active:focus-visible { outline-offset: -4px; outline-color: var(--signal-color); }
.menu-symbol { width: 20px; text-align: center; font-family: var(--font-mono); font-size: 17px; }
.nav-more { margin-bottom: 16px; border-top: 1px solid var(--border-line); }
.nav-more summary { padding: 12px 8px; color: var(--text-secondary); cursor: pointer; font-size: 12px; }
.nav-more .menu-item { padding-left: 24px; }
.terminal-spec { display: flex; align-items: center; gap: 10px; padding: 10px 16px; border-top: 1px solid var(--border-line); font: 9px/1 var(--font-mono); letter-spacing: .14em; color: var(--text-secondary); }
.spec-barcode { width: 34px; height: 12px; background: repeating-linear-gradient(90deg, var(--text-primary) 0 1px, transparent 1px 3px, var(--text-primary) 3px 5px, transparent 5px 6px, var(--text-primary) 6px 7px, transparent 7px 10px); opacity: .7; }
.terminal-footer { padding: 16px; border-top: 1px solid var(--border-line); }
.sound-toggle { display: flex; align-items: center; gap: 6px; width: 100%; margin-top: 8px; padding: 6px 8px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: 11px var(--font-mono, monospace); letter-spacing: .06em; cursor: pointer; }
.sound-bars { display: inline-flex; align-items: flex-end; gap: 2px; height: 10px; }
.sound-bars i { width: 3px; background: var(--border-line); }
.sound-bars i:nth-child(1) { height: 4px; } .sound-bars i:nth-child(2) { height: 7px; } .sound-bars i:nth-child(3) { height: 10px; }
.sound-bars i.lit { background: currentColor; }
.sound-toggle.on { border-color: color-mix(in srgb, var(--accent-color) 60%, var(--border-line)); color: var(--accent-color); }
.bg-link { display: block; margin-top: 8px; padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 11px; cursor: pointer; }
.bg-link:hover { color: var(--accent-color); }
.theme-label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 8px; }
select { width: 100%; border: 1px solid var(--border-line); border-radius: 0; padding: 9px; font-size: 12px; background: var(--bg-main); color: var(--text-primary); }
.user-profile { display: flex; align-items: center; gap: 10px; width: 100%; border: 0; padding: 16px 0 0; color: var(--text-primary); background: transparent; text-align: left; }
.avatar { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 0; border: 1px solid var(--border-line); background: var(--bg-sub); }
.user-info { flex: 1; min-width: 0; }
.user-info strong { display: block; overflow-wrap: anywhere; font-size: 12px; }
.user-info small { display: block; margin-top: 3px; font-size: 11px; color: var(--text-secondary); }
.menu-trigger { display: none; }
@media (prefers-reduced-motion: no-preference) {
  .menu-item.active::before { transform-origin: top; animation: nav-mark .3s ease-out; }
}
@keyframes nav-mark { from { transform: scaleY(0); } to { transform: scaleY(1); } }
@media(max-width: 900px) {
  .terminal-wrapper { width: 100%; border: 0; border-bottom: 1px solid var(--border-line); max-height: 75vh; }
  .terminal-brand { padding: 12px 16px; border-top-width: 3px; }
  .brand-mark { height: 30px; width: 30px; font-size: 19px; }
  .brand-link { font-size: 16px; }.brand-link small { display: none; }
  .menu-trigger { display: block; padding: 7px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-sub); color: var(--text-primary); }
  .side-panel { display: none; overflow: auto; }
  .side-panel.expanded { display: flex; }
  .menu-list { flex: none; display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 0 12px; padding-top: 12px; overflow: visible; }
  .terminal-spec { display: none; }
  .terminal-brand::after { display: none; }
  .terminal-footer { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 16px; }
  .theme-label { grid-column: 1; margin: 0; }
  select { grid-column: 1; }
  .bg-link { grid-column: 1; margin-top: 0; text-align: left; }
  .sound-toggle { grid-column: 1; margin-top: 0; }
  .user-profile { grid-column: 2; grid-row: 1 / 3; padding: 0; }
}
</style>
