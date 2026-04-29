import { ref, computed } from 'vue';

export interface SpiritNote {
  id: string;
  title: string;
  content: any; 
  updateAt: number;
  isPublished: boolean;
  publishTime?: number;
  visibility: 'private' | 'public' | 'link-only';
}

// 定义反向链接的接口，方便面板渲染
export interface Backlink {
  id: string;
  title: string;
  excerpt: string;
}

const notes = ref<SpiritNote[]>([
  { 
    id: '1', 
    title: '太初宇宙起源', 
    content: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: '万物之始...' }] }] },
    updateAt: Date.now(),
    isPublished: true,
    publishTime: Date.now(),
    visibility: 'public'
  },
  { 
    id: '2', 
    title: '灵脉编辑器的设计哲学', 
    content: null,
    updateAt: Date.now() - 10000,
    isPublished: false,
    visibility: 'private'
  }
]);

const currentNoteId = ref<string>(notes.value[0].id);


export function useSpiritData() {
  

  const activeNote = computed<SpiritNote | null>(() => {
    return notes.value.find(n => n.id === currentNoteId.value) || null;
  });

  const selectNote = (id: string) => {
    currentNoteId.value = id;
  };

  const createNewNote = () => {
    const newNote: SpiritNote = {
      id: Date.now().toString(),
      title: '',
      content: null,
      updateAt: Date.now(),
      isPublished: false,
      visibility: 'private'
    };
    notes.value.unshift(newNote);
    currentNoteId.value = newNote.id;
    return newNote;
  };

  const updateNoteTitle = (id: string, newTitle: string) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      note.title = newTitle;
      note.updateAt = Date.now();
    }
  };

  const updateNoteContent = (id: string, content: any) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      note.content = content;
      note.updateAt = Date.now();
    }
  };

  const togglePublish = (id: string) => {
    const note = notes.value.find(n => n.id === id);
    if (note) {
      note.isPublished = !note.isPublished;
      note.visibility = note.isPublished ? 'public' : 'private';
      if (note.isPublished) note.publishTime = Date.now();
      note.updateAt = Date.now();
    }
  };

  const getSearchableNotes = (context: 'note' | 'post') => {
    if (context === 'post') {
      return notes.value.filter(n => n.isPublished);
    }
    return notes.value;
  };

  /**
   * 🌟 核心新增：获取反向链接 (Backlinks)
   * 扫描所有笔记，查找哪些笔记的内容中包含了指向 targetId 的引用
   */
  const getBacklinks = (targetId: string): Backlink[] => {
    return notes.value
      .filter(note => {
        // 排除掉笔记自己引用自己
        if (note.id === targetId) return false;
        
        // 将内容对象转为字符串进行深度扫描
        // 在 Tiptap 的 JSON 中，链接通常存储在 marks 里的 href 属性
        const contentStr = JSON.stringify(note.content || {});
        
        // 匹配格式：/spirit-link/123 或 targetId: "123"
        return contentStr.includes(`/spirit-link/${targetId}`) || 
               contentStr.includes(`"targetId":"${targetId}"`);
      })
      .map(note => ({
        id: note.id,
        title: note.title || '无标题碎片',
        excerpt: `该碎片在「${note.title}」中被提及`
      }));
  };

  return {
    notes,
    currentNoteId,
    activeNote,
    selectNote,
    createNewNote,
    updateNoteTitle,
    updateNoteContent,
    togglePublish,
    getSearchableNotes,
    getBacklinks // 🌟 记得暴露出去
  };
}