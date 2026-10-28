const fs=require('fs');const base='src/views/世界观管理/';let s=fs.readFileSync(base+'ProjectDetail.vue','utf8').replace(/\r\n/g,'\n');
s=s.replace('@select="selectCard" @graph="switchToGraph" @timeline="switchToTimeline"','@select="selectCard" @overview="showOverview" @graph="switchToGraph" @timeline="switchToTimeline"');
s=s.replace('      <main class="main">','      <main class="main" :aria-busy="store.loadingCards || detailLoading">\n        <div v-if="projectError" class="load-state" role="alert">世界观加载失败，请重试。<button class="btn" @click="loadData">重新加载</button></div>');
s=s.replace('            <nav v-if="historyCrumbs.length > 1"','            <button class="crumb" @click="showOverview">世界概览</button>\n            <button v-if="isOwner && selectedCardDetail && !editing" class="edit-entry" @click="editing = true">编辑条目</button>\n            <nav v-if="historyCrumbs.length > 1"');
s=s.replace('v-if="isOwner && (isCreating || selectedCard)"','v-if="isOwner && (isCreating || editing)"').replace('@click="closePanel">← 返回','@click="leaveEditor">← {{ isCreating ? \'返回概览\' : \'返回阅读\' }}').replace("'新建卡片'","'新建条目'").replace('              :project-id="projectId"\n              :card-data','              :key="selectedCardId || \'new\'"\n              :project-id="projectId"\n              :card-data');
s=s.replace('        <div v-else-if="selectedCardDetail"','        <div v-else-if="detailLoading" class="load-state" role="status">正在加载条目…</div>\n        <div v-else-if="detailError" class="load-state" role="alert">条目加载失败。<button class="btn" @click="selectedCardId && selectCard(selectedCardId)">重新加载</button></div>\n        <div v-else-if="selectedCardDetail"').replace('<aside class="doc-side-panel">','<aside v-if="outRelations.length || inRelations.length" class="doc-side-panel">').replace('            <p v-if="!outRelations.length && !inRelations.length" class="panel-empty">暂无关联</p>\n','');
const start=s.indexOf('        <!-- 空状态 -->');const end=s.indexOf('\n        </div>\n      </main>',start);
if(start<0||end<0)throw Error('overview anchors');
s=s.slice(0,start)+`        <section v-else-if="project && !projectError" class="world-overview">
          <span class="overview-eyebrow">世界概览</span>
          <h2>{{ project.name }}</h2>
          <p class="overview-intro">{{ project.description || '这个世界还没有简介，先从下面的设定开始了解吧。' }}</p>
          <p class="overview-meta">{{ project.ownerName || '创作者' }} · {{ cards.length }} 个条目 · {{ project.isPublic ? '公开世界' : '仅自己可见' }}</p>
          <div v-if="!cards.length" class="overview-start">
            <h3>{{ isOwner ? '先写下一个设定' : '作者还在构思这个世界' }}</h3>
            <p>{{ isOwner ? '一个人物、一处地点，或者故事背景都可以。不必一次填完所有信息。' : '有了公开的条目，就会显示在这里。' }}</p>
            <button v-if="isOwner" class="btn-primary" @click="openCreateDialog">创建第一个条目</button>
          </div>
          <section v-for="group in overviewGroups" :key="group.type" class="overview-group">
            <h3>{{ group.label }} <span>{{ group.cards.length }}</span></h3>
            <div class="overview-entries">
              <button v-for="card in group.cards" :key="card.id" @click="selectCard(card.id)">
                <img v-if="parseCoverImage(card.coverImage)" :src="parseCoverImage(card.coverImage)" alt="" loading="lazy" />
                <span v-else class="entry-initial" aria-hidden="true">{{ card.title.slice(0, 1) }}</span>
                <span>{{ card.title }}</span><span class="entry-arrow" aria-hidden="true">↗</span>
              </button>
            </div>
          </section>
        </section>`+s.slice(end);
s=s.replace("import { renderMarkdown }", "import { groupWorldCards, parseCoverImage } from '@/utils/worldPresentation'\nimport { renderMarkdown }");
if(!s.includes("import { groupWorldCards"))throw Error('import anchor');
s=s.replace("let selectionVersion = 0", "let selectionVersion = 0\nconst editing = ref(false)\nconst detailLoading = ref(false)\nconst detailError = ref(false)\nconst projectError = ref(false)\nconst overviewGroups = computed(() => groupWorldCards(cards.value))");
s=s.replace('  if (!navigatingFromHistory) {','  if (!navigatingFromHistory && selectedCardId.value !== id) {');
s=s.replace("  const version = ++selectionVersion\n  view.value", "  const version = ++selectionVersion\n  editing.value = false\n  detailLoading.value = true\n  detailError.value = false\n  view.value");
s=s.replace("    if (version === selectionVersion) ElMessage.error('加载卡片失败')\n  }", "    if (version === selectionVersion) detailError.value = true\n  } finally { if (version === selectionVersion) detailLoading.value = false }");
s=s.replace('function closePanel() {\n  selectionVersion++','function closePanel() {\n  selectionVersion++\n  editing.value = false\n  detailError.value = false\n  detailLoading.value = false');
s=s.replace('function openCreateDialog() {',"function showOverview() { closePanel(); view.value = 'card' }\nfunction leaveEditor() {\n  if (isCreating.value) showOverview()\n  else editing.value = false\n}\nfunction openCreateDialog() {");
s=s.replace("  try { await store.fetchCards(projectId.value) } catch (error) { console.error('加载世界观失败:', error) }", "  const id = projectId.value\n  projectError.value = false\n  try { await store.fetchCards(id) }\n  catch { if (id === projectId.value) projectError.value = true }");
s=s.replace("function onCardChanged() {\n  closePanel()\n  void loadData()\n  graphRef.value?.refresh()\n}","async function onCardChanged() {\n  const savedId = isCreating.value ? store.currentCard?.id : selectedCardId.value\n  closePanel()\n  await loadData()\n  if (savedId && cards.value.some(card => card.id === savedId)) await selectCard(savedId)\n  graphRef.value?.refresh()\n}");
s=s.replace('<style scoped>',`<style scoped>
.world-overview { max-width: 1100px; padding: clamp(24px, 4vw, 56px); margin: 0 auto; }
.overview-eyebrow { color: var(--accent-color); font-size: 13px; }.world-overview h2 { font-size: clamp(28px, 4vw, 40px); margin: 14px 0 20px; overflow-wrap: anywhere; }
.overview-intro { max-width: 70ch; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.9; font-size: 16px; }.overview-meta { color: var(--text-secondary); font-size: 13px; margin: 24px 0 36px; }
.overview-start { padding: 28px; border: 1px dashed var(--border-line); border-radius: 12px; }.overview-start p { color: var(--text-secondary); line-height: 1.8; }.overview-start button { margin-top: 12px; }
.overview-group { margin-top: 32px; }.overview-group h3 { display: flex; gap: 10px; align-items: baseline; font-size: 18px; }.overview-group h3 span { font-size: 12px; color: var(--text-secondary); font-weight: 400; }
.overview-entries { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 220px), 1fr)); gap: 12px; }.overview-entries button { display: flex; align-items: center; gap: 12px; padding: 14px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--paper-card); color: var(--text-primary); text-align: left; font: inherit; cursor: pointer; overflow-wrap: anywhere; }.overview-entries button:hover { border-color: var(--accent-color); }.overview-entries img,.entry-initial { width: 44px; height: 44px; flex-shrink: 0; border-radius: 8px; object-fit: cover; }.entry-initial { display: grid; place-items: center; background: var(--accent-soft); color: var(--accent-color); }.entry-arrow { margin-left: auto; color: var(--text-secondary); }
.edit-entry { margin-left: auto; order: 2; white-space: nowrap; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--accent-color); cursor: pointer; }.load-state { padding: 36px; color: var(--text-secondary); display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }.load-state button { border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); padding: 8px 14px; cursor: pointer; }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
`);fs.writeFileSync(base+'ProjectDetail.vue',s);
