<!-- src/views/世界观管理/type-editors/DeityEditor.vue -->
<template>
  <div class="deity-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>神职领域</label>
          <input v-model="localData.domain" placeholder="如：太阳、战争、智慧" />
        </div>

        <div class="field-group">
          <label>阵营</label>
          <el-select v-model="localData.alignment" filterable allow-create placeholder="选择或输入阵营">
            <el-option
              v-for="a in ['守序善良', '中立善良', '混乱善良', '守序中立', '绝对中立', '混乱中立', '守序邪恶', '中立邪恶', '混乱邪恶']"
              :key="a"
              :label="a"
              :value="a"
            />
          </el-select>
        </div>

        <div class="field-group">
          <label>象征</label>
          <input v-model="localData.symbol" placeholder="如：太阳、火焰、天平" />
        </div>

        <div class="field-group">
          <label>圣典</label>
          <input v-model="localData.holyBook" placeholder="如：圣光之书" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>状态</label>
          <div class="status-options">
            <button
              v-for="s in statusOptions"
              :key="s"
              class="status-btn"
              :class="{ active: localData.status === s }"
              @click="localData.status = s"
            >
              {{ s }}
            </button>
          </div>
        </div>

        <!-- 右列预留空位，保持布局对称 -->
        <div class="empty-hint">此处可放置更多设定</div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」按钮，可以插入「信徒」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { DeityData } from '../card_type'

const props = defineProps<{
  modelValue?: DeityData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: DeityData): void
}>()

// ❌ 删除了 followers 字段
const defaultData: DeityData = {
  id: '',
  projectId: '',
  type: 'deity',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  domain: '',
  alignment: '',
  symbol: '',
  holyBook: '',
  status: undefined,
}

const localData = ref<DeityData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const statusOptions = ['活跃', '沉睡', '陨落', '被遗忘'] as const

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.deity-editor {
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

.status-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.status-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.status-btn.active {
  background: #4f46e5;
  color: white;
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