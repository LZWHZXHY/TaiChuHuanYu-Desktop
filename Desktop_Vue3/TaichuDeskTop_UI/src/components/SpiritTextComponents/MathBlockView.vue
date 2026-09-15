<template>
  <node-view-wrapper class="math-block-wrapper">
    <div v-if="!isEditing" class="math-block-display" @click="startEdit">
      <span v-if="!node.attrs.latex" class="math-placeholder">点击输入公式</span>
      <span v-else v-html="renderedHtml"></span>
    </div>
    <div v-else class="math-block-editor">
      <textarea
        ref="inputRef"
        v-model="draft"
        @blur="commit"
        @keydown.esc.prevent="cancel"
        @keydown.meta.enter.prevent="commit"
        @keydown.ctrl.enter.prevent="commit"
        placeholder="输入 LaTeX，例如 \int_0^1 x^2 \, dx"
        rows="2"
      />
    </div>
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
const inputRef = ref<HTMLTextAreaElement | null>(null)

const renderedHtml = computed(() => {
  const latex = props.node.attrs.latex || ''
  if (!latex) return ''
  try {
    return katex.renderToString(latex, {
      displayMode: true,
      throwOnError: false,
      errorColor: '#ff3b30',
    })
  } catch (e) {
    return '<span style="color:#ff3b30">公式错误</span>'
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
.math-block-wrapper {
  margin: 12px 0;
}
.math-block-display {
  display: block;
  padding: 12px 16px;
  background: #fbfbfd;
  border-radius: 8px;
  border: 1px solid transparent;
  cursor: text;
  min-height: 40px;
  text-align: center;
  transition: all 0.15s;
}
.math-block-display:hover {
  border-color: #e5e5ea;
  background: #ffffff;
}
.math-placeholder {
  color: #c7c7cc;
  font-style: italic;
  font-size: 13px;
}
.math-block-editor {
  padding: 12px 16px;
  background: #ffffff;
  border: 1px solid #0066cc;
  border-radius: 8px;
  box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.08);
}
.math-block-editor textarea {
  width: 100%;
  border: none;
  outline: none;
  font-family: 'SF Mono', Monaco, monospace;
  font-size: 14px;
  resize: none;
  background: transparent;
  box-sizing: border-box;
}
</style>