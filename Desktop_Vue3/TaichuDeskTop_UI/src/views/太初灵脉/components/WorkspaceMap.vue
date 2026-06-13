<template>
  <div class="workspace-map-frame">
    <header class="map-header">
      <input 
        :value="props.title" 
        @input="onTitleInput" 
        class="map-title-input" 
        placeholder="未命名地图 / Map Title" 
      />
      <p class="map-subtitle">俯视全局的纯平面坐标系</p>
    </header>

    <div class="map-container">
      <SpiritMap 
        :bg-url="currentBgUrl"
        :bg-bounds="currentBgBounds"
        @open-editor="handleNodeDoubleClick" 
        @update-map-bg="handleMapBgUpdate"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import SpiritMap from '@/components/SpiritMap.vue';
// 👇 引入你的数据方法，用于触发后端保存
import { useSpiritData } from '@/composables/useSpiritData'; 

const props = defineProps<{
  title: string;
  noteId: string;
  extraData?: string;
}>();

const emit = defineEmits(['update:title', 'change', 'open-sub-drawer', 'update-extraData']);

// 拿到保存数据的方法和当前的 notes 列表
const { notes, updateNoteContent } = useSpiritData();

// 🌟 解析当前地图的 extraData，提取背景图数据
const parsedExtra = computed(() => {
  try {
    if (props.extraData && props.extraData !== "[]") {
      return JSON.parse(props.extraData);
    }
  } catch (e) {
    console.warn("解析地图 extraData 失败", e);
  }
  return {};
});

// 计算出当前的背景链接和边界，响应式地传给子组件
const currentBgUrl = computed(() => parsedExtra.value.bgUrl);
const currentBgBounds = computed(() => parsedExtra.value.bgBounds);

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

// 拦截地图组件里的双击事件，转发给 index.vue 呼出右侧抽屉
const handleNodeDoubleClick = (targetNoteId: string) => {
  emit('open-sub-drawer', targetNoteId);
};

// 🌟 核心：监听到用户上传新底图后，把数据写入 extraData 并保存到数据库
const handleMapBgUpdate = async (bgData: { url: string, bounds: any }) => {
  // 1. 组装新的 extraData，确保不会覆盖掉里面原有的其他数据
  const newExtraData = {
    ...parsedExtra.value,
    bgUrl: bgData.url,
    bgBounds: bgData.bounds
  };
  
  const extraString = JSON.stringify(newExtraData);
  
  // 2. 如果你的外层组件（比如 index.vue）需要响应式更新，抛出事件
  emit('update-extraData', extraString);
  
  // 3. 直接在这里触发保存到后端的逻辑
  const currentNote = notes.value.find(n => n.id === props.noteId);
  if (currentNote) {
    currentNote.extraData = extraString; // 更新前端状态
    updateNoteContent(props.noteId);     // 调用 API 保存到数据库
  }
};
</script>

<style scoped>
.workspace-map-frame { width: 100%; height: 100%; display: flex; flex-direction: column; background: #1d1d1f; }
.map-header { padding: 30px 40px 10px; background: #ffffff; border-bottom: 1px solid #f2f2f7; }
.map-title-input { width: 100%; font-size: 2.2rem; font-weight: 700; border: none; background: transparent; outline: none; }
.map-subtitle { font-size: 12px; color: #86868b; margin-top: 4px; }
.map-container { flex: 1; position: relative; width: 100%; }
</style>