import {createApp,h,ref} from 'vue'
import '../../src/assets/styles/base.css'
import '../../src/assets/styles/variables.css'
import {setupTheme,useTheme} from '../../src/composables/useTheme'
import request from '../../src/utils/request'
import Documents from '../../src/views/太初协作/协作组件/ProjectDocument.vue'
import Task from '../../src/views/太初协作/协作组件/ProjectTaskDetail.vue'
const block=(type:string,text:string)=>({id:type,type,data:JSON.stringify({type,...(type==='heading'?{attrs:{level:2}}:{}),content:[{type:'text',text}]})})
request.get=async(url:string)=>{
 if(url.endsWith('/documents'))return [{id:'doc',title:'协作说明与交付记录',pinnedByUserName:'示例成员',pinnedAt:'2026-10-21'}] as any
 if(url.endsWith('/documents/doc'))return {title:'协作说明与交付记录',blocks:[block('heading','本周协作目标'),block('paragraph','这是隔离预览内容，仅检查真实组件的排版。文档成员权限和撤回行为另由后端回归测试验证。'),block('blockquote','成员可阅读共享内容；作者编辑后，正文同步更新。')]} as any
 if(url.endsWith('/my-permissions'))return {isOwner:true,permissions:[]} as any
 throw Error('隔离预览中没有该数据')
}
for(const method of ['post','put','patch','delete'] as const)request[method]=async()=>{throw Error('隔离预览禁止写入')}
setupTheme()
createApp({setup(){const {mode,setTheme}=useTheme(),open=ref(false);return()=>h('main',{style:'max-width:1120px;margin:auto;padding:24px'},[
 h('header',{style:'display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:24px'},[h('strong','协作页面 · 隔离预览'),h('select',{'aria-label':'验收主题',value:mode.value,onChange:(e:any)=>setTheme(e.target.value)},[h('option',{value:'light'},'白天'),h('option',{value:'dark'},'夜间')]),h('button',{onClick:()=>open.value=true},'检查任务编辑')]),
 h(Documents,{projectId:'fixture',initialData:{name:'示例协作项目'}}),h(Task,{isOpen:open.value,projectId:'fixture',task:{id:'test',title:'检查任务长标题在手机宽度下的完整显示',description:'检查任务表单分组、滚动、固定保存按钮与日夜颜色。',categoryId:null},originalCategoryId:null,boardCategories:[],projectMembers:[],onClose:()=>open.value=false})])}}).mount('#app')
