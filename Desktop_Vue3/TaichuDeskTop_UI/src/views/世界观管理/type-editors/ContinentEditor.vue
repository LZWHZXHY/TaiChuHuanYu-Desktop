<!-- src/views/世界观管理/type-editors/ContinentEditor.vue -->
<template>
  <div class="continent-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>面积 (万km²)</label>
          <input v-model.number="localData.area" type="number" placeholder="如：4500" min="0" />
        </div>

        <div class="field-group">
          <label>总人口</label>
          <input v-model.number="localData.population" type="number" placeholder="如：120000000" min="0" />
        </div>

        <div class="field-group">
          <label>气候特征</label>
          <input v-model="localData.climate" placeholder="如：温带大陆性气候" />
        </div>

        <div class="field-group">
          <label>显著特征</label>
          <div class="tag-input-wrapper">
            <input
              v-model="featureInput"
              placeholder="输入显著特征，按回车添加"
              @keydown.enter.prevent="addFeature"
            />
            <button class="add-tag-btn" @click="addFeature">添加</button>
          </div>
          <div class="tag-list">
            <span v-for="f in localData.notableFeatures" :key="f" class="tag-item feature">
              {{ f }}
              <button class="remove-tag" @click="removeFeature(f)">×</button>
            </span>
          </div>
        </div>
      </div>

      <div class="right-column">
        <!-- 右列留空，保持布局对称 -->
        <div class="empty-hint">此处可放置更多设定</div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+国家」按钮，可以插入「国家」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { ContinentData } from '../card_type'

const props = defineProps<{
  modelValue?: ContinentData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: ContinentData): void
}>()

const featureInput = ref('')

// ❌ 删除了 countries 字段
const defaultData: ContinentData = {
  id: '',
  projectId: '',
  type: 'continent',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  area: undefined,
  population: undefined,
  climate: '',
  notableFeatures: [],
}

const localData = ref<ContinentData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const addFeature = () => {
  const text = featureInput.value.trim()
  if (!text) return
  if (localData.value.notableFeatures?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.notableFeatures) localData.value.notableFeatures = []
  localData.value.notableFeatures.push(text)
  featureInput.value = ''
}

const removeFeature = (f: string) => {
  if (!localData.value.notableFeatures) return
  localData.value.notableFeatures = localData.value.notableFeatures.filter(item => item !== f)
}

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.continent-editor {
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

.tag-input-wrapper {
  display: flex;
  gap: 4px;
}
.tag-input-wrapper input {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
}
.tag-input-wrapper input:focus {
  outline: none;
  border-color: #4f46e5;
}
.add-tag-btn {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  cursor: pointer;
}
.add-tag-btn:hover {
  background: #f1f5f9;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
}
.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: #eef2ff;
  color: #4f46e5;
  padding: 2px 8px 2px 10px;
  border-radius: 12px;
  font-size: 12px;
}
.tag-item.feature {
  background: #fce7f3;
  color: #db2777;
}
.remove-tag {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
}
.remove-tag:hover {
  color: #ef4444;
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