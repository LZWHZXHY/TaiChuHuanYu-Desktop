import axios from 'axios';
import {createApp} from 'vue';
import {createPinia} from 'pinia';
import {createRouter,createMemoryHistory} from 'vue-router';
import ElementPlus from 'element-plus';
import 'element-plus/dist/index.css';
import '/src/assets/styles/base.css';
import '/src/assets/styles/content.css';
import '/src/assets/styles/variables.css';
import '/src/assets/styles/theme-rhine.css';
axios.defaults.adapter = async config => {
 if(config.method !== 'get') throw new Error('隔离检查禁止写入');
 let data=[];
 if(config.url === '/Admin/Data/Metrics') data={totalUsers:{value:255,trendValue:12,isUp:true},mau:{value:120,trendPercent:8,isUp:true},wau:{value:48,trendPercent:5,isUp:true},dau:{value:23,trendPercent:12,isUp:true}};
 else if(config.url === '/DataPanel/overview') data={userCount:255,workCount:352,blogCount:9,noteCount:20};
 else if(config.url === '/news') data={data:[{id:'fixture-news',title:'界面检查：社区创作周',content:'本地示例，用于检查列表与编辑布局。',type:'公告',noticeKind:'persistent',importance:'normal',isPublished:true,createdAt:'2026-10-27T08:00:00Z'}]};
 else if(/users/i.test(config.url)) data={items:[],totalCount:0};
 return {data,status:200,statusText:'OK',headers:{},config};
};
const {useUserStore}=await import('/src/stores/user.ts');
const {default:Admin}=await import('/src/views/管理面板/index.vue');
const pinia=createPinia();
// 只给隔离页面的内存 Store 示例权限，不调用登录/持久化，不改变真实账号。
useUserStore(pinia).$patch({token:'fixture-only',userInfo:{id:'fixture',username:'示例管理员',permissions:['SuperAdmin']}});
createApp(Admin).use(pinia).use(createRouter({history:createMemoryHistory(),routes:[{path:'/',component:{template:'<div></div>'}}]})).use(ElementPlus).mount('#app');
document.querySelector('select').addEventListener('change',e=>document.documentElement.dataset.theme=e.target.value);
const style=document.createElement('style'); style.textContent='body{margin:0;padding:24px;background:var(--bg-main);color:var(--text-primary)}.fixture-note{display:flex;justify-content:space-between;gap:12px;margin-bottom:24px;color:var(--text-secondary);font-size:12px}.fixture-note select{background:var(--paper-card);color:var(--text-primary)}';document.head.append(style);
