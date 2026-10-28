import fs from 'node:fs/promises'
import {createServer} from 'vite'
import path from 'node:path'
const root=process.cwd(),server=await createServer({root,configFile:false,envDir:false,resolve:{alias:{'@':path.join(root,'src')}},server:{middlewareMode:true,hmr:false,ws:false,watch:null},optimizeDeps:{noDiscovery:true,include:[]}})
const {createWorldBackup}=await server.ssrLoadModule('/src/utils/worldBackup.ts')
const {worldMarkdown}=await server.ssrLoadModule('/src/utils/worldTransfer.ts')
const date='2026-10-26T00:00:00Z',image='http://127.0.0.1:5195/.tmp/world-backup-check/original.png'
const id=i=>`d0000000-0000-4000-8000-${String(i).padStart(12,'0')}`
const cards=Array.from({length:60},(_,i)=>({id:id(i),projectId:'fixture',title:`整包验收 ${String(i+1).padStart(2,'0')}`,type:i===0?'event':i===1?'location':'character',subType:null,createdAt:date,updatedAt:date,description:`完整备份第${i+1}条，引用 {CARD:${id((i+1)%60)}}。\n`+'这是用于大文件完整恢复的中文内容。'.repeat(250),content:i===0?JSON.stringify({locationId:id(1),outcome:'抵达'}):'{}',coverImage:i===0?JSON.stringify([image]):null,galleryImages:i<2?[image]:[],aliases:['备份检查'],attributes:[{key:'序号',value:String(i+1),type:'number'}],tags:['整包备份验收'],embeddedCards:[id((i+1)%60)],contentBlocks:[],timelineEvents:[],outRelations:[{targetCardId:id((i+1)%60),relationType:'下一站'}],inRelations:[]}))
const project={id:'fixture',name:'完整备份验收 · 60条含原图',description:'完整备份恢复验收；包含六十个条目、中文、环形引用及包内原图。',isPublic:true,ownerId:'never-restore-owner',ownerName:'验收',createdAt:date,updatedAt:date,cardCount:60}
const result=await createWorldBackup(project,cards,()=>{},{loadImage:async()=>new Uint8Array(await fs.readFile('.tmp/world-backup-check/original.png'))})
await fs.writeFile('.tmp/world-backup-check/full-backup.zip',result.bytes)
await fs.writeFile('.tmp/world-backup-check/large.md',worldMarkdown(project,cards))
await fs.writeFile('.tmp/world-backup-check/source.json',JSON.stringify({project,cards}))
console.log(JSON.stringify({zipBytes:result.bytes.length,mdBytes:Buffer.byteLength(worldMarkdown(project,cards)),cards:cards.length,images:result.imageCount}))
await server.close()
