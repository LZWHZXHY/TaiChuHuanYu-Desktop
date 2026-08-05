import { createRouter, createWebHashHistory } from 'vue-router'

// ===== 静态导入世界观相关组件（避免动态导入问题） =====
import WorldIndex from '@/views/世界观管理/index.vue'
import ProjectCreate from '@/views/世界观管理/ProjectCreate.vue'
import ProjectDetail from '@/views/世界观管理/ProjectDetail.vue'

// ===== 活动中心相关导入 =====
import ActivityLayout from '@/views/活动中心/index.vue'
import ActivityHome from '@/views/活动中心/ActivityHome.vue'

// ---- 打卡专区 ----
import CheckinSquare from '@/views/活动中心/打卡中心/Square.vue'
import CheckinDetail from '@/views/活动中心/打卡中心/ActivityDetail.vue'
import CreateCheckin from '@/views/活动中心/打卡中心/CreateActivity.vue'
import MyCheckins from '@/views/活动中心/打卡中心/MyActivities.vue'


// ===== 问卷专区 =====
import SurveyList from '@/views/活动中心/问卷中心/SurveyList.vue'
import FillSurvey from '@/views/活动中心/问卷中心/FillSurvey.vue'
import SurveyEditor from '@/views/活动中心/问卷中心/SurveyEditor.vue'
import SurveyResult from '@/views/活动中心/问卷中心/SurveyResult.vue'
import Manage from '@/views/活动中心/问卷中心/Manage.vue'
// 后续结果页可选
// import SurveyResult from '@/views/活动中心/问卷中心/SurveyResult.vue'



const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('../views/推送首页/index.vue')
    },

    // ===== 世界观模块 =====
    {
      path: '/world',
      name: 'WorldHome',
      component: WorldIndex,
      meta: { requiresAuth: true },
      children: [
        {
          path: 'create',
          name: 'ProjectCreate',
          component: ProjectCreate,
          meta: { requiresAuth: true }
        },
        {
          path: 'project/:id',
          name: 'ProjectDetail',
          component: ProjectDetail,
          meta: { requiresAuth: true }
        },
        {
          path: 'project/:projectId/card/:cardId',
          name: 'CardDetail',
          component: () => import('@/views/世界观管理/世界观组件/CardDetail.vue'),
          meta: { requiresAuth: true }
        }
      ]
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
    {
      path: '/LoginRegister',
      name: '身份认证',
      component: () => import('../views/身份认证/index.vue')
    },

    // ===== 个人中心 =====
    {
      path: '/user/:id',
      name: 'UserCenter',
      component: () => import('../views/个人中心/index.vue'),
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
      path: '/ocs',   // 路由路径不变（用户访问的URL）
      children: [
        // ✅ 导入路径改为大写 S
        { path: '', component: () => import('@/views/柴圈板块/OCs/index.vue') },
        { path: ':id', component: () => import('@/views/柴圈板块/OCs/detail.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/OCs/create.vue') },
        { path: 'edit/:id', component: () => import('@/views/柴圈板块/OCs/edit.vue') },
        { path: 'my', component: () => import('@/views/柴圈板块/OCs/my.vue') },
      ]
    },
    {
      path: '/joint',
      children: [
        { path: '', component: () => import('@/views/柴圈板块/Joint/index.vue') },
        { path: ':id', component: () => import('@/views/柴圈板块/Joint/index.vue') },
        { path: 'create', component: () => import('@/views/柴圈板块/Joint/components/JointCreate.vue') },
        { path: 'edit/:id', component: () => import('@/views/柴圈板块/Joint/index.vue') },
        { path: 'my', component: () => import('@/views/柴圈板块/Joint/index.vue') },
      ]
    },
    // ==========================================
    // ===== 活动中心（多分区架构） =====
    // ==========================================
    {
      path: '/activity',
      component: ActivityLayout,
      meta: { requiresAuth: true },
      children: [
        // ---- 分区入口页 ----
        { path: '', name: 'ActivityHome', component: ActivityHome },

        // ---- 打卡专区 ----
        { path: 'checkin', name: 'CheckinSquare', component: CheckinSquare },
        { path: 'checkin/detail/:id', name: 'CheckinDetail', component: CheckinDetail },
        { path: 'checkin/create', name: 'CreateCheckin', component: CreateCheckin },
        { path: 'checkin/my', name: 'MyCheckins', component: MyCheckins },
        // 如果有排行榜，取消注释
        // ---- 问卷专区 (新增) ----
        { path: 'survey', name: 'SurveyList', component: SurveyList },
        { path: 'survey/manage', name: 'SurveyManage', component: Manage },
        { path: 'survey/:id', name: 'FillSurvey', component: FillSurvey },
        { path: 'survey/:id/result', name: 'SurveyResult', component: SurveyResult },
        { path: 'survey/create', name: 'CreateSurvey', component: SurveyEditor },
        { path: 'survey/edit/:id', name: 'EditSurvey', component: SurveyEditor },
        

        // ---- 预留扩展 ----
        // { path: 'quiz', name: 'QuizZone', component: QuizZone },
      ]
    }
  ]
})

// ===== 打印所有注册的路由（用于调试） =====
console.log('✅ 已注册的路由路径：', router.getRoutes().map(r => r.path))

// ===== 路由守卫 =====
router.beforeEach((to, from, next) => {
  const hasToken = !!localStorage.getItem('token')
  if (to.meta.requiresAuth && !hasToken) {
    next('/LoginRegister')
  } else {
    next()
  }
})

export default router