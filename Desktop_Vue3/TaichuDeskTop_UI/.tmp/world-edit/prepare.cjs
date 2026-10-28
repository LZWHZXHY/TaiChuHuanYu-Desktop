const fs=require('fs'),path=require('path');
const backend='E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2';
const front=['src/stores/world.ts','src/api/types/world.ts','src/composables/世界观管理/useCardEditor.ts','src/views/世界观管理/ProjectDetail.vue','src/views/世界观管理/世界观组件/CardEditor.vue','src/views/世界观管理/世界观组件/CardRefTextarea.vue','src/views/世界观管理/世界观组件/ContentBlockManager.vue','src/views/世界观管理/世界观组件/RelationSelector.vue','tests/world.test.mjs','tests/world-components.test.mjs'];
const back=['Dtos/World/CreateCardDto.cs','Dtos/World/UpdateCardDto.cs','Services/World/WorldCardService.cs'];
fs.writeFileSync('.tmp/world-edit/baseline.json',JSON.stringify(Object.fromEntries([...front,...back.map(x=>path.join(backend,x))].map(f=>[f,fs.readFileSync(f,'utf8')]))));
for(const f of back)fs.copyFileSync(path.join(backend,f),path.join('.tmp/world-edit/backend',path.basename(f)));
