<template>
  <div class="card-ref-textarea">
    <textarea
      ref="textarea"
      :value="draft"
      :placeholder="placeholder"
      :rows="rows"
      class="doc-desc-input"
      @input="onInput"
      @keydown="onKeydown"
      @blur="onBlur"
    ></textarea>

    <div v-if="dropdownOpen" class="ref-dropdown">
      <div class="ref-dropdown-hint">输入「@」或「[[」引用其它设定</div>
      <div
        v-for="(card, idx) in matches"
        :key="card.id"
        class="ref-item"
        :class="{ active: idx === activeIndex }"
        @mousedown.prevent="selectCard(card)"
        @mouseenter="activeIndex = idx"
      >
        <span class="ref-type">{{ getTypeLabel(card.type) }}</span>
        <span class="ref-title">{{ card.title }}</span>
      </div>
      <div v-if="!matches.length" class="ref-empty">没有匹配的设定</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import type { CardSummary } from '@/api/types/world'

const props = defineProps<{
  modelValue: string
  projectId: string
  excludeCardId?: string
  placeholder?: string
  rows?: number
}>()

const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()

const store = useWorldStore()
const textarea = ref<HTMLTextAreaElement | null>(null)
const draft = ref(props.modelValue)

const dropdownOpen = ref(false)
const triggerStart = ref(-1)
const activeIndex = ref(0)
const query = ref('')

const matches = ref<CardSummary[]>([])

watch(() => props.modelValue, (value) => {
  if (value !== draft.value) draft.value = value
})

function cards() {
  const list = store.cards.filter((c) => c.id !== props.excludeCardId)
  const q = query.value.trim().toLowerCase()
  return q ? list.filter((c) => c.title.toLowerCase().includes(q)) : list
}

function openDropdown() {
  dropdownOpen.value = true
  activeIndex.value = 0
  matches.value = cards().slice(0, 8)
}

function closeDropdown() {
  dropdownOpen.value = false
  triggerStart.value = -1
  query.value = ''
  matches.value = []
}

function detectTrigger() {
  const el = textarea.value
  if (!el) return
  const pos = el.selectionStart ?? draft.value.length
  const before = draft.value.slice(0, pos)

  const dbl = before.match(/\[\[([^\[\]\n]*)$/)
  if (dbl) {
    triggerStart.value = pos - dbl[1].length - 2
    query.value = dbl[1]
    openDropdown()
    return
  }
  const at = before.match(/@([^@\s\n]*)$/)
  if (at) {
    triggerStart.value = pos - at[1].length - 1
    query.value = at[1]
    openDropdown()
    return
  }
  closeDropdown()
}

function onInput(event: Event) {
  draft.value = (event.target as HTMLTextAreaElement).value
  emit('update:modelValue', draft.value)
  detectTrigger()
}

function onKeydown(event: KeyboardEvent) {
  if (!dropdownOpen.value) return
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    activeIndex.value = Math.min(activeIndex.value + 1, matches.value.length - 1)
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    activeIndex.value = Math.max(activeIndex.value - 1, 0)
  } else if (event.key === 'Enter') {
    event.preventDefault()
    const card = matches.value[activeIndex.value]
    if (card) selectCard(card)
  } else if (event.key === 'Escape') {
    event.preventDefault()
    closeDropdown()
  }
}

function onBlur() {
  // 延迟关闭，让 @mousedown.prevent 的选择先完成。
  setTimeout(closeDropdown, 120)
}

function selectCard(card: CardSummary) {
  const el = textarea.value
  if (!el || triggerStart.value < 0) return
  const token = `{CARD:${card.id}}`
  const pos = el.selectionStart ?? draft.value.length
  const next = draft.value.slice(0, triggerStart.value) + token + draft.value.slice(pos)
  draft.value = next
  emit('update:modelValue', next)
  closeDropdown()
  nextTick(() => {
    if (!el) return
    const caret = triggerStart.value + token.length
    el.setSelectionRange(caret, caret)
    el.focus()
  })
}

onMounted(() => { draft.value = props.modelValue })
</script>

<style scoped>
.card-ref-textarea { position: relative; }
.doc-desc-input {
  width: 100%;
  min-height: 96px;
  padding: 10px 12px;
  border: 1px solid var(--border-line);
  border-radius: 8px;
  font: inherit;
  font-size: 14px;
  line-height: 1.7;
  color: var(--text-primary);
  background: var(--paper-card);
  resize: vertical;
  box-sizing: border-box;
}
.doc-desc-input:focus {
  outline: none;
  border-color: var(--accent-color);
}
.ref-dropdown {
  position: absolute;
  left: 0;
  right: 0;
  top: 100%;
  margin-top: 4px;
  z-index: 100;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
  max-height: 240px;
  overflow-y: auto;
}
.ref-dropdown-hint {
  padding: 6px 12px;
  font-size: 11px;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-line);
}
.ref-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  cursor: pointer;
}
.ref-item.active,
.ref-item:hover {
  background: var(--accent-soft);
}
.ref-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 0 8px;
  border-radius: 4px;
  flex-shrink: 0;
}
.ref-title {
  font-size: 13px;
  color: var(--text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ref-empty {
  padding: 10px 12px;
  font-size: 12px;
  color: var(--text-secondary);
  text-align: center;
}
</style>
