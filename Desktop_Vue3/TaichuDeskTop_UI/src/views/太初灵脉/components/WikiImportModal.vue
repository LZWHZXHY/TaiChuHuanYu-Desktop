<template>
  <transition name="fade">
    <div v-if="modelValue" class="loading-overlay" @click.self="closeModal">
      <div class="spirit-modal-content pop-enter-active">
        <h3 class="modal-title">🌌 接入百科宇宙</h3>
        <input 
          v-model="importWikiId" 
          type="text" 
          class="spirit-id-input" 
          placeholder="请输入 Wiki ID..." 
          @keyup.enter="handleConfirm" 
          autofocus 
        />
        <div class="modal-actions">
          <button class="cancel-btn" @click="closeModal">取消</button>
          <button class="save-btn" @click="handleConfirm" :disabled="!importWikiId.trim()">开始感应</button>
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
.loading-overlay { position: fixed; inset: 0; background: rgba(255, 255, 255, 0.9); backdrop-filter: blur(10px); z-index: 9999; display: flex; align-items: center; justify-content: center; }
.spirit-modal-content { background: #ffffff; width: 90%; max-width: 400px; padding: 30px; border-radius: 16px; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1); text-align: center; position: relative; z-index: 10000; }
.modal-title { font-size: 1.2rem; font-weight: 600; color: #1d1d1f; margin: 0 0 8px; }
.spirit-id-input { width: 100%; padding: 12px 16px; border: 1px solid #d2d2d7; border-radius: 10px; font-size: 14px; margin-bottom: 24px; outline: none; transition: all 0.2s; box-sizing: border-box; }
.spirit-id-input:focus { border-color: #0066cc; box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.1); }
.modal-actions { display: flex; justify-content: flex-end; gap: 12px; }
.cancel-btn { background: #f5f5f7; border: none; padding: 8px 20px; border-radius: 40px; color: #1d1d1f; font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.cancel-btn:hover { background: #e5e5ea; }
.save-btn { background: #0066cc; border: none; padding: 8px 20px; border-radius: 40px; color: #ffffff; font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.save-btn:disabled { opacity: 0.5; cursor: not-allowed; }
</style>