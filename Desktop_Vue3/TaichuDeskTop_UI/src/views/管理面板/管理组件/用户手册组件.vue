<template>
  <div class="manual-manager">
    <!-- 左侧：手册章节导航/列表 -->
    <aside class="doc-sidebar">
      <div class="sidebar-header">
        <span class="header-title">手册章节</span>
        <button class="btn-create" @click="handleCreateDoc">+ 新建文档</button>
      </div>

      <div class="doc-list">
        <div 
          v-for="item in docList" 
          :key="item.id"
          class="doc-item"
          :class="{ 'is-selected': currentDoc.id === item.id }"
          @click="selectDoc(item)"
        >
          <div class="doc-title-row">
            <span class="doc-name">{{ item.title || '未命名文档' }}</span>
            <span class="status-badge" :class="{ 'published': item.isPublished }">
              {{ item.isPublished ? '已发布' : '草稿' }}
            </span>
          </div>
          <div class="doc-meta">
            <span>/{{ item.slug }}</span>
            <span class="cat-tag">{{ item.categoryName }}</span>
          </div>
        </div>
        <div v-if="docList.length === 0 && !loadingList" class="empty-hint">
          暂无文档，点击上方新建
        </div>
      </div>
    </aside>

    <!-- 右侧：文档编辑工作区 -->
    <main class="doc-editor-main">
      <div class="editor-header">
        <div class="input-fields">
          <input 
            v-model="currentDoc.title" 
            placeholder="文档标题 (例如: 快速上手)" 
            class="input-clean title-input" 
          />
          <div class="sub-fields">
            <input 
              v-model="currentDoc.slug" 
              placeholder="路由标识: quick-start" 
              class="input-clean slug-input" 
            />
            
            <!-- 🌟 替换原有的手填分类，改为与后端关联的下拉框 -->
            <select v-model="currentDoc.categoryId" class="input-clean cat-select">
              <option v-for="cat in categories" :key="cat.id" :value="cat.id">
                {{ cat.name }}
              </option>
            </select>
            <button class="btn-text-action" @click="openCreateCategoryModal">+ 新建分类</button>

            <!-- 排序权重 -->
            <input 
              type="number" 
              v-model.number="currentDoc.sortOrder" 
              placeholder="排序" 
              class="input-clean sort-input" 
              title="数值越小越靠前"
            />

            <label class="publish-switch">
              <input type="checkbox" v-model="currentDoc.isPublished" />
              <span>立即公开</span>
            </label>
          </div>
        </div>

        <div class="action-buttons">
          <button v-if="currentDoc.id" class="btn-delete" @click="handleDeleteDoc">删除</button>
          <button class="btn-save" :disabled="isSaving" @click="saveDocument">
            {{ isSaving ? '保存中...' : '保存更改' }}
          </button>
        </div>
      </div>

      <!-- 快捷工具栏：超链接与图片 -->
      <div class="editor-toolbar">
        <button class="tool-btn" @click="insertLink">🔗 插入超链接</button>
        <label class="tool-btn upload-btn" :class="{ 'is-disabled': isUploading }">
          {{ isUploading ? '直传COS中...' : '🖼 插入图片' }}
          <input 
            type="file" 
            accept="image/*" 
            class="file-hidden" 
            :disabled="isUploading" 
            @change="handleFileChange" 
          />
        </label>
        <span class="toolbar-hint">支持截图后在正文直接 Ctrl+V 粘贴，自动直传腾讯云 COS</span>
      </div>

      <!-- 编辑区与实时预览 -->
      <div class="editor-workspace">
        <div class="pane edit-pane">
          <textarea
            ref="textareaRef"
            v-model="currentDoc.content"
            class="markdown-textarea"
            placeholder="在此使用 Markdown 编写手册正文，支持直接粘贴图片..."
            @paste="handlePaste"
          ></textarea>
        </div>
        <div class="pane preview-pane">
          <div class="preview-header">实时效果预览</div>
          <div class="markdown-preview" v-html="renderedContent"></div>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';

// 🌟 1. 引入封装好的真实依赖（若相对路径不同可根据文件目录微调）
import request from '@/utils/request';
import { useCos } from '@/composables/useCos'; 

// 🌟 2. 解构出现成的 COS 凭据直传方法
const { uploadFile, isUploading } = useCos(); 

interface Category {
  id: number;
  name: string;
  sortOrder: number;
}

interface ManualDoc {
  id?: number;
  categoryId: number;
  categoryName?: string;
  slug: string;
  title: string;
  content: string;
  sortOrder: number;
  isPublished: boolean;
}

const docList = ref<ManualDoc[]>([]);
const categories = ref<Category[]>([]);
const loadingList = ref(false);
const isSaving = ref(false);
const textareaRef = ref<HTMLTextAreaElement | null>(null);

const currentDoc = ref<ManualDoc>({
  categoryId: 1,
  slug: '',
  title: '',
  content: '',
  sortOrder: 0,
  isPublished: true
});

// 🌟 3. 从 .NET ManualController 拉取分类与文章数据
const initData = async () => {
  loadingList.value = true;
  try {
    const catRes: any = await request.get('/admin/manual/categories');
    categories.value = catRes.data || [];

    const docRes: any = await request.get('/admin/manual/articles');
    docList.value = docRes.data || [];

    if (docList.value.length > 0) {
      currentDoc.value = { ...docList.value[0] };
    } else {
      handleCreateDoc();
    }
  } catch (err: any) {
    console.error('获取文档列表失败:', err);
  } finally {
    loadingList.value = false;
  }
};

onMounted(() => {
  initData();
});

const selectDoc = (doc: ManualDoc) => {
  currentDoc.value = { ...doc };
};

const handleCreateDoc = () => {
  currentDoc.value = {
    id: undefined,
    categoryId: categories.value[0]?.id || 1,
    title: '',
    slug: '',
    content: '## 新章节内容\n\n在此输入文字，或直接按 Ctrl+V 粘贴截图。',
    sortOrder: docList.value.length + 1,
    isPublished: true
  };
};

// 新建分类
const openCreateCategoryModal = async () => {
  const name = window.prompt('请输入新建分类名称 (例如: 进阶技巧):');
  if (!name || !name.trim()) return;

  try {
    await request.post('/admin/manual/category', {
      name: name.trim(),
      sortOrder: categories.value.length + 1
    });
    const catRes: any = await request.get('/admin/manual/categories');
    categories.value = catRes.data || [];
    const created = categories.value.find(c => c.name === name.trim());
    if (created) currentDoc.value.categoryId = created.id;
  } catch (err: any) {
    alert(err.message || '创建分类失败');
  }
};

// 光标位置插入文本
const insertTextAtCursor = (text: string) => {
  const textarea = textareaRef.value;
  if (!textarea) return;

  const start = textarea.selectionStart;
  const end = textarea.selectionEnd;
  const val = currentDoc.value.content;

  currentDoc.value.content = val.substring(0, start) + text + val.substring(end);

  setTimeout(() => {
    textarea.focus();
    textarea.selectionStart = textarea.selectionEnd = start + text.length;
  }, 0);
};

// 插入超链接
const insertLink = () => {
  const url = window.prompt('请输入目标链接 URL:', 'https://');
  if (!url) return;
  const desc = window.prompt('请输入链接显示文字:', '点击查看详情') || '链接';
  insertTextAtCursor(`[${desc}](${url})`);
};

// 🌟 4. 彻底替换原有的模拟上传：直接调用 useCos 推送腾讯云 COS
const uploadToCos = async (file: File): Promise<string> => {
  const res = await uploadFile(file, 'manual'); //[cite: 4]
  return res.url; // 自动获得 https://img.bianyuzhou.com/manual/images/xxx.png[cite: 4]
};

// 处理本地选择图片
const handleFileChange = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  if (input.files && input.files[0]) {
    const file = input.files[0];
    const placeholder = `\n![正在直传图片至COS...]()\n`;
    insertTextAtCursor(placeholder);

    try {
      const cosUrl = await uploadToCos(file);
      currentDoc.value.content = currentDoc.value.content.replace(
        placeholder,
        `\n![${file.name}](${cosUrl})\n`
      );
    } catch (err) {
      alert('上传到 COS 失败，请检查网络或临时密钥配置');
      currentDoc.value.content = currentDoc.value.content.replace(placeholder, '');
    } finally {
      input.value = '';
    }
  }
};

// 🌟 5. 核心：直接在输入框粘贴截图 (Ctrl+V) 直传 COS
const handlePaste = async (e: ClipboardEvent) => {
  const items = e.clipboardData?.items;
  if (!items) return;

  for (const item of items) {
    if (item.type.includes('image')) {
      e.preventDefault();
      const file = item.getAsFile();
      if (!file) continue;

      const placeholder = `\n![截图正在上传至COS...]()\n`;
      insertTextAtCursor(placeholder);

      try {
        const cosUrl = await uploadToCos(file);
        currentDoc.value.content = currentDoc.value.content.replace(
          placeholder,
          `\n![功能截图](${cosUrl})\n`
        );
      } catch (err) {
        alert('截图直传 COS 失败');
        currentDoc.value.content = currentDoc.value.content.replace(placeholder, '');
      }
      break;
    }
  }
};

// 🌟 6. 保存文档至 MySQL
const saveDocument = async () => {
  if (!currentDoc.value.title.trim()) {
    alert('请填写文档标题');
    return;
  }
  if (!currentDoc.value.slug.trim()) {
    alert('请填写路由标识 (slug)');
    return;
  }

  isSaving.value = true;
  try {
    await request.post('/admin/manual/article', {
      id: currentDoc.value.id,
      categoryId: currentDoc.value.categoryId,
      slug: currentDoc.value.slug.trim(),
      title: currentDoc.value.title.trim(),
      content: currentDoc.value.content,
      sortOrder: currentDoc.value.sortOrder,
      isPublished: currentDoc.value.isPublished
    });

    alert('文档保存成功，前台已实时生效！');
    await initData();
  } catch (err: any) {
    alert(err.message || '保存文档失败');
  } finally {
    isSaving.value = false;
  }
};

// 7. 删除文档
const handleDeleteDoc = async () => {
  if (!currentDoc.value.id) return;
  if (!window.confirm(`确定要彻底删除文档《${currentDoc.value.title}》吗？`)) return;

  try {
    await request.delete(`/admin/manual/article/${currentDoc.value.id}`);
    alert('删除成功');
    await initData();
  } catch (err: any) {
    alert(err.message || '删除失败');
  }
};

// 8. 实时预览 Markdown 解析
const renderedContent = computed(() => {
  if (!currentDoc.value.content) return '<p style="color:#aaa;">暂无内容预览</p>';
  return currentDoc.value.content
    .replace(/^### (.*$)/gim, '<h3>$1</h3>')
    .replace(/^## (.*$)/gim, '<h2>$1</h2>')
    .replace(/^# (.*$)/gim, '<h1>$1</h1>')
    .replace(/\!\[(.*?)\]\((.*?)\)/gim, '<img alt="$1" src="$2" class="preview-img" />')
    .replace(/\[(.*?)\]\((.*?)\)/gim, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/^\- (.*$)/gim, '<li>$1</li>')
    .replace(/\n/gim, '<br />');
});
</script>

<style scoped>
.manual-manager {
  display: flex;
  width: 100%;
  height: 100%;
  background: #fff;
  border-radius: 8px;
  border: 1px solid #f0f0f0;
  overflow: hidden;
}

/* 左侧目录 */
.doc-sidebar {
  width: 280px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  background: #fafafa;
}
.sidebar-header {
  height: 56px;
  padding: 0 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #f0f0f0;
}
.header-title { font-size: 0.9rem; font-weight: 600; color: #333; }
.btn-create {
  background: #111; color: #fff; border: none; padding: 5px 12px;
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: 0.2s;
}
.btn-create:hover { background: #333; }
.doc-list { flex: 1; overflow-y: auto; }
.empty-hint { padding: 32px 16px; text-align: center; color: #aaa; font-size: 0.8rem; }
.doc-item {
  padding: 12px 16px; border-bottom: 1px solid #f2f2f2;
  cursor: pointer; transition: 0.2s;
}
.doc-item:hover { background: #f0f0f0; }
.doc-item.is-selected { background: #fff; border-left: 3px solid #111; }
.doc-title-row { display: flex; justify-content: space-between; align-items: center; }
.doc-name { font-size: 0.85rem; font-weight: 500; color: #222; }
.status-badge { font-size: 0.65rem; color: #999; background: #eee; padding: 2px 6px; border-radius: 4px; }
.status-badge.published { color: #00875a; background: #e3fcef; }
.doc-meta { font-size: 0.7rem; color: #999; margin-top: 4px; display: flex; justify-content: space-between; }
.cat-tag { background: #f0f0f0; padding: 1px 4px; border-radius: 3px; }

/* 右侧工作区 */
.doc-editor-main { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.editor-header {
  padding: 16px 24px; border-bottom: 1px solid #f0f0f0;
  display: flex; justify-content: space-between; align-items: flex-start; gap: 16px;
}
.input-fields { display: flex; flex-direction: column; gap: 10px; flex: 1; }
.input-clean {
  border: 1px solid #e5e5e5; border-radius: 4px; padding: 6px 12px;
  outline: none; background: #fff; font-size: 0.85rem;
}
.input-clean:focus { border-color: #111; }
.title-input { font-size: 1.1rem; font-weight: 600; }
.sub-fields { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.slug-input { width: 170px; }
.cat-select { min-width: 120px; cursor: pointer; }
.sort-input { width: 90px; }
.btn-text-action {
  background: none; border: none; color: #0969da; cursor: pointer;
  font-size: 0.75rem; padding: 0 4px;
}
.btn-text-action:hover { text-decoration: underline; }
.publish-switch { display: flex; align-items: center; gap: 4px; font-size: 0.8rem; cursor: pointer; user-select: none; }

.action-buttons { display: flex; gap: 8px; align-items: center; }
.btn-save {
  background: #111; color: #fff; border: none; padding: 8px 20px;
  border-radius: 4px; font-weight: 500; cursor: pointer; font-size: 0.85rem;
}
.btn-save:hover { background: #333; }
.btn-delete {
  background: #fff; color: #cf222e; border: 1px solid #e5e5e5; padding: 8px 16px;
  border-radius: 4px; font-size: 0.85rem; cursor: pointer;
}
.btn-delete:hover { background: #ffebe9; border-color: #ff8182; }

/* 快捷工具栏 */
.editor-toolbar {
  display: flex; align-items: center; gap: 12px; padding: 8px 24px;
  background: #fafafa; border-bottom: 1px solid #f0f0f0;
}
.tool-btn {
  background: #fff; border: 1px solid #ddd; padding: 4px 10px;
  border-radius: 4px; font-size: 0.8rem; cursor: pointer;
}
.tool-btn:hover { background: #f0f0f0; }
.tool-btn.is-disabled { opacity: 0.6; cursor: not-allowed; }
.file-hidden { display: none; }
.toolbar-hint { font-size: 0.75rem; color: #aaa; margin-left: auto; }

/* 双栏工作区 */
.editor-workspace { flex: 1; display: flex; overflow: hidden; }
.pane { flex: 1; height: 100%; overflow-y: auto; padding: 20px 24px; }
.edit-pane { border-right: 1px solid #f0f0f0; }
.markdown-textarea {
  width: 100%; height: 100%; border: none; outline: none;
  font-family: Consolas, Monaco, monospace; font-size: 0.9rem; line-height: 1.6; resize: none;
}
.preview-pane { background: #fff; }
.preview-header { font-size: 0.75rem; color: #bbb; text-transform: uppercase; margin-bottom: 12px; }

:deep(.markdown-preview) { font-size: 0.9rem; line-height: 1.6; color: #222; }
:deep(.markdown-preview h1) { font-size: 1.5rem; margin-bottom: 12px; border-bottom: 1px solid #eee; padding-bottom: 8px; }
:deep(.markdown-preview h2) { font-size: 1.3rem; margin: 16px 0 8px; }
:deep(.markdown-preview h3) { font-size: 1.1rem; margin: 12px 0 6px; }
:deep(.markdown-preview a) { color: #0969da; text-decoration: underline; }
:deep(.preview-img) { max-width: 100%; border-radius: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); margin: 8px 0; }
</style>