import assert from 'node:assert/strict'
import { before, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { build, createLogger } from 'vite'

const root = fileURLToPath(new URL('../', import.meta.url))
let chunks
const warnings = []
before(async () => {
  const logger = createLogger('silent')
  logger.warn = message => warnings.push(message)
  await build({ root, customLogger: logger,
    build: { outDir: 'node_modules/.tmp/build-check', manifest: true },
    plugins: [{ name: 'check-output', writeBundle(_, bundle) {
      chunks = Object.values(bundle).filter(item => item.type === 'chunk')
    } }],
  })
})

function closure(entry) {
  assert.ok(entry, 'entry must be emitted')
  const visited = new Set()
  function visit(chunk) {
    if (!chunk || visited.has(chunk)) return
    visited.add(chunk)
    chunk.imports.forEach(file => visit(chunks.find(item => item.fileName === file)))
  }
  visit(entry)
  return [...visited]
}
const page = path => chunks.find(chunk => chunk.facadeModuleId?.replaceAll('\\', '/').endsWith(path))
const modules = entries => entries.flatMap(chunk => Object.keys(chunk.modules)).join('\n')

test('only the optional official WebGPU module exceeds 500 kB', () => {
  const oversized = chunks.filter(chunk => Buffer.byteLength(chunk.code) > 500_000)
  assert.ok(oversized.length <= 1)
  for (const chunk of oversized) {
    assert.ok(Buffer.byteLength(chunk.code) < 600_000, chunk.fileName)
    assert.match(modules([chunk]), /three[\\/]build[\\/]three\.webgpu\.js/)
  }
})

test('production build has no mixed-import warnings', () => {
  assert.deepEqual(warnings.filter(message => /INEFFECTIVE_DYNAMIC_IMPORT|dynamically imported/.test(message)), [])
})

test('startup and home do not eagerly load charts, 3D, note editors or math engines', () => {
  const initial = [...closure(chunks.find(chunk => chunk.isEntry)), ...closure(page('/src/views/推送首页/index.vue'))]
  assert.doesNotMatch(modules(initial), /node_modules[\\/](three|echarts|zrender|@tiptap|katex|vis-network)[\\/]/)
})

test('world project details load 3D only when the graph view is requested', () => {
  assert.doesNotMatch(modules(closure(page('/src/views/世界观管理/ProjectDetail.vue'))), /node_modules[\\/]three[\\/]/)
  assert.match(modules(closure(page('/src/views/世界观管理/世界观组件/RelationGraph.vue'))), /node_modules[\\/]three[\\/]/)
})

test('finance bundle includes only the registered pie and line chart families', () => {
  const code = modules(closure(page('/src/utils/financeCharts.ts')))
  assert.match(code, /echarts[\\/]lib[\\/]chart[\\/]pie[\\/]/)
  assert.match(code, /echarts[\\/]lib[\\/]chart[\\/]line[\\/]/)
  assert.doesNotMatch(code, /echarts[\\/]lib[\\/]chart[\\/](bar|map|radar|sankey|treemap)[\\/]/)
})

test('every internal static and dynamic chunk import points to an emitted file', () => {
  const files = new Set(chunks.map(chunk => chunk.fileName))
  for (const chunk of chunks) {
    for (const imported of [...chunk.imports, ...chunk.dynamicImports]) assert.ok(files.has(imported), `${chunk.fileName} -> ${imported}`)
  }
})
