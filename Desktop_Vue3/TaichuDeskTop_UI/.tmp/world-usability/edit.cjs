const fs=require('fs');const base='src/views/世界观管理/';const read=p=>fs.readFileSync(base+p,'utf8').replace(/\r\n/g,'\n');const write=(p,s)=>fs.writeFileSync(base+p,s);
let s=read('ProjectCreate.vue');s=s.replace(/            <!-- 世界类型[\s\S]*?(?=            <!-- 提交按钮 -->)/,'');
// Remove fields that were only shown in the preview and never sent to the API.
const start=s.indexOf('            <!-- 世界'); const end=s.indexOf('            <!-- 提交按钮 -->');
if(start>=0&&end>start)s=s.slice(0,start)+s.slice(end);
s=s.replace('        <ProjectCreatePreview :form="form" />','').replace("import ProjectCreatePreview from './世界观组件/ProjectCreatePreview.vue';\n",'').replace("const tagInput = ref('');\n",'').replace("  type: '',\n  tags: [] as string[],\n",'').replace(/const worldTypes = .*\n/,'').replace(/\/\/ 添加标签[\s\S]*?(?=\/\/ ===== 🆕 跳转)/,'');
s=s.replace('项目名称','世界名称').replace('🚀 创建世界','创建世界').replace("await store.createProject({","const project = await store.createProject({").replace("router.push('/world');\n  } catch", "router.push(`/world/project/${project.id}`);\n  } catch");
s=s.replace('              <input\n                id="name"','              <input\n                required\n                id="name"');
s=s.replace('                <div\n                  class="visibility-option"','                <button type="button"\n                  class="visibility-option"').replace('                </div>\n                <div\n                  class="visibility-option"','                </button>\n                <button type="button"\n                  class="visibility-option"');
// Both visibility options contain their own inner div; replace only outer closes.
s=s.replace(/(                  <\/div>\n)                <\/div>/g,'$1                </button>');
s=s.replace(':class="{ active: !form.isPublic }"',':class="{ active: !form.isPublic }"\n                  :aria-pressed="!form.isPublic"').replace(':class="{ active: form.isPublic }"',':class="{ active: form.isPublic }"\n                  :aria-pressed="form.isPublic"');
// Replace unused preview/type/tag layout CSS with a focused single-column form.
s=s.slice(0,s.indexOf('<style scoped>'))+`<style scoped>
.project-create { padding: clamp(20px, 4vw, 44px); color: var(--text-primary); }
.container { max-width: 720px; margin: auto; }
button, input, textarea { font: inherit; }
button { cursor: pointer; }button:disabled { opacity: .55; cursor: not-allowed; }
.back-btn { display: flex; align-items: center; gap: 8px; border: 0; background: none; color: var(--text-secondary); padding: 0; }
.page-header { margin: 28px 0; }.page-header h1 { margin: 0 0 12px; font-size: 30px; }.page-header p { color: var(--text-secondary); line-height: 1.7; }
.form-section { margin-top: 24px; padding: clamp(20px, 4vw, 36px); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 16px; }
.field-group { margin-bottom: 28px; }.field-group>label { display: block; margin-bottom: 10px; font-weight: 600; }
.required { color: var(--danger); }input, textarea { box-sizing: border-box; width: 100%; padding: 12px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); line-height: 1.7; }textarea { resize: vertical; }
.counter { display: block; margin-top: 6px; text-align: right; font-size: 12px; color: var(--text-secondary); }
.visibility-options { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }.visibility-option { display: flex; gap: 10px; align-items: flex-start; text-align: left; padding: 16px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); color: var(--text-primary); }.visibility-option.active { border-color: var(--accent-color); background: var(--accent-soft); }.visibility-option p { margin: 6px 0 0; font-size: 13px; line-height: 1.6; color: var(--text-secondary); }
.form-actions { display: flex; justify-content: flex-end; gap: 12px; padding-top: 12px; }.btn-primary,.btn-outline { padding: 11px 22px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); }.btn-primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
button:focus-visible,input:focus-visible,textarea:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
@media(max-width: 480px) { .visibility-options { grid-template-columns: 1fr; } }
</style>\n`;
write('ProjectCreate.vue',s);
s=read('世界观组件/ProjectCardNavigation.vue').replace('    <div class="search">','    <button class="overview-btn" :class="{ active: view === \'card\' && !selectedCardId }" @click="$emit(\'overview\')">世界概览</button>\n    <div class="search">').replace('placeholder="搜索..."','placeholder="查找这个世界的设定"\n        aria-label="查找这个世界的设定"').replace('class="toggle-all" @click','class="toggle-all" :disabled="Boolean(searchQuery.trim())" @click').replace('graph: []; timeline: []','graph: []; timeline: []; overview: []');
s=s.replace(/\/\/ 折叠状态[\s\S]*?(?=watch\(\(\) => props.selectedCardId)/,'const collapsed = ref<Set<string>>(new Set())\n\n').replace('return collapsed.value.has(type)','return !searchQuery.value.trim() && collapsed.value.has(type)').replace('没有匹配的卡片','没有匹配的设定').replace('还没有卡片','还没有设定');
s=s.replace('.btn-icon {','.overview-btn { margin: 12px 12px 0; padding: 12px; text-align: left; border: 0; border-radius: 8px; background: transparent; color: var(--text-primary); cursor: pointer; font: inherit; }\n.overview-btn.active { background: var(--accent-soft); color: var(--accent-color); }\n.toggle-all:disabled { opacity: .5; cursor: default; }\n.btn-icon {').replace('height: 200px;','height: 290px;');write('世界观组件/ProjectCardNavigation.vue',s);
s=read('世界观组件/CardEditor.vue').replace('✨ 新建卡片','新建条目').replace('placeholder="卡片名称"','aria-label="条目名称"\n      placeholder="条目名称"').replace('    <!-- 类型胶囊 -->','    <p class="editor-hint">先写名称和描述即可保存。图片、属性、时间线和关联都可以以后再补充。</p>\n    <!-- 类型胶囊 -->').replace('描述这个卡片...','写下这个人物、地点或事物的设定…').replace('    <!-- 类型专属属性 -->\n    <div class="doc-section">\n      <h2 class="section-title">{{ currentTypeLabel }}属性</h2>','    <!-- 类型专属属性 -->\n    <details class="doc-section optional-section">\n      <summary>{{ currentTypeLabel }}属性<small>可选</small></summary>').replace('      </DynamicForm>\n    </div>','      </DynamicForm>\n    </details>').replace(':open="Boolean(form.attributes?.length)"',':open="!isCreating && Boolean(form.attributes?.length)"').replace("@import './CardEditor.css';","@import './CardEditor.css';\n.editor-hint { color: var(--text-secondary); font-size: 13px; line-height: 1.7; margin: 12px 0 20px; }");write('世界观组件/CardEditor.vue',s);
