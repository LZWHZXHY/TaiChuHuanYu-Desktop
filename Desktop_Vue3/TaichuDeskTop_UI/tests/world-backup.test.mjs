import assert from 'node:assert/strict'
import {before,after,test} from 'node:test'
import {createServer} from 'vite'
import {fileURLToPath} from 'node:url'
import {zipSync,unzipSync,strToU8,strFromU8} from 'fflate'
const root=fileURLToPath(new URL('../',import.meta.url))
let server,backup,transfer
const project={id:'p',name:'完整世界',description:'世界简介',isPublic:true,ownerId:'old-owner',ownerName:'旧作者',createdAt:'2026-01-01',updatedAt:'2026-01-01',cardCount:0}
const card=(i,extra={})=>({id:`id-${i}`,projectId:'p',title:`条目${i}`,type:'character',subType:null,description:'正文',content:'{}',coverImage:null,galleryImages:[],attributes:[],aliases:[],tags:[],contentBlocks:[],embeddedCards:[],timelineEvents:[],outRelations:[],inRelations:[],updatedAt:project.updatedAt,createdAt:project.createdAt,...extra})
const png=Uint8Array.from(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a51cAAAAASUVORK5CYII=','base64'))
before(async()=>{
 server=await createServer({root,configFile:false,envDir:false,resolve:{alias:{'@':root+'src'}},server:{middlewareMode:true,hmr:false,ws:false,watch:null},optimizeDeps:{noDiscovery:true,include:[]}})
 backup=await server.ssrLoadModule('/src/utils/worldBackup.ts');transfer=await server.ssrLoadModule('/src/utils/worldTransfer.ts')
})
after(async()=>{await server?.close()})
test('large MD and ordinary text no longer stop at 1 MB or 50 entries',()=>{
 const cards=Array.from({length:65},(_,i)=>card(i,{description:'中文段落'.repeat(4500)}))
 const md=transfer.worldMarkdown(project,cards)
 assert.ok(Buffer.byteLength(md)>1024*1024)
 assert.equal(transfer.parseWorldText(md,'世界.md').length,65)
 assert.equal(transfer.parseWorldText(cards.map(c=>`## ${c.title}\n正文`).join('\n'),'普通.md').length,65)
})
test('ZIP keeps original bytes offline, deduplicates images and restores all fields without old hosts',async()=>{
 const a='https://old.test/cover.png',b='https://old.test/another.png'
 const cards=[card(0,{coverImage:JSON.stringify([a,b]),galleryImages:[a],description:`来自 {CARD:id-1}\n![封面](<${a}>)`,content:JSON.stringify({image:b,count:0}),timelineEvents:[{date:'1',era:null,title:'始',description:`<img src="${b}">`}],outRelations:[{targetCardId:'id-1',relationType:'居住于'}]}),card(1)]
 const {bytes,imageCount}=await backup.createWorldBackup(project,cards,()=>{},{loadImage:async()=>png})
 assert.equal(imageCount,1)
 const restored=await backup.readWorldBackup(bytes)
 assert.deepEqual(restored.project,{name:'完整世界',description:'世界简介'})
 assert.deepEqual([...restored.images.values()][0].bytes,png)
 assert.doesNotMatch(strFromU8(unzipSync(bytes)['world.md']),/old.test/)
 const images=new Map([...restored.images.keys()].map(path=>[path,'https://new.test/image.png']))
 const source=backup.replaceWorldImages(restored.items,images)
 const result=transfer.resolveWorldImport(source,['new-0','new-1'])
 assert.equal(result.payloads[0].description,'来自 {CARD:new-1}\n![封面](<https://new.test/image.png>)')
 assert.deepEqual(JSON.parse(result.payloads[0].coverImage),['https://new.test/image.png','https://new.test/image.png'])
 assert.equal(result.payloads[0].relations[0].targetCardId,'new-1')
 assert.equal(JSON.parse(result.payloads[0].content).count,0)
 assert.equal(result.payloads[0].timelineEvents[0].description,'<img src="https://new.test/image.png">')
})
test('inaccessible/non-image resources cannot silently produce a complete backup',async()=>{
 const cards=[card(0,{coverImage:'https://old.test/a'})]
 await assert.rejects(()=>backup.createWorldBackup(project,cards,()=>{},{loadImage:async()=>{throw new Error('offline')}}),/备份未完成/)
 await assert.rejects(()=>backup.createWorldBackup(project,cards,()=>{},{loadImage:async()=>strToU8('<html>403</html>')}),/图片格式无效/)
})
test('missing, corrupt and traversal archive entries fail validation before any writes',async()=>{
 const {bytes}=await backup.createWorldBackup(project,[card(0,{galleryImages:['https://old.test/a.png']})],()=>{},{loadImage:async()=>png})
 const original=unzipSync(bytes),imageName=Object.keys(original).find(n=>n.startsWith('images/'))
 let files={...original};delete files[imageName]
 await assert.rejects(()=>backup.readWorldBackup(zipSync(files)),/图片缺失/)
 files={...original,[imageName]:new Uint8Array(png)};files[imageName][20]^=1
 await assert.rejects(()=>backup.readWorldBackup(zipSync(files)),/校验失败/)
 files={...original,'../escape.txt':strToU8('unsafe')}
 await assert.rejects(()=>backup.readWorldBackup(zipSync(files)),/文件路径/)
 files={...original,'world.md':strToU8('changed')}
 await assert.rejects(()=>backup.readWorldBackup(zipSync(files)),/正文数据校验失败/)
})
test('empty worlds restore their name and introduction and permissions are never restored',async()=>{
 const {bytes}=await backup.createWorldBackup(project,[])
 const result=await backup.readWorldBackup(bytes)
 assert.equal(result.items.length,0);assert.equal(result.project.name,project.name)
 assert.ok(!('isPublic' in result.project));assert.ok(!('ownerId' in result.project))
})
test('image discovery includes project description, Markdown reference images, HTML and typed JSON',()=>{
 const urls=backup.worldImageUrls({description:'![图][a]\n[a]: https://x.test/a.png'},[{coverImage:'["/b"]',galleryImages:['/c'],description:'<img src="https://x.test/d?x=1&amp;y=2">',content:'{"pic":"https://x.test/e.webp"}'}])
 assert.deepEqual(new Set(urls),new Set(['https://x.test/a.png','/b','/c','https://x.test/d?x=1&y=2','https://x.test/e.webp']))
 assert.equal(backup.replaceWorldImages('{ "unchanged": 1 }',new Map([['/a','/b']])),'{ "unchanged": 1 }')
})

test('declared oversized ZIP contents are rejected before decompression',async()=>{
 const {bytes}=await backup.createWorldBackup(project,[])
 const corrupt=new Uint8Array(bytes),view=new DataView(corrupt.buffer)
 for(let i=0;i<corrupt.length-46;i++) {
  if(view.getUint32(i,true)===0x02014b50){view.setUint32(i+24,backup.worldTextBytes+1,true);break}
 }
 await assert.rejects(()=>backup.readWorldBackup(corrupt),/容量/)
})

test('cancelled exports stop before producing a successful backup',async()=>{
 const abort=new AbortController();abort.abort()
 await assert.rejects(()=>backup.createWorldBackup(project,[],()=>{},{signal:abort.signal}),/abort/i)
})
test('restored CDN originals use a fresh CORS response without changing signed or external URLs',async()=>{
 const previousFetch=globalThis.fetch,previousDocument=globalThis.document;const requested=[];
 globalThis.document={baseURI:'http://localhost:5195/'};
 globalThis.fetch=async(url)=>{requested.push(new URL(url));return new Response(png,{status:200,headers:{'content-length':String(png.length)}})};
 try{
  const urls=['https://img.bianyuzhou.com/world/restores/job/image.png','https://img.bianyuzhou.com/world/restores/job/image.png?q-signature=keep','https://external.test/image.png'];
  await backup.createWorldBackup(project,[card(0,{galleryImages:urls})]);
  assert.ok(requested[0].searchParams.get('world-backup'));
  assert.equal(requested[1].search,'?q-signature=keep');assert.equal(requested[2].search,'');
 }finally{globalThis.fetch=previousFetch;if(previousDocument===undefined)delete globalThis.document;else globalThis.document=previousDocument;}
})
