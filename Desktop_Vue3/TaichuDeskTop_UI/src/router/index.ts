import { createRouter, createWebHashHistory } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { canOpenAdmin } from '@/utils/adminAccess'
import { LOGIN_PATH } from './auth'

// 工具箱页面沿用目录生成路由，所有页面均在访问时加载。
const toolModules = import.meta.glob('../views/工具箱/*/index.vue')
const toolsRoutes = Object.entries(toolModules).map(([path, component]) => {
  const name = path.split('/')[3]
  return { path: name, name, component }
})
const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/admin', name: '管理面板', component: () => import('@/views/管理面板/index.vue'), meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/write/:type(blog|post)', name: 'Compose', component: () => import('@/views/太初随笔/ComposeView.vue'), meta: { requiresAuth: true } },
    { path: '/', name: 'root', component: () => import('../views/推送首页/index.vue') },
    { path: '/community', name: 'ChaiCommunity', component: () => import('../views/柴圈板块/index.vue') },
    { path: '/honor', name: 'CommunityHonor', component: () => import('../views/荣誉墙/index.vue') },
    { path: '/games', name: 'GameHub', component: () => import('../views/太虚游/index.vue') },
    { path: '/gallery', name: 'GalleryHome', component: () => import('../views/画廊展厅/index.vue') },
    { path: '/stream', alias: '/posts', name: 'CommunityStream', component: () => import('../views/太初随笔/index.vue') },
    { path: '/posts/:id', redirect: to => ({ name: 'CommunityStream', query: { ...to.query, post: String(to.params.id) } }) },
    { path: '/knowledge', name: 'KnowledgeHome', component: () => import('../views/知识库/index.vue') },
    { path: '/codex', redirect: to => ({ name: 'KnowledgeHome', query: { ...to.query, type: 'wiki' } }) },
    { path: '/dev-progress', name: 'DevProgress', component: () => import('@/views/开发进度/index.vue') },
    { path: '/dev-progress/:id', name: 'DevReportDetail', component: () => import('@/views/开发进度/ReportDetail.vue') },
    { path: '/sponsors', name: 'Sponsors', component: () => import('@/views/赞助墙/index.vue') },
    { path: '/must-eat', name: 'MustEat', component: () => import('@/views/必吃榜/index.vue') },
    { path: '/changelog', name: 'Changelog', component: () => import('@/views/版本更新/index.vue') },
    { path: '/rules', name: 'CommunityRules', component: () => import('@/views/社区规则/index.vue') },

    {
      path: '/toolBox',
      component: () => import('@/views/工具箱/index.vue'),
      children: [
        { path: '', name: 'toolBox', component: () => import('@/views/工具箱/Home.vue') },
        ...toolsRoutes,
      ]
    },

    // ===== 太初俱乐部 =====
    { path: '/Club', name: 'TaichuHome', component: () => import('@/views/太初俱乐部/index.vue') },
    { path: '/taichu/operators', name: 'TaichuOperators', component: () => import('@/views/太初俱乐部/OperatorsView.vue') },
    { path: '/taichu/pricing', name: 'TaichuPricing', component: () => import('@/views/太初俱乐部/PricingView.vue') },
    { path: '/taichu/rules', name: 'TaichuRules', component: () => import('@/views/太初俱乐部/RulesView.vue') },
    { path: '/taichu/events', name: 'TaichuEvents', component: () => import('@/views/太初俱乐部/EventsView.vue') },
    { path: '/taichu/profile', name: 'TaichuProfile', component: () => import('@/views/太初俱乐部/MyProfile.vue') },
    { path: '/taichu/cs/register', name: 'CsRegister', component: () => import('@/views/太初俱乐部/CsRegister.vue') },
    { path: '/taichu/cs/wallet', name: 'CsWallet', component: () => import('@/views/太初俱乐部/CsWallet.vue') },
    { path: '/taichu/cs/dashboard', name: 'CsDashboard', component: () => import('@/views/太初俱乐部/CsDashboard.vue') },
    { path: '/taichu/my-complaints', name: 'MyComplaints', component: () => import('@/views/太初俱乐部/MyComplaints.vue') },
    { path: '/taichu/orders', name: 'TaichuOrders', component: () => import('@/views/太初俱乐部/MyOrders.vue') },
    { path: '/taichu/operator/orders', name: 'TaichuOperatorOrders', component: () => import('@/views/太初俱乐部/OperatorOrders.vue') },
    { path: '/taichu/operator/wallet', name: 'TaichuOperatorWallet', component: () => import('@/views/太初俱乐部/OperatorWallet.vue') },
    { path: '/taichu/operator/reviews', name: 'TaichuOperatorReviews', component: () => import('@/views/太初俱乐部/OperatorReviews.vue') },

    // ===== 世界观模块 =====
    {
      path: '/world',
      name: 'WorldHome',
      component: () => import('@/views/世界观管理/index.vue'),
      meta: { requiresAuth: false },
      children: [
        {
          path: 'create',
          name: 'ProjectCreate',
          component: () => import('@/views/世界观管理/ProjectCreate.vue'),
          meta: { requiresAuth: true }
        },
        {
          path: 'project/:id',
          name: 'ProjectDetail',
          component: () => import('@/views/世界观管理/ProjectDetail.vue'),
        },
        {
          path: 'project/:projectId/card/:cardId',
          name: 'CardDetail',
          component: () => import('@/views/世界观管理/ProjectDetail.vue'),
        }
      ]
    },

    // ===== 世界观配套小说 =====
    {
      path: '/novels',
      name: 'NovelShelf',
      component: () => import('@/views/世界观管理/Novels/index.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/novels/:id',
      name: 'NovelDetail',
      component: () => import('@/views/世界观管理/Novels/Detail.vue'),
      meta: { requiresAuth: false }
    },

    // ===== 灵脉模块 =====
    {
      path: '/lingmai',
      component: () => import('@/views/太初灵脉/SpiritLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/lingmai/note' },
        { path: 'note/:id?', name: 'SpiritNote', component: () => import('@/views/太初灵脉/components/NoteEditorView.vue'), props: true },
        { path: 'graph', name: 'SpiritGraph', component: () => import('@/views/太初灵脉/components/GraphView.vue'), props: true },
        { path: 'settings/:id', name: 'SpiritSettings', component: () => import('@/views/太初灵脉/components/NoteSettingsPanel.vue'), props: true },
        { path: 'wiki/:id', name: 'WikiEdit', component: () => import('@/views/太初灵脉/components/WorkspaceWiki.vue'), props: true }
      ]
    },

    // ===== 身份认证 =====
    { path: LOGIN_PATH, name: '身份认证', component: () => import('../views/身份认证/index.vue') },

    // ===== 个人中心 =====
    {
      path: '/user/:id',
      name: 'UserCenter',
      component: () => import('../views/个人中心/index.vue'),
      meta: { requiresAuth: true },
      props: true
    },

    // ===== 太初协作 =====
    {
      path: '/Project/project/:id',
      name: 'ProjectDetailCollab',
      component: () => import('../views/太初协作/协作组件/ProjectDetail.vue'),
      meta: { requiresAuth: true }
    },

    // ===== 灵脉百科 =====
    {
      path: '/codex/:id',
      name: 'WikiDetail',
      component: () => import('../views/灵脉百科/components/WikiDetail.vue'),
      props: true
    },
    {
      path: '/ocs',
      children: [

        { path: '', component: () => import('@/views/柴圈板块/OCs/index.vue') },
        { path: ':id', component: () => import('@/views/柴圈板块/OCs/components/detail.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/OCs/components/create.vue') },
        { path: 'edit/:id', component: () => import('@/views/柴圈板块/OCs/components/edit.vue') },
        { path: 'my', component: () => import('@/views/柴圈板块/OCs/components/my.vue') },
      ]
    },
    {
      path: '/joint',
      children: [
        { path: '', component: () => import('@/views/柴圈板块/Joint/index.vue') },
        { path: ':id', component: () => import('@/views/柴圈板块/Joint/components/JointDetail.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/Joint/components/JointCreate.vue') },
        { path: 'edit/:id', component: () => import('@/views/柴圈板块/Joint/components/JointEdit.vue') },
        { path: 'my', component: () => import('@/views/柴圈板块/Joint/components/MyJoint.vue') },
      ]
    },
    
    // ===== TCV 约战系统 =====
    {
      path: '/battles',
      children: [
        { path: '', component: () => import('@/views/柴圈板块/Battle/index.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/Battle/components/CreateBattle.vue') },
        { path: 'leaderboard', component: () => import('@/views/柴圈板块/Battle/components/Leaderboard.vue') },

        { path: ':id', component: () => import('@/views/柴圈板块/Battle/components/BattleDetail.vue') },

        { path: 'my', component: () => import('@/views/柴圈板块/Battle/components/MyBattles.vue') },
      ]
    },

    // ===== 柴圈作品（动画/图片） =====
    {
      path: '/works',
      children: [
        { path: '', component: () => import('@/views/柴圈板块/Works/index.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/Works/Create.vue') },
        { path: ':id', component: () => import('@/views/柴圈板块/Works/Detail.vue') },
      ]
    },

    // ===== 活动中心（多分区架构） =====

    {
      path: '/activity',
      component: () => import('@/views/活动中心/index.vue'),
      meta: { requiresAuth: true },
      children: [
        // ---- 分区入口页 ----
        { path: '', name: 'ActivityHome', component: () => import('@/views/活动中心/ActivityHome.vue') },

        // ---- 打卡专区 ----
        { path: 'checkin', name: 'CheckinSquare', component: () => import('@/views/活动中心/打卡中心/Square.vue') },
        { path: 'checkin/detail/:id', name: 'CheckinDetail', component: () => import('@/views/活动中心/打卡中心/ActivityDetail.vue') },
        { path: 'checkin/create', name: 'CreateCheckin', component: () => import('@/views/活动中心/打卡中心/CreateActivity.vue') },
        { path: 'checkin/my', name: 'MyCheckins', component: () => import('@/views/活动中心/打卡中心/MyActivities.vue') },

        // ---- 问卷专区 (新增) ----
        { path: 'survey', name: 'SurveyList', component: () => import('@/views/活动中心/问卷中心/SurveyList.vue') },
        { path: 'survey/manage', name: 'SurveyManage', component: () => import('@/views/活动中心/问卷中心/Manage.vue') },
        { path: 'survey/:id', name: 'FillSurvey', component: () => import('@/views/活动中心/问卷中心/FillSurvey.vue') },
        { path: 'survey/:id/result', name: 'SurveyResult', component: () => import('@/views/活动中心/问卷中心/SurveyResult.vue') },
        { path: 'survey/create', name: 'CreateSurvey', component: () => import('@/views/活动中心/问卷中心/SurveyEditor.vue') },
        { path: 'survey/edit/:id', name: 'EditSurvey', component: () => import('@/views/活动中心/问卷中心/SurveyEditor.vue') },

      ]
    }
  ]
})
// ===== 路由守卫 =====
router.beforeEach(async (to) => {
  const user = useUserStore()
  if (to.meta.requiresAuth && !user.isLoggedIn) return { path: LOGIN_PATH, query: { redirect: to.fullPath } }
  if (to.meta.requiresAdmin || to.name === '管理面板' || to.name === '用户治理中枢') {
    const profile = await user.fetchUserInfo(true)
    if (!profile || !canOpenAdmin(user.permissions)) return { path: '/', query: { access: 'denied' } }
  }
})

export default router
