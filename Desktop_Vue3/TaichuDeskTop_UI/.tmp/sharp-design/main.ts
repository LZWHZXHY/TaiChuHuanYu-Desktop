import {createApp} from 'vue'
import {createPinia,defineStore} from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import router from '../../src/router'
import request from '../../src/utils/request'
import Lab from './Lab.vue'
import '../../src/assets/styles/base.css'
import '../../src/assets/styles/content.css'
import '../../src/assets/styles/variables.css'
import '../../src/assets/styles/theme-rhine.css'
import '../../src/views/活动中心/theme.css'
import '../../src/views/柴圈板块/detail.css'
const pinia=createPinia()
const profile={id:'visual-fixture',username:'样例用户',permissions:['SuperAdmin']}
// 仅此隔离页面的内存 Store，不写 token / localStorage，不更改真实账户权限。
defineStore('user',{state:()=>({token:'fixture-only',userInfo:profile,userId:profile.id,isLoggedIn:true,permissions:profile.permissions,canManageSurvey:true}),actions:{async fetchUserInfo(){return profile}}})(pinia)
const date='2026-10-27T12:00:00Z'
const cover='/taichu-logo.png'
const world=[{id:'1',name:'群星之间',description:'一个关于探索与相遇的世界。人物、地点与组织，在这里形成彼此的联系。',isPublic:true,coverUrl:cover,createdAt:date,updatedAt:date,ownerId:profile.id,cardCount:12},{id:'2',name:'未命名的远方',description:'从一个简单的设定开始。',isPublic:false,createdAt:date,updatedAt:date,ownerId:profile.id,cardCount:3}]
const projects=[{id:'1',name:'社区创作计划',description:'把想象变成看得见的作品。',isPublic:true,joinPolicy:1,myRole:0,memberCount:5,taskCount:3,createdAt:date,status:0}]
const tasks=[{id:'t1',title:'整理人物设定',description:'梳理人物动机、经历与关系。',categoryName:'设定',assigneeId:profile.id,dueDate:date,status:0},{id:'t2',title:'绘制场景草图',description:'留下下一次探索的线索。',categoryName:'美术',dueDate:date,status:1}]
request.get=async (url:string,config:any)=>{
 if(url==='/Sponsor')return {totalAmount:1200,totalCount:3,sponsors:['觉醒者','大超凡者','君者'].map((tier,i)=>({id:i+1,name:['同行者','星辰记录者','远行者'][i],tier,amount:100*(i+1),message:'愿每一份想象都能成为真实的世界。',isAnonymous:false,sponsoredAt:date}))} as any
 if(url==='/Sponsor/config')return {hasToken:false,enabled:false} as any
 if(url==='/Release')return [{id:1,version:'2.4.0',title:'整理创作与阅读体验',content:'清晰的阅读层次\n更简洁的创作流程\n保留每一份想象',releasedAt:date,isPublished:true},{id:2,version:'2.3.0',title:'社区内容更新',content:'记录每一次改进。',releasedAt:date,isPublished:true}] as any
 if(url==='/community/honor-wall')return {list:Array.from({length:8},(_,i)=>({id:i+1,name:'社区共建者 '+(i+1),totalExp:3200-i*270,totalPoints:240-i*16,totalHours:20-i})),stats:{totalExp:20000,totalPoints:1600}} as any
 if(/^\/world\/projects(\/public)?$/.test(url))return world as any
 if(url==='/world/quota')return {maxProjects:10,currentProjects:2,maxCardsPerProject:200,maxTotalCards:500,currentTotalCards:15} as any
 if(url==='/world/card-types')return [] as any
 if(url.toLowerCase()==='/project/my'||url.toLowerCase()==='/project/public')return projects as any
 if(url.toLowerCase().endsWith('/tasks'))return tasks as any
 if(url==='/trade/status')return {experience:2500,level:5,maxSpaces:10,usedSpaces:3,maxNotes:100,usedNotes:20,maxProjectCount:5,usedProjectCount:1} as any
 if(url==='/trade/items')return [{id:1,name:'创作空间',description:'为现有创作增加空间配额。',benefit:'增加可用空间',category:'Quota',baseCost:300,priceMultiplier:1,baseWeight:1,delivery:'None',purchaseCount:0}] as any
 if(url==='/Admin/Data/Metrics')return Object.fromEntries(['totalUsers','mau','wau','dau'].map((k,i)=>[k,{value:1600/(i+1),isUp:true,trendValue:12,trendPercent:8}])) as any
 if(url==='/DataPanel/overview')return null as any
 if(url==='/Users/Settings')return {receiveUpdateEmail:true,receiveActivityEmail:false,weeklyReport:true} as any
 if(url==='/Artwork')return {data:[1,2,3].map(id=>({id,title:'社区作品 · '+id,coverImageUrl:location.origin+cover,authorName:'样例作者',uploadAt:date,imageCount:1,viewCount:12,likesCount:id*3,favoritesCount:id,commentsCount:2,isLiked:false,isFavorited:false})),total:3,hasMore:false} as any
 if(url==='/knowledge-base/catalog')return {items:[{id:'1',type:'blog',title:'从一个想法开始',summary:'在这里阅读、讨论和引用社区的公开资料。',author:'样例作者',tags:'创作,资料',updatedAt:date},{id:'2',type:'world',title:'群星之间',summary:'人物、地点和组织的公开设定。',author:'样例作者',tags:'世界观',updatedAt:date}],total:2,page:1,pageSize:20} as any
 if(url.includes('/categories')||url.includes('/Categories')||url.startsWith('/activities')||url.startsWith('/survey/list')||url.includes('/comments'))return [] as any
 throw new Error('尚未配置此接口的视觉样例：'+url)
}
for(const method of ['post','put','patch','delete'] as const) request[method]=async()=>{throw new Error('隔离检查禁止写入服务器')}
createApp(Lab).use(pinia).use(router).use(ElementPlus).mount('#app')
