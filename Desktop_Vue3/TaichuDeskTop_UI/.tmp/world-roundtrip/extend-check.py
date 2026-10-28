from pathlib import Path
p=Path('.tmp/world-roundtrip/check.html')
s=p.read_text(encoding='utf-8-sig')
s=s.replace("const project=await worldApi.getProject(pid), summaries=(await worldApi.getCards(pid)).filter(c=>c.title.startsWith('往返验收 · '));", "const project=await worldApi.getProject(pid), allSummaries=await worldApi.getCards(pid), summaries=allSummaries.filter(c=>c.title.startsWith('往返验收 · '));\n const allCards=await worldApi.getCardsBatch(pid,allSummaries.map(c=>c.id));\n let completeWorld;try{completeWorld={restoredCount:parseWorldText(worldMarkdown(project,allCards),'全世界.md').length}}catch(e){completeWorld={error:String(e)}};")
s=s.replace("JSON.stringify({public:project.isPublic,total:project.cardCount,groups}","JSON.stringify({public:project.isPublic,total:project.cardCount,completeWorld,groups}")
p.write_text(s,encoding='utf-8')
