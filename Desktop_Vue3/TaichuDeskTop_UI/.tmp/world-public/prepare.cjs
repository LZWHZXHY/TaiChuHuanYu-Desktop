const fs = require('fs'), path = require('path');
const backend = 'E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2';
const front = ['src/router/index.ts','src/views/世界观管理/index.vue','src/views/世界观管理/ProjectDetail.vue','src/views/世界观管理/世界观组件/CardDetail.vue','src/views/世界观管理/世界观组件/ProjectHeader.vue','src/stores/world.ts','src/api/worldApi.ts','tests/world-components.test.mjs','tests/world.test.mjs','tests/backend-world/ContractChecks.cs','tests/backend-world/QueryChecks.cs'];
const back = ['Controllers/World/WorldControllerBase.cs','Controllers/World/WorldProjectsController.cs','Controllers/World/WorldCardsController.cs','Controllers/World/WorldRelationsController.cs','Controllers/World/WorldSearchController.cs','Services/World/WorldCardService.cs','Services/World/IWorldCardService.cs'];
const files = [...front, ...back.map(p => path.join(backend,p))];
fs.writeFileSync('.tmp/world-public/baseline.json',JSON.stringify(Object.fromEntries(files.map(p=>[p,fs.readFileSync(p,'utf8')]))));
for (const p of back) fs.copyFileSync(path.join(backend,p),path.join('.tmp/world-public/backend',path.basename(p)));
