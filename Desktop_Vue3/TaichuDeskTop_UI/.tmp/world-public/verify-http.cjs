const https = require('https'), fs = require('fs');
const get = path => new Promise((resolve,reject)=>https.get('https://localhost:5049/api/world'+path,{rejectUnauthorized:false},r=>{let body='';r.on('data',d=>body+=d);r.on('end',()=>resolve({status:r.statusCode,body:JSON.parse(body||'null')}));}).on('error',reject));
(async()=>{
const p='c2e269b3-38ff-4ba0-aaac-691d48edc7de',c='13b92284-8f4e-4785-b371-ec18ba44551d',priv='a550e279-fc4a-4954-b29b-3fc0b0aa0ea1',pc='bacb6c6e-8eb9-414d-82b9-59893f5d9c2c';
const results=[];
for (const [path,status] of [
  ['/projects/public',200],['/projects',401],
  ...['', '/cards','/cards/'+c,'/cards/timeline','/relations','/cards/'+c+'/backlinks'].map(s=>['/projects/'+p+s,200]),
  ['/cards/'+c+'/relations',200],['/search?q='+encodeURIComponent('泛空间'),200],
  ...['','/cards','/cards/'+pc,'/cards/timeline','/relations'].map(s=>['/projects/'+priv+s,404]),
  ['/cards/'+pc+'/relations',404],['/projects/'+p+'/cards/'+pc,404]
]){const r=await get(path);if(r.status!==status)throw Error(path+' '+r.status);results.push({path,status:r.status});}
for(const path of ['/search?q='+encodeURIComponent('守灯人 · 阿岚'),'/projects/'+p+'/cards/'+pc+'/backlinks','/projects/'+priv+'/cards/'+pc+'/backlinks']){const r=await get(path);if(r.status!==200||r.body.length)throw Error('private data leaked: '+path);results.push({path,status:200,empty:true});}
fs.writeFileSync('.tmp/world-public/http-checks.json',JSON.stringify(results,null,2));console.log(results.length+' real anonymous HTTP checks passed');
})().catch(e=>{console.error(e.message);process.exitCode=1;});
