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
function browser(t) { const old = globalThis.window; globalThis.window = { addEventListener() {}, removeEventListener() {}, confirm: () => false }; t.after(() => { if (old) globalThis.window = old }) }

async function stream(t, getPublicStream) {
  const route = vue.reactive({ path:'/stream', query:{} }), calls=[];
  const old=globalThis.IntersectionObserver;
  globalThis.IntersectionObserver=class {observe(){} disconnect(){}};
  t.after(()=>{globalThis.IntersectionObserver=old});
  const navigate=async value=>{ calls.push(value); route.query=value.query };
  const state=mount(t,'views/太初随笔/index.vue',{
    'vue-router':{useRoute:()=>route,useRouter:()=>({push:navigate,replace:navigate})},
    '../../api/NotePublish':{notePublishApi:{getPublicStream}},
  });
  await settle(); return {state,route,calls};
}

test('stream ignores stale filter responses and preserves current loading state', async t=>{
  const first=deferred(), second=deferred();let calls=0;
  const {state:s,route}=await stream(t,()=>++calls===1?first.promise:second.promise);
  route.query={type:'post'};await settle();
  first.resolve([{id:'old',type:'blog',excerpt:'old'}]);await settle();
  assert.equal(s.loading.value,true);assert.equal(s.posts.value.length,0);
  second.resolve([{id:'new',type:'post',excerpt:'new',extraData:'{"cardCover":"/cover"}'}]);await settle();
  assert.equal(s.loading.value,false);assert.equal(s.posts.value[0].id,'new');assert.equal(s.posts.value[0].cardCover,'/cover');
});

test('stream retries the failed page without duplicates or losing loaded content', async t=>{
  const requests=[];let fail=true;
  const {state:s}=await stream(t,async p=>{
    requests.push(p.page);
    if(p.page===1)return Array.from({length:20},(_,i)=>({id:String(i),type:'blog',excerpt:'text'}));
    if(fail){fail=false;throw Error('offline')}
    return [{id:'19',type:'blog'},{id:'20',type:'post'}];
  });
  await s.fetchStream(true);assert.equal(s.posts.value.length,20);assert.ok(s.error.value);
  await s.fetchStream(true);assert.deepEqual(requests,[1,2,2]);assert.equal(s.posts.value.length,21);assert.equal(s.hasMore.value,false);
});

test('stream detail and filters use router query, retaining list context',async t=>{
  const {state:s,route}=await stream(t,async()=>[]);
  await s.changeFilter('post');await settle();
  const target=s.detailRoute('public-one');assert.deepEqual(target,{path:'/stream',query:{type:'post',post:'public-one'}});
  route.query=target.query;await settle();assert.equal(s.activePostId.value,'public-one');
  await s.closeArtwork();assert.equal(s.activePostId.value,null);assert.equal(route.query.type,'post');
});

test('stream retains legacy blog cover, plain text and rich-text excerpts',async t=>{
  const {state:s}=await stream(t,async()=>[]);
  assert.equal(s.formatPost({id:'a',type:'blog',excerpt:'{"text":"摘要","coverUrl":"/cover"}'}).content,'摘要');
  assert.equal(s.formatPost({id:'b',type:'note',excerpt:'old text'}).content,'old text');
  const rich=JSON.stringify(doc('正文'));assert.equal(s.formatPost({id:'c',type:'blog',excerpt:rich}).content,rich);
  assert.equal(s.formatPost({id:'d',type:'post',excerpt:'短文',extraData:'{broken'}).content,'短文');
});


test('topic filtering reaches the API before pagination and survives detail navigation', async t => {
  const requests=[]; const {state:s,route}=await stream(t,async p=>{requests.push({...p});return []});
  route.query={tag:'我的世界',type:'post'};await settle();
  assert.equal(requests.at(-1).tag,'我的世界');assert.equal(requests.at(-1).page,1);
  assert.equal(s.detailRoute('one').query.tag,'我的世界');
  assert.equal(s.composeRoute('post').query.tag,'我的世界');
  await s.changeTag('');await settle();assert.equal(requests.at(-1).tag,undefined);
  assert.equal(route.query.type,'post');
});

test('navigation keeps secondary tools and falls back when moved routes are unavailable', t => {
  const user=vue.reactive({isLoggedIn:true,permissions:[]});
  const routes=vue.reactive(new Set(['任务栏','交易行']));
  const s=mount(t,'components/OperationTerminal.vue',{
    'vue-router':{useRoute:()=>({path:'/tasks',name:'任务栏'}),useRouter:()=>({hasRoute:n=>routes.has(n),resolve:p=>({href:'#'+p})})},
    '@/stores/user':{useUserStore:()=>user},'@/router/auth':{LOGIN_PATH:'/login'},
    '@/composables/useTheme':{useTheme:()=>({mode:vue.ref('light'),setTheme(){}})},
  },{menuItems:['太初协作','太初灵脉','任务栏','交易行','管理面板','未知插件'].map(name=>({name,url:'/'+name})),activeName:''});
  assert.deepEqual(s.secondary.value.map(i=>i.name),['未知插件']);
  assert.equal(s.isActive({name:'太初协作',url:'/Project'}),true);
  routes.clear();assert.deepEqual(s.secondary.value.map(i=>i.name),['任务栏','交易行','未知插件']);
  user.permissions.push('SuperAdmin');assert.ok(s.secondary.value.some(i=>i.name==='管理面板'));
  let prevented=false;s.handleSelect({ctrlKey:true,button:0,preventDefault(){prevented=true}},{name:'太初灵脉',url:'/lingmai'});
  assert.equal(prevented,false);assert.equal(s.menuHref({url:'/lingmai'}),'#/lingmai');
});
