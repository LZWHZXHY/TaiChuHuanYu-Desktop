import fs from 'node:fs/promises'
import {unzipSync,strFromU8} from 'fflate'
import {createHash} from 'node:crypto'
const base='.tmp/world-backup-check/'
const files=unzipSync(new Uint8Array(await fs.readFile(base+'actual-reexport.zip')))
const manifest=JSON.parse(strFromU8(files['manifest.json']))
const original=await fs.readFile(base+'original.offline.png')
const image=files[Object.keys(files).find(p=>p.startsWith('images/'))]
const report={originalAddressFileExists:await fs.access(base+'original.png').then(()=>true,()=>false),bytesExactlyEqual:Buffer.from(image).equals(original),originalSha256:createHash('sha256').update(original).digest('hex'),restoredSha256:createHash('sha256').update(image).digest('hex'),archivePaths:Object.keys(files),project:manifest.project}
await fs.writeFile(base+'offline-verification.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report))
