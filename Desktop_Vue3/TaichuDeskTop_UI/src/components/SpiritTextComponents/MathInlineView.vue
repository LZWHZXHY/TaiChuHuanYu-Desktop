<template>
  <node-view-wrapper as="span" class="math-inline-wrapper">
    <span v-if="!isEditing" class="math-inline-display" @click="startEdit">
      <span v-if="!node.attrs.latex" class="math-inline-placeholder">ƒ</span>
      <span v-else v-html="renderedHtml"></span>
    </span>
    <span v-else class="math-inline-editor">
      <input
        ref="inputRef"
        v-model="draft"
        @blur="commit"
        @keydown.enter.prevent="commit"
        @keydown.esc.prevent="cancel"
        placeholder="LaTeX"
      />
    </span>
  </node-view-wrapper>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'
import { NodeViewWrapper, nodeViewProps } from '@tiptap/vue-3'
import katex from 'katex'
import 'katex/dist/katex.min.css'

const props = defineProps(nodeViewProps)

const isEditing = ref(false)
const draft = ref('')
const inputRef = ref<HTMLInputElement | null>(null)

const renderedHtml = computed(() => {
  const latex = props.node.attrs.latex || ''
  if (!latex) return ''
  try {
    return katex.renderToString(latex, {
      displayMode: false,
      throwOnError: false,
      errorColor: '#ff3b30',
    })
  } catch (e) {
    return '<span style="color:#ff3b30">?</span>'
  }
})

const startEdit = () => {
  draft.value = props.node.attrs.latex || ''
  isEditing.value = true
  nextTick(() => {
    inputRef.value?.focus()
    inputRef.value?.select()
  })
}

const commit = () => {
  if (!isEditing.value) return
  isEditing.value = false
  props.updateAttributes({ latex: draft.value.trim() })
}

const cancel = () => {
  isEditing.value = false
}
</script>

<style scoped>
.math-inline-wrapper {
  display: inline-block;
  vertical-align: middle;
}
.math-inline-display {
  padding: 0 4px;
  border-radius: 4px;
  cursor: text;
  transition: background 0.15s;
}
.math-inline-display:hover {
  background: rgba(0, 102, 204, 0.06);
}
.math-inline-placeholder {
  color: #0066cc;
  font-style: italic;
  font-weight: 700;
}
.math-inline-editor input {
  border: 1px solid #0066cc;
  border-radius: 4px;
  padding: 2px 6px;
  outline: none;
  font-family: 'SF Mono', Monaco, monospace;
  font-size: 13px;
  min-width: 100px;
}
</style>