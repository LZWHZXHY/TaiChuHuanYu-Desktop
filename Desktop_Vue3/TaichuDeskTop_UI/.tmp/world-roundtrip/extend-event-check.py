from pathlib import Path
p=Path('.tmp/world-roundtrip/check.html')
s=p.read_text(encoding='utf-8')
s=s.replace('JSON.stringify({public:project.isPublic,total:project.cardCount,completeWorld,groups}',"JSON.stringify({public:project.isPublic,total:project.cardCount,completeWorld,groups,eventReferences:allCards.filter(c=>c.title==='事件往返验收 · 启程').map(c=>({id:c.id,locationId:JSON.parse(c.content).locationId,targetTitle:allCards.find(t=>t.id===JSON.parse(c.content).locationId)?.title,originalIdRemaining:c.content.includes('22222222-2222-4222-8222-222222222222')}))}")
p.write_text(s,encoding='utf-8')
