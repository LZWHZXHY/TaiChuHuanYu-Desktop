<!-- 「参考来源」区块：写这个设定时参考了哪些资料（网页、书、视频、游戏……），方便以后回头查。
     每条：名称 + 链接（可选）+ 备注（可选，比如第几页、第几分钟、参考了哪一点）。 -->
<template>
  <div class="src" :class="{ editing: editable }">
    <template v-if="editable">
      <p v-if="!items.length" class="src-empty">还没有参考来源。可以记下网页、书、视频、游戏……以后要查直接点开。</p>
      <ol class="src-edit-list">
        <li v-for="(item, i) in items" :key="item.id" class="src-edit">
          <span class="src-no" aria-hidden="true">{{ i + 1 }}</span>
          <div class="src-fields">
            <input v-model="item.title" class="src-title-input" maxlength="120" placeholder="名称：比如《山海经·南山经》、某某百科词条" :aria-label="`第 ${i + 1} 条来源的名称`" @input="emitChange" />
            <input v-model="item.url" class="src-url-input" maxlength="1000" placeholder="链接（可选）：https://…" inputmode="url" :aria-label="`第 ${i + 1} 条来源的链接`" @input="emitChange" @blur="fillTitle(item)" />
            <input v-model="item.note" class="src-note-input" maxlength="300" placeholder="备注（可选）：第 32 页 / 12:30 / 参考了服饰部分" :aria-label="`第 ${i + 1} 条来源的备注`" @input="emitChange" />
          </div>
          <div class="src-ops">
            <button type="button" :disabled="i === 0" aria-label="上移" @click="move(i, -1)">↑</button>
            <button type="button" :disabled="i === items.length - 1" aria-label="下移" @click="move(i, 1)">↓</button>
            <button type="button" class="src-del" aria-label="删除这条来源" @click="remove(i)">×</button>
          </div>
        </li>
      </ol>
      <button type="button" class="src-add" :disabled="items.length >= MAX" @click="add">＋ 添加来源</button>
    </template>

    <ol v-else class="src-list">
      <li v-for="item in shown" :key="item.id">
        <a v-if="safeUrl(item.url)" :href="safeUrl(item.url)!" target="_blank" rel="noopener noreferrer nofollow" class="src-link">{{ item.title || hostOf(item.url) }}</a>
        <span v-else class="src-plain">{{ item.title }}</span>
        <small v-if="safeUrl(item.url) && item.title && item.title !== hostOf(item.url)" class="src-host">{{ hostOf(item.url) }}</small>
        <span v-if="item.note" class="src-note">{{ item.note }}</span>
      </li>
    </ol>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { newId, type SourceItem } from '@/utils/worldLayout'

const props = defineProps<{ sources?: SourceItem[]; editable?: boolean }>()
const emit = defineEmits<{ 'update:sources': [value: SourceItem[]] }>()
const MAX = 100

// 编辑时用本地副本，改了就整体发出去
const items = ref<SourceItem[]>([])
watch(() => props.sources, v => { if (v !== items.value) items.value = (v ?? []).map((s: SourceItem) => ({ ...s })) }, { immediate: true })
const emitChange = () => emit('update:sources', items.value)

const shown = computed(() => (props.sources ?? []).filter(s => s.title?.trim() || s.url?.trim()))

function add() { items.value.push({ id: newId('s'), title: '', url: '', note: '' }); emitChange() }
function remove(i: number) {
  const it = items.value[i]
  if (it && (it.title || it.url || it.note) && !window.confirm('删除这条参考来源？')) return
  items.value.splice(i, 1); emitChange()
}
function move(i: number, d: -1 | 1) {
  const [it] = items.value.splice(i, 1)
  items.value.splice(i + d, 0, it!); emitChange()
}

/** 只允许 http / https 链接（防止 javascript: 之类） */
function safeUrl(url?: string) {
  const u = (url ?? '').trim()
  if (!u) return null
  const withScheme = /^[a-z][a-z0-9+.-]*:/i.test(u) ? u : `https://${u}`
  try { const p = new URL(withScheme); return p.protocol === 'http:' || p.protocol === 'https:' ? p.href : null } catch { return null }
}
function hostOf(url?: string) {
  const s = safeUrl(url)
  if (!s) return ''
  try { return new URL(s).hostname.replace(/^www\./, '') } catch { return '' }
}
/** 只填了链接没填名称：先用网址的域名当名称 */
function fillTitle(item: SourceItem) {
  if (!item.title?.trim() && safeUrl(item.url)) { item.title = hostOf(item.url); emitChange() }
}
</script>

<style scoped>
.src-list { margin: 0; padding-left: 22px; display: grid; gap: 6px; font-size: 14px; line-height: 1.6; }
.src-list li { overflow-wrap: anywhere; }
.src-link { color: var(--accent-color); text-decoration: none; font-weight: 500; }
.src-link:hover { text-decoration: underline; }
.src-link::after { content: ' ↗'; font-size: 11px; opacity: .7; }
.src-plain { font-weight: 500; }
.src-host { margin-left: 6px; font-size: 12px; color: var(--text-secondary); }
.src-note { display: block; font-size: 12px; color: var(--text-secondary); }

.src-empty { margin: 0 0 8px; font-size: 13px; color: var(--text-secondary); }
.src-edit-list { list-style: none; margin: 0 0 8px; padding: 0; display: grid; gap: 8px; }
.src-edit { display: grid; grid-template-columns: 22px minmax(0, 1fr) auto; gap: 8px; align-items: start; padding: 8px; background: var(--bg-sub); }
.src-no { padding-top: 7px; font-size: 12px; color: var(--text-secondary); text-align: center; font-variant-numeric: tabular-nums; }
.src-fields { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 6px; }
.src-fields input { box-sizing: border-box; width: 100%; min-width: 0; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.src-fields input:focus { outline: none; border-color: var(--accent-color); }
.src-note-input { grid-column: 1 / -1; }
.src-ops { display: flex; gap: 2px; }
.src-ops button, .src-add { padding: 4px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.src-ops button:disabled { opacity: .4; cursor: default; }
.src-del:hover { color: var(--danger, #d9534f); border-color: currentColor; }
.src-add { padding: 6px 14px; }
@media (max-width: 640px) { .src-fields { grid-template-columns: 1fr; } }
</style>
