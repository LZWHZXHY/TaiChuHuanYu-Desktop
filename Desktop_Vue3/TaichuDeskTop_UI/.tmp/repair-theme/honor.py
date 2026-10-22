from pathlib import Path
import re
p=Path('src/views/荣誉墙/index.vue'); s=p.read_text(encoding='utf-8-sig')
s=s.replace('<div class="honor-wall-root">','<div class="honor-wall-root content-page">').replace('<div class="honor-header">','<header class="content-header">',1).replace('<h2>共建荣誉殿堂</h2>','<p class="eyebrow">社区共建</p><h1>排行榜 · 荣誉墙</h1>').replace('      </div>\n    </div>','      </div>\n      <button class="btn-line" :disabled="isLoading" @click="loadHonorData">{{ isLoading ? \'正在加载…\' : \'刷新榜单\' }}</button>\n    </header>',1)
s=s.replace('    <div class="honor-content" v-if="!isLoading">','    <div v-if="loadError" class="content-state" role="alert">榜单加载失败，请点击上方刷新重试。</div>\n    <div class="honor-content" v-if="!isLoading && !loadError">')
s=s.replace('      <div class="honor-grid">','      <div v-if="!topTenByExp.length" class="content-state">暂时还没有公开的贡献记录。</div>\n      <div class="honor-grid">')
s=s.replace('    <div v-else class="honor-loading">','    <div v-if="isLoading" class="honor-loading" role="status" aria-label="加载榜单">')
s=s.replace('const isLoading = ref(true);','const isLoading = ref(true);\nconst loadError = ref(false);').replace('  isLoading.value = true;','  isLoading.value = true;\n  loadError.value = false;').replace('  } catch (err) {','  } catch (err) {\n    loadError.value = true;')
s=s[:s.index('<style scoped>')]+'''<style scoped>
.overview-banner { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 16px; margin-bottom: 32px; }
.overview-item { display: grid; gap: 12px; padding: 24px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.label { font-size: 12px; color: var(--text-secondary); }
.value { font-size: clamp(18px,2vw,26px); font-weight: 650; }
.section-title { margin-bottom: 16px; }
.section-title h3 { font-size: 18px; }
.honor-grid { display: grid; gap: 10px; }
.honor-card { display: grid; grid-template-columns: 64px minmax(0,1fr) auto; align-items: center; gap: 16px; padding: 20px 24px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.honor-card.top-tier { border-left: 3px solid var(--accent-color); }
.rank-badge { font: 600 16px var(--font-mono); color: var(--text-secondary); }
.top-tier .rank-badge { color: var(--accent-color); }
.user-name { font-size: 16px; overflow-wrap: anywhere; }
.user-title { display: block; font-size: 12px; color: var(--text-secondary); margin-top: 6px; }
.metrics-area { display: flex; flex-wrap: wrap; gap: 8px; }
.metric-pill { padding: 5px 10px; border-radius: 6px; font-size: 12px; color: var(--text-secondary); background: var(--bg-sub); }
.metric-pill.exp { color: var(--accent-color); background: var(--accent-soft); }
.honor-loading { padding: 64px; text-align: center; }
.loading-bar { width: 60px; height: 3px; margin: auto; background: var(--accent-color); }
@media(max-width: 700px) { .overview-banner { grid-template-columns: minmax(0,1fr); gap: 10px; } .overview-item { display: flex; align-items: center; justify-content: space-between; padding: 16px; } .honor-card { grid-template-columns: 48px minmax(0,1fr); padding: 16px; } .metrics-area { grid-column: 2; } }
</style>
'''; p.write_text(s,encoding='utf-8')
Path('src/views/荣誉墙/UniverseHonorWall.vue').write_text('''<script setup lang="ts">
import HonorWall from './index.vue'
</script>
<template><HonorWall /></template>
''',encoding='utf-8')
# 仅转换俱乐部现有样式的中性色，业务状态色和图片保持原样。
for p in Path('src/views/太初俱乐部').rglob('*.vue'):
 s=p.read_text(encoding='utf-8-sig')
 def convert(m):
  css=m[0]
  css=re.sub(r'(background(?:-color)?\s*:[^;{}]*?)#(?:05080c|070a0f|080c11|0a0f15|0c131b|101923|0a0d12|0b1017|0d1117|111827)\b',r'\1var(--panel)',css,flags=re.I)
  css=re.sub(r'(color\s*:\s*)#(?:fff(?:fff)?|f2f5f7|e5e7eb)\b',r'\1var(--text)',css,flags=re.I)
  css=re.sub(r'(background(?:-color)?\s*:\s*)rgba\(\s*7,\s*10,\s*15,\s*[\d.]+\)',r'\1var(--panel)',css)
  css=re.sub(r'rgba\(255,\s*255,\s*255,\s*0\.0[234568]\)', 'var(--bg-soft)', css)
  css=re.sub(r'((?:border(?:-[a-z]+)?|border-color)\s*:[^;{}]*?)rgba\(255,\s*255,\s*255,\s*[\d.]+\)',r'\1var(--line)',css)
  return css
 s=re.sub(r'<style\b[\s\S]*?</style>',convert,s)
 p.write_text(s,encoding='utf-8')
