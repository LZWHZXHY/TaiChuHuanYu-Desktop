<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-modal-overlay" @click.self="$emit('update:modelValue', false)">
      <div class="md-modal-container">
        <header class="md-header">
          <div class="md-title">
            <h1>编织发布</h1>
            <span class="md-space-tag"># {{ spaceName }}</span>
          </div>
          <button class="md-close-btn" @click="$emit('update:modelValue', false)">ESC</button>
        </header>

        <div class="md-body">
          <p class="md-label">选择折射形态 / SELECT TYPE</p>
          <ul class="md-type-list">
            <li 
              v-for="opt in PUBLISH_OPTIONS" 
              :key="opt.type"
              :class="{ 'is-active': selectedType === opt.type }"
              @click="selectedType = opt.type"
            >
              <span class="md-radio-indicator"></span>
              <div class="md-type-content">
                <span class="md-type-title">{{ opt.title }}</span>
                <span class="md-type-desc">{{ opt.desc }}</span>
              </div>
            </li>
          </ul>
        </div>

        <footer class="md-footer">
          <div class="md-footer-line"></div>
          <div class="md-actions">
            <button class="md-btn-secondary" @click="$emit('update:modelValue', false)">放弃</button>
            <button 
              class="md-btn-primary" 
              :disabled="isProcessing"
              @click="handleConfirm"
            >
              {{ isProcessing ? '同步中...' : '确认发布' }}
            </button>
          </div>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { lingmaiApi } from '../../../api/lingmai';

const props = defineProps<{
  modelValue: boolean;
  noteId: string;
  spaceName: string;
  initialType?: string;
}>();

const emit = defineEmits(['update:modelValue', 'success']);

// 🌟 选项描述也改为了更书面、克制的文字

const PUBLISH_OPTIONS = [
  { type: 'note',    title: '随笔 (Blog)', desc: '深度记录，保留于知识骨架中。' },
  { type: 'thought', title: '简语 (Post)', desc: '瞬时灵感，不占据目录空间。' },
  { type: 'wiki',    title: '词条 (Wiki)', desc: '底层设定，作为世界观之基石。' },
  { type: 'char',    title: '人物 (Char)', desc: '角色档案，包含生命数值映射。' },
  // 🎨 新增：艺术画廊展厅选项
  { type: 'art', title: '画廊 (Gallery)', desc: '视觉呈现，将意象物理同步至艺术展厅。' }
];

const selectedType = ref(props.initialType || 'note');
const isProcessing = ref(false);

watch(() => props.initialType, (val) => {
  if (val) selectedType.value = val;
});

const handleConfirm = async () => {
  if (!props.noteId) return;
  isProcessing.value = true;
  try {
    // 🌟 物理同步至发布表
    await lingmaiApi.publishNote(props.noteId, selectedType.value);
    emit('success', selectedType.value);
    emit('update:modelValue', false);
  } catch (err) {
    console.error('发布异常', err);
  } finally {
    isProcessing.value = false;
  }
};
</script>

<style scoped>
/* 🌟 极致留白风格样式表 */
.md-modal-overlay {
  position: fixed; inset: 0; background: rgba(255, 255, 255, 0.95);
  z-index: 5000; display: flex; align-items: center; justify-content: center;
  backdrop-filter: blur(2px);
}

.md-modal-container {
  width: 100%; max-width: 500px; padding: 40px;
  background: transparent; color: #1a1a1a;
  font-family: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

/* 头部 */
.md-header {
  display: flex; justify-content: space-between; align-items: baseline;
  margin-bottom: 60px;
}
.md-title h1 { font-size: 24px; font-weight: 700; margin: 0; letter-spacing: -0.02em; }
.md-space-tag { font-size: 13px; color: #86868b; margin-top: 8px; display: block; font-family: monospace; }
.md-close-btn { 
  background: none; border: 1px solid #e5e5e5; padding: 4px 10px; 
  font-size: 10px; color: #c7c7cc; cursor: pointer; transition: all 0.2s;
}
.md-close-btn:hover { border-color: #000; color: #000; }

/* 列表主体 */
.md-label { font-size: 11px; font-weight: 700; color: #d2d2d7; letter-spacing: 0.1em; margin-bottom: 24px; }
.md-type-list { list-style: none; padding: 0; margin: 0; }
.md-type-list li {
  display: flex; align-items: flex-start; gap: 20px;
  padding: 20px 0; border-bottom: 1px solid #f2f2f2;
  cursor: pointer; transition: all 0.2s; opacity: 0.4;
}
.md-type-list li:hover { opacity: 0.8; }
.md-type-list li.is-active { opacity: 1; }

.md-radio-indicator {
  width: 12px; height: 12px; border: 1px solid #000; border-radius: 50%;
  margin-top: 4px; position: relative;
}
.is-active .md-radio-indicator::after {
  content: ''; position: absolute; inset: 2px;
  background: #000; border-radius: 50%;
}

.md-type-content { display: flex; flex-direction: column; gap: 4px; }
.md-type-title { font-size: 16px; font-weight: 600; }
.md-type-desc { font-size: 13px; color: #86868b; line-height: 1.5; }

/* 底部 */
.md-footer { margin-top: 60px; }
.md-footer-line { height: 1px; background: #eee; width: 40px; margin-bottom: 32px; }
.md-actions { display: flex; gap: 32px; align-items: center; }

.md-btn-secondary {
  background: none; border: none; font-size: 14px; color: #86868b;
  cursor: pointer; padding: 0;
}
.md-btn-secondary:hover { color: #ff3b30; }

.md-btn-primary {
  background: #000; color: #fff; border: none;
  padding: 10px 24px; font-size: 14px; font-weight: 600;
  cursor: pointer; transition: opacity 0.2s;
}
.md-btn-primary:hover { opacity: 0.8; }
.md-btn-primary:disabled { background: #d2d2d7; cursor: not-allowed; }

/* 动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>