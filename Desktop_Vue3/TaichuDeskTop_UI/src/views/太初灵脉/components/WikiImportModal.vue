<template>
  <transition name="fade">
    <div v-if="modelValue" class="loading-overlay" @click.self="closeModal">
      <div class="spirit-modal-content pop-enter-active workbench-skin editor-surface community-surface tone-library">
        <h3 class="modal-title">📚 导入百科词条</h3>
        <input 
          v-model="importWikiId" 
          type="text" 
          class="spirit-id-input" 
          placeholder="输入词条 ID" 
          @keyup.enter="handleConfirm" 
          autofocus 
        />
        <div class="modal-actions">
          <button class="cancel-btn" @click="closeModal">取消</button>
          <button class="save-btn" @click="handleConfirm" :disabled="!importWikiId.trim()">导入</button>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';

const props = defineProps<{ modelValue: boolean }>();
const emit = defineEmits(['update:modelValue', 'confirm']);

const importWikiId = ref('');

watch(() => props.modelValue, (newVal) => {
  if (newVal) importWikiId.value = '';
});

const closeModal = () => emit('update:modelValue', false);
const handleConfirm = () => {
  if (!importWikiId.value.trim()) return;
  emit('confirm', importWikiId.value.trim());
};
</script>

<style scoped>
/* 这里放你原来 index.vue 里关于弹窗的样式，比如 .spirit-modal-content 等 */
.loading-overlay { position: fixed; inset: 0; background: var(--paper-card); backdrop-filter: blur(10px); z-index: 9999; display: flex; align-items: center; justify-content: center; }
.spirit-modal-content { background: var(--paper-card); width: 90%; max-width: 400px; padding: 30px; border-radius: 0; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1); text-align: center; position: relative; z-index: 10000; }
.modal-title { font-size: 1.2rem; font-weight: 600; color: var(--text-primary); margin: 0 0 8px; }
.spirit-id-input { width: 100%; padding: 12px 16px; border: 1px solid var(--border-line); border-radius: 0; font-size: 14px; margin-bottom: 24px; outline: none; transition: all 0.2s; box-sizing: border-box; }
.spirit-id-input:focus { border-color: var(--accent-color); box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.1); }
.modal-actions { display: flex; justify-content: flex-end; gap: 12px; }
.cancel-btn { background: var(--bg-sub); border: none; padding: 8px 20px; border-radius: 0; color: var(--text-primary); font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.cancel-btn:hover { background: var(--bg-sub); }
.save-btn { background: var(--accent-color); border: none; padding: 8px 20px; border-radius: 0; color: var(--on-accent); font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.save-btn:disabled { opacity: 0.5; cursor: not-allowed; }
</style>