import { createServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'node:path'
import fs from 'node:fs/promises'
const root=process.cwd()
const server=await createServer({root,configFile:false,envDir:false,plugins:[vue()],resolve:{alias:{'@':path.join(root,'src')}},server:{middlewareMode:true,hmr:false,ws:false,watch:null},optimizeDeps:{noDiscovery:true,include:[]}})
const {worldMarkdown}=await server.ssrLoadModule('/src/utils/worldTransfer.ts')
const date='2026-10-25T12:00:00Z'
const card=(id,title,type,extra={})=>({id,projectId:'fixture',title,type,createdAt:date,updatedAt:date,subType:null,description:'',content:'{}',coverImage:null,aliases:[],attributes:[],tags:[],embeddedCards:[],contentBlocks:[],timelineEvents:[],galleryImages:[],outRelations:[],inRelations:[],...extra})
const a='11111111-1111-4111-8111-111111111111',b='22222222-2222-4222-8222-222222222222'
const cards=[card(a,'往返验收 · 行者','character',{subType:'测试子类型',aliases:['灯塔旅人'],description:'## 原始正文标题\n行者来自 {CARD:'+b+'}。\n\n```md\n## 代码中的标题\n```\n末尾保留空格  ',content:JSON.stringify({age:20,enabled:false,notes:'类型数据'}),coverImage:'["http://127.0.0.1:5195/.tmp/world-transfer-check/landscape.png"]',galleryImages:['http://127.0.0.1:5195/.tmp/world-transfer-check/portrait.png'],attributes:[{key:'身高',value:'170',type:'number'}],tags:['往返验收'],embeddedCards:[b],contentBlocks:[{id:'33333333-3333-4333-8333-333333333333',cardId:b,cardType:'location',order:0,contextLabel:'常住地'}],timelineEvents:[{date:'100年',era:'雾海纪元',title:'启程',description:'从港口出发'}],outRelations:[{id:'r',sourceCardId:a,targetCardId:b,relationType:'居住于'}]}),card(b,'往返验收 · 港城','location',{description:'迎接 {CARD:'+a+'}。',content:'{"dangerLevel":"低"}'})]
const project={id:'fixture',name:'MD 往返验收',description:'世界简介不应变成额外条目',createdAt:date,updatedAt:date,isPublic:false,ownerId:'fixture',ownerName:'验收',cardCount:2}
await fs.writeFile('.tmp/world-roundtrip/source.json',JSON.stringify({project,cards},null,2))
await fs.writeFile('.tmp/world-roundtrip/roundtrip.md',worldMarkdown(project,cards))
await server.close()
