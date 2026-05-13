// src/composables/useSpiritData.ts
import { ref, computed } from 'vue';
import { lingmaiApi } from '../api/lingmai';
import { debounce } from 'lodash-es';
import type { NoteType } from '../utils/NoteType';

// --- 与后端 Note.cs 100% 对齐的前端模型 ---
export interface SpiritNote {
  id: string;
  title: string;
  spaceId: string;
  folderId: string | null;
  type: NoteType;
  content: any;
  updateAt: number;
  
  // 🌟 核心升级：视界隔离与社交字段
  showInSidebar: boolean;
  isPublic: boolean;
  resonance: number;
  status: number;
  targetId?: number | null;
}

export interface Backlink {
  id: string;
  title: string;
  excerpt: string;
}

// 单例状态，确保数据在不同组件间无缝同步
const notes = ref<SpiritNote[]>([]);
const currentNoteId = ref<string>("");
const currentSpaceId = ref<string>(""); 
const isLoading = ref(false);

export function useSpiritData() {

  const archiveNote = async (noteId: string) => {
    try {
      await lingmaiApi.archiveNote(noteId); // 调用 Patch /archive
      // 🌟 核心：从当前活跃列表中移除该笔记，实现“视觉消失”
      notes.value = notes.value.filter(n => n.id !== noteId);
      // 如果当前正在编辑这个笔记，清除选中状态
      if (currentNoteId.value === noteId) {
        currentNoteId.value = '';
      }
    } catch (err) {
      console.error('归档失败:', err);
    }
  };

  const restoreNote = async (noteId: string) => {
    try {
      await lingmaiApi.restoreNote(noteId); // 调用 Patch /restore
      // 重新拉取一次列表以同步状态
      await fetchAllNotes();
    } catch (err) {
      console.error('还原失败:', err);
    }
  };



  // 当前选中的笔记对象
  const activeNote = computed<SpiritNote | null>(() => {
    return notes.value.find(n => n.id === currentNoteId.value) || null;
  });

  // 🌟 分类计算属性：严格执行视界隔离，侧边栏不加载不该显示的碎片
  const folders = computed(() => notes.value.filter(n => n.type === 'folder'));
  
  // 1. 根目录笔记：除了文件夹以外的所有活跃内容
const rootNotes = computed(() => 
  notes.value.filter(n => 
    n.type !== 'folder' &&           // 🌟 不管它是 note 还是 wiki，只要不是文件夹就行
    !n.folderId &&                   // 必须在根目录
    n.showInSidebar !== false &&     // 允许显示
    n.status === 0                   // 必须是活跃状态（未归档）
  )
);

// 2. 文件夹内的笔记：逻辑一致，只是多了 folderId 匹配
const getNotesInFolder = (folderId: string) => 
  notes.value.filter(n => 
    n.folderId === folderId && 
    n.type !== 'folder' &&           // 🌟 同样，只要不是文件夹就行
    n.showInSidebar !== false && 
    n.status === 0
  );

  const fetchAllNotes = async () => {
    if (!currentSpaceId.value || currentSpaceId.value === "" || currentSpaceId.value.startsWith('0000')) {
      notes.value = [];
      currentNoteId.value = "";
      return;
    }

    try {
      isLoading.value = true;
      const res: any = await lingmaiApi.getNoteList(currentSpaceId.value); 
      
      if (!res || res.length === 0) {
        notes.value = [];
        currentNoteId.value = "";
        return;
      }

      // 🌟 精准解析后端新加的所有字段
      notes.value = res.map((n: any) => ({
        id: n.id,
        title: n.title || (n.type === 'folder' ? '新文件夹' : '无标题碎片'),
        spaceId: n.spaceId,
        folderId: n.folderId,
        type: n.type || 'note',
        content: null,
        updateAt: n.updatedAt ? new Date(n.updatedAt).getTime() : Date.now(),
        
        // 🌟 解析重构后的属性
        showInSidebar: n.showInSidebar !== false,
        isPublic: n.isPublic || false,
        resonance: n.resonance || 0,
        status: n.status || 0,
        targetId: n.targetId || null
      }));

      const hasValidNote = notes.value.some(n => n.id === currentNoteId.value);
      if (!hasValidNote) {
        // 默认选中第一个允许展示在目录树中的笔记
        const firstNote = notes.value.find(n => n.type === 'note' && n.showInSidebar);
        currentNoteId.value = firstNote ? firstNote.id : "";
      }

    } catch (error: any) {
      if (error.response?.status === 403) {
        console.error("安全拦截：您无权访问该空间的数据");
        notes.value = [];
      } else {
        console.error("灵感列表同步失败:", error);
      }
    } finally {
      isLoading.value = false;
    }
  };

  const selectNote = async (id: string, forceRefresh = false) => {
    if (!id) return;
    
    // ⚠️ 注意：不要在这里提早给 currentNoteId 赋值！
    const index = notes.value.findIndex(n => n.id === id);
    if (index === -1) return;

    if (forceRefresh || !notes.value[index].content) {
      isLoading.value = true;
      try {
        const freshData = await lingmaiApi.getNote(id);
        // 1. 数据拿到了，存进内存
        notes.value[index].content = freshData.tiptapContent || { type: 'doc', content: [] };
        notes.value[index].title = freshData.title;
      } catch (err) {
        console.error("加载详情失败:", err);
      } finally {
        isLoading.value = false;
      }
    }
    
    // 🌟 核心修复：等数据确确实实存好后，再切换 ID！
    // 这样当组件响应 currentNoteId 变化时，数据已经是 Ready 的了
    currentNoteId.value = id;
    return notes.value[index];
  };

  const moveNote = async (noteId: string, folderId: string | null) => {
    try {
      await lingmaiApi.moveNote(noteId, folderId);
      const note = notes.value.find(n => n.id === noteId);
      if (note) {
        note.folderId = folderId;
        note.updateAt = Date.now();
      }
    } catch (error) {
      console.error("移动碎片失败:", error);
    }
  };

  /**
   * 🌟 创建：基于多态规则注入创建数据
   */
  const createNewNote = async (dto?: { 
    title?: string, 
    type?: NoteType,
    folderId?: string | null 
  }) => {
    try {
      const selectedType = dto?.type || 'note';
      
      const payload = {
        title: dto?.title || (selectedType === 'folder' ? "新文件夹" : "新灵感碎片"),
        spaceId: currentSpaceId.value,
        folderId: dto?.folderId || null,
        type: selectedType
      };

      const res: any = await lingmaiApi.createNote(payload);
      
      // 🌟 根据类型默认初始化本地属性（对齐后端逻辑）
      const newNote: SpiritNote = {
        id: res.id,
        title: payload.title,
        spaceId: payload.spaceId,
        folderId: payload.folderId,
        type: payload.type as NoteType,
        content: payload.type === 'folder' ? null : { type: 'doc', content: [] },
        updateAt: Date.now(),
        
        // 🌟 视界隔离：只有长文随笔默认显示在侧边栏中
        showInSidebar: selectedType === 'note' || selectedType === 'folder',
        isPublic: selectedType === 'thought', // 简语默认公开，随笔默认私密
        resonance: 0,
        status: 0,
        targetId: null
      };

      notes.value.unshift(newNote);
      if (newNote.type === 'note') {
        currentNoteId.value = newNote.id;
      }
      return newNote;
    } catch (error) {
      console.error("创建失败:", error);
    }
  };

  const syncContentToApi = async (id: string, content: any) => {
    try {
      await lingmaiApi.syncBlocks(id, content);
    } catch (error) {
      console.error("❌ 灵脉内容云端同步失败:", error);
    }
  };

  const syncTitleToApi = async (id: string, title: string) => {
    try {
      await lingmaiApi.updateNoteInfo(id, title);
    } catch (error) {
      console.error("❌ 灵脉标题云端同步失败:", error);
    }
  };

  const debouncedSyncContent = debounce(syncContentToApi, 1000);
  const debouncedSyncTitle = debounce(syncTitleToApi, 1000);

  const updateNoteContent = async (id: string, content: any) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      note.content = content;
      note.updateAt = Date.now();
      debouncedSyncContent(id, content);
    }
  };

  const updateNoteTitle = async (id: string, newTitle: string) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      note.title = newTitle;
      note.updateAt = Date.now();
      debouncedSyncTitle(id, newTitle);
    }
  };

  const deleteNote = async (id: string) => {
    try {
      await lingmaiApi.deleteNote(id);
      notes.value = notes.value.filter(n => n.id !== id);
      if (currentNoteId.value === id) {
        currentNoteId.value = notes.value.find(n => n.type === 'note' && n.showInSidebar)?.id || "";
      }
    } catch (error) {
      console.error("删除失败:", error);
    }
  };

  const togglePublish = async (id: string) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      const targetState = !note.isPublic;
      try {
        // 调用你已有的后端 API 更新公开状态
        await lingmaiApi.updateNotePublishStatus(id, targetState);
        note.isPublic = targetState;
        note.updateAt = Date.now();
      } catch (e) {
        console.error("同步公开状态失败:", e);
      }
    }
  };

  return {
    notes, currentNoteId, currentSpaceId, activeNote, isLoading,
    folders, rootNotes, getNotesInFolder,
    fetchAllNotes, selectNote, createNewNote, togglePublish,
    updateNoteTitle, updateNoteContent, deleteNote, moveNote,archiveNote,
    restoreNote
  };
}