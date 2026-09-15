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
import { computed, ref, watch } from 'vue';
import SpiritMap from '@/components/SpiritMap.vue';
import { useSpiritData } from '@/composables/useSpiritData'; 

const props = defineProps<{
  title: string;
  noteId: string;
  blocks?: any[];     // 🌟 接收拉取出来的区块数据
  extraData?: string; // 🌟 松绑对齐：纯净释放给右侧属性面板使用
}>();

const emit = defineEmits(['update:title', 'change', 'open-sub-drawer']);

const { activeNote } = useSpiritData();

// 本地响应式底图状态
const currentBgUrl = ref('');
const currentBgBounds = ref<any>(null);

// 🌟 自治修复：从 blocks 积木池中精准过滤提炼出地图底图的配置数据
const loadMapMetaFromBlocks = () => {
  if (!props.blocks || !Array.isArray(props.blocks)) return;
  
  const mapLayoutBlock = props.blocks.find(b => b.type === 'map-layout-block');
  if (mapLayoutBlock?.data) {
    try {
      const parsed = JSON.parse(mapLayoutBlock.data);
      currentBgUrl.value = parsed.bgUrl || '';
      currentBgBounds.value = parsed.bgBounds || null;
    } catch (e) {
      console.warn("解析地图数据块异常", e);
    }
  }
};

// 监听外界区块传入，同步还原地图底图
watch(() => props.blocks, () => { loadMapMetaFromBlocks(); }, { immediate: true, deep: true });

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

const handleNodeDoubleClick = (targetNoteId: string) => {
  emit('open-sub-drawer', targetNoteId);
};

// 🌟 自治核心：监听到上传新底图后，组装成 map-layout-block，通过 change 标准协议安全上报
const handleMapBgUpdate = async (bgData: { url: string, bounds: any }) => {
  currentBgUrl.value = bgData.url;
  currentBgBounds.value = bgData.bounds;

  // 1. 将底图坐标信息包装为地图专有形态的元积木块
  const mapLayoutData = {
    bgUrl: bgData.url,
    bgBounds: bgData.bounds
  };

  const mapMetaBlock = {
    id: `map_layout_block_${props.noteId}`,
    ownerId: props.noteId,
    ownerType: 'map',
    type: 'map-layout-block', // 地图布局特态标识
    data: JSON.stringify(mapLayoutData),
    sortOrder: 0
  };

  // 2. 预留：过滤掉原有的旧布局块，防止重复堆叠（未来如果地图有图标打点等其他 type，可以在此进行 filter 过滤合流）
  const otherMapBlocks = (props.blocks || []).filter(b => b.type !== 'map-layout-block');

  const fullBlocks = [mapMetaBlock, ...otherMapBlocks];

  // 3. 强行刷新共享缓冲区及本地主内存，维持状态同步闭环
  if (activeNote.value) {
    activeNote.value.blocks = fullBlocks;
  }

  // 4. 标准格式协议上报给主控层 index.vue 去统一执行防抖自动保存
  emit('change', { blocks: fullBlocks });
};
</script>

<style scoped>
.workspace-map-frame { width: 100%; height: 100%; display: flex; flex-direction: column; background: #1d1d1f; }
.map-header { padding: 30px 40px 10px; background: #ffffff; border-bottom: 1px solid #f2f2f7; }
.map-title-input { width: 100%; font-size: 2.2rem; font-weight: 700; border: none; background: transparent; outline: none; }
.map-subtitle { font-size: 12px; color: #86868b; margin-top: 4px; }
.map-container { flex: 1; position: relative; width: 100%; }
</style>