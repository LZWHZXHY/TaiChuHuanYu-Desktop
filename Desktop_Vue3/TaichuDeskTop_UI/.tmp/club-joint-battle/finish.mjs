import fs from 'node:fs';
const edit=(p,fn)=>fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')));
edit('src/views/柴圈板块/Battle/battle_api.ts',s=>s.replace(/request.post\(`/g,'request.post<Battle>(`'));
edit('src/views/柴圈板块/Battle/components/BattleDetail.vue',s=>{
 s=s.replace('ref, computed, onMounted, onUnmounted','ref, computed, watch, onUnmounted');
 s=s.replace('      <p>约战不存在</p>',"      <p>{{ loadError || '约战不存在' }}</p>").replace('const battle = computed',"const loadError = ref('')\nconst battle = computed");
 s=s.replace(/    await battleApi\.(\w+)\((battle.value.id[^;]*?)\)\n(?=    (?:await fetchDetail|show|isRejected))/g,`    const updated = await battleApi.$1($2)\n    if (battleStore.currentBattle?.id === updated.id) battleStore.currentBattle = updated\n`);
 s=s.replace(/    await fetchDetail\(\)\n/g,'');
 s=s.replace(/onMounted\(\(\) => \{[\s\S]*?\n}\)/,`watch(() => route.params.id, async () => {
  isRejected.value = false
  showRegisterModal.value = showSubmitModal.value = showInternalResultModal.value = false
  loadError.value = ''
  fetchMyOcs()
  try { await fetchDetail() }
  catch { loadError.value = '约战详情加载失败，请刷新重试' }
}, { immediate: true })`);
 s=s.replace('📊 投票进行中，等待问卷结果','📊 投票判定暂不可用，此约战尚未产生投票结果');
 s=s.replace('@click="handleCloseRegistration"',':disabled="actionBusy || battle.participants.length < 2" @click="handleCloseRegistration"');
 for(const name of ['handleCancel','handleCloseCreation','handlePublishResult'])s=s.replace(`@click="${name}"`,`:disabled="actionBusy" @click="${name}"`);
 return s;
});
