<!-- 岁月史书「提到谁」：输入名字搜成员，点一下加进来；被 @ 的人会收到通知，在「和 TA 有关的」里能看到这一段。 -->
<template>
  <div class="hmp">
    <ul v-if="modelValue.length" class="hmp-chips" aria-label="已经提到的人">
      <li v-for="p in modelValue" :key="p.id" class="hmp-chip">
        @{{ p.username }}
        <button type="button" :aria-label="`不提 ${p.username} 了`" @click="remove(p.id)">×</button>
      </li>
    </ul>
    <div v-if="modelValue.length < max" class="hmp-search">
      <input v-model="keyword" type="search" maxlength="30" :placeholder="modelValue.length ? '再加一个…' : '输入名字找人，比如：老王'" aria-label="搜索要提到的成员"
        @keydown.enter.prevent="pickFirst" @keydown.down.prevent="move(1)" @keydown.up.prevent="move(-1)" @blur="onBlur" @focus="open = true" />
      <ul v-if="open && keyword.trim()" class="hmp-list" role="listbox">
        <li v-if="loading && !results.length" class="hmp-tip">正在找…</li>
        <li v-else-if="!results.length" class="hmp-tip">没有找到「{{ keyword.trim() }}」</li>
        <li v-for="(u, i) in results" :key="u.id" role="option" :aria-selected="i === active" class="hmp-option" :class="{ active: i === active }"
          @mousedown.prevent="add(u)" @mouseenter="active = i">
          <img v-if="u.avatar" :src="u.avatar" alt="" class="hmp-avatar" />
          <span v-else class="hmp-avatar hmp-avatar-empty" aria-hidden="true">{{ u.username.slice(0, 1) }}</span>
          <span class="hmp-name">{{ u.username }}</span>
          <small>Lv.{{ u.level }}</small>
        </li>
      </ul>
    </div>
    <small v-else class="hmp-tip">最多提到 {{ max }} 个人</small>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { membersApi, type MemberItem } from '@/api/members'

export interface MentionPerson { id: string; username: string }
const props = withDefaults(defineProps<{ modelValue: MentionPerson[]; max?: number }>(), { max: 20 })
const emit = defineEmits<{ 'update:modelValue': [value: MentionPerson[]] }>()

const keyword = ref('')
const found = ref<MemberItem[]>([])
const loading = ref(false)
const open = ref(false)
const active = ref(0)
const results = computed(() => found.value.filter(u => !props.modelValue.some(p => p.id === u.id)).slice(0, 8))

let timer = 0
let seq = 0
watch(keyword, v => {
  window.clearTimeout(timer)
  active.value = 0
  const q = v.trim()
  if (!q) { found.value = []; return }
  timer = window.setTimeout(async () => {
    const mine = ++seq
    loading.value = true
    try {
      const res = await membersApi.list({ q, sort: 'name', pageSize: 12 })
      if (mine === seq) found.value = res.items
    } catch { if (mine === seq) found.value = [] }
    finally { if (mine === seq) loading.value = false }
  }, 250)
})

function add(u: MemberItem) {
  if (props.modelValue.length >= props.max) return
  emit('update:modelValue', [...props.modelValue, { id: u.id, username: u.username }])
  keyword.value = ''
  found.value = []
}
function remove(id: string) { emit('update:modelValue', props.modelValue.filter(p => p.id !== id)) }
function pickFirst() { const u = results.value[active.value]; if (u) add(u) }
function move(d: number) { const n = results.value.length; if (n) active.value = (active.value + d + n) % n }
function onBlur() { window.setTimeout(() => { open.value = false }, 120) }
</script>

<style scoped>
.hmp { display: grid; gap: 6px; }
.hmp-chips { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; }
.hmp-chip { display: inline-flex; align-items: center; gap: 4px; padding: 2px 4px 2px 8px; background: var(--accent-soft); color: var(--accent-color); font-size: 13px; }
.hmp-chip button { width: 20px; height: 20px; border: 0; background: none; color: inherit; font-size: 15px; line-height: 1; cursor: pointer; }
.hmp-search { position: relative; }
.hmp-search input { width: 100%; box-sizing: border-box; padding: 8px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; }
.hmp-search input:focus { outline: none; border-color: var(--accent-color); }
/* 不用浮层：放在弹窗的滚动区里也不会被裁掉 */
.hmp-list { margin-top: 2px; max-height: 220px; overflow-y: auto; list-style: none; margin: 0; padding: 4px 0; background: var(--paper-card); border: 1px solid var(--border-line); box-shadow: var(--shadow-md, 0 8px 24px rgb(0 0 0 / .12)); }
.hmp-option { display: flex; align-items: center; gap: 8px; padding: 6px 10px; cursor: pointer; font-size: 14px; }
.hmp-option.active { background: var(--bg-sub); }
.hmp-option small { margin-left: auto; color: var(--text-secondary); font-size: 11px; }
.hmp-avatar { width: 24px; height: 24px; object-fit: cover; flex-shrink: 0; }
.hmp-avatar-empty { display: inline-flex; align-items: center; justify-content: center; background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; font-weight: 700; }
.hmp-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.hmp-tip { padding: 6px 10px; color: var(--text-secondary); font-size: 12px; list-style: none; }
</style>
