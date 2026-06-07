<template>
  <div class="workspace-note">
    <input 
      :value="title" 
      @input="$emit('update:title', ($event.target as HTMLInputElement).value)"
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
defineProps<{
  title: string;
  readonly?: boolean;
}>();

defineEmits(['update:title']);
</script>

<style scoped>
.workspace-note {
  width: 100%;
  margin: 0 auto;
  background: #ffffff;
  padding: 60px;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.02);
}

.title-input {
  width: 100%; 
  border: none; 
  font-size: 3rem; 
  font-weight: 800;
  margin-bottom: 40px; 
  outline: none; 
  background: transparent;
  letter-spacing: -0.04em; 
  color: #1d1d1f;
}

@media (max-width: 1024px) {
  .workspace-note { padding: 20px; }
  .title-input { font-size: 2.2rem; }
}
</style>