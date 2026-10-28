<template>
  <Teleport to="body"><div class="transfer-mask" @click.self="close"><section role="dialog" aria-modal="true" :aria-label="mode==='import'?'导入世界观文件':'导出世界观'" class="transfer-dialog" @keydown.esc="close">
    <header><h2>{{ mode==='import'?'导入世界观文件':'导出世界观' }}</h2><button :disabled="busy" aria-label="关闭导入导出" @click="close">×</button></header>
    <template v-if="mode==='import' && canEdit">
      <p>支持 UTF-8 编码的 TXT、Markdown。按二级标题分成条目；没有二级标题时，整份文件作为一个条目。仅新建，不覆盖已有内容。</p>
      <input type="file" accept=".txt,.md,text/plain,text/markdown" aria-label="选择世界观文件" :disabled="busy" @change="readFile" />
      <label v-if="items.length">新条目类型 <select v-model="type" :disabled="busy"><option v-for="option in store.cardTypes" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
      <ol><li v-for="(item,index) in items" :key="index"><strong>{{ item.title }}</strong><span>{{ done.has(index)?'已创建':`${item.description.length} 字` }}</span><p>{{ item.description.slice(0,160) }}</p></li></ol>
      <button class="primary" :disabled="busy || uncertain || !items.length || done.size===items.length || !type" @click="importFile">{{ busy?'正在创建…':done.size?'继续创建未完成条目':`确认新建 ${items.length} 个条目` }}</button>
    </template>
    <template v-else-if="mode==='export'">
      <p>导出当前世界的正文、属性、关系和时间线。图片保留链接。PDF 使用浏览器打印，请选择“另存为 PDF”；导出文件只保存在你的设备。</p>
      <div class="export-actions"><button :disabled="busy" @click="exportFile('md')">下载 Markdown</button><button :disabled="busy" @click="exportFile('pdf')">打印 / 保存 PDF</button></div>
    </template>
    <template v-if="printHtml"><iframe ref="printFrame" :srcdoc="printHtml" title="PDF 导出预览" sandbox="allow-same-origin allow-modals" /><button @click="printFrame?.contentWindow?.print()">打印 / 另存为 PDF</button></template>
    <p v-if="status" role="status">{{ status }}</p><p v-if="error" role="alert" class="error">{{ error }}</p>
  </section></div></Teleport>
</template>
<script setup lang="ts">
import { ref, reactive, onMounted, watch, onBeforeUnmount } from 'vue';
import type { WorldProject, CardDetail } from '@/api/types/world';
import { worldApi } from '@/api/worldApi';
import { useWorldStore } from '@/stores/world';
import { useUserStore } from '@/stores/user';
import { parseWorldText, worldMarkdown } from '@/utils/worldTransfer';
import { renderMarkdown } from '@/utils/markdown';
const props = defineProps<{
  project: WorldProject;
  mode: 'import' | 'export';
  canEdit: boolean;
}>();
const emit = defineEmits<{
  close: [
  ];
}>(), store = useWorldStore(), user = useUserStore();
const printHtml = ref(''), printFrame = ref<HTMLIFrameElement | null>(null);
const uncertain = ref(false);
const busy = ref(false), error = ref(''), status = ref(''), type = ref(''), items = ref<{
  title: string;
  description: string;
}[]>([]), done = reactive(new Set<number>());
let disposed = false;
onBeforeUnmount(() => { disposed = true; });
watch([() => user.userId, () => props.project.id], () => { items.value = []; done.clear(); emit('close'); });
const account = user.userId, projectId = props.project.id;
const valid = () => !disposed && user.userId === account && props.project.id === projectId;
function close() { if (!busy.value)
  emit('close'); }
onMounted(async () => { if (props.mode === 'import') {
  await store.fetchCardTypes();
  type.value = store.cardTypes.find(t => t.value === 'lore')?.value || store.cardTypes[0]?.value || '';
} });
async function readFile(event: Event) { error.value = ''; status.value = ''; items.value = []; done.clear(); const file = (event.target as HTMLInputElement).files?.[0]; if (!file)
  return; try {
  if (!/\.(txt|md)$/i.test(file.name) || file.size > 1024 * 1024)
    throw new Error('请选择不超过 1MB 的 TXT 或 Markdown 文件。');
  const text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer());
  items.value = parseWorldText(text, file.name);
}
catch (e) {
  error.value = e instanceof Error ? e.message : '文件读取失败';
} }
async function importFile() { if (busy.value || !props.canEdit || !valid())
  return; busy.value = true; error.value = ''; try {
  for (const [index, item] of items.value.entries()) {
    if (done.has(index))
      continue;
    if (!valid())
      throw new Error('账号或世界已切换，已停止导入。');
    await worldApi.createCard(projectId, { ...item, type: type.value });
    done.add(index);
    status.value = `已创建 ${done.size} / ${items.value.length} 个条目`;
  }
}
catch (e) {
  const code = (e as {
    response?: {
      status: number;
    };
  }).response?.status;
  uncertain.value = !code || code >= 500;
  error.value = uncertain.value ? '导入已暂停，最后一条的保存结果不确定，请关闭窗口并核对列表，避免重复导入。' : '导入已暂停。已成功的条目不会重复创建；请检查权限、容量后再继续。';
}
finally {
  if (valid())
    await store.fetchCards(projectId, true).catch(() => { error.value += ' 列表刷新失败，请重新加载。'; });
  busy.value = false;
} }
async function exportFile(format: 'md' | 'pdf') {
  if (busy.value || !valid())
    return;
  busy.value = true;
  error.value = '';
  status.value = '正在读取条目…';
  try {
    const summaries = await worldApi.getCards(projectId), cards: CardDetail[] = [];
    for (let i = 0; i < summaries.length; i += 100) {
      if (!valid())
        throw new Error('账号或世界已切换');
      const ids = summaries.slice(i, i + 100).map(c => c.id), batch = await worldApi.getCardsBatch(projectId, ids);
      if (batch.length !== ids.length)
        throw new Error('条目已变化，请重新导出');
      const byId = new Map(batch.map(card => [card.id, card]));
      for (const summary of summaries.slice(i, i + 100)) {
        const card = byId.get(summary.id);
        if (!card || card.updatedAt !== summary.updatedAt)
          throw new Error('导出期间内容发生变化，请重新导出。');
        cards.push(card);
      }
      status.value = `已读取 ${cards.length} / ${summaries.length} 个条目`;
    }
    if (!valid())
      throw new Error('账号已切换');
    const markdown = worldMarkdown(props.project, cards);
    if (format === 'md') {
      const url = URL.createObjectURL(new Blob([markdown], { type: 'text/markdown;charset=utf-8' }));
      const a = document.createElement('a');
      a.href = url;
      a.download = props.project.name.replace(/[<>:"/\\|?*]/g, '_') + '.md';
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      status.value = 'Markdown 已生成。';
    }
    else {
      const doc = document.implementation.createHTMLDocument(props.project.name);
      doc.documentElement.lang = 'zh-CN';
      doc.body.textContent = '';
      const style = doc.createElement('style');
      style.textContent = 'body{max-width:800px;margin:40px auto;padding:0 24px;font:15px/1.8 sans-serif;color:#222;background:#fff}h1,h2,h3{line-height:1.4;break-after:avoid}h2{border-top:1px solid #ddd;padding-top:24px}p,li{orphans:3;widows:3}pre{white-space:pre-wrap;overflow-wrap:anywhere}img{max-width:100%}a{color:#333;overflow-wrap:anywhere}@page{size:A4;margin:20mm}@media print{button{display:none}body{margin:0;padding:0;max-width:none}}';
      doc.head.append(style);
      const article = doc.createElement('article');
      article.innerHTML = renderMarkdown(markdown);
      doc.body.append(article);
      const base = doc.createElement('base');
      base.href = 'about:srcdoc';
      doc.head.prepend(base);
      printHtml.value = '<!doctype html>' + doc.documentElement.outerHTML;
      status.value = '预览已生成。点击“打印 / 另存为 PDF”，在打印窗口选择另存为 PDF。';
    }
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : '导出失败，请重试';
  }
  finally {
    busy.value = false;
  }
}
</script>
<style scoped>
iframe{width:100%;height:380px;border:1px solid var(--border-line);margin:16px 0;background:white}
.transfer-mask{position:fixed;inset:0;z-index:2000;background:#0006;display:grid;place-items:center;padding:16px}.transfer-dialog{box-sizing:border-box;width:min(640px,100%);max-height:85vh;overflow:auto;padding:24px;background:var(--paper-card);color:var(--text-primary);border:1px solid var(--border-line);border-radius:10px}.transfer-dialog header{display:flex;align-items:center;justify-content:space-between}h2{margin:0;font-size:20px}p{font-size:13px;line-height:1.8;color:var(--text-secondary)}button,select,input{font:inherit;max-width:100%;box-sizing:border-box}button,select{padding:9px 12px;border:1px solid var(--border-line);border-radius:6px;background:var(--bg-main);color:var(--text-primary);cursor:pointer}button:disabled{opacity:.5;cursor:default}.export-actions{display:flex;gap:12px;flex-wrap:wrap}label{display:block;margin-top:16px}ol{padding-left:22px}li{padding:12px 0;border-bottom:1px solid var(--border-line)}li span{margin-left:12px;font-size:12px;color:var(--text-secondary)}li p{white-space:pre-wrap;overflow-wrap:anywhere}.primary{background:var(--accent-color);color:var(--on-accent)}.error{color:var(--danger)}
</style>
