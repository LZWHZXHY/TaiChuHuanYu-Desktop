<!-- src/views/世界观管理/type-editors/LoreEditor.vue -->
<template>
  <div class="lore-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>分类</label>
          <el-select v-model="localData.category" filterable allow-create placeholder="选择或输入分类">
            <el-option
              v-for="c in ['传说', '历史', '文化', '宗教', '科技', '魔法', '神话']"
              :key="c"
              :label="c"
              :value="c"
            />
          </el-select>
        </div>

        <div class="field-group">
          <label>来源</label>
          <input v-model="localData.source" placeholder="如：古籍、口述、考古发现" />
        </div>
      </div>

      <div class="right-column">
        <!-- 右列留空，保持布局对称 -->
        <div class="empty-hint">此处可放置更多设定</div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+事件」按钮，可以插入「相关事件」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { LoreData } from '../card_type'

const props = defineProps<{
  modelValue?: LoreData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: LoreData): void
}>()

// ❌ 删除了 relatedEvents 字段
const defaultData: LoreData = {
  id: '',
  projectId: '',
  type: 'lore',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  category: '',
  source: '',
}

const localData = ref<LoreData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.lore-editor {
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

.empty-hint {
  padding: 20px;
  text-align: center;
  color: #c0c4cc;
  font-size: 13px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
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