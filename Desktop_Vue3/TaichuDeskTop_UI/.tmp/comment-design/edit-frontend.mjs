import fs from 'node:fs';
fs.writeFileSync('src/utils/adminAccess.ts',`// 与后台各管理接口的权限范围保持一致；最终授权由服务端完成。
export const adminPermissions = ['SuperAdmin', 'Trade_Manage', 'User_Audit', 'Wiki_Editor', 'System_Monitor']
export const canOpenAdmin = (permissions: string[]) => adminPermissions.some(permission => permissions.includes(permission))
`);
let p='src/router/index.ts',s=fs.readFileSync(p,'utf8');s=s.replace("import { LOGIN_PATH }", "import { canOpenAdmin } from '@/utils/adminAccess'\nimport { LOGIN_PATH }");s=s.replace('  routes: [',`  routes: [
    { path: '/admin', name: '管理面板', component: () => import('@/views/管理面板/index.vue'), meta: { requiresAuth: true, requiresAdmin: true } },`);s=s.replace('router.beforeEach((to) => {','router.beforeEach(async (to) => {');s=s.replace("  if (to.meta.requiresAuth && !useUserStore().isLoggedIn) return { path: LOGIN_PATH, query: { redirect: to.fullPath } }",`  const user = useUserStore()
  if (to.meta.requiresAuth && !user.isLoggedIn) return { path: LOGIN_PATH, query: { redirect: to.fullPath } }
  if (to.meta.requiresAdmin || to.name === '管理面板' || to.name === '用户治理中枢') {
    const profile = await user.fetchUserInfo(true)
    if (!profile || !canOpenAdmin(user.permissions)) return { path: '/', query: { access: 'denied' } }
  }`);fs.writeFileSync(p,s);
p='src/components/OperationTerminal.vue';s=fs.readFileSync(p,'utf8').replace("import { LOGIN_PATH }", "import { canOpenAdmin } from '@/utils/adminAccess'\nimport { LOGIN_PATH }").replace("userStore.permissions.includes('SuperAdmin')","canOpenAdmin(userStore.permissions)");fs.writeFileSync(p,s);
p='src/App.vue';s=fs.readFileSync(p,'utf8').replace("const allPlugins = ref<Plugin[]>([])","const navigationPending = ref(''), navigationError = ref('')\nlet lastNavigation: Pick<Plugin, 'name' | 'url'> | null = null\nlet navigationVersion = 0\nconst allPlugins = ref<Plugin[]>([])");s=s.replace("  { name: '推送首页'", "  { name: '太初灵脉', url: '/lingmai/note', icon: '·', requiresAuth: true },\n  { name: '推送首页'");s=s.replace("item.name === '知识库' ? { ...item, url: '/knowledge' } : item", "item.name === '知识库' ? { ...item, url: '/knowledge' } : item.name === '太初灵脉' ? { ...item, url: '/lingmai/note' } : item.name === '管理面板' ? { ...item, url: '/admin' } : item");s=s.replace("      if (item.url !== '/'",`      if (item.name === '管理面板' && item.url.startsWith('/') && !router.getRoutes().some(route => route.path.toLowerCase() === item.url.toLowerCase())) {
        router.addRoute({ path: item.url, redirect: '/admin', meta: { requiresAuth: true, requiresAdmin: true } })
      }
      if (item.url !== '/'`);
s=s.replace("meta: { requiresAuth: item.requiresAuth }", "meta: { requiresAuth: item.requiresAuth, requiresAdmin: ['管理面板', '用户治理中枢'].includes(item.name) }");
const start=s.indexOf('const handleNavigation =');const end=s.indexOf('\nonMounted(',start);
s=s.slice(0,start)+`const handleNavigation = async (item: Pick<Plugin, 'name' | 'url'>) => {
  lastNavigation = item
  const version = ++navigationVersion
  navigationPending.value = item.name
  navigationError.value = ''
  try {
    if (item.url.startsWith('http')) {
      const host = (window as any).chrome?.webview
      if (host) host.postMessage({ cmd: 'load_external_url', url: item.url })
      else window.open(item.url, '_blank', 'noopener,noreferrer')
    } else await router.push(item.url)
  } catch {
    if (version === navigationVersion) navigationError.value = '页面加载失败，请重试。若刚更新过网站，请刷新后再打开。'
  } finally { if (version === navigationVersion) navigationPending.value = '' }
}
`+s.slice(end);
s=s.replace('    <main class="container">',`    <main class="container">
      <div v-if="navigationPending" class="navigation-state" role="status">正在打开 {{ navigationPending }}…</div>
      <div v-if="navigationError" class="navigation-state" role="alert">{{ navigationError }} <button @click="lastNavigation && handleNavigation(lastNavigation)">重试</button></div>
      <p v-if="router.currentRoute.value.query.access === 'denied'" class="navigation-state" role="alert">当前账号没有管理权限。</p>`);
s=s.replace('.app-shell {','.navigation-state { margin: 0; padding: 12px 20px; color: var(--text-primary); background: var(--accent-soft); font-size: 14px; }\n.app-shell {');fs.writeFileSync(p,s);
p='src/views/管理面板/index.vue';s=fs.readFileSync(p,'utf8').replace("import { ref, computed}","import { useUserStore } from '@/stores/user'\nimport { ref, computed, watch}").replace('const menus = [','const user = useUserStore()\nconst allMenus = [');s=s.replace('const currentView = computed',`const modulePermissions: Record<string,string> = { data: 'System_Monitor', trade: 'Trade_Manage', users: 'User_Audit', product: 'Trade_Manage', wiki: 'Wiki_Editor' }
const menus = computed(() => allMenus.filter(menu => user.permissions.includes('SuperAdmin') || (modulePermissions[menu.id] && user.permissions.includes(modulePermissions[menu.id]))))
watch(menus, items => { if (!items.some(item => item.id === activeTab.value)) activeTab.value = items[0]?.id || '' }, { immediate: true })
const currentView = computed`).replaceAll('menus.find(', 'menus.value.find(').replace('return menu ? menu.component : 数据中心;', 'return menu?.component;').replace('<component :is="currentView" />','<component v-if="currentView" :is="currentView" />');fs.writeFileSync(p,s);
p='src/views/太初灵脉/SpiritLayout.vue';s=fs.readFileSync(p,'utf8');let before=JSON.parse(fs.readFileSync('.tmp/comment-design/frontend-before.json','utf8'));before[p]=s;fs.writeFileSync('.tmp/comment-design/frontend-before.json',JSON.stringify(before));s=s.replace('import { ref, computed,','import { defineAsyncComponent, ref, computed,');for(const name of ['GraphView','HistoryPanel','NoteSettingsPanel','PublishModal','QuickEditorDrawer']){s=s.replace(new RegExp('import '+name+" from '([^']+)';"),(m,file)=>`const ${name} = defineAsyncComponent(() => import('${file}'));`);if(name!=='GraphView'){const visible={HistoryPanel:'isHistoryOpen',NoteSettingsPanel:'isSettingsOpen',PublishModal:'showPublishModal',QuickEditorDrawer:'isQuickEditorOpen'}[name];s=s.replace('<'+name+'\n','<'+name+'\n      v-if="'+visible+'"\n');}}
s=s.replace('position: fixed; inset: 0; background: var(--paper-card); backdrop-filter', 'position: absolute; inset: 0; background: var(--paper-card); backdrop-filter');fs.writeFileSync(p,s);
