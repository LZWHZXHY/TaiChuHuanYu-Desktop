import fs from 'node:fs';
const edit=(p,fn)=>fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')));
const base='src/views/太初协作/';
edit(base+'index.vue',s=>s.replaceAll('活跃灵脉负载','活跃项目').replaceAll('我的灵脉','我的项目').replaceAll('活跃灵脉已满','项目额度已满').replaceAll('灵脉负载已满','项目额度已满').replaceAll('开启新灵脉','创建协作项目').replaceAll('定义一段新的时空协作','填写目标与计划，开始协作').replace('getRoleLabel(project.roleId)','getRoleLabel(project)').replace("{{ viewMode === 'mine' ? getRoleLabel(project.roleId) : 'OWNER' }}",'项目负责人').replace(/const getRoleLabel = \(role: number\) => \{[\s\S]*?\n};/,`const getRoleLabel = (project: any) => {
  if (project.ownerId?.toLowerCase() === userStore.userId?.toLowerCase()) return '负责人';
  return project.roleIds?.includes('role_system_viewer') ? '观察者' : '协作成员';
};`));
for(const file of ['协作组件/ProjectKanban.vue','协作组件/ProjectTaskDetail.vue'])edit(base+file,s=>s.replaceAll('游离意图 (未分类)','未分类任务').replaceAll('注入游离意图','新建任务').replaceAll('注入意图','新建任务').replaceAll('铸造新分栏维度','新建任务分组').replaceAll('命名此维度...','输入分组名称...').replaceAll('所属维度 (分类)','任务分组').replaceAll('任务核心意图...','任务标题...').replaceAll('深层描绘 (Description)','任务描述').replaceAll('在此展开意图的细节...','填写任务要求与验收标准...').replaceAll('抹除此意图','删除任务').replaceAll('确立修改','保存修改').replaceAll('正在同步灵脉...','正在保存...'));
edit(base+'协作组件/ProjectTaskDetail.vue',s=>s.replace('class="task-detail-drawer"','class="task-detail-drawer" role="dialog" aria-modal="true" aria-label="任务详情"').replace('class="huge-title-input"','class="huge-title-input" aria-label="任务标题"').replace('v-model="localTask.description"','aria-label="任务描述" v-model="localTask.description"').replace('const closeDrawer = () => emit(\'close\');',"const closeDrawer = () => { if (!isSaving.value) emit('close'); };").replace('const handleSave = async () => {\n  isSaving.value = true;',"const handleSave = async () => {\n  if (isSaving.value) return;\n  if (!localTask.value.title?.trim()) return alert('请填写任务标题');\n  if (localTask.value.startDate && localTask.value.dueDate && localTask.value.startDate > localTask.value.dueDate) return alert('截止时间不能早于开始时间');\n  isSaving.value = true;").replace("    emit('refresh');\n    closeDrawer();", "    emit('refresh');\n    emit('close');").replace('class="save-btn" @click="handleSave"','class="save-btn" :disabled="isSaving" @click="handleSave"').replace('class="close-btn" aria-label=', 'class="close-btn" :disabled="isSaving" aria-label=').replace('class="drawer-content"','class="drawer-content" :inert="isSaving"'));
edit(base+'协作组件/ProjectDocument.vue',s=>{
 s=s.replace('项目长卷大纲','项目文档').replaceAll('未命名长卷','未命名文档').replaceAll('暂无归档长卷','暂无项目文档').replace('编织者：','分享者：').replace('固化于：','分享时间：').replace('太初共建长卷已就绪','项目文档').replace('请在左侧大纲中点选项目文档，开启沉浸协同研读空间。','选择文档开始阅读；在灵脉中将笔记分享至项目后，会显示在这里。');
 s=s.replace('<div \n              v-for="doc', '<button type="button"\n              v-for="doc').replace(':class="{ active: selectedDocId === doc.id }"',':class="{ active: selectedDocId === doc.id }" :aria-pressed="selectedDocId === doc.id"').replace('            </div>\n\n            <div v-if="documentList', '            </button>\n\n            <div v-if="documentList');
 s=s.replace('<div v-if="isLoading" class="doc-state-loading">','<div v-if="listError || detailError" class="doc-state-loading" role="alert">\n            <p>{{ listError || detailError }}</p>\n            <button @click="listError ? fetchProjectDocuments() : handleSelectDocument(selectedDocId!)">重新加载</button>\n          </div>\n          <div v-else-if="isLoading || detailLoading" class="doc-state-loading">');
 s=s.replace('<p v-if="block.type === \'paragraph\'" v-html="parseBlockContent(block.data)"></p>','<SpiritPreview v-if="block.type !== \'image\'" :model-value="block.data" />');
 s=s.replace("import { ref, watch, onMounted } from 'vue';", "import { ref, watch, onUnmounted } from 'vue';\nimport SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';");
 s=s.replace("const emit = defineEmits(['updated']);",'const listError = ref(\'\'), detailError = ref(\'\'), detailLoading = ref(false);\nlet listVersion = 0, detailVersion = 0;');
 const start=s.indexOf('const fetchProjectDocuments'),end=s.indexOf('const parseImageData',start);
 s=s.slice(0,start)+`const fetchProjectDocuments = async () => {
  if (!props.projectId) return;
  const version = ++listVersion;
  isLoading.value = true; listError.value = '';
  try {
    const result: any = await request.get(\`/Project/\${props.projectId}/documents\`);
    if (version === listVersion) documentList.value = Array.isArray(result) ? result : result?.data ?? [];
  } catch { if (version === listVersion) listError.value = '项目文档加载失败，请重试'; }
  finally { if (version === listVersion) isLoading.value = false; }
};

const handleSelectDocument = async (noteId: string) => {
  const version = ++detailVersion;
  selectedDocId.value = noteId; currentDocDetail.value = null;
  detailLoading.value = true; detailError.value = '';
  try {
    const result: any = await request.get(\`/Project/\${props.projectId}/documents/\${noteId}\`);
    if (version !== detailVersion) return;
    const metadata = documentList.value.find(doc => doc.id === noteId);
    currentDocDetail.value = { ...(result?.data ?? result), pinnedByUserName: metadata?.pinnedByUserName, pinnedAt: metadata?.pinnedAt };
  } catch { if (version === detailVersion) detailError.value = '文档读取失败或已取消分享，请重试'; }
  finally { if (version === detailVersion) detailLoading.value = false; }
};

`+s.slice(end);
 s=s.replace("  selectedDocId.value = null;", "  detailVersion++; detailLoading.value = false; detailError.value = ''; documentList.value = [];\n  selectedDocId.value = null;").replace('onMounted(fetchProjectDocuments);','onUnmounted(() => { listVersion++; detailVersion++; });');
 return s.slice(0,s.indexOf('<style scoped>'))+`<style scoped>
.project-document-root { color: var(--text-primary); min-width: 0; }
.doc-workspace-layout { display: grid; grid-template-columns: 240px minmax(0, 1fr); gap: 24px; align-items: start; }
.doc-sidebar, .doc-viewer-content { border: 1px solid var(--border-line); border-radius: var(--radius-lg); background: var(--paper-card); min-width: 0; }
.doc-sidebar { position: sticky; top: 0; padding: 16px; }
.sidebar-header { display: flex; align-items: center; justify-content: space-between; padding: 4px 0 16px; font-weight: 600; }
.doc-count { color: var(--text-secondary); font-size: 13px; }
.doc-list-flat { display: grid; gap: 8px; max-height: 65vh; overflow-y: auto; }
.doc-list-item { display: grid; gap: 8px; padding: 12px; width: 100%; border: 1px solid transparent; border-radius: var(--radius-sm); background: transparent; color: var(--text-primary); text-align: left; cursor: pointer; }
.doc-list-item:hover { background: var(--bg-sub); }
.doc-list-item.active { background: var(--accent-soft); border-color: var(--accent-color); }
.doc-title { display: block; overflow-wrap: anywhere; font-size: 14px; font-weight: 600; }
.doc-item-meta, .reader-author-bar { display: flex; flex-wrap: wrap; gap: 8px; font-size: 12px; color: var(--text-secondary); }
.sidebar-empty { padding: 20px 0; color: var(--text-secondary); font-size: 14px; }
.doc-viewer-content { min-height: 360px; padding: clamp(20px, 3vw, 36px); }
.doc-reader-inner { max-width: 800px; margin-inline: auto; }
.reader-header { border-bottom: 1px solid var(--border-line); padding-bottom: 24px; margin-bottom: 24px; }
.reader-breadcrumbs { display: flex; flex-wrap: wrap; gap: 8px; color: var(--text-secondary); font-size: 13px; }
.reader-title { font-size: clamp(24px, 3vw, 34px); margin: 16px 0; overflow-wrap: anywhere; }
.reader-body { font-size: 16px; line-height: 1.8; }
.rich-paragraph { margin-bottom: 20px; overflow-wrap: anywhere; }
.rich-image-wrapper img { max-width: 100%; height: auto; border-radius: 8px; }
.img-caption { color: var(--text-secondary); text-align: center; font-size: 13px; }
.doc-state-loading, .layout-placeholder-inner { min-height: 280px; display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; gap: 12px; color: var(--text-secondary); }
.doc-state-loading button { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-sub); color: var(--text-primary); }
.placeholder-content p { max-width: 440px; line-height: 1.7; }
.loading-pulse { width: 32px; height: 4px; background: var(--accent-color); }
@media (max-width: 760px) { .doc-workspace-layout { grid-template-columns: minmax(0, 1fr); gap: 16px; } .doc-sidebar { position: static; } .doc-list-flat { max-height: 220px; } }
</style>
`;
});
