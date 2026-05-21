<template>
  <div class="md-admin-container">
    <!-- 极简头部 -->
    <header class="md-header">
      <div class="header-content">
        <h1 class="md-title">动态管理</h1>
        <p class="md-subtitle">目前共有 {{ newsList.length }} 条记录</p>
      </div>
      <button class="md-fab-button" @click="openModal">
        <span class="icon">＋</span>
        <span class="label">新动态</span>
      </button>
    </header>

    <!-- 数据列表区域 -->
    <div class="md-content">
      <div v-if="loading && newsList.length === 0" class="md-loader">数据同步中...</div>
      
      <div v-else-if="newsList.length === 0" class="md-empty">
        <p>没有任何动态记录</p>
      </div>

      <div v-else class="md-list">
        <div v-for="item in newsList" :key="item.id" class="md-list-item" :class="{ 'is-draft-row': !item.isPublished }">
          <!-- 视觉缩略 -->
          <div class="item-visual">
            <img v-if="item.imageUrl" :src="item.imageUrl" class="md-img" />
            <div v-else class="md-img-placeholder"></div>
          </div>
          
          <!-- 文字内容 -->
          <div class="item-info">
            <div class="item-top">
              <span class="item-tag">{{ item.type }}</span>
              <span class="item-date">{{ formatDate(item.createdAt) }}</span>
            </div>
            <h3 class="item-title">{{ item.title }}</h3>
          </div>

          <!-- 状态展示 -->
          <div class="item-status">
            <span :class="['md-dot', item.isPublished ? 'is-active' : 'is-draft']"></span>
            {{ item.isPublished ? '公开中' : '已下架' }}
          </div>

          <!-- 操作区 -->
          <div class="item-actions">
            <!-- 🌟 上下架开关 -->
            <button class="text-btn is-status" @click="handleTogglePublish(item)">
              {{ item.isPublished ? '下架' : '上架' }}
            </button>
            <button class="text-btn" @click="handleEdit(item)">编辑</button>
            <button class="text-btn is-danger" @click="handleDelete(item.id)">移除</button>
          </div>
        </div>
      </div>
    </div>

    <!-- MD 3 风格沉浸式弹窗 -->
    <Teleport to="body">
      <Transition name="md-fade">
        <div v-if="isModalOpen" class="md-modal-overlay" @click.self="closeModal">
          <div class="md-modal-card">
            <h2 class="modal-header">{{ editingId ? '编辑内容' : '撰写新动态' }}</h2>
            
            <div class="md-form">
              <div class="md-input-group">
                <select v-model="formData.type" class="md-select">
                  <option value="公告">公告</option>
                  <option value="更新">更新</option>
                  <option value="活动">活动</option>
                </select>
                <input v-model="formData.title" class="md-input" placeholder="输入动态标题" />
              </div>

              <!-- 图片预览/上传 -->
              <div class="md-upload-zone">
                <div v-if="formData.imageUrl" class="md-preview-container">
                  <img :src="formData.imageUrl" class="md-preview-img" />
                  <button class="md-icon-btn" @click="formData.imageUrl = null">✕</button>
                </div>
                <label v-else class="md-upload-label">
                  <input type="file" accept="image/*" @change="handleUploadCover" hidden />
                  <span>＋ 上传封面图</span>
                </label>
              </div>

              <textarea v-model="formData.content" class="md-textarea" placeholder="在此书写正文内容..."></textarea>
            </div>

            <div class="md-modal-actions">
              <button class="md-btn-flat" @click="closeModal">取消</button>
              <button class="md-btn-primary" @click="handleSubmit" :disabled="submitting || isUploading">
                {{ submitting ? '提交中...' : (editingId ? '保存更改' : '确认发布') }}
              </button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { newsApi, type CreateNewsDto } from '@/api/news'; 
import { useCos } from '@/composables/useCos'; 

// 1. 初始化状态
const { uploadFile, isUploading } = useCos();
const newsList = ref<any[]>([]);
const loading = ref(false);
const isModalOpen = ref(false);
const submitting = ref(false);
const editingId = ref<string | null>(null);

const formData = ref<CreateNewsDto>({
  title: '',
  type: '公告',
  imageUrl: null,
  content: ''
});

// 2. 核心数据逻辑
const fetchNews = async () => {
  loading.value = true;
  try {
    newsList.value = await newsApi.getAllNews();
  } catch (error) {
    console.error('API Error');
  } finally {
    loading.value = false;
  }
};

onMounted(fetchNews);

// 3. 弹窗控制逻辑
const openModal = () => {
  editingId.value = null;
  formData.value = { title: '', type: '公告', imageUrl: null, content: '' };
  isModalOpen.value = true;
};

const handleEdit = (item: any) => {
  editingId.value = item.id;
  formData.value = { ...item };
  isModalOpen.value = true;
};

const closeModal = () => {
  isModalOpen.value = false;
  editingId.value = null;
};

// 4. 文件上传
const handleUploadCover = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;
  try {
    const result = await uploadFile(file, 'news_covers'); 
    formData.value.imageUrl = result.url;
  } catch (error) {
    alert('上传失败');
  } finally {
    target.value = ''; 
  }
};

// 5. 🌟 新增：上下架切换
const handleTogglePublish = async (item: any) => {
  const original = item.isPublished;
  item.isPublished = !original; // 乐观更新
  try {
    await newsApi.togglePublish(item.id, item.isPublished);
  } catch (error) {
    item.isPublished = original; // 失败回滚
    alert('同步失败');
  }
};

// 6. 提交与更新
const handleSubmit = async () => {
  if (!formData.value.title.trim()) return;
  submitting.value = true;
  try {
    if (editingId.value) {
      await newsApi.updateNews(editingId.value, formData.value);
    } else {
      await newsApi.createNews(formData.value);
    }
    closeModal();
    await fetchNews();
  } catch (error) {
    alert('操作失败');
  } finally {
    submitting.value = false;
  }
};

// 7. 删除
const handleDelete = async (id: string) => {
  if (!confirm('确定彻底移除此内容吗？')) return;
  try {
    await newsApi.deleteNews(id);
    newsList.value = newsList.value.filter(n => n.id !== id);
  } catch (error) {
    alert('删除失败');
  }
};

const formatDate = (dateString: string) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' });
};
</script>

<style scoped>
/* 
  MD 3 极致简约设计语言
*/
.md-admin-container {
  --md-primary: #000;
  --md-text: #1a1a1a;
  --md-sub: #999;
  --md-danger: #ff4d4f;
  max-width: 1100px;
  margin: 0 auto;
  padding: 80px 20px;
  background: #fff;
  min-height: 100vh;
}

/* 头部排版 */
.md-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 80px;
}
.md-title { font-size: 3rem; font-weight: 200; letter-spacing: -2px; margin: 0; }
.md-subtitle { color: var(--md-sub); margin-top: 10px; font-size: 0.9rem; }

.md-fab-button {
  background: var(--md-primary); color: #fff; border: none;
  padding: 14px 28px; border-radius: 40px; cursor: pointer;
  display: flex; align-items: center; gap: 10px; font-weight: 600;
  transition: transform 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}
.md-fab-button:hover { transform: scale(1.05) translateY(-2px); }

/* 列表样式 */
.md-list-item {
  display: grid; grid-template-columns: 100px 1fr 100px 220px;
  align-items: center; gap: 40px; padding: 40px 0;
  border-bottom: 1px solid #f0f0f0; transition: all 0.3s;
}
.md-list-item:hover { border-bottom-color: #000; }
.md-list-item.is-draft-row { opacity: 0.5; }

.md-img { width: 100px; height: 60px; object-fit: cover; filter: grayscale(100%); transition: 0.4s; }
.md-list-item:hover .md-img { filter: grayscale(0%); }
.md-img-placeholder { width: 100px; height: 60px; background: #f9f9f9; }

.item-tag { font-size: 0.7rem; text-transform: uppercase; letter-spacing: 2px; color: var(--md-sub); }
.item-date { font-size: 0.7rem; color: #ccc; margin-left: 15px; }
.item-title { margin: 8px 0 0; font-size: 1.2rem; font-weight: 500; }

.md-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 10px; }
.is-active { background: #52c41a; box-shadow: 0 0 10px rgba(82,196,26,0.2); }
.is-draft { background: #d9d9d9; }

.item-status { font-size: 0.85rem; color: var(--md-sub); font-weight: 500; }

/* 按钮样式 */
.text-btn {
  background: none; border: none; font-size: 0.85rem;
  cursor: pointer; font-weight: 700; opacity: 0.3; transition: 0.2s;
  margin-left: 20px;
}
.text-btn:hover { opacity: 1; }
.text-btn.is-status { color: #0052cc; opacity: 0.6; }
.text-btn.is-danger { color: var(--md-danger); }

/* 模态框样式 */
.md-modal-overlay {
  position: fixed; inset: 0; background: rgba(255,255,255,0.98);
  display: flex; align-items: center; justify-content: center; z-index: 1000;
  backdrop-filter: blur(10px);
}
.md-modal-card { width: 100%; max-width: 700px; padding: 60px; }
.modal-header { font-size: 2.5rem; font-weight: 200; margin-bottom: 60px; letter-spacing: -1px; }

.md-input-group { display: flex; gap: 30px; border-bottom: 2px solid #000; padding-bottom: 15px; margin-bottom: 50px; }
.md-select { border: none; background: none; font-weight: 700; outline: none; font-size: 1.1rem; cursor: pointer; }
.md-input { border: none; flex: 1; font-size: 1.5rem; outline: none; background: none; font-weight: 300; }
.md-textarea {
  width: 100%; border: none; min-height: 250px; font-size: 1.1rem;
  resize: none; outline: none; line-height: 2; background: none;
}

.md-upload-zone { margin-bottom: 50px; }
.md-upload-label { padding: 12px 24px; border: 1px dashed #ddd; font-size: 0.8rem; cursor: pointer; color: var(--md-sub); }
.md-preview-container { position: relative; display: inline-block; }
.md-preview-img { max-height: 200px; border-radius: 4px; }
.md-icon-btn {
  position: absolute; top: -10px; right: -10px; background: #000; color: #fff;
  border: none; border-radius: 50%; width: 24px; height: 24px; cursor: pointer;
}

.md-modal-actions { margin-top: 60px; display: flex; justify-content: flex-end; gap: 30px; }
.md-btn-flat { background: none; border: none; font-weight: 700; cursor: pointer; color: var(--md-sub); font-size: 1rem; }
.md-btn-primary { background: #000; color: #fff; border: none; padding: 16px 48px; font-weight: 700; cursor: pointer; border-radius: 4px; }

/* 动画效果 */
.md-fade-enter-active, .md-fade-leave-active { transition: all 0.5s cubic-bezier(0.19, 1, 0.22, 1); }
.md-fade-enter-from, .md-fade-leave-to { opacity: 0; transform: translateY(20px); }

.md-loader { font-size: 1.5rem; font-weight: 200; text-align: center; padding: 100px; color: #eee; }
.md-empty { text-align: center; padding: 100px; color: #ccc; font-style: italic; }

.modal-close-btn {
  position: absolute; top: 40px; right: 40px; background: none; border: none;
  font-size: 2rem; font-weight: 100; cursor: pointer; color: #ccc;
}
.modal-close-btn:hover { color: #000; }
</style>