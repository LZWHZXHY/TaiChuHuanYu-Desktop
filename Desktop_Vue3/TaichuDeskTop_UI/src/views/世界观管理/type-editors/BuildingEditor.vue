<!-- src/views/世界观管理/type-editors/BuildingEditor.vue -->
<template>
  <div class="building-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>所在位置</label>
          <input v-model="localData.location" placeholder="如：暴风城、艾尔文森林" />
        </div>

        <div class="field-group">
          <label>建造时间</label>
          <input v-model="localData.builtDate" type="date" />
        </div>

        <div class="field-group">
          <label>用途</label>
          <input v-model="localData.purpose" placeholder="如：军事要塞、宗教圣地" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>建筑风格</label>
          <input v-model="localData.style" placeholder="如：哥特式、巴洛克、古典" />
        </div>

        <div class="field-group">
          <label>层数</label>
          <input v-model.number="localData.floors" type="number" placeholder="如：5" min="0" />
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」按钮，可以插入「建造者」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { BuildingData } from '../card_type'

const props = defineProps<{
  modelValue?: BuildingData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: BuildingData): void
}>()

// ❌ 删除了 builder 字段
const defaultData: BuildingData = {
  id: '',
  projectId: '',
  type: 'building',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  location: '',
  builtDate: '',
  purpose: '',
  style: '',
  floors: undefined,
}

const localData = ref<BuildingData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.building-editor {
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.editor-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 32px;
}

.field-group {
  margin-bottom: 16px;
}
.field-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  margin-bottom: 4px;
}
.field-group input {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
}

.editor-hint {
  margin-top: 12px;
  padding: 8px 12px;
  background: #f0f4ff;
  border-radius: 6px;
  font-size: 12px;
  color: #4f46e5;
  border: 1px solid #c7d2fe;
}
.editor-hint p {
  margin: 0;
}
</style>