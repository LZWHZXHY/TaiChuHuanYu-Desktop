<template>
  <aside class="spirit-sidebar">
    <div class="space-selector-area">
      <div class="current-space-label" @click.stop="toggleSpaceList">
        <span class="space-text">{{ currentSpaceName }}</span>
        <svg class="chevron-icon" :class="{ rotated: isSpaceListOpen }" width="8" height="8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9l6 6 6-6" /></svg>
      </div>

      <transition name="fade">
        <div v-if="isSpaceListOpen" class="space-dropdown" @click.stop>
          <div v-for="space in spaces" :key="space.id" class="space-opt" :class="{ active: currentSpaceId === space.id }" @click="switchSpace(space)">
            <input v-if="editingSpaceId === space.id" 
              class="inline-input"
              v-model="tempName" 
              @blur="saveSpaceName(space)" 
              @keyup.enter="saveSpaceName(space)"
              v-focus
            />
            <span v-else class="opt-name">{{ space.name }}</span>

            <div class="space-actions">
              <span @click.stop="startRenameSpace(space)">Edit</span>
              <span class="danger" @click.stop="startDeleteSpace(space)" v-if="spaces.length > 1">Delete</span>
            </div>
          </div>

          <div v-if="isCreatingSpace" class="space-opt">
            <input class="inline-input" v-model="tempName" placeholder="New World..." @blur="cancelCreateSpace" @keyup.enter="confirmCreateSpace" v-focus />
          </div>
          <div v-else-if="quota.usedSpaces < quota.maxSpaces" class="space-footer-action" @click="startCreateSpace">
            New Space
          </div>
        </div>
      </transition>
    </div>

    <div class="sidebar-header">
      <div class="header-left">
        <span class="index-label">INDEX</span>
        <span class="quota-text">{{ quota.usedNotes }}/{{ quota.maxNotes }}</span>
      </div>
      
      <div class="header-actions" style="position: relative;">
        <span class="text-btn" @click="$emit('create', 'folder')">Folder</span>
        
        <span class="text-btn active" @click.stop="toggleCreateMenu">
          New <span style="font-size: 8px; margin-left: 2px;">▼</span>
        </span>

        <transition name="fade">
          <div v-if="isCreateMenuOpen" class="create-dropdown" @click.stop>
            <div class="create-opt" @click="handleCreateWithType('note')">笔记 (Note)</div>
            <div class="create-opt" @click="handleCreateWithType('post')">简语 (Post)</div>
            <div class="create-opt" @click="handleCreateWithType('blog')">随笔 (Blog)</div>
            <div class="create-opt" @click="handleCreateWithType('wiki')">词条 (Wiki)</div>
            <div class="create-opt" @click="handleCreateWithType('char')">角色 (Char)</div>
            <div class="create-opt" @click="handleCreateWithType('art')">画廊 (Art)</div>
          </div>
        </transition>
      </div>
    </div>

    <div class="sidebar-search">
      <input type="text" v-model="searchQuery" placeholder="Search..." spellcheck="false" />
    </div>

    <nav class="note-list">
      <div class="section-group" @dragover.prevent @drop="onDrop($event, null)">
        <div 
          v-for="note in filteredRootNotes" 
          :key="note.id" 
          class="note-item" 
          :class="['type-' + (note.type || 'note'), { active: activeId === note.id }]" 
          @click="$emit('select', note.id)"
          draggable="true" 
          @dragstart="onDragStart($event, note.id)"
        >
          <div class="item-content">
            <span class="item-title">{{ note.title || 'Untitled' }}</span>
            <span class="type-label">{{ note.type || 'note' }}</span>
          </div>
          <div class="item-hover-actions">
            <span class="danger" @click.stop="startDeleteItem(note.id)">Delete</span>
          </div>
        </div>
      </div>

      <div class="section-group">
        <div v-for="folder in folders" :key="folder.id" class="folder-container">
          <div 
            class="folder-header" 
            @click="toggleFolder(folder.id)" 
            @dragover.prevent
            @drop="onDrop($event, folder.id)"
          >
            <span class="folder-arrow" :class="{ rotated: expandedFolders.has(folder.id) }"></span>
            
            <input v-if="editingFolderId === folder.id" 
              class="inline-input" v-model="tempName" 
              @blur="saveFolderName(folder)" @keyup.enter="saveFolderName(folder)" v-focus />
            <span v-else class="item-title" @dblclick.stop="startRenameFolder(folder)">{{ folder.title }}</span>

            <div class="item-hover-actions">
              <span @click.stop="$emit('create', 'note', folder.id)">Add</span>
            </div>
          </div>

          <transition name="expand">
            <div v-if="expandedFolders.has(folder.id)" class="folder-content">
              <div 
                v-for="subNote in filteredNotesInFolder(folder.id)" 
                :key="subNote.id" 
                class="note-item sub" 
                :class="['type-' + (subNote.type || 'note'), { active: activeId === subNote.id }]" 
                @click="$emit('select', subNote.id)"
                draggable="true"
                @dragstart="onDragStart($event, subNote.id)"
              >
                <div class="item-content">
                  <span class="item-title">{{ subNote.title || 'Untitled' }}</span>
                  <span class="type-label">{{ subNote.type || 'note' }}</span>
                </div>
                <div class="item-hover-actions">
                  <span class="danger" @click.stop="startDeleteItem(subNote.id)">Delete</span>
                </div>
              </div>
            </div>
          </transition>
        </div>
      </div>
    </nav>

    <div class="sidebar-footer">All synced</div>

    <transition name="fade">
      <div v-if="confirmDialog.visible" class="spirit-overlay" @click="confirmDialog.visible = false">
        <div class="spirit-dialog" @click.stop>
          <p class="dialog-msg">{{ confirmDialog.message }}</p>
          <div class="dialog-actions">
            <span class="dialog-btn" @click="confirmDialog.visible = false">Cancel</span>
            <span class="dialog-btn danger" @click="executeConfirm">Confirm</span>
          </div>
        </div>
      </div>
    </transition>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';

const vFocus = { mounted: (el: HTMLElement) => el.focus() };

const props = defineProps<{ 
  activeId: string;
  filters?: Record<string, boolean>; 
}>();
const emit = defineEmits(['select', 'create']);

const { 
  folders, rootNotes, getNotesInFolder, 
  updateNoteTitle, deleteNote, moveNote, currentSpaceId, fetchAllNotes 
} = useSpiritData();

const quota = ref({ usedNotes: 0, maxNotes: 100, usedSpaces: 0, maxSpaces: 1 });
const isSpaceListOpen = ref(false);
const searchQuery = ref('');
const expandedFolders = ref(new Set<string>());
const spaces = ref<any[]>([]);
const editingSpaceId = ref<string | null>(null);
const editingFolderId = ref<string | null>(null);
const isCreatingSpace = ref(false);
const tempName = ref('');
const confirmDialog = ref({ visible: false, message: '', onConfirm: () => {} });

const isCreateMenuOpen = ref(false);

const toggleCreateMenu = () => {
  isCreateMenuOpen.value = !isCreateMenuOpen.value;
};

const handleCreateWithType = (type: string) => {
  emit('create', type);
  isCreateMenuOpen.value = false;
};

const closeDropdowns = () => {
  if (isCreateMenuOpen.value) isCreateMenuOpen.value = false;
  if (isSpaceListOpen.value) isSpaceListOpen.value = false;
};

const filteredRootNotes = computed(() => {
  return rootNotes.value.filter(n => {
    if (n.type === 'folder' || n.status !== 0) return false;
    if (n.showInSidebar === false) return false;
    if (props.filters && props.filters[n.type] === false) return false;
    return true;
  });
});

const filteredNotesInFolder = (folderId: string) => {
  return getNotesInFolder(folderId).filter(n => {
    if (n.status !== 0) return false;
    if (n.showInSidebar === false) return false;
    if (props.filters && props.filters[n.type] === false) return false;
    return true;
  });
};

const currentSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || 'Spirit');

const toggleSpaceList = () => isSpaceListOpen.value = !isSpaceListOpen.value;

const switchSpace = async (space: any) => {
  currentSpaceId.value = space.id;
  isSpaceListOpen.value = false;
  await fetchAllNotes();
};

const startCreateSpace = () => { tempName.value = ''; isCreatingSpace.value = true; };
const cancelCreateSpace = () => { isCreatingSpace.value = false; };

const confirmCreateSpace = async () => {
  if (tempName.value.trim()) {
    const newSpace: any = await lingmaiApi.createSpace(tempName.value.trim());
    spaces.value.push(newSpace);
    await switchSpace(newSpace);
    await fetchQuota();
  }
  isCreatingSpace.value = false;
};

const startRenameSpace = (space: any) => { editingSpaceId.value = space.id; tempName.value = space.name; };
const saveSpaceName = async (space: any) => {
  if (tempName.value.trim() && tempName.value !== space.name) {
    await lingmaiApi.updateSpaceName(space.id, tempName.value.trim());
    space.name = tempName.value;
  }
  editingSpaceId.value = null;
};

const startDeleteSpace = (space: any) => {
  confirmDialog.value = {
    visible: true,
    message: `归流此位面: 「${space.name}」吗？其内所有碎片将一并消失。`,
    onConfirm: async () => {
      await lingmaiApi.deleteSpace(space.id);
      spaces.value = spaces.value.filter(s => s.id !== space.id);
      if (currentSpaceId.value === space.id && spaces.value.length > 0) {
        await switchSpace(spaces.value[0]);
      }
      await fetchQuota();
    }
  };
};

const onDragStart = (e: DragEvent, noteId: string) => {
  if (e.dataTransfer) {
    e.dataTransfer.setData('noteId', noteId);
    e.dataTransfer.effectAllowed = 'move';
  }
};

const onDrop = async (e: DragEvent, targetFolderId: string | null) => {
  const noteId = e.dataTransfer?.getData('noteId');
  if (noteId) {
    await moveNote(noteId, targetFolderId);
    if (targetFolderId) expandedFolders.value.add(targetFolderId);
    await fetchAllNotes();
  }
};

const startRenameFolder = (folder: any) => { editingFolderId.value = folder.id; tempName.value = folder.title; };
const saveFolderName = async (folder: any) => {
  if (tempName.value.trim() && tempName.value !== folder.title) {
    await updateNoteTitle(folder.id, tempName.value.trim());
  }
  editingFolderId.value = null;
};

// 【重点修复】真正执行物理删除并刷新UI
const startDeleteItem = (id: string) => {
  confirmDialog.value = {
    visible: true,
    message: "彻底粉碎这枚灵魂碎片吗？此操作不可逆。",
    onConfirm: async () => {
      await deleteNote(id);
      await fetchAllNotes();
      await fetchQuota();
    }
  };
};

const executeConfirm = () => { confirmDialog.value.onConfirm(); confirmDialog.value.visible = false; };

const fetchQuota = async () => {
  try { quota.value = await lingmaiApi.getQuota(); } catch (e) { console.error(e); }
};

const initSpaces = async () => {
  const res: any = await lingmaiApi.getSpaces(); 
  spaces.value = res;
  if (spaces.value.length > 0 && (!currentSpaceId.value || currentSpaceId.value.startsWith('0000'))) {
    await switchSpace(spaces.value[0]);
  }
};

const toggleFolder = (id: string) => expandedFolders.value.has(id) ? expandedFolders.value.delete(id) : expandedFolders.value.add(id);

onMounted(() => { 
  initSpaces(); 
  fetchQuota(); 
  window.addEventListener('click', closeDropdowns);
});

onUnmounted(() => {
  window.removeEventListener('click', closeDropdowns);
});
</script>

<style scoped>
/* 基础容器 */
.spirit-sidebar { display: flex; flex-direction: column; height: 100%; background: #ffffff; color: #1d1d1f; border-right: 1px solid #f2f2f2; }
.inline-input { border: none; background: transparent; padding: 0; font-size: inherit; color: #0066cc; outline: none; width: 100%; border-bottom: 1px solid #0066cc; }
.space-selector-area { padding: 40px 24px 20px; position: relative; }
.current-space-label { display: flex; align-items: center; gap: 6px; cursor: pointer; font-size: 13px; font-weight: 500; }
.chevron-icon { color: #c7c7cc; transition: transform 0.3s; }
.chevron-icon.rotated { transform: rotate(180deg); }
.space-dropdown { position: absolute; top: 65px; left: 24px; width: 200px; background: #ffffff; border: 1px solid #f2f2f2; border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,0.03); z-index: 100; padding: 6px; }
.space-opt { padding: 8px 12px; font-size: 12px; border-radius: 4px; display: flex; justify-content: space-between; cursor: pointer; transition: background 0.2s; }
.space-opt:hover { background: #fbfbfb; }
.space-opt.active { color: #0066cc; font-weight: 600; }
.space-actions { font-size: 10px; display: none; gap: 8px; color: #86868b; }
.space-opt:hover .space-actions { display: flex; }
.sidebar-header { padding: 10px 24px; display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #f9f9f9; }
.index-label { font-size: 10px; font-weight: 600; color: #86868b; letter-spacing: 0.05em; }
.quota-text { font-size: 9px; color: #c7c7cc; margin-left: 8px; }
.header-actions { display: flex; gap: 12px; }
.text-btn { font-size: 11px; color: #86868b; cursor: pointer; }
.text-btn.active { color: #0066cc; }

/* 创建菜单下拉 */
.create-dropdown { position: absolute; top: 30px; right: 0; width: 140px; background: #ffffff; border: 1px solid #f2f2f2; border-radius: 8px; box-shadow: 0 8px 24px rgba(0,0,0,0.06); z-index: 100; padding: 6px; }
.create-opt { padding: 8px 12px; font-size: 12px; color: #1d1d1f; border-radius: 4px; cursor: pointer; transition: background 0.2s; }
.create-opt:hover { background: #f5f5f7; color: #0066cc; }

.sidebar-search { padding: 0 24px 15px; }
.sidebar-search input { width: 100%; border: none; padding: 8px 0; font-size: 12px; border-bottom: 1px solid #f2f2f2; outline: none; background: transparent; }
.note-list { flex: 1; overflow-y: auto; padding: 0 16px; }

/* =========================================
   🌟 核心重构：多态列表排版体系 (无图标极简风)
========================================== */

.note-item, .folder-header { 
  border-radius: 6px; 
  cursor: pointer; 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  font-size: 13px; 
  color: #3a3a3c; 
}
.folder-header { padding: 10px 8px; }
/* 预留左侧光轴的位置 */
.note-item { padding: 10px 8px 10px 16px; position: relative; }
/* 文件夹内的节点再向右缩进 */
.note-item.sub { padding-left: 28px; }

.note-item:hover, .folder-header:hover { background: #fbfbfb; }
.note-item.active { background: #f5f5f7; color: #0066cc; font-weight: 500; }

/* 1. 侧边琉璃光轴 (色带暗示) */
.note-item::before {
  content: '';
  position: absolute;
  left: 6px;
  top: 50%;
  transform: translateY(-50%);
  width: 2px;
  height: 10px;
  border-radius: 2px;
  background: #d2d2d7;
  opacity: 0.4;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
.note-item.sub::before { left: 18px; }

.note-item:hover::before, .note-item.active::before {
  height: 16px;
  opacity: 1;
}

/* 形态专属色系呼应 */
.note-item.type-art::before { background: #af52de; }
.note-item.type-thought::before { background: #32ade6; }
.note-item.type-char::before { background: #ff9500; }
.note-item.type-wiki::before { background: #34c759; }
.note-item.type-note::before { background: #8e8e93; }

/* 2. 标题区组合 */
.item-content {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex: 1;
  min-width: 0; 
}

.item-title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: color 0.2s;
}

/* 🌟 多态字体暗示 (极简表达) */
.note-item.type-thought .item-title { font-style: italic; color: #6e6e73; }
.note-item.type-wiki .item-title { font-weight: 600; letter-spacing: 0.02em; }
.note-item.type-art .item-title { font-family: "Georgia", serif; letter-spacing: 0.02em; }

/* 3. 微型排版标签 (Micro Typography) */
.type-label {
  font-size: 8px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  opacity: 0.5;
  flex-shrink: 0;
  transition: opacity 0.2s;
}
.note-item.type-art .type-label { color: #af52de; }
.note-item.type-thought .type-label { color: #32ade6; }
.note-item.type-char .type-label { color: #ff9500; }
.note-item.type-wiki .type-label { color: #34c759; }
.note-item.type-note .type-label { color: #8e8e93; }

/* 4. 悬浮交互：标签消失，动作按钮显现 */
.item-hover-actions { display: none; gap: 8px; font-size: 10px; color: #c7c7cc; }
.danger { color: #ff3b30 !important; }

.note-item:hover .type-label { display: none; }
.note-item:hover .item-hover-actions, .folder-header:hover .item-hover-actions { display: flex; }

/* ========================================= */

/* 弹窗样式 */
.spirit-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.85); backdrop-filter: blur(8px); z-index: 5000; display: flex; align-items: center; justify-content: center; }
.spirit-dialog { background: #ffffff; padding: 40px; border: 1px solid #f2f2f2; border-radius: 12px; box-shadow: 0 15px 50px rgba(0,0,0,0.05); text-align: center; max-width: 320px; }
.dialog-msg { font-size: 14px; margin-bottom: 30px; color: #1d1d1f; line-height: 1.6; }
.dialog-actions { display: flex; justify-content: center; gap: 40px; }
.dialog-btn { font-size: 12px; color: #86868b; cursor: pointer; border-bottom: 1px solid transparent; }
.dialog-btn:hover { color: #1d1d1f; border-bottom-color: #1d1d1f; }
.dialog-btn.danger:hover { color: #ff3b30; border-bottom-color: #ff3b30; }

.sidebar-footer { padding: 20px 24px; font-size: 9px; color: #c7c7cc; text-transform: uppercase; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>