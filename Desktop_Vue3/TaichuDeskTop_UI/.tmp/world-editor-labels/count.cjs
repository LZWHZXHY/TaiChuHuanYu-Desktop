const fs=require('node:fs');
const baseline=JSON.parse(fs.readFileSync('.tmp/world-editor-labels/baseline.json','utf8'));
baseline['src/utils/worldEditorText.ts']='';
const lines=s=>s?s.replace(/\r\n/g,'\n').replace(/\n$/,'').split('\n'):[];
const files=[];
for(const [file,before] of Object.entries(baseline)){
 const a=lines(before),b=lines(fs.existsSync(file)?fs.readFileSync(file,'utf8'):'');
 let row=new Uint32Array(b.length+1);
 for(const line of a){const next=new Uint32Array(b.length+1);for(let j=1;j<=b.length;j++)next[j]=line===b[j-1]?row[j-1]+1:Math.max(row[j],next[j-1]);row=next;}
 const common=row[b.length];if(a.length!==common||b.length!==common)files.push({file,added:b.length-common,removed:a.length-common,net:b.length-a.length});
}
const sum=items=>items.reduce((s,c)=>({added:s.added+c.added,removed:s.removed+c.removed,net:s.net+c.net}),{added:0,removed:0,net:0});
const result={source:sum(files.filter(f=>!f.file.startsWith('tests/'))),tests:sum(files.filter(f=>f.file.startsWith('tests/'))),total:sum(files),files};
fs.writeFileSync('.tmp/world-editor-labels/changes.json',JSON.stringify(result,null,2)); console.log(JSON.stringify(result,null,2));
