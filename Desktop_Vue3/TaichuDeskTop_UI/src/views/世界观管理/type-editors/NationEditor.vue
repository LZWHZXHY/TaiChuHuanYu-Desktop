<!-- src/views/世界观管理/type-editors/NationEditor.vue -->
<template>
  <div class="nation-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>政体</label>
          <el-select v-model="localData.government" filterable allow-create placeholder="选择或输入政体">
            <el-option
              v-for="g in ['君主制', '共和制', '民主制', '神权制', '独裁制', '联邦制', '部落制']"
              :key="g"
              :label="g"
              :value="g"
            />
          </el-select>
        </div>

        <div class="field-group">
          <label>首都</label>
          <input v-model="localData.capital" placeholder="输入首都名称" />
        </div>

        <div class="field-group">
          <label>人口</label>
          <input v-model.number="localData.population" type="number" placeholder="如：2500000" min="0" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>建国时间</label>
          <input v-model="localData.foundedDate" type="date" />
        </div>

        <div class="field-group">
          <label>国家格言</label>
          <input v-model="localData.motto" placeholder="如：为了联盟！" />
        </div>

        <div class="empty-hint">此处可放置更多设定</div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」按钮，可以插入「统治者」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { NationData } from '../card_type'

const props = defineProps<{
  modelValue?: NationData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: NationData): void
}>()

// ❌ 删除了 ruler 字段
const defaultData: NationData = {
  id: '',
  projectId: '',
  type: 'nation',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  government: '',
  capital: '',
  population: undefined,
  foundedDate: '',
  motto: '',
}

const localData = ref<NationData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.nation-editor {
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
  padding: 16px;
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