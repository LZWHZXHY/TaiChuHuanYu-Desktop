<!-- src/views/世界观管理/世界观组件/DynamicForm.vue -->
<template>
  <div class="dynamic-form">
    <div v-for="field in fields" :key="field.key" class="df-field">
      <label class="df-label">
        {{ field.label }}
        <span v-if="field.required" class="df-required">*</span>
      </label>

      <!-- text -->
      <input
        v-if="field.type === 'text'"
        v-model="local[field.key]"
        type="text"
        :placeholder="field.placeholder"
        class="df-input"
      />

      <!-- textarea -->
      <textarea
        v-else-if="field.type === 'textarea'"
        v-model="local[field.key]"
        :placeholder="field.placeholder"
        :rows="3"
        class="df-textarea"
      />

      <!-- number -->
      <input
        v-else-if="field.type === 'number'"
        v-model.number="local[field.key]"
        type="number"
        :placeholder="field.placeholder"
        :min="field.min"
        class="df-input"
      />

      <!-- date -->
      <input
        v-else-if="field.type === 'date'"
        v-model="local[field.key]"
        type="date"
        class="df-input"
      />

      <!-- select -->
      <select
        v-else-if="field.type === 'select'"
        v-model="local[field.key]"
        class="df-input"
      >
        <option value="">请选择</option>
        <option v-for="opt in field.options" :key="opt" :value="opt">
          {{ opt }}
        </option>
      </select>

      <!-- button-group -->
      <div v-else-if="field.type === 'button-group'" class="df-button-group">
        <button
          v-for="opt in field.options"
          :key="opt"
          type="button"
          class="df-button"
          :class="{ active: local[field.key] === opt }"
          @click="local[field.key] = opt"
        >
          {{ opt }}
        </button>
      </div>

      <!-- tags -->
      <div v-else-if="field.type === 'tags'" class="df-tags">
        <div class="df-tags-input">
          <input
            v-model="tagInputs[field.key]"
            :placeholder="`输入${field.label}，按回车添加`"
            @keydown.enter.prevent="addTag(field)"
          />
          <button type="button" @click="addTag(field)">添加</button>
        </div>
        <div class="df-tags-list">
          <span
            v-for="(tag, idx) in (local[field.key] || [])"
            :key="idx"
            class="df-tag"
          >
            {{ tag }}
            <button
              type="button"
              class="df-tag-remove"
              @click="removeTag(field, Number(idx))"
            >×</button>
          </span>
          <span v-if="!(local[field.key] || []).length" class="df-tag-empty">
            暂无
          </span>
        </div>
      </div>

      <!-- card-ref -->
      <select
        v-else-if="field.type === 'card-ref'"
        v-model="local[field.key]"
        class="df-input"
      >
        <option value="">请选择</option>
        <option v-for="card in candidateCards" :key="card.id" :value="card.id">
          {{ card.title }}
        </option>
      </select>

      <!-- custom -->
      <slot
        v-else-if="field.type === 'custom'"
        :name="field.custom"
        :field="field"
        :value="local[field.key]"
        :on-change="(v: any) => (local[field.key] = v)"
      >
        <div class="df-custom-placeholder">
          自定义字段：{{ field.custom }}（暂未实现渲染器）
        </div>
      </slot>

      <p v-if="field.hint" class="df-hint">{{ field.hint }}</p>
    </div>

    <div v-if="!fields.length" class="df-empty">
      该类型暂无专属字段，可通过「自定义属性」和「关联内容」扩展
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { ElMessage } from 'element-plus';
import { useWorldStore } from '@/stores/world';
import type { CardType } from '../card_type';
import { cardSchemas, type FieldSchema } from '../card-schemas';

const props = defineProps<{
  /** Content JSON 对象 */
  modelValue: Record<string, any>;
  /** 卡片类型 */
  type: CardType;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, any>): void;
}>();

const store = useWorldStore();

// 本地副本
const local = ref<Record<string, any>>({ ...props.modelValue });

// 标签输入的临时值
const tagInputs = ref<Record<string, string>>({});

// 当前类型的字段定义
const fields = computed<FieldSchema[]>(() => cardSchemas[props.type] || []);

// card-ref 的候选卡片（暂用全部卡片，未来可按类型过滤）
const candidateCards = computed(() => store.cards);

// ===== tags 操作 =====
const addTag = (field: FieldSchema) => {
  const text = (tagInputs.value[field.key] || '').trim();
  if (!text) return;

  const arr = local.value[field.key];
  const current = Array.isArray(arr) ? arr : [];

  if (current.includes(text)) {
    ElMessage.warning('已存在');
    return;
  }
  if (field.max && current.length >= field.max) {
    ElMessage.warning(`最多 ${field.max} 个`);
    return;
  }

  local.value[field.key] = [...current, text];
  tagInputs.value[field.key] = '';
};

const removeTag = (field: FieldSchema, idx: number) => {
  const arr = local.value[field.key];
  if (!Array.isArray(arr)) return;
  const next = [...arr];
  next.splice(idx, 1);
  local.value[field.key] = next;
};

// ===== 双向绑定 =====
watch(
  local,
  (val) => {
    emit('update:modelValue', val);
  },
  { deep: true }
);

watch(
  () => props.modelValue,
  (val) => {
    if (JSON.stringify(val) !== JSON.stringify(local.value)) {
      local.value = { ...val };
    }
  },
  { deep: true }
);

// 类型变化时重置
watch(
  () => props.type,
  () => {
    local.value = { ...props.modelValue };
    tagInputs.value = {};
  }
);
</script>

<style scoped>
.dynamic-form {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.df-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.df-label {
  font-size: 13px;
  font-weight: 500;
  color: #334155;
}
.df-required {
  color: #ef4444;
  margin-left: 2px;
}

.df-input,
.df-textarea {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 13px;
  font-family: inherit;
  background: #fafbfc;
  transition: border-color 0.2s, background 0.2s;
}
.df-input:focus,
.df-textarea:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.df-textarea {
  resize: vertical;
  min-height: 60px;
}

/* 按钮组 */
.df-button-group {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.df-button {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.15s;
}
.df-button:hover {
  background: #f1f5f9;
}
.df-button.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}

/* 标签 */
.df-tags {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.df-tags-input {
  display: flex;
  gap: 4px;
}
.df-tags-input input {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
  background: #fafbfc;
}
.df-tags-input input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.df-tags-input button {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.df-tags-input button:hover {
  background: #f1f5f9;
}
.df-tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  min-height: 20px;
}
.df-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px 2px 10px;
  background: #eef2ff;
  color: #4f46e5;
  border-radius: 12px;
  font-size: 12px;
}
.df-tag-remove {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
  padding: 0 2px;
}
.df-tag-remove:hover {
  color: #ef4444;
}
.df-tag-empty {
  font-size: 12px;
  color: #c0c4cc;
}

.df-hint {
  font-size: 11px;
  color: #94a3b8;
  margin: 2px 0 0;
  font-style: italic;
}

.df-custom-placeholder {
  padding: 12px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
  text-align: center;
  color: #94a3b8;
  font-size: 13px;
}

.df-empty {
  padding: 20px;
  text-align: center;
  color: #94a3b8;
  font-size: 13px;
}
</style>