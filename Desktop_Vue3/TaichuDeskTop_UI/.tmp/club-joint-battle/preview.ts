import {createApp,h,ref} from 'vue'
import {createPinia} from 'pinia'
import {createRouter,createWebHashHistory} from 'vue-router'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import '../../src/assets/styles/base.css'
import '../../src/assets/styles/variables.css'
import '../../src/views/太初俱乐部/theme.css'
import {setupTheme,useTheme} from '../../src/composables/useTheme'
import request from '../../src/utils/request'
import Dashboard from '../../src/views/太初俱乐部/CsDashboard.vue'
import OrderModal from '../../src/views/太初俱乐部/OrderModal.vue'
const order={orderNo:'隔离验收-001',gameCode:'test',gameName:'测试游戏',orderTypeName:'组队体验',status:'pending',paidConfirmedAt:'2026-10-21',price:100,basePrice:100,minOperatorCount:1,maxOperatorCount:2,pricingMode:'level-based',createdAt:'2026-10-21',userName:'测试老板',operators:[]}
const ops=[{userId:'one',name:'测试成员甲',status:'online',level:'L2',reputation:100,totalOrders:5,levelSurchargeRate:0.1},{userId:'two',name:'测试成员乙',status:'online',level:'L1',reputation:100,totalOrders:3},{userId:'off',name:'离线成员',status:'offline',reputation:100}]
request.get=async(url:string,config:any)=>{
 if(url==='/club/cs/me')return {hasApplied:true,auditStatus:'approved',name:'模拟客服',status:'online'} as any
 if(url==='/club/cs/orders')return {items:[config?.params?.tab==='processing'?{...order,status:'processing',operatorUserId:'one',operators:[{userId:'one',name:'测试成员甲',status:'accepted'}]}:{...order,userPaidAt:'2026-10-21'}],total:1} as any
 if(url==='/club/cs/operators')return {items:ops} as any
 if(url==='/club/operators/list')return {items:ops.map(op=>({...op,games:[{gameCode:'test',level:'L1'}]}))} as any
 return {items:[],total:0} as any
}
for(const method of ['post','put','patch','delete'] as const)request[method]=async()=>{throw Error('隔离验收：禁止写入与财务操作')}
setupTheme()
const DashboardFixture={...Dashboard,computed:{...Dashboard.computed,isLoggedIn:()=>true}}
createApp({setup(){const {mode,setTheme}=useTheme(),modal=ref(false);return()=>h('div',{style:'padding:20px;max-width:1200px;margin:auto'},[
 h('header',{style:'display:flex;gap:16px;align-items:center;flex-wrap:wrap;margin-bottom:20px'},[h('strong','隔离验收 · 模拟数据 · 禁止数据库写入'),h('select',{'aria-label':'验收主题',value:mode.value,onChange:(e:any)=>setTheme(e.target.value)},[h('option',{value:'light'},'白天'),h('option',{value:'dark'},'夜间')]),h('button',{onClick:()=>modal.value=true},'检查订单弹窗')]),
 h(DashboardFixture),h(OrderModal,{visible:modal.value,orderData:{...order,orderTypeCode:'test',params:{}},onClose:()=>modal.value=false})])}}).use(createPinia()).use(createRouter({history:createWebHashHistory(),routes:[]})).use(ElementPlus).mount('#app')
