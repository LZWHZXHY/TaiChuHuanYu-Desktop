<template>
  <div class="project-create">
    <div class="container">
      <!-- 返回按钮 -->
      <button class="back-btn" @click="goBack">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="20" height="20">
          <line x1="19" y1="12" x2="5" y2="12"/>
          <polyline points="12 19 5 12 12 5"/>
        </svg>
        返回
      </button>

      <!-- 主标题 -->
      <header class="page-header">
        <h1>创建新世界</h1>
        <p>从一个想法开始，构建属于你的世界观</p>
      </header>

      <!-- 🆕 配额信息横幅 -->
      <WorldQuotaBanner :quota-info="quotaInfo" :upgrading="upgrading" @upgrade="upgradeWorldQuota" />

      <!-- 双栏布局 -->
      <div class="create-layout">
        <!-- 左侧：表单 -->
        <section class="form-section">
          <form @submit.prevent="handleSubmit">
            <!-- 项目名称 -->
            <div class="field-group">
              <label for="name">项目名称 <span class="required">*</span></label>
              <input
                id="name"
                v-model="form.name"
                type="text"
                placeholder="给世界起一个响亮的名字"
                maxlength="100"
                autofocus
              />
              <span class="counter">{{ form.name.length }}/100</span>
            </div>

            <!-- 项目描述 -->
            <div class="field-group">
              <label for="description">简介</label>
              <textarea
                id="description"
                v-model="form.description"
                rows="4"
                placeholder="简单描述这个世界的故事背景、风格或核心设定"
                maxlength="500"
              ></textarea>
              <span class="counter">{{ form.description.length }}/500</span>
            </div>

            <!-- 可见性 -->
            <div class="field-group">
              <label>可见性</label>
              <div class="visibility-options">
                <div
                  class="visibility-option"
                  :class="{ active: !form.isPublic }"
                  @click="form.isPublic = false"
                >
                  <span class="icon">🔒</span>
                  <div>
                    <strong>私有</strong>
                    <p>只有你自己能看到</p>
                  </div>
                </div>
                <div
                  class="visibility-option"
                  :class="{ active: form.isPublic }"
                  @click="form.isPublic = true"
                >
                  <span class="icon">🌐</span>
                  <div>
                    <strong>公开</strong>
                    <p>所有人都可以浏览</p>
                  </div>
                </div>
              </div>
            </div>

            <!-- 世界类型（可选） -->
            <div class="field-group">
              <label>世界类型</label>
              <div class="tag-select">
                <span
                  v-for="type in worldTypes"
                  :key="type"
                  class="tag-option"
                  :class="{ active: form.type === type }"
                  @click="form.type = type"
                >
                  {{ type }}
                </span>
                <span
                  class="tag-option clear"
                  :class="{ active: !form.type }"
                  @click="form.type = ''"
                >
                  不限
                </span>
              </div>
            </div>

            <!-- 标签 -->
            <div class="field-group">
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

            <!-- 提交按钮 -->
            <div class="form-actions">
              <button type="button" class="btn-outline" @click="goBack">取消</button>
              <button type="submit" class="btn-primary" :disabled="submitting || quotaFull">
                {{ submitting ? '创建中...' : quotaFull ? '配额已满，请扩容' : '🚀 创建世界' }}
              </button>
            </div>
          </form>
        </section>

        <!-- 右侧：预览 -->
        <ProjectCreatePreview :form="form" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import WorldQuotaBanner from './世界观组件/WorldQuotaBanner.vue';
import ProjectCreatePreview from './世界观组件/ProjectCreatePreview.vue';
import { ref, reactive, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '../../stores/world';
import { worldApi, type WorldQuota } from '@/api/worldApi';

const router = useRouter();
const store = useWorldStore();

const submitting = ref(false);
const tagInput = ref('');
const quotaInfo = ref<WorldQuota | null>(null);

const form = reactive({
  name: '',
  description: '',
  isPublic: false,
  type: '',
  tags: [] as string[],
});

const worldTypes = ['奇幻', '科幻', '末世', '现代', '历史', '神话', '悬疑', '治愈'];

// ===== 🆕 判断配额是否已满 =====
const quotaFull = computed(() => {
  if (!quotaInfo.value) return false;
  return quotaInfo.value.remainingWorldCount <= 0;
});

// ===== 🆕 加载配额信息 =====
const loadQuota = async () => {
  try {
    quotaInfo.value = await worldApi.getQuota();
  } catch (error) {
    console.error('获取配额信息失败:', error);
  }
};

// 添加标签
const addTag = () => {
  const text = tagInput.value.trim();
  if (!text) return;
  if (form.tags.includes(text)) {
    ElMessage.warning('标签已存在');
    return;
  }
  if (form.tags.length >= 10) {
    ElMessage.warning('最多添加 10 个标签');
    return;
  }
  form.tags.push(text);
  tagInput.value = '';
};

// 移除标签
const removeTag = (tag: string) => {
  form.tags = form.tags.filter(t => t !== tag);
};

// ===== 🆕 跳转到配额管理 =====
const upgrading = ref(false);
const upgradeWorldQuota = async () => {
  if (upgrading.value || !quotaInfo.value) return;
  upgrading.value = true;
  try {
    await ElMessageBox.confirm(`增加 1 个世界观名额将消耗 ${quotaInfo.value.expCostPerWorldSlot} 经验，是否继续？`, '扩充世界观名额', { confirmButtonText: '确认扩容', cancelButtonText: '取消' });
    const result = await worldApi.upgradeQuota('WorldCount');
    if (result.success) { ElMessage.success(result.message); await loadQuota(); }
    else ElMessage.warning(result.message);
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error('扩容失败，请稍后重试');
  } finally { upgrading.value = false; }
};

const warnQuota = (message: string) => ElMessage.warning({ message, duration: 5000, showClose: true });

// ===== 🆕 提交前检查配额 =====
const handleSubmit = async () => {
  if (!form.name.trim()) {
    ElMessage.warning('请输入项目名称');
    return;
  }

  // ✅ 如果配额已满，直接提示
  if (quotaFull.value && quotaInfo.value) {
    warnQuota(`世界观数量已达上限（${quotaInfo.value.maxWorldCount} 个），请先去扩容`);
    return;
  }

  // ✅ 再次检查配额（防止并发变化）
  try {
    const data = await worldApi.canCreateProject();
    if (!data.canCreate) {
      warnQuota(data.message || '世界观数量已达上限');
      // 刷新配额信息
      await loadQuota();
      return;
    }
  } catch (error) {
    console.error('检查配额失败:', error);
    // 如果检查失败，继续尝试创建（让后端最终决定）
  }

  submitting.value = true;
  try {
    await store.createProject({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      isPublic: form.isPublic,
    });
    ElMessage.success('🎉 世界创建成功！');
    router.push('/world');
  } catch (error: any) {
    // ✅ 处理后端返回的配额错误
    if (error?.response?.data?.code === 'QUOTA_EXCEEDED') {
      warnQuota(error.response.data.message || '世界观数量已达上限，请扩容');
      // 刷新配额信息
      await loadQuota();
    } else {
      ElMessage.error('创建失败，请稍后重试');
    }
    console.error(error);
  } finally {
    submitting.value = false;
  }
};

// 返回
const goBack = () => {
  router.back();
};

// ===== 生命周期 =====
onMounted(loadQuota);
</script>

<style scoped>

.project-create {
  min-height: 0;
  background: var(--bg-main);
  padding: 24px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.container {
  max-width: 1100px;
  margin: 0 auto;
}

.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 10px;
  transition: all 0.2s;
  margin-bottom: 20px;
}

.back-btn:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}

.page-header {
  margin-bottom: 20px;
}

.page-header h1 {
  font-size: 30px;
  font-weight: 700;
  margin: 0 0 4px 0;
  color: var(--text-primary);
  letter-spacing: -0.5px;
}

.page-header p {
  margin: 0;
  color: var(--text-secondary);
  font-size: 16px;
}

.create-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(240px, 300px);
  gap: 40px;
  align-items: start;
}

.form-section {
  background: var(--paper-card);
  border-radius: 24px;
  padding: clamp(16px, 3vw, 32px);
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid var(--border-line);
}

.field-group {
  margin-bottom: 28px;
}

.field-group:last-of-type {
  margin-bottom: 32px;
}

.field-group label {
  display: block;
  font-weight: 600;
  font-size: 14px;
  color: var(--text-primary);
  margin-bottom: 6px;
}

.field-group label .required {
  color: var(--danger);
}

.field-group input,
.field-group textarea {
  width: 100%;
  border: 1px solid var(--border-line);
  border-radius: 12px;
  padding: 12px 16px;
  font-size: 15px;
  font-family: inherit;
  transition: all 0.2s;
  background: var(--paper-card);
}

.field-group input:focus,
.field-group textarea:focus {
  outline: none;
  border-color: var(--accent-color);
  background: var(--paper-card);
  box-shadow: 0 0 0 4px rgba(79, 70, 229, 0.06);
}

.field-group textarea {
  resize: vertical;
  min-height: 100px;
}

.counter {
  display: block;
  text-align: right;
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.visibility-options {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.visibility-option {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px 18px;
  border: 2px solid var(--border-line);
  border-radius: 14px;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--paper-card);
}

.visibility-option:hover {
  border-color: var(--border-line);
}

.visibility-option.active {
  border-color: var(--accent-color);
  background: var(--accent-soft);
}

.visibility-option .icon {
  font-size: 24px;
  flex-shrink: 0;
}

.visibility-option strong {
  display: block;
  font-size: 15px;
  color: var(--text-primary);
}

.visibility-option p {
  margin: 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.tag-select {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.tag-option {
  padding: 6px 16px;
  border-radius: 20px;
  background: var(--bg-sub);
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
}

.tag-option:hover {
  background: var(--bg-sub);
}

.tag-option.active {
  background: var(--accent-color);
  color: var(--on-accent);
}

.tag-option.clear {
  background: transparent;
  border: 1px dashed var(--border-line);
}

.tag-option.clear:hover {
  background: var(--bg-sub);
}

.tag-option.clear.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  border-color: var(--accent-color);
}

.tag-input {
  display: flex;
  gap: 8px;
}

.tag-input input {
  flex: 1;
}

.add-tag-btn {
  padding: 0 18px;
  border: 1px solid var(--border-line);
  border-radius: 12px;
  background: var(--paper-card);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}

.add-tag-btn:hover {
  background: var(--accent-color);
  color: var(--on-accent);
  border-color: var(--accent-color);
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: var(--accent-soft);
  color: var(--accent-color);
  padding: 4px 10px 4px 14px;
  border-radius: 16px;
  font-size: 13px;
  font-weight: 500;
}

.remove-tag {
  background: none;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 16px;
  padding: 0 2px;
  line-height: 1;
  transition: color 0.2s;
}

.remove-tag:hover {
  color: var(--danger);
}

.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 8px;
}

.btn-primary {
  padding: 12px 32px;
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  flex: 1;
}

.btn-primary:hover:not(:disabled) {
  background: var(--accent-color);
  transform: translateY(-1px);
  box-shadow: 0 6px 20px rgba(79, 70, 229, 0.25);
}

.btn-primary:active:not(:disabled) {
  transform: scale(0.98);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-outline {
  padding: 12px 24px;
  background: transparent;
  border: 1px solid var(--border-line);
  border-radius: 12px;
  font-size: 16px;
  font-weight: 500;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.2s;
}

.btn-outline:hover {
  background: var(--bg-sub);
  border-color: var(--border-line);
}

@media (max-width: 820px) {
  .create-layout {
    grid-template-columns: 1fr;
    gap: 24px;
  }
  .visibility-options {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 480px) {
  .project-create {
    padding: 12px;
  }
  .form-section {
    padding: 20px;
  }
  .visibility-options {
    grid-template-columns: 1fr;
  }
  .form-actions {
    flex-direction: column;
  }
  .btn-primary,
.btn-outline {
    width: 100%;
    justify-content: center;
  }
}
</style>
