<!-- src/views/世界观管理/type-editors/OrganizationEditor.vue -->
<template>
  <div class="organization-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>宗旨/目标</label>
          <input v-model="localData.purpose" placeholder="如：维护世界和平" />
        </div>

        <div class="field-group">
          <label>总部</label>
          <input v-model="localData.headquarters" placeholder="总部所在地" />
        </div>

        <div class="field-group">
          <label>成立时间</label>
          <input v-model="localData.foundedDate" type="date" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>规模</label>
          <div class="size-options">
            <button
              v-for="s in ['小型', '中型', '大型', '巨型']"
              :key="s"
              class="size-btn"
              :class="{ active: localData.size === s }"
              @click="localData.size = s"
            >
              {{ s }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」按钮，可以插入「领袖」和「成员」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { OrganizationData } from '../card_type'

const props = defineProps<{
  modelValue?: OrganizationData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: OrganizationData): void
}>()

// ❌ 删除了 leader 和 members 字段
const defaultData: OrganizationData = {
  id: '',
  projectId: '',
  type: 'organization',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  purpose: '',
  headquarters: '',
  foundedDate: '',
  size: undefined,
}

const localData = ref<OrganizationData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.organization-editor {
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

.size-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.size-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
}
.size-btn.active {
  background: #4f46e5;
  color: white;
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