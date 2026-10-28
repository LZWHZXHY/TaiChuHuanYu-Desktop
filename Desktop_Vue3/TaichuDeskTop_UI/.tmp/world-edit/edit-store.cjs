const fs=require('fs');
const file='src/stores/world.ts';let s=fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n');
s=s.replace('diffRelations, toCardSummary','toCardSummary').replace('UpdateCardPayload, RelationInput,','UpdateCardPayload,');
s=s.replace(/  async function syncRelations\([^]*?(?=  function findCard)/,`  async function createCard(projectId: string, payload: CardSavePayload) {
    const card = await mutateProject(projectId, () => worldApi.createCard(projectId, payload))
    return getCardById(card.id, projectId) ?? toCardSummary(card)
  }

`);
s=s.replace(/    const \{ relations, \.\.\.cardPayload \} = payload[^]*?    return getCardById/,`    const card = await mutateProject(projectId, () => worldApi.updateCard(projectId, cardId, payload))
    return getCardById`);
fs.writeFileSync(file+'.tmp',s);fs.renameSync(file+'.tmp',file);
const types='src/api/types/world.ts';s=fs.readFileSync(types,'utf8').replace(/(  relations\?: RelationInput\[\]\r?\n)\1/,'$1');fs.writeFileSync(types,s);
