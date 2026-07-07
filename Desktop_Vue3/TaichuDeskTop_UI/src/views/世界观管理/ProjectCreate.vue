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
              <button type="submit" class="btn-primary" :disabled="submitting">
                {{ submitting ? '创建中...' : '🚀 创建世界' }}
              </button>
            </div>
          </form>
        </section>

        <!-- 右侧：预览 -->
        <aside class="preview-section">
          <div class="preview-card">
            <h4>实时预览</h4>
            <div class="preview-content">
              <div class="preview-header">
                <h3>{{ form.name || '世界名称' }}</h3>
                <span class="preview-tag" :class="form.isPublic ? 'public' : 'private'">
                  {{ form.isPublic ? '公开' : '私有' }}
                </span>
              </div>
              <p class="preview-desc">{{ form.description || '世界简介...' }}</p>
              <div class="preview-meta">
                <span v-if="form.type">类型：{{ form.type }}</span>
                <span>标签：{{ form.tags.length ? form.tags.map(t => '#' + t).join(' ') : '无' }}</span>
                <span>创建于：{{ new Date().toLocaleDateString() }}</span>
              </div>
            </div>
          </div>
        </aside>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { useWorldStore } from '../../stores/world';

const router = useRouter();
const store = useWorldStore();

const submitting = ref(false);
const tagInput = ref('');

const form = reactive({
  name: '',
  description: '',
  isPublic: false,
  type: '',
  tags: [] as string[],
});

const worldTypes = ['奇幻', '科幻', '末世', '现代', '历史', '神话', '悬疑', '治愈'];

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

// 提交
const handleSubmit = async () => {
  if (!form.name.trim()) {
    ElMessage.warning('请输入项目名称');
    return;
  }

  submitting.value = true;
  try {
    await store.createProject({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      isPublic: form.isPublic,
    });
    ElMessage.success('🎉 世界创建成功！');
    router.push('/world/projects');
  } catch (error) {
    ElMessage.error('创建失败，请稍后重试');
    console.error(error);
  } finally {
    submitting.value = false;
  }
};

// 返回
const goBack = () => {
  router.back();
};
</script>

<style scoped>
/* ===== 页面整体 ===== */
.project-create {
  min-height: 100vh;
  background: #f8f9fc;
  padding: 24px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.container {
  max-width: 1100px;
  margin: 0 auto;
}

/* ===== 返回按钮 ===== */
.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  color: #64748b;
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 10px;
  transition: all 0.2s;
  margin-bottom: 20px;
}
.back-btn:hover {
  background: #eef2f6;
  color: #1e293b;
}

/* ===== 页面标题 ===== */
.page-header {
  margin-bottom: 36px;
}
.page-header h1 {
  font-size: 30px;
  font-weight: 700;
  margin: 0 0 4px 0;
  color: #0f172a;
  letter-spacing: -0.5px;
}
.page-header p {
  margin: 0;
  color: #94a3b8;
  font-size: 16px;
}

/* ===== 双栏布局 ===== */
.create-layout {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 40px;
  align-items: start;
}

/* ===== 左侧表单 ===== */
.form-section {
  background: white;
  border-radius: 24px;
  padding: 32px 36px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
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
  color: #334155;
  margin-bottom: 6px;
}
.field-group label .required {
  color: #ef4444;
}

.field-group input,
.field-group textarea {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 12px 16px;
  font-size: 15px;
  font-family: inherit;
  transition: all 0.2s;
  background: #fafbfc;
}
.field-group input:focus,
.field-group textarea:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
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
  color: #94a3b8;
  margin-top: 4px;
}

/* ===== 可见性选项 ===== */
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
  border: 2px solid #e2e8f0;
  border-radius: 14px;
  cursor: pointer;
  transition: all 0.2s;
  background: #fafbfc;
}
.visibility-option:hover {
  border-color: #cbd5e1;
}
.visibility-option.active {
  border-color: #4f46e5;
  background: #eef2ff;
}
.visibility-option .icon {
  font-size: 24px;
  flex-shrink: 0;
}
.visibility-option strong {
  display: block;
  font-size: 15px;
  color: #0f172a;
}
.visibility-option p {
  margin: 0;
  font-size: 13px;
  color: #94a3b8;
}

/* ===== 世界类型标签 ===== */
.tag-select {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag-option {
  padding: 6px 16px;
  border-radius: 20px;
  background: #f1f3f5;
  color: #64748b;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
}
.tag-option:hover {
  background: #e2e8f0;
}
.tag-option.active {
  background: #4f46e5;
  color: white;
}
.tag-option.clear {
  background: transparent;
  border: 1px dashed #d1d5db;
}
.tag-option.clear:hover {
  background: #f1f3f5;
}
.tag-option.clear.active {
  background: #e2e8f0;
  color: #1e293b;
  border-color: #4f46e5;
}

/* ===== 标签输入 ===== */
.tag-input {
  display: flex;
  gap: 8px;
}
.tag-input input {
  flex: 1;
}
.add-tag-btn {
  padding: 0 18px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: #fafbfc;
  color: #64748b;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}
.add-tag-btn:hover {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
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
  background: #eef2ff;
  color: #4f46e5;
  padding: 4px 10px 4px 14px;
  border-radius: 16px;
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
  line-height: 1;
  transition: color 0.2s;
}
.remove-tag:hover {
  color: #ef4444;
}

/* ===== 表单按钮 ===== */
.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 8px;
}
.btn-primary {
  padding: 12px 32px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  flex: 1;
}
.btn-primary:hover:not(:disabled) {
  background: #4338ca;
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
  border: 1px solid #d1d5db;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 500;
  color: #374151;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-outline:hover {
  background: #f3f4f6;
  border-color: #9ca3af;
}

/* ===== 右侧预览 ===== */
.preview-section {
  position: sticky;
  top: 24px;
}
.preview-card {
  background: white;
  border-radius: 24px;
  padding: 24px 28px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
}
.preview-card h4 {
  margin: 0 0 16px 0;
  font-size: 14px;
  font-weight: 600;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.preview-content {
  min-height: 180px;
}
.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.preview-header h3 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #0f172a;
}
.preview-tag {
  font-size: 11px;
  font-weight: 500;
  padding: 2px 12px;
  border-radius: 20px;
  height: 24px;
  line-height: 24px;
}
.preview-tag.public {
  background: #dcfce7;
  color: #16a34a;
}
.preview-tag.private {
  background: #f1f3f5;
  color: #64748b;
}

.preview-desc {
  color: #64748b;
  font-size: 15px;
  line-height: 1.6;
  margin: 0 0 16px 0;
  min-height: 50px;
}

.preview-meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: #94a3b8;
}

/* ===== 响应式 ===== */
@media (max-width: 820px) {
  .create-layout {
    grid-template-columns: 1fr;
    gap: 24px;
  }
  .preview-section {
    position: static;
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