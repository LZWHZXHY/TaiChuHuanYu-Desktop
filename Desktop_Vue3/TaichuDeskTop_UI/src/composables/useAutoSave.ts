// src/composables/useAutoSave.ts
import { debounce } from 'lodash-es';
import { lingmaiApi } from '@/api/lingmai';

// 🌟 核心：在模块作用域下创建 debounce 实例。
// 无论有多少个组件调用 syncToCloud，它们触发的都是这同一个计时器。
const debouncedSync = debounce(async (noteId: string, payload: any, showToast?: Function) => {
  try {
    await lingmaiApi.updateNoteContent(noteId, payload);
    if (showToast) {
      showToast('☁️ 已自动同步', 1500);
    }
  } catch (e) {
    console.error('同步失败', e);
    if (showToast) {
      showToast('❌ 同步失败', 2000);
    }
  }
}, 2000);

export function useAutoSave() {
  const syncToCloud = (noteId: string, payload: any, showToast?: Function) => {
    debouncedSync(noteId, payload, showToast);
  };

  return { syncToCloud };
}