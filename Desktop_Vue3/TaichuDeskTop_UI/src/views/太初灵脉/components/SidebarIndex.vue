<template>
  <aside class="spirit-sidebar">
    <div class="space-selector-area">
      <div class="current-space-label" @click="toggleSpaceList">
        <span class="space-text">{{ currentSpaceName }}</span>
        <svg class="chevron-icon" :class="{ rotated: isSpaceListOpen }" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M6 9l6 6 6-6" /></svg>
      </div>

      <transition name="fade-slide">
        <div v-if="isSpaceListOpen" class="space-dropdown" @click.stop>
          <div v-for="space in spaces" :key="space.id" class="space-opt" :class="{ active: currentSpaceId === space.id }" @click="switchSpace(space)">
            <span class="opt-name">{{ space.name }}</span>
            <div class="space-actions">
              <button class="action-btn" @click.stop="handleRenameSpace(space)">✎</button>
              <button class="action-btn hover-danger" @click.stop="handleDeleteSpace(space)" v-if="spaces.length > 1">✕</button>
            </div>
          </div>
          <div class="space-footer-action" @click="handleCreateNewSpace">＋ <span>New Space</span>
          <span class="quota-info">{{ quota.usedSpaces }} / {{ quota.maxSpaces }}</span>
          </div>
        </div>
      </transition>
    </div>

    <div class="sidebar-header">
      <span class="index-label">INDEX</span>
      <span class="quota-badge" :class="quotaStatusClass">
      {{ quota.usedNotes }}/{{ quota.maxNotes }}
      </span>
      <div class="header-actions">
        <button class="action-btn" @click="$emit('create', 'folder')" title="New Folder">📁</button>
        <button class="action-btn primary" @click="$emit('create', 'note')" title="New Note">＋</button>
      </div>
    </div>

    <div class="sidebar-search">
      <input type="text" v-model="searchQuery" placeholder="Search..." spellcheck="false" />
    </div>

    <nav class="note-list">
      <div class="section-group" @dragover.prevent @drop="onDrop($event, null)">
        <div v-for="note in filteredRootNotes" :key="note.id" class="note-item draggable-item" :class="{ active: activeId === note.id }" draggable="true" @dragstart="onDragStart($event, note.id)" @dragend="onDragEnd" @click="$emit('select', note.id)">
          <span class="item-title">{{ note.title || 'Untitled' }}</span>
          <button class="item-delete-btn" @click.stop="confirmDelete(note.id)">✕</button>
        </div>
      </div>

      <div class="section-group">
        <div v-for="folder in folders" :key="folder.id" class="folder-container">
          <div class="folder-header" :class="{ active: activeId === folder.id, 'drag-over': dragOverFolder === folder.id }" @click="toggleFolder(folder.id)" @dragover.prevent="onDragOverFolder(folder.id)" @dragleave="onDragLeaveFolder" @drop="onDrop($event, folder.id)">
            <span class="folder-arrow" :class="{ rotated: expandedFolders.has(folder.id) }"></span>
            <span class="item-title" @dblclick.stop="handleRenameFolder(folder)">{{ folder.title }}</span>
            <div class="folder-actions">
              <button class="folder-add-btn" @click.stop="$emit('create', 'note', folder.id)">＋</button>
              <button class="folder-delete-btn" @click.stop="confirmDelete(folder.id)">✕</button>
            </div>
          </div>

          <transition name="expand">
            <div v-if="expandedFolders.has(folder.id)" class="folder-content">
              <div v-for="subNote in filteredNotesInFolder(folder.id)" :key="subNote.id" class="note-item sub draggable-item" :class="{ active: activeId === subNote.id }" draggable="true" @dragstart="onDragStart($event, subNote.id)" @dragend="onDragEnd" @click="$emit('select', subNote.id)">
                <span class="item-title">{{ subNote.title }}</span>
                <button class="item-delete-btn" @click.stop="confirmDelete(subNote.id)">✕</button>
              </div>
            </div>
          </transition>
        </div>
      </div>
    </nav>

    <div class="sidebar-footer">
      <div class="sync-status"><span class="pulse-dot"></span> Synced</div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch} from 'vue';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';

const quota = ref({
  usedNotes: 0,
  maxNotes: 100,
  usedSpaces: 0,
  maxSpaces: 1,
  isLocked: false
});

const props = defineProps<{ activeId: string }>();
const emit = defineEmits(['select', 'create']);

const { 
  notes, folders, rootNotes, getNotesInFolder, 
  updateNoteTitle, deleteNote, moveNote, currentSpaceId, fetchAllNotes 
} = useSpiritData();


const fetchQuota = async () => {
  try {
    const res: any = await lingmaiApi.getQuota();
    quota.value = res;
  } catch (e) {
    console.error("获取配额失败", e);
  }
};
const isSpaceListOpen = ref(false);
const searchQuery = ref('');
const expandedFolders = ref(new Set<string>());
const dragOverFolder = ref<string | null>(null);
const spaces = ref<any[]>([]);

const filteredRootNotes = computed(() => rootNotes.value.filter(n => n.showInSidebar !== false && n.type !== 'folder'));
const filteredNotesInFolder = (folderId: string) => getNotesInFolder(folderId).filter(n => n.showInSidebar !== false);
const currentSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || 'Spirit');

const toggleSpaceList = () => isSpaceListOpen.value = !isSpaceListOpen.value;
const toggleFolder = (id: string) => expandedFolders.value.has(id) ? expandedFolders.value.delete(id) : expandedFolders.value.add(id);

const initSpaces = async () => {
  const res: any = await lingmaiApi.getSpaces(); 
  spaces.value = res;
  if (spaces.value.length > 0 && (!currentSpaceId.value || currentSpaceId.value.startsWith('0000'))) {
    await switchSpace(spaces.value[0]);
  }
};

const switchSpace = async (space: any) => {
  currentSpaceId.value = space.id;
  isSpaceListOpen.value = false;
  notes.value = []; 
  await fetchAllNotes();
};

const handleCreateNewSpace = async () => {
  const name = prompt("Enter new space name:");
  if (name?.trim()) {
    const newSpace: any = await lingmaiApi.createSpace(name.trim());
    spaces.value.push(newSpace);
    await switchSpace(newSpace);
  }
};

const handleDeleteSpace = async (space: any) => {
  if (confirm(`确定要毁灭空间「${space.name}」吗？`)) {
    await lingmaiApi.deleteSpace(space.id);
    spaces.value = spaces.value.filter(s => s.id !== space.id);
    if (currentSpaceId.value === space.id && spaces.value.length > 0) switchSpace(spaces.value[0]);
  }
};

const handleRenameSpace = async (space: any) => {
  const newName = prompt("请输入新名字", space.name);
  if (newName && newName !== space.name) {
    await lingmaiApi.updateSpaceName(space.id, newName);
    space.name = newName;
  }
};

const handleRenameFolder = async (folder: any) => {
  const newTitle = prompt("请输入新文件夹名称", folder.title);
  if (newTitle?.trim() && newTitle !== folder.title) await updateNoteTitle(folder.id, newTitle.trim());
};

const onDragStart = (e: DragEvent, noteId: string) => {
  if (e.dataTransfer) { e.dataTransfer.setData('noteId', noteId); e.dataTransfer.effectAllowed = 'move'; }
};
const onDragEnd = () => dragOverFolder.value = null;
const onDragOverFolder = (id: string) => dragOverFolder.value = id;
const onDragLeaveFolder = () => dragOverFolder.value = null;

const onDrop = async (e: DragEvent, targetFolderId: string | null) => {
  const noteId = e.dataTransfer?.getData('noteId');
  if (!noteId) return;
  await moveNote(noteId, targetFolderId);
  if (targetFolderId) expandedFolders.value.add(targetFolderId);
};

const confirmDelete = async (id: string) => {
  if (confirm(`确定要彻底删除该项吗？`)) await deleteNote(id);
};


watch(() => notes.value.length, () => {
  fetchQuota();
});

const quotaStatusClass = computed(() => {
  const percent = (quota.value.usedNotes / quota.value.maxNotes) * 100;
  if (percent >= 100) return 'status-danger';
  if (percent >= 80) return 'status-warning';
  return 'status-normal';
});

onMounted(() => {
  initSpaces();
  fetchQuota();
});
</script>

<style scoped>
/* 1. 核心布局 */
.spirit-sidebar { display: flex; flex-direction: column; height: 100%; background: var(--bg-main); color: var(--text-main); font-family: var(--font-sans); border-right: 1px solid var(--border-light); user-select: none; }
.space-selector-area { padding: 32px 24px 12px; position: relative; }
.current-space-label { display: flex; align-items: center; gap: 8px; cursor: pointer; font-size: 14px; font-weight: 600; }
.chevron-icon { color: var(--text-mute); transition: transform 0.3s ease; }
.chevron-icon.rotated { transform: rotate(180deg); }

/* 下拉菜单 */
.space-dropdown { position: absolute; top: 70px; left: 16px; right: 16px; background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(20px); border: 1px solid var(--border-light); border-radius: var(--radius-md); box-shadow: var(--shadow-md); z-index: 1000; padding: 8px; }
.space-opt { display: flex; justify-content: space-between; align-items: center; padding: 10px 14px; border-radius: var(--radius-sm); font-size: 13px; cursor: pointer; }
.space-opt:hover { background: var(--bg-hover); }
.space-opt.active { color: var(--accent); background: rgba(0, 102, 204, 0.06); font-weight: 600; }

.space-actions { display: flex; gap: 4px; opacity: 0; transition: opacity 0.2s ease; }
.space-opt:hover .space-actions { opacity: 1; }

.space-footer-action { margin-top: 6px; padding: 10px 14px; font-size: 12px; color: var(--accent); border-top: 1px solid var(--border-light); cursor: pointer; font-weight: 500; display: flex; align-items: center; gap: 8px; }
.space-footer-action:hover { background: var(--bg-hover); border-radius: 0 0 var(--radius-sm) var(--radius-sm); }

/* 列表区 */
.sidebar-header { padding: 12px 24px; display: flex; justify-content: space-between; align-items: center; }
.index-label { font-size: 10px; font-weight: 700; color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.1em; }
.header-actions { display: flex; gap: 8px; }
.action-btn { background: none; border: none; color: var(--text-mute); cursor: pointer; border-radius: var(--radius-sm); padding: 4px; display: flex; align-items: center; justify-content: center; transition: all 0.2s ease; }
.action-btn:hover { background: var(--bg-hover); color: var(--text-main); }
.action-btn.hover-danger:hover { background: rgba(255, 59, 48, 0.1); color: var(--danger); }

.sidebar-search { padding: 8px 24px 20px; }
.sidebar-search input { width: 100%; background: var(--bg-hover); border: none; padding: 10px 14px; border-radius: var(--radius-md); font-size: 13px; outline: none; color: var(--text-main); transition: background 0.2s ease; }
.sidebar-search input:focus { background: #ffffff; box-shadow: 0 0 0 1px var(--border-light); }

.note-list { flex: 1; overflow-y: auto; padding: 0 16px; }
.note-item { padding: 8px 12px; border-radius: var(--radius-sm); cursor: pointer; display: flex; justify-content: space-between; align-items: center; color: var(--text-mute); transition: all 0.2s ease; margin-bottom: 1px; }
.note-item:hover { background: var(--bg-hover); color: var(--text-main); }
.note-item.active { background: #ffffff; color: var(--accent); font-weight: 500; box-shadow: var(--shadow-sm); }

.folder-header { display: flex; align-items: center; padding: 8px 12px; cursor: pointer; border-radius: var(--radius-sm); font-weight: 600; font-size: 14px; color: var(--text-main); transition: background 0.2s ease; position: relative; }
.folder-header:hover { background: var(--bg-hover); }
.folder-header.drag-over { background: rgba(0, 102, 204, 0.06); outline: 1px dashed var(--accent); }

.folder-arrow { width: 0; height: 0; border-top: 4px solid transparent; border-bottom: 4px solid transparent; border-left: 5px solid var(--text-mute); margin-right: 12px; transition: transform 0.25s ease; }
.folder-arrow.rotated { transform: rotate(90deg); }
.folder-content { margin-left: 14px; padding-left: 14px; border-left: 1px solid var(--border-light); }

.folder-actions { display: flex; gap: 4px; margin-left: auto; }
.folder-add-btn, .folder-delete-btn { background: none; border: none; color: var(--text-mute); cursor: pointer; opacity: 0; font-size: 11px; padding: 2px 6px; border-radius: var(--radius-sm); transition: all 0.2s; }
.folder-header:hover .folder-add-btn, .folder-header:hover .folder-delete-btn { opacity: 0.6; }
.folder-add-btn:hover { background: var(--bg-hover); color: var(--text-main); opacity: 1 !important; }
.folder-delete-btn:hover { background: rgba(255, 59, 48, 0.08); color: var(--danger) !important; opacity: 1 !important; }

.item-delete-btn { opacity: 0; background: none; border: none; color: var(--text-mute); cursor: pointer; padding: 4px; font-size: 10px; transition: all 0.2s; border-radius: var(--radius-sm); }
.note-item:hover .item-delete-btn { opacity: 0.6; }
.item-delete-btn:hover { background: rgba(255, 59, 48, 0.08); color: var(--danger) !important; opacity: 1 !important; }

.sidebar-footer { padding: 24px; }
.sync-status { font-size: 10px; color: var(--text-mute); display: flex; align-items: center; gap: 8px; }
.pulse-dot { width: 4px; height: 4px; background: #34c759; border-radius: 50%; }

.expand-enter-active, .expand-leave-active { transition: all 0.3s; max-height: 800px; overflow: hidden; }
.expand-enter-from, .expand-leave-to { max-height: 0; opacity: 0; }
.fade-slide-enter-active, .fade-slide-leave-active { transition: all 0.25s; }
.fade-slide-enter-from, .fade-slide-leave-to { opacity: 0; transform: translateY(-10px); }
/* 🌟 新增样式 */
.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.quota-badge {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 10px;
  background: var(--bg-hover);
  color: var(--text-mute);
  font-weight: 500;
  transition: all 0.3s ease;
}

.status-warning {
  background: rgba(255, 159, 10, 0.15);
  color: #ff9f0a;
}

.status-danger {
  background: rgba(255, 69, 58, 0.15);
  color: #ff453a;
  animation: pulse-danger 2s infinite;
}

.btn-disabled {
  opacity: 0.5;
  filter: grayscale(1);
  cursor: not-allowed !important;
}

@keyframes pulse-danger {
  0% { opacity: 1; }
  50% { opacity: 0.6; }
  100% { opacity: 1; }
}
/* 🌟 顶部小标签样式 */
.space-count-mini {
  font-size: 10px;
  background: var(--bg-hover);
  color: var(--text-mute);
  padding: 1px 4px;
  border-radius: 4px;
  margin-left: 4px;
  font-weight: 400;
}

/* 🌟 下拉菜单底部的额度信息 */
.quota-info {
  margin-left: auto;
  font-size: 11px;
  opacity: 0.6;
}

/* 🌟 空间满额时的禁用状态 */
.space-footer-action.is-full {
  cursor: not-allowed;
  background: var(--bg-hover);
  color: var(--text-mute);
}

.space-footer-action.is-full span {
  text-decoration: line-through; /* 可选：增加视觉上的“禁止”感 */
  opacity: 0.5;
}

/* 之前你加的 note 额度样式，确保 header-left 布局正确 */
.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between; /* 确保标题和按钮分居两侧 */
  padding: 12px 24px;
}

.index-label {
  display: flex;
  align-items: center;
  gap: 8px; /* 让 INDEX 和 0/100 之间有间距 */
}
</style>