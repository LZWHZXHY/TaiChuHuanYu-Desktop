<template>
  <transition name="drawer-slide">
    <aside v-if="modelValue" class="quick-editor-drawer">
      <header class="quick-drawer-header">
        <h4>沉浸编辑 <span class="sub-id">#{{ noteId.substring(0,6) }}</span></h4>
        <button class="close-drawer-btn" @click="$emit('update:modelValue', false)">✕</button>
      </header>
      <div class="quick-drawer-body">
        <div v-if="isLoading" class="content-loading-state">
          <div class="mini-spinner"></div>
          <p>抽取本体中...</p>
        </div>
        <SpiritEditor
          v-else
          ref="quickEditorRef"
          :key="noteId"
          @change="handleQuickEditorChange" 
        />
      </div>
    </aside>
  </transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import SpiritEditor from '@/components/SpiritText.vue';
import { lingmaiApi } from '@/api/lingmai'; // 🌟 必须引入 API

const props = defineProps<{
  modelValue: boolean;
  noteId: string;
  noteMeta: any;
  isLoading: boolean;
}>();

// 不再需要向外 emit 'change'，抽屉自己负责保存
defineEmits(['update:modelValue']); 

const quickEditorRef = ref();
let quickSyncTimer: any = null;

// 🌟 核心新增：抽屉自治！监听打开动作，自己把数据解析并塞给编辑器
watch(
  () => props.modelValue, 
  (isOpen) => {
    // 只有在抽屉打开，且没有在 loading 的时候才去设置内容
    if (isOpen && props.noteMeta && !props.isLoading) {
      setTimeout(() => {
        if (quickEditorRef.value && quickEditorRef.value.editor) {
          let contentToSet = { type: 'doc', content: [{ type: 'paragraph' }] };
          
          if (props.noteMeta.blocks && props.noteMeta.blocks.length > 0) {
            const parsedBlocks = props.noteMeta.blocks.map((b: any) => {
              try { return JSON.parse(b.data); } catch { return null; }
            }).filter((b: any) => b && b.type !== 'canvas-node' && b.type !== 'canvas-edge');

            if (parsedBlocks.length > 0) {
              contentToSet.content = parsedBlocks;
            }
          }
          quickEditorRef.value.editor.commands.setContent(contentToSet);
        }
      }, 100); // 稍微延迟等待编辑器实例挂载完成
    }
  }
);

// 处理自动保存（全部替换为 props.xxx）
const handleQuickEditorChange = (json: any) => {
  if (quickSyncTimer) clearTimeout(quickSyncTimer);
  quickSyncTimer = setTimeout(async () => {
     if (!props.noteId) return;
     
     let finalBlocks: any[] = [];
     if (json && json.content) {
        finalBlocks = json.content.map((b: any, i: number) => ({
          id: b.attrs?.id || Math.random().toString(36).substring(2, 11),
          ownerId: props.noteId,
          ownerType: props.noteMeta?.type || 'note',
          type: b.type,
          sortOrder: i,
          data: JSON.stringify(b)
        }));
     }
     
     try {
        const syncPayload = {
            noteId: props.noteId,
            title: props.noteMeta?.title || '',
            extraData: props.noteMeta?.extraData || '[]',
            tags: props.noteMeta?.tags || [],
            blocks: finalBlocks
        };
        await lingmaiApi.updateNoteContent(props.noteId, syncPayload); 
     } catch(e) {
        console.error("抽屉同步失败", e);
     }
  }, 2000);
};

// 依然暴露实例，防止其他地方偶尔需要
defineExpose({
  editor: quickEditorRef
});
</script>

<style scoped>
/* 样式保持不变 */
.quick-editor-drawer { position: absolute; top: 16px; right: 16px; bottom: 16px; width: 480px; background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(24px) saturate(180%); border-radius: 20px; box-shadow: -10px 0 40px rgba(0,0,0,0.08), 0 0 1px rgba(0,0,0,0.2); display: flex; flex-direction: column; z-index: 1000; overflow: hidden; }
.quick-drawer-header { padding: 18px 24px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(0,0,0,0.05); }
.quick-drawer-header h4 { margin: 0; font-size: 15px; color: #1d1d1f; font-weight: 700; }
.sub-id { color: #86868b; font-weight: 500; font-size: 12px; margin-left: 8px; background: #f2f2f7; padding: 2px 6px; border-radius: 6px; }
.close-drawer-btn { background: #f2f2f7; border: none; width: 28px; height: 28px; border-radius: 50%; font-size: 14px; cursor: pointer; color: #1d1d1f; display: flex; align-items: center; justify-content: center; transition: all 0.2s; }
.close-drawer-btn:hover { background: #e5e5ea; transform: scale(1.05); }
.quick-drawer-body { flex: 1; overflow-y: auto; padding: 24px; }
.drawer-slide-enter-active, .drawer-slide-leave-active { transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1), opacity 0.3s; }
.drawer-slide-enter-from, .drawer-slide-leave-to { transform: translateX(120%); opacity: 0; }
.content-loading-state { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; min-height: 200px; color: #86868b; gap: 12px; font-size: 13px; }
.mini-spinner { width: 24px; height: 24px; border: 2px solid #f2f2f7; border-top-color: #0066cc; border-radius: 50%; animation: spin 0.8s linear infinite; }
</style>