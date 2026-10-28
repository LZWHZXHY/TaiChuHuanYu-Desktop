const fs = require('node:fs');
const baseline = JSON.parse(fs.readFileSync('.tmp/world-edit/baseline.json', 'utf8'));
const lines = text => text.replace(/\r\n/g, '\n').replace(/\n$/, '').split('\n');
const changes = [];
for (const [file, before] of Object.entries(baseline)) {
  const exists = fs.existsSync(file), after = exists ? fs.readFileSync(file, 'utf8') : '';
  if (before.replace(/\r\n/g, '\n') === after.replace(/\r\n/g, '\n')) continue;
  const a = lines(before), b = exists ? lines(after) : [];
  let row = new Uint32Array(b.length + 1);
  for (const line of a) {
    const next = new Uint32Array(b.length + 1);
    for (let j = 1; j <= b.length; j++) next[j] = line === b[j-1] ? row[j-1] + 1 : Math.max(row[j], next[j-1]);
    row = next;
  }
  const common = row[b.length];
  changes.push({ file, added: b.length-common, removed: a.length-common, net: b.length-a.length, test: file.startsWith('tests/') });
}
const total = items => items.reduce((s, c) => ({ added:s.added+c.added, removed:s.removed+c.removed, net:s.net+c.net }), {added:0,removed:0,net:0});
const result = { source: total(changes.filter(c=>!c.test)), tests: total(changes.filter(c=>c.test)), total: total(changes), changes };
fs.writeFileSync('.tmp/world-edit/changes.json', JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));
