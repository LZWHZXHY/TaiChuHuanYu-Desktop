import {createApp, h, ref} from 'vue'
import {createPinia} from 'pinia'
import {createRouter, createWebHashHistory} from 'vue-router'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import '../../src/assets/styles/base.css'
import '../../src/assets/styles/content.css'
import '../../src/assets/styles/variables.css'
import '../../src/views/太初俱乐部/theme.css'
import {setupTheme,useTheme} from '../../src/composables/useTheme'
import request from '../../src/utils/request'
import Admin from '../../src/views/管理面板/index.vue'
import ReviewModal from '../../src/views/太初俱乐部/ReviewModal.vue'
import ComplaintModal from '../../src/views/太初俱乐部/ComplaintModal.vue'
const title='隔离验收 · 模拟数据 · 不写入数据库'
const item={id:'1',title:'用于验收的画作',authorName:'测试创作者',authorId:'test-author',status:'published',views:10,likes:3,favorites:2,createdAt:'2026-10-21',isFeatured:false}
request.get=async(url:any)=>{
 if(url==='/Admin/Data/Metrics')return Object.fromEntries(['totalUsers','dau','wau','mau'].map(k=>[k,{value:12,trendValue:2,trendPercent:3,isUp:true}])) as any
 if(url==='/admin/product/gallery')return {items:[item],totalCount:1} as any
 if(url.includes('categories'))return [] as any
 return {items:[],totalCount:0,total:0,data:[]} as any
}
for(const method of ['post','put','patch','delete'] as const)request[method]=async()=>({message:'隔离检查，无数据库写入'}) as any
setupTheme()
const router=createRouter({history:createWebHashHistory(),routes:[{path:'/:pathMatch(.*)*',component:Admin}]})
createApp({setup(){
 const {mode,setTheme}=useTheme(),modal=ref('')
 return ()=>h('div',{style:'padding:24px;max-width:1440px;margin:auto'},[
 h('header',{style:'display:flex;gap:16px;flex-wrap:wrap;align-items:center;margin-bottom:24px'},[
 h('strong',title),h('select',{'aria-label':'验收主题',value:mode.value,onChange:(e:any)=>setTheme(e.target.value)},[h('option',{value:'light'},'白天'),h('option',{value:'dark'},'夜间')]),
 h('button',{onClick:()=>modal.value='review'},'检查俱乐部评价弹窗'),h('button',{onClick:()=>modal.value='complaint'},'检查俱乐部投诉弹窗')]),
 h(Admin),h(ReviewModal,{visible:modal.value==='review',order:{id:'test',orderNo:'本地验收',gameName:'测试游戏',orderTypeName:'陪玩',operatorName:'测试陪玩'},onClose:()=>modal.value=''}),
 h(ComplaintModal,{visible:modal.value==='complaint',order:{id:'test',orderNo:'本地验收',gameName:'测试游戏'},onClose:()=>modal.value=''})])
}}).use(createPinia()).use(router).use(ElementPlus).mount('#app')
