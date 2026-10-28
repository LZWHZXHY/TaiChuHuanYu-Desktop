<template>
  <div class="workspace-note">
    <textarea rows="2" aria-label="文档标题"
      :value="title" 
      @input="$emit('update:title', ($event.target as HTMLTextAreaElement).value)"
      class="title-input" 
      placeholder="无标题随笔..." 
      spellcheck="false"
      :readonly="readonly"
    />

    <div class="editor-wrapper">
      <slot name="editor"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
// 🌟 契约对齐：显式声明接收主控层分发的 noteId、extraData、blocks（在这里不改写、不污染）
defineProps<{
  title: string;
  readonly?: boolean;
  noteId?: string;
  extraData?: string; // 纯净释放给右侧栏使用
  blocks?: any[];
}>();

defineEmits(['update:title']);
</script>

<style scoped>
.workspace-note {
  width: 100%;
  max-width: 900px; 
  margin: 0 auto;
  background: var(--paper-card);
  padding: 20px 0;
  container-type: inline-size;
  border-radius: 0;
  border-top: 2px solid var(--text-primary);
}

.title-input {
  width: 100%; 
  border: none; 
  font-size: clamp(24px, 5cqi, 36px);
  line-height: 1.4;
  resize: vertical;
  font-weight: 700;
  margin-bottom: 24px;
  outline: none; 
  background: transparent;
  letter-spacing: -0.04em; 
  color: var(--text-primary);
}

@media (max-width: 1024px) {
  .workspace-note { padding: 16px 0; }
}
</style>
