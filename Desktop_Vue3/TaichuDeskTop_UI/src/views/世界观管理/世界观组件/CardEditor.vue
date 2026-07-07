<template>
  <!-- 移除了 Teleport，改为直接渲染 div -->
  <Transition name="fade">
    <div v-if="visible" class="dialog-overlay" @click.self="close">
      <div class="dialog card-editor-dialog">
        <header class="dialog-header">
          <h2>{{ isEdit ? '编辑卡片' : '新建卡片' }}</h2>
          <button class="close-btn" @click="close">✕</button>
        </header>

        <div class="dialog-body">
          <!-- ===== 标题 ===== -->
          <div class="field">
            <label>标题 <span class="required">*</span></label>
            <input v-model="form.title" placeholder="给卡片起个名字" maxlength="100" />
          </div>

          <!-- ===== 封面图 ===== -->
          <div class="field">
            <label>封面图</label>
            <div class="cover-upload">
              <div v-if="form.coverImage" class="cover-preview">
                <img :src="form.coverImage" alt="封面图" />
                <button class="remove-cover" @click="form.coverImage = ''">×</button>
              </div>
              <div v-else class="cover-upload-area" @click="triggerFileInput">
                <span class="upload-icon">📷</span>
                <span class="upload-text">点击上传封面图</span>
                <span class="upload-hint">支持 JPG, PNG, WEBP</span>
              </div>
              <input
                ref="fileInput"
                type="file"
                accept="image/*"
                style="display: none"
                @change="handleFileUpload"
              />
              <div v-if="uploadingCover" class="upload-progress">
                <el-progress :percentage="uploadProgress" />
              </div>
            </div>
          </div>

          <!-- ===== 类型选择（从后端获取） ===== -->
          <div class="field">
            <label>类型</label>
            <div class="type-grid">
              <div
                v-for="type in cardTypeOptions"
                :key="type.value"
                class="type-card"
                :class="{ active: form.type === type.value }"
                @click="selectType(type)"
              >
                <span class="type-icon">{{ type.icon || '📄' }}</span>
                <span class="type-name">{{ type.label }}</span>
              </div>
            </div>
          </div>

          <!-- ===== 别名 ===== -->
          <div class="field">
            <label>别名</label>
            <div class="tag-input">
              <input
                v-model="aliasInput"
                placeholder="输入别名，按回车添加"
                @keydown.enter.prevent="addAlias"
              />
              <button type="button" @click="addAlias" class="add-tag-btn">添加</button>
            </div>
            <div class="tag-list">
              <span v-for="alias in form.aliases" :key="alias" class="tag-item">
                {{ alias }}
                <button type="button" @click="removeAlias(alias)" class="remove-tag">×</button>
              </span>
            </div>
          </div>

          <!-- ===== 属性 ===== -->
          <div class="field">
            <label>属性</label>
            <div class="attribute-list">
              <div v-for="(attr, idx) in form.attributes" :key="idx" class="attribute-item">
                <input v-model="attr.key" placeholder="属性名" class="attr-key" />
                <span class="attr-sep">：</span>
                <input v-model="attr.value" placeholder="属性值" class="attr-value" />
                <button type="button" class="remove-attr" @click="removeAttribute(idx)">×</button>
              </div>
            </div>
            <button type="button" class="add-attr-btn" @click="addAttribute">+ 添加属性</button>
          </div>

          <!-- ===== 描述 ===== -->
          <div class="field">
            <label>描述</label>
            <textarea
              v-model="form.description"
              rows="4"
              placeholder="描述这个卡片..."
            />
          </div>

          <!-- ===== 内容块（可视化插入） ===== -->
          <div class="field">
            <div class="blocks-header">
              <label>内容块</label>
              <div class="insert-toolbar">
                <button
                  v-for="type in cardTypeOptions"
                  :key="type.value"
                  class="insert-btn"
                  @click="openInsertPicker(type.value)"
                >
                  {{ type.icon || '📄' }} {{ type.label }}
                </button>
              </div>
            </div>

            <!-- 已插入的内容块 -->
            <div v-if="form.contentBlocks.length > 0" class="blocks-list">
              <div
                v-for="(block, idx) in form.contentBlocks"
                :key="block.id"
                class="block-item"
              >
                <div class="block-preview">
                  <span class="block-icon">{{ getTypeIcon(block.cardType) }}</span>
                  <div class="block-info">
                    <span class="block-title">{{ getCardTitle(block.cardId) }}</span>
                    <span class="block-type">{{ getTypeLabel(block.cardType) }}</span>
                    <span class="block-desc">{{ getBlockPreview(block.cardId) }}</span>
                  </div>
                </div>
                <button class="remove-block" @click="removeBlock(idx)">×</button>
              </div>
            </div>
            <div v-else class="blocks-empty">
              <span>点击上方按钮插入卡片内容块</span>
            </div>
          </div>

          <!-- ===== 标签 ===== -->
          <div class="field">
            <label>标签</label>
            <div class="tag-input">
              <input
                v-model="tagInput"
                placeholder="输入标签，按回车添加"
                @keydown.enter.prevent="addTag"
              />
              <button type="button" @click="addTag" class="add-tag-btn">添加</button>
            </div>
            <div class="tag-list">
              <span v-for="tag in form.tags" :key="tag" class="tag-item">
                #{{ tag }}
                <button type="button" @click="removeTag(tag)" class="remove-tag">×</button>
              </span>
            </div>
          </div>

          <!-- ===== 关联卡片 ===== -->
          <div class="field">
            <label>关联卡片</label>
            <div class="relation-list">
              <div v-for="(rel, idx) in form.relations" :key="idx" class="relation-item">
                <span class="relation-source">{{ getCardTitle(rel.targetCardId) }}</span>
                <span class="relation-arrow">←</span>
                <span class="relation-type">「{{ rel.relationType }}」</span>
                <span class="relation-target">{{ form.title || '(当前卡片)' }}</span>
                <button type="button" class="remove-relation" @click="removeRelation(idx)">×</button>
              </div>
            </div>

            <div class="relation-add">
              <el-select
                v-model="newRelation.targetId"
                filterable
                remote
                :remote-method="searchCards"
                placeholder="搜索并选择要关联的卡片"
                size="default"
              >
                <el-option
                  v-for="card in searchResults"
                  :key="card.id"
                  :label="`${card.title} (${getTypeLabel(card.type)})`"
                  :value="card.id"
                />
              </el-select>
              <input
                v-model="newRelation.relationType"
                placeholder="关系描述"
                class="relation-input"
                @keydown.enter.prevent="addRelation"
              />
              <button type="button" class="add-relation-btn" @click="addRelation">添加</button>
            </div>
          </div>
        </div>

        <footer class="dialog-footer">
          <button v-if="isEdit" class="btn-danger" @click="handleDelete">删除</button>
          <div class="footer-right">
            <button class="btn-outline" @click="close">取消</button>
            <button class="btn-primary" @click="handleSave" :disabled="saving">
              {{ saving ? '保存中...' : '保存' }}
            </button>
          </div>
        </footer>
      </div>
    </div>
  </Transition>

  <!-- ===== 卡片插入选择器 ===== -->
  <div v-if="showInsertPicker" class="picker-overlay" @click.self="closeInsertPicker">
    <div class="picker-modal">
      <div class="picker-header">
        <span>选择要插入的 {{ pickerTypeLabel }} 卡片</span>
        <button class="picker-close" @click="closeInsertPicker">✕</button>
      </div>
      <div class="picker-search">
        <input v-model="pickerSearch" placeholder="搜索卡片..." />
      </div>
      <div class="picker-list">
        <div
          v-for="card in pickerResults"
          :key="card.id"
          class="picker-item"
          @click="insertBlock(card)"
        >
          <span class="picker-icon">{{ getTypeIcon(card.type) }}</span>
          <span class="picker-title">{{ card.title }}</span>
          <span class="picker-type">{{ getTypeLabel(card.type) }}</span>
        </div>
        <div v-if="pickerResults.length === 0 && !pickerLoading" class="picker-empty">
          没有找到 {{ pickerTypeLabel }} 卡片
        </div>
        <div v-if="pickerLoading" class="picker-empty">加载中...</div>
      </div>
      <div class="picker-footer">
        <button class="btn-outline" @click="closeInsertPicker">取消</button>
        <button class="btn-primary" @click="createAndInsert">新建 {{ pickerTypeLabel }} 卡片并插入</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, nextTick, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '../../../stores/world';
import { useCos } from '@/composables/useCos';
import { v4 as uuidv4 } from 'uuid';

const props = defineProps<{
  visible: boolean;
  projectId: string;
  cardData?: any | null;
}>();

console.log('🔵 CardEditor 组件初始化，visible:', props.visible);

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'saved'): void;
  (e: 'deleted'): void;
}>();

const store = useWorldStore();
const saving = ref(false);
const tagInput = ref('');
const aliasInput = ref('');

// ===== COS 上传 =====
const fileInput = ref<HTMLInputElement | null>(null);
const uploadingCover = ref(false);
const uploadProgress = ref(0);
const { uploadFile } = useCos();

// ===== 卡片类型：从 store 获取 =====
const cardTypeOptions = computed(() => {
  if (store.cardTypes && store.cardTypes.length > 0) {
    return store.cardTypes.map((t: any) => ({
      value: t.id || t.value,
      label: t.label,
      icon: t.icon || '📄',
    }));
  }
  return [
    { value: 'character', label: '角色', icon: '🧙' },
    { value: 'location', label: '地点', icon: '📍' },
    { value: 'item', label: '物品', icon: '⚔️' },
    { value: 'event', label: '事件', icon: '📖' },
    { value: 'ecology', label: '生态', icon: '🌿' },
    { value: 'faction', label: '派系', icon: '🏛️' },
    { value: 'species', label: '物种', icon: '🐉' },
    { value: 'lore', label: '背景设定', icon: '📜' },
  ];
});

// ===== 插入选择器 =====
const showInsertPicker = ref(false);
const pickerType = ref('');
const pickerSearch = ref('');
const pickerResults = ref<any[]>([]);
const pickerLoading = ref(false);

const pickerTypeLabel = computed(() => {
  const found = cardTypeOptions.value.find(t => t.value === pickerType.value);
  return found?.label || pickerType.value;
});

// ===== 关联卡片 =====
const newRelation = ref({ targetId: '', relationType: '' });
const searchResults = ref<any[]>([]);

// ===== 表单 =====
const form = ref({
  title: '',
  type: 'character',
  subType: '',
  coverImage: '',  // 👈 新增封面图
  aliases: [] as string[],
  attributes: [] as { key: string; value: string }[],
  description: '',
  content: '{}',
  tags: [] as string[],
  relations: [] as { targetCardId: string; relationType: string }[],
  timelineEvents: [] as { date: string; title: string; description?: string }[],
  embeddedCards: [] as string[],
  contentBlocks: [] as { id: string; cardId: string; cardType: string; order: number }[],
});

const isEdit = computed(() => !!props.cardData);

// ===== 选择类型 =====
const selectType = (type: any) => {
  form.value.type = type.value;
  form.value.subType = '';
};

// ===== 卡片标题和类型辅助 =====
const getCardTitle = (cardId: string) => {
  const card = store.cards.find(c => c.id === cardId);
  return card?.title || '已删除的卡片';
};

const getTypeLabel = (type: string) => {
  const found = cardTypeOptions.value.find(t => t.value === type);
  return found?.label || type;
};

const getTypeIcon = (type: string) => {
  const found = cardTypeOptions.value.find(t => t.value === type);
  return found?.icon || '📄';
};

// ===== 获取被嵌入卡片的预览内容 =====
const getBlockPreview = (cardId: string) => {
  const card = store.cards.find(c => c.id === cardId);
  if (!card) return '已删除的卡片';
  if (card.description) return card.description;
  try {
    const data = JSON.parse(card.content || '{}');
    return data.description || data.summary || '';
  } catch {
    return '';
  }
};

// ===== 搜索卡片 =====
const searchCards = (query: string) => {
  const cards = store.cards;
  if (!query) {
    searchResults.value = cards.filter(c => c.id !== props.cardData?.id).slice(0, 10);
    return;
  }
  const lower = query.toLowerCase();
  searchResults.value = cards
    .filter(c => c.title.toLowerCase().includes(lower) && c.id !== props.cardData?.id)
    .slice(0, 10);
};

// ===== 关联 =====
const addRelation = () => {
  if (!newRelation.value.targetId) {
    ElMessage.warning('请选择要关联的卡片');
    return;
  }
  if (!newRelation.value.relationType.trim()) {
    ElMessage.warning('请输入关系描述');
    return;
  }
  if (form.value.relations.some(r => r.targetCardId === newRelation.value.targetId)) {
    ElMessage.warning('该卡片已关联');
    return;
  }
  if (newRelation.value.targetId === props.cardData?.id) {
    ElMessage.warning('不能关联自己');
    return;
  }
  form.value.relations.push({
    targetCardId: newRelation.value.targetId,
    relationType: newRelation.value.relationType.trim(),
  });
  newRelation.value = { targetId: '', relationType: '' };
};

const removeRelation = (idx: number) => {
  form.value.relations.splice(idx, 1);
};

// ===== 内容块插入 =====
const openInsertPicker = (type: string) => {
  pickerType.value = type;
  pickerSearch.value = '';
  showInsertPicker.value = true;
  loadPickerCards(type);
};

const closeInsertPicker = () => {
  showInsertPicker.value = false;
};

const loadPickerCards = (type: string) => {
  pickerLoading.value = true;
  const cards = store.cards.filter(c => c.type === type && c.id !== props.cardData?.id);
  pickerResults.value = cards.slice(0, 20);
  pickerLoading.value = false;
};

const insertBlock = (card: any) => {
  if (form.value.contentBlocks.some(b => b.cardId === card.id)) {
    ElMessage.warning('该卡片已插入');
    return;
  }
  form.value.contentBlocks.push({
    id: uuidv4(),
    cardId: card.id,
    cardType: card.type,
    order: form.value.contentBlocks.length,
  });
  closeInsertPicker();
  ElMessage.success(`已插入：${card.title}`);
};

const removeBlock = (idx: number) => {
  form.value.contentBlocks.splice(idx, 1);
};

const createAndInsert = async () => {
  closeInsertPicker();
  window.dispatchEvent(new CustomEvent('create-card-with-type', {
    detail: { type: pickerType.value }
  }));
};

// 监听插入选择器搜索
watch(pickerSearch, (query) => {
  if (!showInsertPicker.value) return;
  const cards = store.cards.filter(c => c.type === pickerType.value && c.id !== props.cardData?.id);
  if (!query) {
    pickerResults.value = cards.slice(0, 20);
    return;
  }
  const lower = query.toLowerCase();
  pickerResults.value = cards
    .filter(c => c.title.toLowerCase().includes(lower))
    .slice(0, 20);
});

// ===== 别名 =====
const addAlias = () => {
  const text = aliasInput.value.trim();
  if (!text) return;
  if (form.value.aliases.includes(text)) {
    ElMessage.warning('别名已存在');
    return;
  }
  form.value.aliases.push(text);
  aliasInput.value = '';
};

const removeAlias = (alias: string) => {
  form.value.aliases = form.value.aliases.filter(a => a !== alias);
};

// ===== 属性 =====
const addAttribute = () => {
  form.value.attributes.push({ key: '', value: '' });
};

const removeAttribute = (idx: number) => {
  form.value.attributes.splice(idx, 1);
};

// ===== 标签 =====
const addTag = () => {
  const text = tagInput.value.trim();
  if (!text) return;
  if (form.value.tags.includes(text)) {
    ElMessage.warning('标签已存在');
    return;
  }
  if (form.value.tags.length >= 10) {
    ElMessage.warning('最多添加 10 个标签');
    return;
  }
  form.value.tags.push(text);
  tagInput.value = '';
};

const removeTag = (tag: string) => {
  form.value.tags = form.value.tags.filter(t => t !== tag);
};

const getCardTags = (tags: any) => {
  if (Array.isArray(tags)) return tags;
  if (typeof tags === 'string') {
    try {
      return JSON.parse(tags);
    } catch {
      return [];
    }
  }
  return [];
};

// ===== 重置表单 =====
const resetForm = () => {
  form.value = {
    title: '',
    type: 'character',
    subType: '',
    coverImage: '',
    aliases: [],
    attributes: [],
    description: '',
    content: '{}',
    tags: [],
    relations: [],
    timelineEvents: [],
    embeddedCards: [],
    contentBlocks: [],
  };
  tagInput.value = '';
  aliasInput.value = '';
  newRelation.value = { targetId: '', relationType: '' };
  searchResults.value = [];
};

// ===== 加载编辑数据 =====
const loadCardData = () => {
  console.log('📦 loadCardData 被调用，cardData:', props.cardData);
  if (props.cardData) {
    const tags = getCardTags(props.cardData.tags);
    const relations = (props.cardData.relations || []).map((r: any) => ({
      targetCardId: r.targetCardId,
      relationType: r.relationType,
    }));
    form.value = {
      title: props.cardData.title || '',
      type: props.cardData.type || 'character',
      subType: props.cardData.subType || '',
      coverImage: props.cardData.coverImage || '',
      aliases: props.cardData.aliases || [],
      attributes: props.cardData.attributes || [],
      description: props.cardData.description || '',
      content: props.cardData.content || '{}',
      tags: tags,
      relations: relations,
      timelineEvents: props.cardData.timelineEvents || [],
      embeddedCards: props.cardData.embeddedCards || [],
      contentBlocks: props.cardData.contentBlocks || [],
    };
  } else {
    resetForm();
  }
  searchCards('');
};

// ===== 封面图上传 =====
const triggerFileInput = () => {
  fileInput.value?.click();
};

const handleFileUpload = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    ElMessage.warning('请上传图片文件');
    return;
  }
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('图片大小不能超过 5MB');
    return;
  }

  uploadingCover.value = true;
  uploadProgress.value = 0;

  try {
    const result = await uploadFile(file, 'world/covers');
    form.value.coverImage = result.url;
    ElMessage.success('封面图上传成功');
  } catch (error) {
    console.error('上传失败:', error);
    ElMessage.error('上传失败，请重试');
  } finally {
    uploadingCover.value = false;
    uploadProgress.value = 0;
    input.value = '';
  }
};

// ===== 保存 =====
const handleSave = async () => {
  if (!form.value.title.trim()) {
    ElMessage.warning('请输入卡片标题');
    return;
  }

  let contentStr = form.value.content.trim();
  if (!contentStr) {
    contentStr = '{}';
  } else {
    try {
      JSON.parse(contentStr);
    } catch {
      contentStr = JSON.stringify({ description: contentStr });
    }
  }

  const payload = {
    title: form.value.title.trim(),
    type: form.value.type,
    subType: form.value.subType || '',
    coverImage: form.value.coverImage || '',
    aliases: form.value.aliases,
    attributes: form.value.attributes,
    description: form.value.description.trim(),
    content: contentStr,
    tags: form.value.tags,
    relations: form.value.relations,
    timelineEvents: form.value.timelineEvents,
    embeddedCards: form.value.embeddedCards,
    contentBlocks: form.value.contentBlocks,
  };

  saving.value = true;
  try {
    if (isEdit.value && props.cardData) {
      await store.updateCard(props.cardData.id, payload);
      ElMessage.success('卡片已更新');
    } else {
      await store.createCard(props.projectId, payload);
      ElMessage.success('卡片已创建');
    }
    emit('saved');
  } catch (error) {
    console.error('保存失败:', error);
    ElMessage.error('保存失败，请重试');
  } finally {
    saving.value = false;
  }
};

// ===== 删除 =====
const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('确定要删除这张卡片吗？此操作不可恢复。', '确认删除', {
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
      type: 'warning',
    });
    if (props.cardData) {
      await store.deleteCard(props.cardData.id);
      ElMessage.success('卡片已删除');
      emit('deleted');
    }
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除失败:', error);
    }
  }
};

const close = () => {
  console.log('❌ CardEditor close 被调用');
  emit('update:visible', false);
};

// ===== 监听 visible 变化 =====
watch(
  () => props.visible,
  (val) => {
    console.log('🟢 CardEditor visible 变化:', val);
    if (val) {
      console.log('📦 加载卡片数据，cardData:', props.cardData);
      loadCardData();
    }
  }
);

watch(
  () => props.cardData,
  () => {
    if (props.visible) {
      console.log('📦 cardData 变化，重新加载');
      loadCardData();
    }
  }
);

// ===== 监听创建卡片事件 =====
onMounted(() => {
  window.addEventListener('create-card-with-type', ((e: CustomEvent) => {
    const type = e.detail?.type;
    if (type) {
      close();
      window.dispatchEvent(new CustomEvent('open-create-card', {
        detail: { type }
      }));
    }
  }) as EventListener);
});
</script>



<style scoped>
.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.dialog {
  background: white;
  border-radius: 24px;
  width: 640px;
  max-width: 94%;
  max-height: 90vh;
  overflow-y: auto;
  padding: 28px 32px 24px;
  box-shadow: 0 32px 64px rgba(0, 0, 0, 0.12);
}
.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}
.dialog-header h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  color: #0f172a;
}
.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  color: #94a3b8;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 8px;
}
.close-btn:hover {
  background: #f1f3f5;
}
.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.field label {
  font-weight: 600;
  font-size: 14px;
  color: #334155;
}
.field label .required {
  color: #ef4444;
}
.field input,
.field textarea {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 10px 14px;
  font-size: 15px;
  font-family: inherit;
  transition: border 0.2s;
  background: #fafbfc;
}
.field input:focus,
.field textarea:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
  box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.06);
}
.field textarea {
  resize: vertical;
  min-height: 60px;
  font-family: 'Monaco', 'Menlo', monospace;
  font-size: 14px;
}
.hint {
  font-size: 12px;
  color: #94a3b8;
}

/* ===== 类型选择 ===== */
.type-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 8px;
}
.type-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 10px 6px;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s;
  background: #fafbfc;
  text-align: center;
}
.type-card:hover {
  border-color: #cbd5e1;
  background: #f1f5f9;
}
.type-card.active {
  border-color: #4f46e5;
  background: #eef2ff;
  box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.1);
}
.type-icon {
  font-size: 24px;
  margin-bottom: 2px;
}
.type-name {
  font-weight: 500;
  font-size: 12px;
  color: #0f172a;
}
.type-sub {
  font-size: 9px;
  color: #94a3b8;
  margin-top: 1px;
}
.sub-type-select {
  margin-top: 6px;
  padding: 8px 12px;
  background: #f8f9fc;
  border-radius: 10px;
}
.sub-label {
  font-size: 12px;
  font-weight: 500;
  color: #64748b;
  display: block;
  margin-bottom: 4px;
}
.sub-type-options {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.sub-type-btn {
  padding: 3px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  background: white;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}
.sub-type-btn:hover {
  background: #f1f5f9;
}
.sub-type-btn.active {
  border-color: #4f46e5;
  background: #eef2ff;
  color: #4f46e5;
}

/* ===== 标签 ===== */
.tag-input {
  display: flex;
  gap: 6px;
}
.tag-input input {
  flex: 1;
}
.add-tag-btn {
  padding: 0 16px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  background: #fafbfc;
  color: #64748b;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 13px;
}
.add-tag-btn:hover {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
}
.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: #eef2ff;
  color: #4f46e5;
  padding: 2px 10px 2px 12px;
  border-radius: 14px;
  font-size: 13px;
  font-weight: 500;
}
.remove-tag {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 16px;
  padding: 0 2px;
}
.remove-tag:hover {
  color: #ef4444;
}

/* ===== 属性 ===== */
.attribute-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.attribute-item {
  display: flex;
  align-items: center;
  gap: 6px;
}
.attr-key {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 14px;
  background: #fafbfc;
}
.attr-key:focus {
  outline: none;
  border-color: #4f46e5;
}
.attr-value {
  flex: 1.5;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 14px;
  background: #fafbfc;
}
.attr-value:focus {
  outline: none;
  border-color: #4f46e5;
}
.attr-sep {
  color: #94a3b8;
}
.remove-attr {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 18px;
  padding: 0 4px;
}
.remove-attr:hover {
  color: #ef4444;
}
.add-attr-btn {
  padding: 4px 14px;
  border: 1px dashed #d1d5db;
  border-radius: 8px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
  align-self: flex-start;
}
.add-attr-btn:hover {
  border-color: #4f46e5;
  color: #4f46e5;
}

/* ===== 时间线 ===== */
.timeline-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.timeline-item {
  display: flex;
  align-items: center;
  gap: 6px;
}
.tl-date {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 13px;
  background: #fafbfc;
}
.tl-date:focus {
  outline: none;
  border-color: #4f46e5;
}
.tl-title {
  flex: 1.5;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 13px;
  background: #fafbfc;
}
.tl-title:focus {
  outline: none;
  border-color: #4f46e5;
}
.tl-desc {
  flex: 2;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 13px;
  background: #fafbfc;
}
.tl-desc:focus {
  outline: none;
  border-color: #4f46e5;
}
.remove-tl {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 18px;
  padding: 0 4px;
}
.remove-tl:hover {
  color: #ef4444;
}
.add-tl-btn {
  padding: 4px 14px;
  border: 1px dashed #d1d5db;
  border-radius: 8px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
  align-self: flex-start;
}
.add-tl-btn:hover {
  border-color: #4f46e5;
  color: #4f46e5;
}

/* ===== 关联 ===== */
.relation-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 6px;
  min-height: 16px;
}
.relation-item {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #f8f9fc;
  padding: 4px 10px;
  border-radius: 8px;
  font-size: 13px;
  border: 1px solid #f1f3f5;
}
.relation-source {
  font-weight: 500;
  color: #0f172a;
}
.relation-arrow {
  color: #94a3b8;
}
.relation-type {
  color: #4f46e5;
  font-weight: 500;
}
.relation-target {
  color: #64748b;
}
.remove-relation {
  margin-left: auto;
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 18px;
  padding: 0 4px;
}
.remove-relation:hover {
  color: #ef4444;
}
.relation-add {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.relation-add :deep(.el-select) {
  flex: 2;
  min-width: 140px;
}
.relation-add :deep(.el-select .el-input__wrapper) {
  border-radius: 10px;
}
.relation-input {
  flex: 3;
  min-width: 120px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 6px 12px;
  font-size: 14px;
  font-family: inherit;
  background: #fafbfc;
}
.relation-input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.add-relation-btn {
  padding: 6px 16px;
  border: none;
  border-radius: 10px;
  background: #4f46e5;
  color: white;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 13px;
}
.add-relation-btn:hover {
  background: #4338ca;
  transform: translateY(-1px);
}
.add-relation-btn:active {
  transform: scale(0.97);
}

/* ===== 底部按钮 ===== */
.dialog-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid #f1f3f5;
}
.footer-right {
  display: flex;
  gap: 10px;
}
.btn-danger {
  padding: 8px 18px;
  background: #fef2f2;
  color: #ef4444;
  border: 1px solid #fecaca;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}
.btn-danger:hover {
  background: #fee2e2;
  border-color: #fca5a5;
}
.btn-outline {
  padding: 8px 20px;
  background: transparent;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  color: #374151;
  cursor: pointer;
}
.btn-outline:hover {
  background: #f3f4f6;
}
.btn-primary {
  padding: 8px 24px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-primary:hover:not(:disabled) {
  background: #4338ca;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.25);
}
.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* ===== 描述区 ===== */
.description-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.insert-card-btn {
  padding: 4px 14px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: white;
  color: #4f46e5;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}
.insert-card-btn:hover {
  background: #eef2ff;
  border-color: #4f46e5;
}
.hint-code {
  background: #f1f3f5;
  padding: 1px 6px;
  border-radius: 4px;
  font-family: monospace;
  font-size: 12px;
  color: #4f46e5;
}

/* ===== 内容块 ===== */
.blocks-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
.insert-toolbar {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
.insert-btn {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  background: white;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
  color: #475569;
}
.insert-btn:hover {
  background: #eef2ff;
  border-color: #4f46e5;
  color: #4f46e5;
}
.blocks-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 4px;
}
.block-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 14px;
  background: #f8f9fc;
  border: 1px solid #f1f3f5;
  border-radius: 10px;
}
.block-preview {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
}
.block-icon {
  font-size: 20px;
  flex-shrink: 0;
}
.block-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}
.block-title {
  font-weight: 500;
  color: #0f172a;
}
.block-type {
  font-size: 12px;
  color: #94a3b8;
}
.block-desc {
  font-size: 12px;
  color: #94a3b8;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.remove-block {
  background: none;
  border: none;
  font-size: 18px;
  color: #94a3b8;
  cursor: pointer;
  flex-shrink: 0;
  padding: 0 4px;
}
.remove-block:hover {
  color: #ef4444;
}
.blocks-empty {
  padding: 16px;
  text-align: center;
  color: #94a3b8;
  font-size: 14px;
  border: 1px dashed #e2e8f0;
  border-radius: 10px;
}

/* ===== 卡片引用选择器 ===== */
.picker-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.picker-modal {
  background: white;
  border-radius: 20px;
  width: 440px;
  max-width: 92%;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 32px 64px rgba(0, 0, 0, 0.15);
}
.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #f1f3f5;
  font-weight: 600;
}
.picker-close {
  background: none;
  border: none;
  font-size: 24px;
  color: #94a3b8;
  cursor: pointer;
}
.picker-close:hover {
  color: #1e293b;
}
.picker-search {
  padding: 12px 16px;
  border-bottom: 1px solid #f1f3f5;
}
.picker-search input {
  width: 100%;
  padding: 8px 14px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  font-size: 14px;
}
.picker-search input:focus {
  outline: none;
  border-color: #4f46e5;
}
.picker-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}
.picker-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 20px;
  cursor: pointer;
  transition: background 0.15s;
}
.picker-item:hover {
  background: #f1f5f9;
}
.picker-icon {
  font-size: 20px;
}
.picker-title {
  font-weight: 500;
  flex: 1;
}
.picker-type {
  font-size: 13px;
  color: #94a3b8;
}
.picker-sub {
  font-size: 12px;
  color: #94a3b8;
  background: #f1f3f5;
  padding: 0 8px;
  border-radius: 10px;
}
.picker-empty {
  padding: 30px;
  text-align: center;
  color: #94a3b8;
}
.picker-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 12px 20px;
  border-top: 1px solid #f1f3f5;
}
.picker-footer .btn-outline {
  padding: 6px 18px;
  background: transparent;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  cursor: pointer;
}
.picker-footer .btn-outline:hover {
  background: #f3f4f6;
}
.picker-footer .btn-primary {
  padding: 6px 18px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
}
.picker-footer .btn-primary:hover {
  background: #4338ca;
}

/* ===== 过渡动画 ===== */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.25s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
.fade-enter-active .dialog,
.fade-leave-active .dialog {
  transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.25s ease;
}
.fade-enter-from .dialog,
.fade-leave-to .dialog {
  transform: scale(0.95);
  opacity: 0;
}
/* ===== 封面图上传 ===== */
.cover-upload {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cover-upload-area {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 120px;
  border: 2px dashed #d1d5db;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s;
  background: #fafbfc;
  padding: 16px;
}

.cover-upload-area:hover {
  border-color: #4f46e5;
  background: #f8f9fc;
}

.upload-icon {
  font-size: 32px;
  margin-bottom: 8px;
}

.upload-text {
  font-size: 14px;
  font-weight: 500;
  color: #374151;
}

.upload-hint {
  font-size: 12px;
  color: #94a3b8;
  margin-top: 4px;
}

.cover-preview {
  position: relative;
  width: 100%;
  max-height: 240px;
  overflow: hidden;
  border-radius: 12px;
  border: 1px solid #e2e8f0;
}

.cover-preview img {
  width: 100%;
  height: auto;
  max-height: 240px;
  object-fit: cover;
  display: block;
}

.remove-cover {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.6);
  color: white;
  border: none;
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}

.remove-cover:hover {
  background: rgba(0, 0, 0, 0.8);
}

.upload-progress {
  margin-top: 8px;
}
</style>