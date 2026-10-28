import fs from 'node:fs/promises'
import {createServer} from 'vite'
import path from 'node:path'
const root=process.cwd(),server=await createServer({root,configFile:false,envDir:false,resolve:{alias:{'@':path.join(root,'src')}},server:{middlewareMode:true,hmr:false,ws:false,watch:null},optimizeDeps:{noDiscovery:true,include:[]}})
const {worldMarkdown}=await server.ssrLoadModule('/src/utils/worldTransfer.ts')
const {project,cards}=JSON.parse(await fs.readFile('.tmp/world-roundtrip/source.json','utf8'))
cards[0]={...cards[0],title:'事件往返验收 · 启程',type:'event',content:JSON.stringify({locationId:cards[1].id,outcome:'到达',count:0})}
cards[1]={...cards[1],title:'事件往返验收 · 港城'}
await fs.writeFile('.tmp/world-roundtrip/event.md',worldMarkdown(project,cards));await server.close()
