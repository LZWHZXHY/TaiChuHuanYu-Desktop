const fs=require('node:fs');
const files=['src/views/世界观管理/世界观组件/CardRefTextarea.vue','src/views/世界观管理/世界观组件/CardEditor.vue','src/views/世界观管理/世界观组件/ContentBlockManager.vue','src/composables/世界观管理/useCardEditor.ts','src/views/世界观管理/ProjectDetail.vue','tests/world-components.test.mjs'];
fs.writeFileSync('.tmp/world-editor-labels/baseline.json',JSON.stringify(Object.fromEntries(files.map(f=>[f,fs.readFileSync(f,'utf8')]))));
