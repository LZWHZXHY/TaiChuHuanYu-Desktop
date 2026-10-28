import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { parse, compileScript } from '@vue/compiler-sfc'
import ts from 'typescript'
import * as vue from 'vue'

const require = createRequire(import.meta.url)
function load(file, mocks = {}) {
  const source = readFileSync(new URL('../src/' + file, import.meta.url), 'utf8')
  const text = file.endsWith('.vue') ? compileScript(parse(source).descriptor, { id: file }).content : source
  const exports = {}
  new Function('require', 'exports', ts.transpileModule(text, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText)(id => {
    if (id in mocks) return mocks[id]
    if (id.endsWith('.vue')) return {}
    if (id.startsWith('@/utils/')) return load(id.replace('@/', '') + '.ts', mocks)
    if (id === './wikiContent') return load('utils/wikiContent.ts')
    return require(id)
  }, exports)
  return exports
}
function mount(t, file, mocks, props = {}) {
  let state
  const component = load(file, mocks).default
  const renderer = vue.createRenderer({ createComment: () => ({}), insert() {}, remove() {}, parentNode() {}, nextSibling() {} })
  const app = renderer.createApp({ setup() { state = component.setup(props, { expose() {}, emit() {} }); return () => null } })
  app.mount({}); t.after(() => app.unmount()); return state
}
const settle = async () => { await vue.nextTick(); await new Promise(resolve => setImmediate(resolve)); await vue.nextTick() }
const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => {resolve=a;reject=b}); return {promise,resolve,reject} }
const paragraph = (text, id = 'one') => ({ type: 'paragraph', attrs: { id }, content: [{ type: 'text', text }] })
const doc = text => ({ type: 'doc', content: [paragraph(text)] })
test('project documents load once, ignore stale detail and clear failed reading', async t => {
  const first=deferred(), second=deferred(); let listCalls=0
  const s=mount(t,'views/太初协作/协作组件/ProjectDocument.vue', {
    '@/utils/request':{get:url=>{
      if(url.endsWith('/documents')) {listCalls++; return Promise.resolve([{id:'a'},{id:'b'}])}
      return url.endsWith('/a') ? first.promise : second.promise
    }},
  },{projectId:'project'})
  await settle(); assert.equal(listCalls,1)
  const a=s.handleSelectDocument('a'),b=s.handleSelectDocument('b')
  second.resolve({id:'b',title:'最新',blocks:[]});await b
  first.resolve({id:'a',title:'过期',blocks:[]});await a
  assert.equal(s.currentDocDetail.value.id,'b')
  // A second instance checks rejected detail without leaving the previous article visible.
  let attempts=0
  const failing=mount(t,'views/太初协作/协作组件/ProjectDocument.vue', {
    '@/utils/request':{get:async url=>{
      if(url.endsWith('/documents'))return [{id:'a'},{id:'b'}]
      if(++attempts===1)return {id:'a',blocks:[]}
      throw Error('permission revoked')
    }},
  },{projectId:'project'})
  await settle(); await failing.handleSelectDocument('a'); await failing.handleSelectDocument('b')
  assert.equal(failing.currentDocDetail.value,null);assert.ok(failing.detailError.value);assert.equal(failing.detailLoading.value,false)
})

test('project task save prevents duplicate requests and retains draft after failure', async t => {
  const gate=deferred();let writes=0
  const oldAlert=globalThis.alert;globalThis.alert=()=>{};t.after(()=>{globalThis.alert=oldAlert})
  const s=mount(t,'views/太初协作/协作组件/ProjectTaskDetail.vue', {
    '../../../api/projectService':{updateTaskDetails:()=>{writes++;return gate.promise}},
    '@/utils/request':{get:async()=>({isOwner:true,permissions:[]})},
  },{isOpen:true,projectId:'p',task:{id:'t',title:'待保存',categoryId:null},originalCategoryId:null,boardCategories:[],projectMembers:[]})
  await settle();const save=s.handleSave();await s.handleSave();assert.equal(writes,1)
  gate.reject(Error('offline'));await save
  assert.equal(s.localTask.value.title,'待保存');assert.equal(s.isSaving.value,false)
})
function browser(t) { const old = globalThis.window; globalThis.window = { addEventListener() {}, removeEventListener() {}, confirm: () => false }; t.after(() => { if (old) globalThis.window = old }) }

test('wiki parser preserves legacy content and rejects damaged structured data', () => {
  const { parseWikiDocument, wikiTags } = load('utils/wikiContent.ts')
  assert.equal(parseWikiDocument('普通文本').content[0].content[0].text, '普通文本')
  assert.equal(parseWikiDocument(JSON.stringify(paragraph('一')) + '\n' + JSON.stringify(paragraph('二'))).content.length, 2)
  assert.throws(() => parseWikiDocument('{broken'))
  assert.deepEqual(wikiTags('风景， 练习, '), ['风景', '练习'])
})

test('wiki differences identify the changed paragraph and preserve literal text', () => {
  const { wikiDiff, changedText } = load('utils/wikiDiff.ts')
  const result = wikiDiff(doc('社区维护'), doc('社区共同维护'))
  assert.equal(result.length, 1); assert.equal(result[0].newParagraph, 1)
  assert.deepEqual(changedText(result[0].before, result[0].after), { prefix:'社区',removed:'',added:'共同',suffix:'维护' })
  assert.equal(wikiDiff(doc('<script>'), doc('<img>'))[0].after, '<img>')
  assert.equal(wikiDiff(doc('一'), { type:'doc',content:[paragraph('新增','new'),paragraph('一')] }).length, 1)
})

async function composer(t, kind = 'blog', overrides = {}) {
  browser(t)
  const route = vue.reactive({ params: { type:kind }, query: {} }), calls = [], guards = []
  const api = { getSpaces: async () => [{id:'space'}], createNote:async data => {calls.push(['create',data]);return {id:'draft'}}, updateNoteContent:async (...args) => calls.push(['save',...args]), publishNote:async (...args) => {calls.push(['publish',...args]);return {success:true,publishedId:'public'}}, ...overrides }
  const user = vue.reactive({ token:'one' })
  const state = mount(t, 'views/太初随笔/ComposeView.vue', {
    '@/api/lingmai':{lingmaiApi:api}, '@/stores/user':{useUserStore:()=>user},
    'vue-router':{useRoute:()=>route,useRouter:()=>({replace: async value => {route.query=value.query}}),onBeforeRouteLeave:guard=>guards.push(guard),onBeforeRouteUpdate(){}},
  })
  await settle(); state.editor.value = {getSnapshot:()=>doc('正文')}; return {state,calls,guards,user,route}
}
test('independent blog saves once and publishes without a relation or project', async t => {
  const {state:s,calls,guards}=await composer(t); s.title.value='独立文章';s.tags.value='风景，练习';s.summary.value='摘要'
  await s.save(true)
  assert.equal(s.publishedId.value,'public');assert.equal(s.dirty.value,false);assert.equal(guards[0](),true)
  assert.deepEqual(calls.find(c=>c[0]==='publish').slice(1),['draft',{type:'blog',tags:['风景','练习']}])
  const payload=calls.find(c=>c[0]==='save')[2];assert.ok(payload.blocks.some(b=>b.type==='blog_fixed_excerpt'));assert.equal(payload.projectId,undefined)
  await s.save(true);assert.equal(calls.filter(c=>c[0]==='create').length,1)
})
test('failed save retains the draft identity and content, retries do not create a second draft', async t => {
  let attempts=0;const {state:s,calls}=await composer(t,'post',{updateNoteContent:async()=>{if(++attempts===1)throw Error('offline')}})
  await s.save(true);assert.equal(s.noteId.value,'draft');assert.ok(s.error.value);assert.equal(calls.filter(c=>c[0]==='publish').length,0)
  await s.save(true);assert.equal(calls.filter(c=>c[0]==='create').length,1);assert.equal(s.publishedId.value,'public')
})
test('saving blocks double submission and ignores completion after account changes', async t => {
  const gate=deferred();const {state:s,calls,user}=await composer(t,'post',{getSpaces:()=>gate.promise})
  const first=s.save(true);await s.save(true);assert.equal(s.busy.value,true)
  user.token='two';gate.resolve([{id:'space'}]);await first
  assert.equal(calls.length,0);assert.ok(s.loadError.value);assert.equal(s.busy.value,false)
})

test('knowledge ignores stale searches and resource failure does not block community results', async t => {
  const old=deferred(),latest=deferred(),route=vue.reactive({query:{}});let calls=0
  const knowledgeTypes={all:'全部',wiki:'公共知识',resource:'资源'}
  const s=mount(t,'views/知识库/index.vue',{
    'vue-router':{useRoute:()=>route,useRouter:()=>({})},'@/api/Wiki':{wikiApi:{}},
    '@/api/knowledge':{knowledgeTypes,knowledgeApi:{search:()=>++calls===1?old.promise:latest.promise,resources:async()=>{throw Error('offline')}}},
  });route.query.q='new';await settle();latest.resolve({items:[{id:'new'}],total:1});await settle();old.resolve({items:[{id:'old'}],total:9});await settle()
  assert.equal(s.items.value[0].id,'new');assert.equal(s.total.value,1);assert.equal(s.loading.value,false);assert.ok(s.resourceError.value)
  route.query.page='Infinity';await settle();assert.equal(s.page.value,1)
})

test('wiki editor preserves proposed changes on conflict and submits a base revision', async t => {
  browser(t);const calls=[];const route=vue.reactive({params:{id:'wiki'},query:{},fullPath:'/codex/wiki'})
  const s=mount(t,'views/灵脉百科/components/WikiDetail.vue',{
    'vue-router':{useRoute:()=>route,useRouter:()=>({}),onBeforeRouteLeave(){},onBeforeRouteUpdate(){}},
    '@/stores/user':{useUserStore:()=>vue.reactive({token:'one',isLoggedIn:true})},
    '@/api/Wiki':{wikiApi:{getArticleDetail:async()=>({title:'词条',content:JSON.stringify(doc('原文')),currentRevisionId:5}),history:async()=>({items:[],contributors:[],total:1}),updateFromNote:async value=>{calls.push(value);throw {response:{status:409},friendlyMessage:'冲突'}}}},
  });await settle();s.startEdit();s.editor.value={getSnapshot:()=>doc('修改后的正文')};s.dirty.value=true;await s.submit()
  assert.equal(calls[0].baseRevisionId,5);assert.equal(s.conflict.value,true);assert.equal(s.editing.value,true);assert.equal(s.dirty.value,true);assert.equal(s.saving.value,false)
})

test('COS credential failure settles upload instead of waiting forever', async () => {
  class FakeCos { constructor(options){this.options=options} uploadFile(_,callback){this.options.getAuthorization({},data=>callback(data.TmpSecretId?null:Error('credential failure'),{}))} }
  const {useCos}=load('composables/useCos.ts',{'cos-js-sdk-v5':{__esModule:true,default:FakeCos},'../utils/request':{__esModule:true,default:{get:async()=>{throw Error('offline')}}}})
  const upload=useCos();await assert.rejects(upload.uploadFile(new File(['a'],'a.png',{type:'image/png'})),/credential failure/);assert.equal(upload.isUploading.value,false)
})

test('cold entry waits for initial route resolution and does not overwrite built-in plugin routes', async t => {
  browser(t); const ready=deferred(), calls=[];
  const router={isReady:()=>ready.promise,hasRoute:()=>false,getRoutes:()=>[{path:'/knowledge'}],addRoute:route=>calls.push(['add',route]),currentRoute:{value:{matched:[],fullPath:'/'}},replace:path=>calls.push(['replace',path])};
  mount(t, 'App.vue', {
    'vue-router': { useRouter: () => router },
    './stores/user': { useUserStore: () => ({ isLoggedIn: false }) },
    './utils/request': { __esModule: true, default: {
      get: async () => { calls.push(['plugins']); return [{ name: '知识库', url: '/knowledge' }] },
    } },
  });
  await settle(); assert.deepEqual(calls,[]);
  router.currentRoute.value={matched:[{}],fullPath:'/knowledge?detail=article'};ready.resolve();await settle();
  assert.deepEqual(calls,[['plugins']]);
})

test('long independent articles retain paragraph ordering after backend string sorting', async t => {
  const {state:s,calls}=await composer(t);s.title.value='长文';
  s.editor.value={getSnapshot:()=>({type:'doc',content:Array.from({length:15},(_,i)=>paragraph('第'+i+'段','p'+i))})};
  await s.save(false);
  const blocks=calls.find(c=>c[0]==='save')[2].blocks;
  assert.deepEqual([...blocks].sort((a,b)=>a.sortOrder - b.sortOrder).map(b=>b.id),Array.from({length:15},(_,i)=>'p'+i));
  assert.equal(calls.find(c=>c[0]==='save')[3],true);
})

test('my writing lists only the current content type and clears on account changes', async t => {
  const {state:s,user}=await composer(t,'blog',{getSpaces:async()=>[{id:'ok'},{id:'offline'}],getNoteList:async id=>{if(id==='offline')throw Error();return [{id:'b',type:'blog',title:'文章',isPublic:true},{id:'p',type:'post'}]}});
  await s.loadDrafts();assert.equal(s.drafts.value.length,1);assert.equal(s.drafts.value[0].id,'b');assert.ok(s.draftError.value);
  user.token='another';assert.deepEqual(s.drafts.value,[]);
})

test('reopening a published draft keeps update mode and clears prior success messages', async t => {
  const {state:s,route}=await composer(t,'blog',{getNote:async()=>({title:'已发布文章',type:'blog',isPublic:true,blocks:[],tiptapContent:doc('原文')})});
  s.message.value='旧成功提示';route.query={draft:'existing'};await settle();
  assert.equal(s.isPublished.value,true);assert.equal(s.message.value,'');assert.equal(s.dirty.value,false);
})

test('reference picker paginates and stores a stable resource route instead of a download URL', async t => {
  const requests=[];const routes=[];
  const knowledge=load('api/knowledge.ts',{'@/utils/request':{__esModule:true,default:{}}});
  const s=mount(t,'components/ContentReferencePicker.vue',{'vue-router':{useRouter:()=>({resolve:route=>{routes.push(route);return {href:'#/knowledge?type=resource'}}})},'@/api/knowledge':{...knowledge,knowledgeApi:{search:async params=>{requests.push(params);return {items:[{id:'second',title:'第二页',type:'wiki'}],total:25}},resources:async()=>({success:true,data:[{name:'资料/说明.txt'}]})}}});
  await s.search(2);assert.equal(requests[0].page,2);assert.equal(s.page.value,2);assert.equal(s.total.value,25);
  s.type.value='resource';await s.search(1);s.select(s.items.value[0]);
  assert.deepEqual(routes[0],{name:'KnowledgeHome',query:{type:'resource',q:'资料/说明.txt'}});assert.equal(s.open.value,false);assert.match(s.message.value,/已插入引用/);
})

test('catalog corrects an out-of-range page after content disappears', async t => {
  const route=vue.reactive({query:{page:'99'}}), replacements=[];
  const s=mount(t,'views/知识库/index.vue',{'vue-router':{useRoute:()=>route,useRouter:()=>({replace:value=>{replacements.push(value);route.query=value.query}})},'@/api/Wiki':{wikiApi:{}},'@/api/knowledge':{knowledgeTypes:{all:'全部',resource:'资源'},knowledgeApi:{search:async()=>({items:[{id:'last'}],total:1}),resources:async()=>({success:true,data:[]})}}});
  await settle();assert.equal(replacements.length,1);assert.equal(s.page.value,1);assert.equal(s.items.value[0].id,'last');
})

test('wiki static preview preserves PDF access and uses the configured editor extensions once', t => {
  let rendered, extensions;
  const pdf = { name:'pdfEmbed', extend: definition => ({name:'pdfEmbed',...definition}) };
  const s=mount(t,'components/SpiritTextComponents/SpiritPreview.vue', {
    'dompurify':{__esModule:true,default:{sanitize:html=>html}},
    '../../utils/editorConfig':{spiritExtensions:[{name:'starterKit'},pdf]},
    '@tiptap/html':{generateHTML:(doc,configured)=>{rendered=doc;extensions=configured;return 'rendered'}},
  }, {modelValue:{type:'doc',content:[{type:'pdfEmbed',attrs:{url:'https://example.invalid/file.pdf',fileName:'说明.pdf'}}]}});
  assert.equal(s.renderedHtml.value,'rendered');assert.equal(rendered.content[0].type,'pdfEmbed');
  assert.equal(extensions.filter(e=>e.name==='starterKit').length,1);
  const node=extensions.find(e=>e.name==='pdfEmbed').renderHTML({node:rendered.content[0]});
  assert.equal(node[1][1].href,'https://example.invalid/file.pdf');assert.equal(node[1][2],'PDF：说明.pdf');
})


test('Minecraft draft preselects an optional tag without overwriting an existing draft', async t => {
  const {state:s,route,calls}=await composer(t,'post',{getNote:async()=>({type:'post',tags:['原创'],tiptapContent:doc('已有正文'),blocks:[]})});
  route.query={tag:'我的世界'};await settle();assert.equal(s.tags.value,'我的世界');
  s.tags.value='';await s.save(false);
  assert.deepEqual(calls.find(c=>c[0]==='save')[2].tags,[]);
  route.query={draft:'existing',tag:'我的世界'};await settle();assert.equal(s.tags.value,'原创');
});
