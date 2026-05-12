<template>
  <aside class="spirit-sidebar">
    <div class="space-selector-area">
      <div class="current-space-label" @click="toggleSpaceList">
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
      <div class="header-actions">
        <span class="text-btn" @click="$emit('create', 'folder')">Folder</span>
        <span class="text-btn active" @click="$emit('create', 'note')">New</span>
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
          :class="{ active: activeId === note.id }" 
          @click="$emit('select', note.id)"
          draggable="true" 
          @dragstart="onDragStart($event, note.id)"
        >
          <span class="item-title">{{ note.title || 'Untitled' }}</span>
          <div class="item-hover-actions">
            <span @click.stop="handleArchive(note.id)">Archive</span>
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
              <span @click.stop="handleArchive(folder.id)">Archive</span>
            </div>
          </div>

          <transition name="expand">
            <div v-if="expandedFolders.has(folder.id)" class="folder-content">
              <div 
                v-for="subNote in filteredNotesInFolder(folder.id)" 
                :key="subNote.id" 
                class="note-item sub" 
                :class="{ active: activeId === subNote.id }" 
                @click="$emit('select', subNote.id)"
                draggable="true"
                @dragstart="onDragStart($event, subNote.id)"
              >
                <span class="item-title">{{ subNote.title }}</span>
                <div class="item-hover-actions">
                  <span @click.stop="handleArchive(subNote.id)">Archive</span>
                </div>
              </div>
            </div>
          </transition>
        </div>
      </div>
    </nav>

    <div class="archive-vault-entry" @click="openArchiveVault">Vault</div>
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

    <transition name="fade">
      <div v-if="isArchiveOpen" class="archive-overlay" @click.self="isArchiveOpen = false">
        <div class="archive-panel">
          <div class="archive-header">
            <h3>VAULT</h3>
            <span class="close-txt" @click="isArchiveOpen = false">Close</span>
          </div>
          <div class="archive-body">
            <div v-if="archivedNotes.length === 0" class="empty-archive">
              <p>档案馆内尚无沉淀内容</p>
            </div>
            <div v-for="note in archivedNotes" :key="note.id" class="archive-row">
              <div class="item-info">
                <span class="row-title">{{ note.title || 'Untitled' }}</span>
                <span class="row-meta">{{ new Date(note.updatedAt).toLocaleDateString() }}</span>
              </div>
              <div class="row-actions">
                <span class="action-txt" @click="handleRestore(note.id)">Restore</span>
                <span class="action-txt danger" @click="startDeleteItem(note.id)">Delete</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </transition>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';

// 自动聚焦指令
const vFocus = { mounted: (el: HTMLElement) => el.focus() };

const props = defineProps<{ activeId: string }>();
const emit = defineEmits(['select', 'create']);

const { 
  notes, folders, rootNotes, getNotesInFolder, 
  updateNoteTitle, deleteNote, moveNote, currentSpaceId, fetchAllNotes 
} = useSpiritData();

// --- 状态定义 ---
const quota = ref({ usedNotes: 0, maxNotes: 100, usedSpaces: 0, maxSpaces: 1 });
const isSpaceListOpen = ref(false);
const searchQuery = ref('');
const expandedFolders = ref(new Set<string>());
const spaces = ref<any[]>([]);
const isArchiveOpen = ref(false);
const archivedNotes = ref<any[]>([]);
const editingSpaceId = ref<string | null>(null);
const editingFolderId = ref<string | null>(null);
const isCreatingSpace = ref(false);
const tempName = ref('');
const confirmDialog = ref({ visible: false, message: '', onConfirm: () => {} });

// --- 计算属性 ---
const filteredRootNotes = computed(() => rootNotes.value.filter(n => n.type !== 'folder' && n.status === 0));
const filteredNotesInFolder = (folderId: string) => getNotesInFolder(folderId).filter(n => n.status === 0);
const currentSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || 'Spirit');

// --- 空间管理逻辑 ---
const toggleSpaceList = () => isSpaceListOpen.value = !isSpaceListOpen.value;

const switchSpace = async (space: any) => {
  currentSpaceId.value = space.id;
  isSpaceListOpen.value = false;
  await fetchAllNotes();
};

const startCreateSpace = () => { tempName.value = ''; isCreatingSpace.value = true; };

// 🌟 修复：补全 cancelCreateSpace 函数
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

// 🌟 修复：补全 startDeleteSpace 函数
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

// --- 拖拽逻辑 ---
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

// --- 归档与删除 ---
const handleArchive = async (id: string) => {
  await lingmaiApi.archiveNote(id);
  await fetchAllNotes();
};

const openArchiveVault = async () => {
  if (!currentSpaceId.value) return;
  isArchiveOpen.value = true;
  archivedNotes.value = await lingmaiApi.getArchivedNoteList(currentSpaceId.value);
};

const handleRestore = async (id: string) => {
  await lingmaiApi.restoreNote(id);
  archivedNotes.value = archivedNotes.value.filter(n => n.id !== id);
  await fetchAllNotes();
};

const startRenameFolder = (folder: any) => { editingFolderId.value = folder.id; tempName.value = folder.title; };

const saveFolderName = async (folder: any) => {
  if (tempName.value.trim() && tempName.value !== folder.title) {
    await updateNoteTitle(folder.id, tempName.value.trim());
  }
  editingFolderId.value = null;
};

const startDeleteItem = (id: string) => {
  confirmDialog.value = {
    visible: true,
    message: "彻底粉碎这枚灵魂碎片吗？此操作不可逆。",
    onConfirm: async () => {
      await deleteNote(id);
      archivedNotes.value = archivedNotes.value.filter(n => n.id !== id);
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

onMounted(() => { initSpaces(); fetchQuota(); });
</script>

<style scoped>
/* 核心：纯白背景、极细线 */
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
.sidebar-search { padding: 0 24px 15px; }
.sidebar-search input { width: 100%; border: none; padding: 8px 0; font-size: 12px; border-bottom: 1px solid #f2f2f2; outline: none; background: transparent; }
.note-list { flex: 1; overflow-y: auto; padding: 0 16px; }
.note-item, .folder-header { padding: 10px 8px; border-radius: 6px; cursor: pointer; display: flex; justify-content: space-between; align-items: center; font-size: 13px; color: #3a3a3c; }
.note-item:hover, .folder-header:hover { background: #fbfbfb; }
.note-item.active { background: #f5f5f7; color: #0066cc; font-weight: 500; }
.item-hover-actions { display: none; gap: 8px; font-size: 10px; color: #c7c7cc; }
.note-item:hover .item-hover-actions, .folder-header:hover .item-hover-actions { display: flex; }
.danger { color: #ff3b30 !important; }

/* 弹窗样式 */
.spirit-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.85); backdrop-filter: blur(8px); z-index: 5000; display: flex; align-items: center; justify-content: center; }
.spirit-dialog { background: #ffffff; padding: 40px; border: 1px solid #f2f2f2; border-radius: 12px; box-shadow: 0 15px 50px rgba(0,0,0,0.05); text-align: center; max-width: 320px; }
.dialog-msg { font-size: 14px; margin-bottom: 30px; color: #1d1d1f; line-height: 1.6; }
.dialog-actions { display: flex; justify-content: center; gap: 40px; }
.dialog-btn { font-size: 12px; color: #86868b; cursor: pointer; border-bottom: 1px solid transparent; }
.dialog-btn:hover { color: #1d1d1f; border-bottom-color: #1d1d1f; }
.dialog-btn.danger:hover { color: #ff3b30; border-bottom-color: #ff3b30; }

.archive-vault-entry { margin: 20px 24px; padding: 10px 0; font-size: 11px; color: #c7c7cc; border-top: 1px solid #f9f9f9; cursor: pointer; text-align: center; text-transform: uppercase; letter-spacing: 0.1em; }
.sidebar-footer { padding: 20px 24px; font-size: 9px; color: #c7c7cc; text-transform: uppercase; }

/* 档案馆样式 */
.archive-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.98); z-index: 200; display: flex; align-items: center; justify-content: center; }
.archive-panel { width: 100%; max-width: 600px; padding: 80px 40px; height: 100vh; overflow-y: auto; }
.archive-header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 50px; border-bottom: 1px solid #1d1d1f; padding-bottom: 12px; }
.archive-header h3 { font-size: 26px; font-weight: 300; letter-spacing: 0.2em; }
.close-txt { font-size: 11px; color: #86868b; cursor: pointer; text-transform: uppercase; border-bottom: 1px solid #86868b; }
.archive-row { display: flex; justify-content: space-between; align-items: center; padding: 20px 0; border-bottom: 1px solid #f9f9f9; }
.row-title { font-size: 15px; color: #1d1d1f; }
.row-meta { font-size: 10px; color: #c7c7cc; }
.row-actions { display: flex; gap: 20px; font-size: 11px; color: #86868b; }
.action-txt:hover { color: #0066cc; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>