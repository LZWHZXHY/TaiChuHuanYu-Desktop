from pathlib import Path
p=Path('src/views/交易行/index.vue');s=p.read_text(encoding='utf-8-sig')
s=s.replace('class="editorial-container"','class="editorial-container content-page"').replace('<h1 class="main-title">资源中心</h1>','<span class="eyebrow">经验兑换 · 账户权益</span><h1 class="main-title">交易行</h1>').replace('账户权益审计与维度扩展 [2026.05]','查看可用经验和配额，选择需要的资源。兑换前会再次确认消耗。')
s=s.replace("@click=\"sortKey = 'cost'\"", "@click=\"sortKey = 'cost'\" :aria-pressed=\"sortKey === 'cost'\"").replace("@click=\"sortKey = 'name'\"", "@click=\"sortKey = 'name'\" :aria-pressed=\"sortKey === 'name'\"")
s=s.replace('state.experience.toLocaleString()',"loaded ? state.experience.toLocaleString() : '—'")
for field in ['level','usedSpaces','maxSpaces','usedNotes','maxNotes','usedProjectCount','maxProjectCount']:
 s=s.replace('{{ state.'+field+' }}',"{{ loaded ? state."+field+" : '—' }}")
s=s.replace('当前位阶','当前等级').replace('逻辑节点','灵脉笔记').replace('在线项目','协作项目').replace('已加持','已兑换').replace('系统共鸣请求','确认兑换')
s=s.replace('      <main class="dynamic-feed">','      <div v-if="loadError" class="trade-state" role="alert">{{ loadError }} <button class="btn-line" @click="initHome">重新加载</button></div>\n      <p v-else-if="loaded && !state.items.length" class="trade-state">暂时没有可兑换的资源。</p>\n      <main class="dynamic-feed" v-if="loaded && !loadError">')
s=s.replace(':class="[item.rankClass, { \'locked\': state.experience < item.cost }]"\n          @click="openConfirm(item)"',':class="{ locked: state.experience < item.cost }"')
s=s.replace('class="exchange-btn" :disabled="state.experience < item.cost"','class="exchange-btn" @click="openConfirm(item)" :disabled="purchasing || state.experience < item.cost"').replace("'封印中' : '点击兑换'","'经验不足' : '兑换资源'")
s=s.replace('@click.self="dialog.show = false"','@click.self="closeDialog"').replace('<div class="modal-content">','<div class="modal-content" role="dialog" aria-modal="true" aria-labelledby="trade-confirm-title" @keydown.esc="closeDialog">').replace('class="modal-header">确认兑换','class="modal-header" id="trade-confirm-title">确认兑换')
s=s.replace('@click="dialog.show = false"','@click="closeDialog" :disabled="purchasing"').replace('@click="executeExchange">确认扣除','@click="executeExchange" :disabled="purchasing">{{ purchasing ? \'兑换中…\' : \'确认扣除\' }}')
s=s.replace('class="minimal-toast"','class="minimal-toast" role="status"')
s=s.replace('onMounted, ref','onMounted, onUnmounted, ref').replace("const loading = ref(false);", "const loading = ref(false), loaded = ref(false), purchasing = ref(false);\nconst loadError = ref('');\nlet noticeTimer: ReturnType<typeof setTimeout> | undefined;\nonUnmounted(() => clearTimeout(noticeTimer));")
s=s.replace("  setTimeout(() => notice.show = false, 4000);","  clearTimeout(noticeTimer);\n  noticeTimer = setTimeout(() => notice.show = false, 4000);")
s=s.replace('  loading.value = true;','  loading.value = true; loadError.value = \'\';')
s=s.replace('    state.items = storeItems;','    state.items = storeItems; loaded.value = true;')
s=s.replace('    showNotice("灵脉审计感应中断", "error");','    loadError.value = "账户或资源读取失败，请重试。";')
s=s.replace("    const score = (item.baseWeight || 0) + (cost * 0.1);\n    \n    let rankClass = 'r-standard';\n    if (score >= 6000) rankClass = 'r-hero';\n    else if (score >= 2500) rankClass = 'r-feature';\n    \n    return { ...item, cost, rankClass, purchaseCount: pCount };", "    return { ...item, cost, purchaseCount: pCount };")
s=s.replace('  if (state.experience < item.cost) return;', '  if (purchasing.value || loading.value || loadError.value || state.experience < item.cost) return;')
s=s.replace('const executeExchange = async () => {','const closeDialog = () => { if (!purchasing.value) dialog.show = false; };\nconst executeExchange = async () => {\n  if (purchasing.value || !dialog.item) return;\n  purchasing.value = true;')
s=s.replace('  dialog.show = false;\n  try {','  try {').replace('    if (res.isSuccess) {','    if (res.isSuccess) {\n      dialog.show = false;')
s=s.replace('    showNotice("灵脉传输震荡", "error");\n  }','    showNotice("兑换失败，请稍后重试。", "error");\n  } finally { purchasing.value = false; }')
s=s[:s.index('<style scoped>')]+'''<style scoped>
.editorial-container { color: var(--text-primary); line-height: 1.6; }
.editorial-header { display: grid; grid-template-columns: minmax(0,1fr) auto; gap: 24px; padding: 28px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-lg); }
.main-title { margin: 8px 0; }.sub-caption { color: var(--text-secondary); font-size: 14px; margin: 0; max-width: 600px; }
.header-right { display: flex; flex-direction: column-reverse; justify-content: space-between; align-items: end; gap: 16px; }
.experience-meter { display: flex; align-items: baseline; gap: 16px; }.experience-meter .label { color: var(--text-secondary); font-size: 13px; }.experience-meter .value { font-size: 30px; font-weight: 700; color: var(--accent-color); }
.sort-controls { display: flex; gap: 8px; }.sort-controls button { padding: 7px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-secondary); }
.sort-controls button.active { background: var(--accent-soft); color: var(--accent-color); border-color: var(--accent-color); }
.audit-line { display: grid; grid-template-columns: repeat(4,minmax(0,1fr)); gap: 16px; margin: 24px 0; }
.audit-cell { display: grid; gap: 10px; padding: 20px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); }.c-label { font-size: 13px; color: var(--text-secondary); }.c-val { font-size: 20px; font-weight: 600; }
.dynamic-feed { display: grid; grid-template-columns: repeat(auto-fill,minmax(min(100%,280px),1fr)); gap: 20px; }
.feed-item { display: flex; padding: 24px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); min-width: 0; }.item-inner { width: 100%; display: flex; flex-direction: column; }
.item-header { display: flex; justify-content: space-between; gap: 12px; font-size: 12px; color: var(--text-secondary); }.item-body { flex: 1; }.item-title { font-size: 21px; margin: 18px 0 10px; overflow-wrap: anywhere; }.item-desc { color: var(--text-secondary); font-size: 14px; overflow-wrap: anywhere; }.item-benefit { background: var(--accent-soft); color: var(--accent-color); font-size: 13px; padding: 8px 12px; border-radius: 8px; margin: 16px 0; }
.item-footer { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; border-top: 1px solid var(--border-line); padding-top: 16px; }.p-val { font-size: 24px; font-weight: 700; }.p-unit { color: var(--text-secondary); font-size: 12px; margin-left: 5px; }
.exchange-btn,.btn-confirm { background: var(--accent-color); color: var(--on-accent); border: 1px solid var(--accent-color); border-radius: 8px; padding: 10px 18px; }.exchange-btn:disabled { background: var(--bg-sub); color: var(--text-secondary); border-color: var(--border-line); }
.modal-overlay { position: fixed; inset: 0; background: var(--overlay); display: grid; place-items: center; padding: 20px; z-index: 3000; }.modal-content { width: min(100%,480px); max-height: calc(100dvh - 40px); overflow: auto; background: var(--paper-card); padding: 28px; border: 1px solid var(--border-line); border-radius: 16px; box-shadow: var(--shadow-lg); }.modal-header { font-size: 21px; font-weight: 600; }.modal-body { padding: 24px 0; line-height: 1.9; }.highlight { color: var(--accent-color); font-weight: 700; }.modal-actions { display: flex; justify-content: flex-end; gap: 12px; }.btn-cancel { border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); border-radius: 8px; padding: 10px 18px; }.mobile-only { display: none; }
.trade-state { padding: 40px; text-align: center; color: var(--text-secondary); }.minimal-toast { position: fixed; bottom: 24px; right: 24px; max-width: min(440px,calc(100vw - 48px)); padding: 16px 24px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 12px; box-shadow: var(--shadow-lg); color: var(--success); z-index: 3100; overflow-wrap: anywhere; }.minimal-toast.error { color: var(--danger); }
@media(max-width:700px) { .editorial-header { grid-template-columns: 1fr; padding: 20px; }.header-right { align-items: start; }.audit-line { grid-template-columns: repeat(2,minmax(0,1fr)); gap: 10px; }.audit-cell { padding: 16px; } }
</style>
'''
p.write_text(s,encoding='utf-8')
