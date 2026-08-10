<!-- src/views/世界观管理/type-editors/EcologyEditor.vue -->
<template>
  <div class="ecology-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>环境类型</label>
          <el-select v-model="localData.environment" filterable allow-create placeholder="选择或输入环境类型">
            <el-option
              v-for="e in ['森林', '草原', '沙漠', '海洋', '湿地', '山地', '极地', '地下']"
              :key="e"
              :label="e"
              :value="e"
            />
          </el-select>
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>气候模式</label>
          <input v-model="localData.climatePattern" placeholder="如：四季分明、干旱少雨" />
        </div>

        <div class="field-group">
          <label>食物链描述</label>
          <textarea v-model="localData.foodChain" rows="3" placeholder="描述生态系统的食物链..." class="foodchain-textarea" />
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+物种」按钮，可以插入「主要物种」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { EcologyData } from '../card_type'

const props = defineProps<{
  modelValue?: EcologyData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: EcologyData): void
}>()

// ❌ 删除了 species 字段
const defaultData: EcologyData = {
  id: '',
  projectId: '',
  type: 'ecology',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  environment: '',
  foodChain: '',
  climatePattern: '',
}

const localData = ref<EcologyData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.ecology-editor {
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
.field-group input,
.field-group .el-select {
  width: 100%;
}
.field-group input {
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
}

.foodchain-textarea {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-family: inherit;
  resize: vertical;
  min-height: 60px;
}
.foodchain-textarea:focus {
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