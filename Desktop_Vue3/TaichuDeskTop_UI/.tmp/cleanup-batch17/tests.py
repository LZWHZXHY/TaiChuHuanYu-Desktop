from pathlib import Path
source=Path('tests/content-flows.test.mjs').read_text(encoding='utf-8-sig')
source=source[:source.index("test('wiki parser")]
source+='''async function stream(t, getPublicStream) {
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
'''
Path('tests/community-layout.test.mjs').write_text(source,encoding='utf-8')
