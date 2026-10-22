import fs from 'node:fs';
const read=p=>fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');
const edit=(p,f)=>fs.writeFileSync(p,f(read(p)));
const root='src/views/柴圈板块/';
edit(root+'Joint/joint_store.ts',s=>s.replace('  // ===== 详情 =====','  let detailRequest = 0\n\n  // ===== 详情 =====').replace('async function fetchDetail(id: string) {\n    loading.value = true','async function fetchDetail(id: string) {\n    const request = ++detailRequest\n    if (currentActivity.value?.id !== id) currentActivity.value = null\n    loading.value = true').replace('      currentActivity.value = res','      if (request === detailRequest) currentActivity.value = res').replace("      console.error('获取联合详情失败:', error)","      if (request === detailRequest) currentActivity.value = null\n      console.error('获取联合详情失败:', error)").replace("      throw error\n    } finally {\n      loading.value = false\n    }\n  }\n\n  // ===== 创建", "      throw error\n    } finally {\n      if (request === detailRequest) loading.value = false\n    }\n  }\n\n  // ===== 创建").replace('function clearCurrent() {','function clearCurrent() {\n    detailRequest++\n    loading.value = false'));
edit(root+'Joint/components/JointDetail.vue',s=>{
 s=s.replace('computed, onMounted','computed, watch').replace('const joining = ref(false)','const actionBusy = ref(false)\nconst loadError = ref(\'\')\nconst joining = ref(false)').replace('<p>联合活动不存在</p>',`<p>{{ loadError || '联合活动不存在' }}</p>`).replace("isExpired.value && activity.value.status !== 'ended'","isExpired.value && activity.value.status === 'open'").replace('  if (!activity.value) return false\n  if (activity.value.organizerType', "  if (!activity.value) return false\n  if (activity.value.organizerType");
 s=s.replace('const canJoin = computed(() => {\n  if (!activity.value) return false',"const canJoin = computed(() => {\n  if (!activity.value || activity.value.status !== 'open') return false").replace('<span v-else-if="joining">','<span v-else-if="!canJoin">当前状态不可报名</span>\n            <span v-else-if="joining">');
 s=s.replace('async function handleJoin() {\n  if (!activity.value) return','async function handleJoin() {\n  if (!activity.value || joining.value) return').replace('async function handleCancelJoin() {\n  if (!activity.value) return','async function handleCancelJoin() {\n  if (!activity.value || cancelling.value) return');
 s=s.replace(/    await store.fetchDetail\(activity.value.id\)\n/g,'');
 // Keep the action result already returned by the API instead of requesting it again.
 for(const name of ['handleAuditParticipant','handleKick','handleApprove','handleToggleBan','handleDelete']) {
  const start=s.indexOf('async function '+name); const end=s.indexOf('\n}',start)+2;
  let block=s.slice(start,end).replace('if (!activity.value) return','if (!activity.value || actionBusy.value) return').replace('  try {','  actionBusy.value = true\n  try {').replace(/\n  }\n}$/,'\n  } finally {\n    actionBusy.value = false\n  }\n}');
  s=s.slice(0,start)+block+s.slice(end);
 }
 s=s.replace(/(@click="handle(?:Approve|ToggleBan|Delete|AuditParticipant|Kick)[^"]*")/g,':disabled="actionBusy" $1');
 return s.replace(/onMounted\(async \(\) => \{[\s\S]*?\n}\)/,`watch(() => route.params.id, async id => {
  loadError.value = ''
  try { await store.fetchDetail(String(id)) }
  catch { loadError.value = '联合详情加载失败，请刷新重试' }
}, { immediate: true })`);
});
edit(root+'Battle/components/CreateBattle.vue',s=>{
 s=s.replace('onMounted, watch','onMounted, onUnmounted, watch').replace('userStore.userInfo?.id ||','userStore.userId ||');
 const a=s.indexOf('  // 队伍A至少有一个OC'), b=s.indexOf('\n  return true',a);
 s=s.slice(0,a)+`  const members = [...teamA.value, ...teamB.value]
  if (!userId.value || teamA.value[0]?.userId !== userId.value) return false
  if (members.some(m => !m.userId.trim() || !m.ocIds.length)) return false
  if (new Set(members.map(m => m.userId.toLowerCase())).size !== members.length) return false
`+s.slice(b);
 s=s.replace("if (member.ocIds.length === 0) {","if (member.ocIds.length === 0 && !(team === 'A' && index === 0)) {").replace('    member.ocIds.push(oc.id)','    member.ocIds.push(oc.id)\n    if (!allOcList.value.some(item => item.id === oc.id)) allOcList.value.push(oc)').replace('oc.authorId === member.userId.trim()', 'oc.authorId?.toLowerCase() === member.userId.trim().toLowerCase()').replace('  if (!isValid.value) {','  if (loading.value || uploading.value) return\n  if (!isValid.value) {');
 const c=s.indexOf('  // 队伍A所有OC'), d=s.indexOf('  loading.value = true',c);
 s=s.slice(0,c)+`  // 发起者只提交自己的 OC；同队成员和对手都需要本人接受邀请。
  const challengerOcIds = [...teamA.value[0].ocIds]
  const opponentOcIds = Object.fromEntries([...teamA.value.slice(1), ...teamB.value]
    .map(member => [member.userId.trim(), [...member.ocIds]]))
  const teams = Object.fromEntries([
    ...teamA.value.map(member => [member.userId.trim(), 1]),
    ...teamB.value.map(member => [member.userId.trim(), 2]),
  ])

`+s.slice(d);
 return s.replace('      challengerOcIds: challengerOcIds,','      challengerOcIds,\n      battleConfigJson: JSON.stringify({ teams }),').replace(':disabled="loading || !isValid"',':disabled="loading || uploading || !isValid"').replace('onMounted(async () => {','onUnmounted(() => clearTimeout(searchTimer))\n\nonMounted(async () => {');
});
edit(root+'Battle/components/BattleDetail.vue',s=>{
 s=s.replace('const cancelling = ref(false)','const actionBusy = ref(false)\nconst cancelling = actionBusy').replace("battle.value?.participants?.[0]?.userId === userStore.userInfo?.id", "(battle.value?.initiatorId || battle.value?.participants?.[0]?.userId)?.toLowerCase() === userStore.userId?.toLowerCase()").replace('Array.isArray(res) ? res : []','Array.isArray(res) ? res.filter(oc => oc.isBattleEnabled) : []');
 s=s.replace('  if (!battle.value) return\n  joining.value = true','  if (!battle.value || joining.value) return false\n  joining.value = true').replace("    ElMessage.success('报名成功！')","    ElMessage.success('报名成功！')\n    return true").replace("    ElMessage.error(error.response?.data?.message || '报名失败')","    ElMessage.error(error.response?.data?.message || '报名失败')\n    return false");
 s=s.replace('  await doRegister(selectedRegisterOcIds.value)\n  showRegisterModal.value = false\n  selectedRegisterOcIds.value = []','  if (await doRegister([...selectedRegisterOcIds.value])) {\n    showRegisterModal.value = false\n    selectedRegisterOcIds.value = []\n  }');
 s=s.replace('确定拒绝此约战吗？拒绝后你将无法参与，该约战将变为公开约战。','确定拒绝此邀请吗？其他人的邀请会保留；无人受邀时约战将转为公开报名。');
 for (const name of ['handleCancelJoin','handleCloseRegistration','handleCloseCreation','handleSubmitWork','handleSetInternalResult','handlePublishResult','handleCancel']) {
  const start=s.indexOf('const '+name+' = async'), end=s.indexOf('\n}',start)+2;
  let block=s.slice(start,end).replace('if (!battle.value) return','if (!battle.value || actionBusy.value) return').replace('  try {','  actionBusy.value = true\n  try {').replace(/\n  }\n}$/,'\n  } finally {\n    actionBusy.value = false\n  }\n}');
  s=s.slice(0,start)+block+s.slice(end);
 }
 s=s.replace('!submitForm.value.title || !submitForm.value.contentUrl','!submitForm.value.title.trim() || !submitForm.value.contentUrl.trim()').replace('title: submitForm.value.title,','title: submitForm.value.title.trim(),').replace('contentUrl: submitForm.value.contentUrl,','contentUrl: submitForm.value.contentUrl.trim(),');
 s=s.replace(/width="500px"/g,'width="min(500px, calc(100vw - 32px))" class="battle-dialog" :close-on-click-modal="!actionBusy && !joining" :close-on-press-escape="!actionBusy && !joining" :show-close="!actionBusy && !joining"');
 s=s.replace(/<el-button @click="(show\w+Modal = false)"/g,'<el-button :disabled="actionBusy || joining" @click="$1"').replace(':disabled="!submitForm.title || !submitForm.contentUrl"',':loading="actionBusy" :disabled="!submitForm.title.trim() || !submitForm.contentUrl.trim()"').replace(':disabled="!internalResult.winnerIds.length"',':loading="actionBusy" :disabled="!internalResult.winnerIds.length"');
 return s.replace('  fetchDetail()\n})',"  fetchDetail().catch(() => ElMessage.error('约战详情加载失败，请刷新重试'))\n})");
});
edit('src/views/太初俱乐部/CsDashboard.vue',s=>{
 s=s.replace('      loadingOps: false,','      loadingOps: false,\n      opsRequest: 0,\n      ordersRequest: 0,\n      opsError: \'\',');
 s=s.replace('    canAssign() {','    canAssign() {\n      if (this.loadingOps || this.opsError) return false\n      if (this.selectedOpIds.some(id => !this.availableOps.some(op => op.userId === id && op.status !== \'offline\'))) return false');
 s=s.replace('      const tab = this.tab\n      this.loadingOrders = true','      const tab = this.tab\n      const requestId = ++this.ordersRequest\n      this.loadingOrders = true').replace('        this.orders = data?.items ?? []','        if (requestId !== this.ordersRequest || tab !== this.tab) return false\n        this.orders = data?.items ?? []').replace("        console.error('加载订单失败', e)","        if (requestId !== this.ordersRequest || tab !== this.tab) return false\n        console.error('加载订单失败', e)").replace('        this.loadingOrders = false','        if (requestId === this.ordersRequest) this.loadingOrders = false');
 for (const name of ['openAssign','openReassign']) s=s.replace(`    ${name}(o) {`,`    ${name}(o) {\n      if (this.assigning || this.reassigning) return`);
 for (const name of ['closeAssign','closeReassign']) s=s.replace(`    ${name}() {`,`    ${name}(completed = false) {\n      if ((this.assigning || this.reassigning) && completed !== true) return\n      this.opsRequest++\n      this.loadingOps = false`);
 s=s.replace('    async loadAssignableOps(gameCode) {\n      this.loadingOps = true',"    async loadAssignableOps(gameCode) {\n      const requestId = ++this.opsRequest\n      this.availableOps = []\n      this.opsError = ''\n      this.loadingOps = true").replace('        this.availableOps = data?.items ?? []','        if (requestId !== this.opsRequest) return\n        this.availableOps = data?.items ?? []').replace("        console.error('加载打手失败', e)","        if (requestId !== this.opsRequest) return\n        this.opsError = '打手加载失败，请重试'\n        console.error('加载打手失败', e)").replace('} finally { this.loadingOps = false }','} finally { if (requestId === this.opsRequest) this.loadingOps = false }');
 s=s.replace('    toggleOp(opId) {','    toggleOp(opId) {\n      if (this.loadingOps || this.assigning || this.reassigning) return\n      if (!this.availableOps.some(op => op.userId === opId && op.status !== \'offline\')) return');
 s=s.replace('if (!this.canAssign || !this.assignOrder) return','if (this.assigning || !this.canAssign || !this.assignOrder) return').replace('if (!this.canReassign || !this.reassignOrder) return','if (this.reassigning || !this.canReassign || !this.reassignOrder) return').replace('        this.closeAssign()','        this.closeAssign(true)').replace('        this.closeReassign()','        this.closeReassign(true)');
 s=s.replace(/<div class="assign-modal">/g,'<div class="assign-modal" role="dialog" aria-modal="true" aria-label="安排订单打手">');
 return s.replace(/(<div v-if="loadingOps"[^>]*>)/g,`<div v-if="opsError" role="alert" class="empty-state">{{ opsError }} <button class="btn-secondary" @click="loadAssignableOps(currentOpOrder.gameCode)">重新加载</button></div>\n          $1`);
});
