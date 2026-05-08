<template>
  <transition name="panel-slide">
    <aside v-if="modelValue" class="spirit-history-panel">
      <div class="panel-header">
        <div class="header-main">
          <h3>版本历史</h3>
          <button class="close-btn" @click="$emit('update:modelValue', false)">✕</button>
        </div>
        <p class="header-sub">最多保留最近 20 份快照</p>
      </div>

      <div class="panel-body">
        <div v-if="loading" class="list-loading">
          <div class="mini-spinner"></div>
          <span>感应时间线...</span>
        </div>

        <div v-else-if="historyList.length > 0" class="history-list">
          <div 
            v-for="(rev, index) in historyList" 
            :key="rev.id" 
            class="history-card"
            @click="handleRollback(rev)"
          >
            <div class="card-icon">🕒</div>
            <div class="card-content">
              <div class="rev-time">{{ formatTime(rev.createdAt) }}</div>
              <div class="rev-remark">{{ rev.remark || '自动归流' }}</div>
              <div class="rev-tag" v-if="index === 0">最新版本</div>
            </div>
            <div class="rev-action-hint">恢复</div>
          </div>
        </div>

        <div v-else class="list-empty">
          <span class="empty-icon">🕳️</span>
          <p>此碎片尚未留下时间印记</p>
        </div>
      </div>

      <div class="panel-footer">
        <button class="manual-save-btn" @click="handleManualSave">
          <span class="plus">+</span> 固化当前版本
        </button>
      </div>
    </aside>
  </transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { lingmaiApi } from '../../../api/lingmai';

const props = defineProps<{
  modelValue: boolean; // 控制面板显示
  noteId: string;
}>();

const emit = defineEmits(['update:modelValue', 'rollback', 'manual-save']);

const historyList = ref<any[]>([]);
const loading = ref(false);

// 格式化时间
const formatTime = (dateStr: string) => {
  const d = new Date(dateStr);
  return `${d.getMonth() + 1}月${d.getDate()}日 ${d.getHours()}:${d.getMinutes().toString().padStart(2, '0')}`;
};

// 获取历史
const fetchHistory = async () => {
  if (!props.noteId) return;
  loading.value = true;
  try {
    const res: any = await lingmaiApi.getHistoryList(props.noteId);
    historyList.value = res;
  } finally {
    loading.value = false;
  }
};

// 监听打开动作
watch(() => props.modelValue, (val) => {
  if (val) fetchHistory();
});

const handleRollback = (rev: any) => {
  if (confirm('确定要穿梭回此版本吗？当前未同步的改动将被覆盖。')) {
    emit('rollback', rev);
  }
};

const handleManualSave = () => {
  emit('manual-save');
};
</script>

<style scoped>
.spirit-history-panel {
  position: fixed;
  top: 0; right: 0; bottom: 0;
  width: 320px;
  background: #ffffff;
  border-left: 1px solid #f2f2f2;
  box-shadow: -10px 0 30px rgba(0,0,0,0.05);
  z-index: 3000;
  display: flex;
  flex-direction: column;
}

.panel-header { padding: 24px 20px; border-bottom: 1px solid #f9f9f9; }
.header-main { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.header-main h3 { font-size: 16px; font-weight: 700; color: #1d1d1f; }
.close-btn { background: none; border: none; font-size: 18px; color: #d2d2d7; cursor: pointer; }
.header-sub { font-size: 11px; color: #86868b; text-transform: uppercase; letter-spacing: 0.05em; }

.panel-body { flex: 1; overflow-y: auto; padding: 12px; }

.history-card {
  display: flex; gap: 12px; padding: 12px;
  border-radius: 12px; cursor: pointer; transition: all 0.2s;
  position: relative; border: 1px solid transparent;
}
.history-card:hover { background: #f5f5f7; border-color: #f0f0f0; }

.card-icon { font-size: 18px; opacity: 0.5; }
.rev-time { font-size: 13px; font-weight: 600; color: #1d1d1f; }
.rev-remark { font-size: 11px; color: #86868b; margin-top: 2px; }
.rev-tag { 
  display: inline-block; margin-top: 6px; padding: 2px 6px; 
  background: #eef7ff; color: #0066cc; font-size: 10px; border-radius: 4px; font-weight: 600;
}

.rev-action-hint {
  position: absolute; right: 12px; top: 12px;
  font-size: 11px; color: #0066cc; font-weight: 600;
  opacity: 0; transform: translateX(5px); transition: all 0.2s;
}
.history-card:hover .rev-action-hint { opacity: 1; transform: translateX(0); }

.panel-footer { padding: 20px; border-top: 1px solid #f9f9f9; }
.manual-save-btn {
  width: 100%; padding: 12px; background: #1d1d1f; color: white;
  border: none; border-radius: 10px; font-size: 13px; font-weight: 600;
  cursor: pointer; display: flex; align-items: center; justify-content: center; gap: 8px;
}

/* 动画 */
.panel-slide-enter-active, .panel-slide-leave-active { transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
.panel-slide-enter-from, .panel-slide-leave-to { transform: translateX(100%); }

.mini-spinner {
  width: 16px; height: 16px; border: 2px solid #f3f3f3;
  border-top: 2px solid #0066cc; border-radius: 50%;
  animation: spin 1s linear infinite;
}
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>