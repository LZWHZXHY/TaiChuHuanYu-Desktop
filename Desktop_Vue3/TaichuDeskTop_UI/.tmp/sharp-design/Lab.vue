<script setup lang="ts">
import {ref,shallowRef,watch,defineAsyncComponent} from 'vue'
import {useRouter} from 'vue-router'
import {useSurfaceDepth} from '../../src/composables/useSurfaceDepth'
const root=ref<HTMLElement>();useSurfaceDepth(root)
const router=useRouter(), selected=ref(new URLSearchParams(location.search).get('page') || '赞助墙'), narrow=ref(false)
document.documentElement.dataset.theme=new URLSearchParams(location.search).get('theme') || 'light'
const pages:Record<string,[string,string]>={
 '赞助墙':['赞助墙/index.vue','/sponsors'], '更新日志':['版本更新/index.vue','/changelog'],
 '荣誉榜':['荣誉墙/index.vue','/honor'], '世界观卡片':['世界观管理/index.vue','/world'],
 '世界观创建':['世界观管理/ProjectCreate.vue','/world/create'], '知识库':['知识库/index.vue','/knowledge'],
 '画廊卡片':['画廊展厅/index.vue','/gallery'], '任务栏':['任务栏/index.vue','/'],
 '协作项目':['太初协作/index.vue','/'], '交易行':['交易行/index.vue','/'],
 '活动中心':['活动中心/ActivityHome.vue','/activity'], '打卡编辑':['活动中心/打卡中心/CreateActivity.vue','/activity/checkin/create'],
 '问卷编辑':['活动中心/问卷中心/SurveyEditor.vue','/activity/survey/create'],
 '管理端':['管理面板/index.vue','/admin'], '账号安全':['个人中心/PreferenceSettings.vue','/'],
 '博客编辑':['太初随笔/ComposeView.vue','/write/blog'], '太虚游':['太虚游/index.vue','/games']
}
const loaders=import.meta.glob('../../src/views/**/*.vue')
const view=shallowRef<any>()
watch(selected,async name=>{view.value=undefined;await router.isReady();await router.push(pages[name][1]);view.value=defineAsyncComponent(loaders['../../src/views/'+pages[name][0]] as any)},{immediate:true})
function theme(){document.documentElement.dataset.theme=document.documentElement.dataset.theme==='dark'?'light':'dark'}
</script>
<template><div ref="root" class="app-shell community-design lab-shell"><nav class="lab-tools"><strong>全站视觉检查 · 独立样例</strong><span>模拟权限，仅内存数据，禁止提交</span><select v-model="selected" aria-label="检查页面"><option v-for="(_,name) in pages" :key="name">{{ name }}</option></select><button @click="theme">切换日夜</button><button @click="narrow=!narrow">窄宽度</button></nav><main :class="{'lab-narrow':narrow,'activity-module':['活动中心','打卡编辑','问卷编辑'].includes(selected)}"><component :is="view" v-if="view" /></main></div></template>
<style>html,body,#app{height:auto!important;overflow:auto!important}.lab-shell{padding:24px}.lab-tools{display:flex;flex-wrap:wrap;gap:12px;align-items:center;max-width:1200px;margin:0 auto 28px;font-size:12px;color:var(--text-secondary)}.lab-tools :is(button,select){padding:8px;border:1px solid var(--border-line);background:var(--paper-card);color:var(--text-primary)}.lab-shell>main{max-width:1200px;margin:auto}.lab-shell>.lab-narrow{max-width:390px}.lab-tools strong{color:var(--accent-color)}</style>
