<template>
  <div class="wiki-category-manager">
    <div class="table-card">
      <table class="ink-table">
        <thead>
          <tr>
            <th>分类名称 (层级结构)</th>
            <th>ID</th>
            <th>排序</th>
            <th class="text-right">
              <button class="btn-add" @click="openAdd">＋ 新增</button>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in flattened" :key="item.id" :class="{'child-row': item.level > 0}">
            <td :style="{ paddingLeft: (item.level * 24 + 12) + 'px' }">
              <span v-if="item.level > 0" class="tree-line">↳</span>
              {{ item.name }}
            </td>
            <td class="mono">#{{ String(item.id).padStart(3, '0') }}</td>
            <td>{{ item.sortOrder }}</td>
            <td class="text-right actions">
              <button class="btn-s" @click="openEdit(item)">修订</button>
              <button class="btn-s danger" @click="confirmDelete(item)">抹除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <Teleport to="body">
      <div v-if="showModal" class="modal-mask" @mousedown="showModal = false">
        <div class="modal-container" @mousedown.stop>
          <h3>{{ isEdit ? '修订分类' : '开辟新分类' }}</h3>
          
          <label class="md-label">分类名称</label>
          <input v-model="form.name" class="md-input" placeholder="输入名称..." />

          <label class="md-label">归属父分类 (可选)</label>
          <select v-model="form.parentId" class="md-input">
            <option :value="null">无 (作为大分类)</option>
            <option v-for="cat in data" :key="cat.id" :value="cat.id" :disabled="cat.id === form.id">
              {{ cat.name }}
            </option>
          </select>

          <button class="btn-black" @click="submit">确认并同步</button>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { adminWikiApi } from '@/api/Admin';

const props = defineProps<{ data: any[] }>();
const emit = defineEmits(['refresh']);

const showModal = ref(false);
const isEdit = ref(false);
const form = ref({ id: 0, name: '', parentId: null as number | null, sortOrder: 0 });

const flattened = computed(() => {
  const result: any[] = [];
  const build = (parentId: number | null, level: number) => {
    props.data.filter(i => i.parentId === parentId).forEach(c => {
      result.push({ ...c, level });
      build(c.id, level + 1);
    });
  };
  build(null, 0);
  return result;
});

const openAdd = () => {
  isEdit.value = false;
  form.value = { id: 0, name: '', parentId: null, sortOrder: 0 };
  showModal.value = true;
};

const openEdit = (item: any) => {
  isEdit.value = true;
  form.value = { id: item.id, name: item.name, parentId: item.parentId, sortOrder: item.sortOrder };
  showModal.value = true;
};

const confirmDelete = async (item: any) => {
  if (confirm(`确定要抹除 [${item.name}] 吗？`)) {
    await adminWikiApi.deleteCategory(item.id);
    emit('refresh');
  }
};

const submit = async () => {
  isEdit.value 
    ? await adminWikiApi.updateCategory(form.value.id, form.value as any)
    : await adminWikiApi.createCategory(form.value as any);
  showModal.value = false;
  emit('refresh');
};
</script>

<style scoped>
@import './Wiki子组件风格.css';

.btn-add { background: #000; color: #fff; border: none; padding: 6px 12px; border-radius: 6px; font-size: 0.75rem; font-weight: 600; cursor: pointer; }
.tree-line { display: inline-block; width: 16px; color: #d1d1d6; margin-right: 4px; font-weight: bold; }
.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; justify-content: center; align-items: center; z-index: 9999; }
.modal-container { background: #fff; width: 400px; padding: 30px; border-radius: 12px; }
.md-label { display: block; font-size: 0.8rem; font-weight: 600; margin-bottom: 6px; color: #515154; }
.md-input { width: 100%; padding: 10px; margin-bottom: 16px; border: 1px solid #eee; border-radius: 6px; box-sizing: border-box; }
.btn-black { width: 100%; background: #000; color: #fff; padding: 12px; border: none; cursor: pointer; border-radius: 6px; }
</style>