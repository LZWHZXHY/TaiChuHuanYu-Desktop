const fs=require('fs');const p='src/views/世界观管理/世界观组件/ProjectHeader.vue';let s=fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');const start=s.indexOf('    <div class="header-actions">');s=s.slice(0,start)+`    <div class="header-actions">
      <button v-if="isOwner" class="create-btn" @click="$emit('create')">＋ 新建条目</button>
      <details class="world-tools">
        <summary>更多操作</summary>
        <div class="tools-menu">
          <button @click="$emit('organize')">AI 整理</button>
          <template v-if="isOwner">
            <button @click="$emit('import')">AI 导入设定</button>
            <button class="visibility-btn" :disabled="toggling" @click="togglePublic">{{ toggling ? '保存中…' : project?.isPublic ? '改为私有世界' : '公开这个世界' }}</button>
          </template>
        </div>
      </details>
    </div>
  </header>
</template>
`+s.slice(s.indexOf('<script setup'));
s=s.replace("import { computed, ref } from 'vue'", "import { computed, ref } from 'vue'\nimport { ElMessage } from 'element-plus'").replace('  } finally {',"  } catch {\n    ElMessage.error('可见性修改失败，请重试')\n  } finally {");
s=s.slice(0,s.indexOf('<style scoped>'))+`<style scoped>
.page-header { display: flex; align-items: flex-start; gap: 24px; padding: 22px 28px; border-bottom: 1px solid var(--border-line); background: var(--paper-card); }
button, summary { font: inherit; cursor: pointer; }
.back-btn { border: 0; background: none; color: var(--text-secondary); padding: 6px 0; white-space: nowrap; }
.header-main { flex: 1; min-width: 0; }h1 { margin: 0 0 8px; font-size: 22px; overflow-wrap: anywhere; }
.header-meta { display: flex; flex-wrap: wrap; gap: 7px; color: var(--text-secondary); font-size: 12px; }.header-desc { display: none; }
.header-actions { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }
.create-btn { padding: 9px 16px; border: 0; border-radius: 8px; background: var(--accent-color); color: var(--on-accent); }
.world-tools { position: relative; }.world-tools summary { padding: 9px 6px; color: var(--text-secondary); font-size: 13px; }
.tools-menu { position: absolute; top: calc(100% + 8px); right: 0; width: 180px; z-index: 30; padding: 6px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--paper-card); box-shadow: 0 10px 30px #0002; }.tools-menu button { display: block; width: 100%; padding: 12px; border: 0; border-radius: 6px; text-align: left; background: none; color: var(--text-primary); }.tools-menu button:hover { background: var(--bg-sub); }.tools-menu button:disabled { opacity: .6; }
button:focus-visible,summary:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
@media(max-width: 700px) { .page-header { flex-wrap: wrap; gap: 14px; padding: 20px; }.header-main { flex-basis: calc(100% - 80px); }.header-actions { width: 100%; justify-content: flex-end; } }
</style>\n`;fs.writeFileSync(p,s);
let c='src/views/世界观管理/ProjectCreate.vue';s=fs.readFileSync(c,'utf8').replaceAll('项目名称','世界名称').replace('      <!-- 双栏布局 -->','      <!-- 创建表单 -->').replace('        <!-- 右侧：预览 -->\n','');fs.writeFileSync(c,s);
const backend='E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2/Filters/AdminAccessFilter.cs';s=fs.readFileSync(backend,'utf8');fs.writeFileSync('.tmp/world-usability/AdminAccessFilter.before.cs',s);s=s.replace('"News" => action.ActionName', '"Migration" => true,\n            "News" => action.ActionName');fs.writeFileSync('.tmp/world-usability/AdminAccessFilter.cs',s);
