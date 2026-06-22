<template>
  <div class="workspace-schedule-frame">
    <header class="schedule-header">
      <input 
        :value="props.title" 
        @input="onTitleInput" 
        class="schedule-title-input" 
        placeholder="未命名日程空间" 
      />
      <p class="schedule-subtitle">时间线与任务矩阵的自由流沙交织</p>
    </header>

    <div class="schedule-main-stage">
      
      <section class="calendar-section">
        <div class="section-top-bar">
          <span class="view-title">📅 时间演化星图</span>
          <div class="calendar-controls">
            <button @click="adjustMonth(-1)" class="spirit-arrow-btn">◀</button>
            <span class="current-month-label">{{ currentYear }} . {{ String(currentMonth + 1).padStart(2, '0') }}</span>
            <button @click="adjustMonth(1)" class="spirit-arrow-btn">▶</button>
          </div>
        </div>
        
        <div class="calendar-grid-box">
          <div class="grid-header-row">
            <span v-for="d in ['一','二','三','四','五','六','日']" :key="d">{{ d }}</span>
          </div>
          
          <div class="grid-days-container">
            <div 
              v-for="day in calendarDays" 
              :key="day.dateStr" 
              class="calendar-day-cell"
              :class="{ 'is-sibling-month': !day.isCurrentMonth, 'is-today': day.isToday }"
              @dragover.prevent
              @drop="handleDropOnDate($event, day.dateStr)"
            >
              <div class="day-cell-header">
                <span class="day-number">{{ day.dayNum }}</span>
                <button @click="addNewItemIntoDate(day.dateStr)" class="quick-add-event-btn">+</button>
              </div>
              
              <div class="day-events-list">
                <div 
                  v-for="item in getItemsByDate(day.dateStr)" 
                  :key="item.id"
                  class="calendar-mini-strip"
                  :class="{ 'strip-done': item.isDone }"
                  :style="{ 
                    backgroundColor: item.themeColor + '0d', 
                    color: item.themeColor, 
                    borderLeftColor: item.themeColor 
                  }"
                  draggable="true"
                  @dragstart="handleItemDragStart($event, item.id)"
                >
                  <label class="strip-checkbox-wrapper">
                    <input type="checkbox" v-model="item.isDone" @change="compileAndDispatchAll" />
                    <span class="strip-checkmark" :style="{ borderColor: item.themeColor }"></span>
                  </label>

                  <input 
                    v-model="item.title" 
                    @blur="onItemBlur(item)"
                    @keyup.enter="($event.target as HTMLInputElement).blur()"
                    class="strip-inline-input"
                    placeholder="输入日程..."
                  />

                  <input 
                    type="color" 
                    v-model="item.themeColor" 
                    @change="compileAndDispatchAll" 
                    class="strip-mini-color-picker" 
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section class="kanban-section">
        <div class="section-top-bar">
          <span class="view-title">📋 状态演化矩阵</span>
          <button @click="addNewColumn" class="spirit-mini-btn">+ 追加状态</button>
        </div>

        <div 
          class="kanban-columns-container"
          :style="{ '--total-cols': localColumns.length }"
        >
          <div 
            v-for="(col, cIdx) in localColumns" 
            :key="col.id" 
            class="kanban-column"
            :style="{ '--column-theme': col.color }"
            draggable="true"
            @dragstart="handleColumnDragStart($event, cIdx)"
            @dragover.prevent
            @drop="handleDropOnColumnContainer($event, cIdx)"
          >
            <div class="column-header">
              <span class="column-drag-indicator">⋮⋮</span>
              <span class="column-color-dot" :style="{ backgroundColor: col.color }"></span>
              <input v-model="col.name" @input="compileAndDispatchAll" class="column-name-input" placeholder="状态名称" />
              
              <div class="column-actions-trigger">
                <input type="color" v-model="col.color" @change="compileAndDispatchAll" class="column-color-picker" />
                <button @click="removeColumn(cIdx)" class="del-col-btn">✕</button>
              </div>
            </div>

            <div 
              class="kanban-cards-dropzone"
              @dragover.prevent
              @drop.stop="handleDropOnColumnZone($event, col.id)"
            >
              <div 
                v-for="item in getItemsByColumn(col.id)" 
                :key="item.id"
                class="schedule-card"
                :class="{ 'is-done': item.isDone }"
                :style="{ borderLeftColor: item.themeColor }"
                draggable="true"
                @dragstart.stop="handleItemDragStart($event, item.id)"
              >
                <div class="card-body">
                  <label class="custom-checkbox-wrapper">
                    <input type="checkbox" v-model="item.isDone" @change="compileAndDispatchAll" />
                    <span class="checkmark"></span>
                  </label>
                  
                  <input 
                    v-model="item.title" 
                    @blur="onItemBlur(item)"
                    @keyup.enter="($event.target as HTMLInputElement).blur()"
                    class="card-inline-input"
                    placeholder="新碎片内容..."
                  />
                </div>
                
                <div class="card-meta-row">
                  <input type="color" v-model="item.themeColor" @change="compileAndDispatchAll" class="mini-color-picker" />
                  <span v-if="item.date" class="card-date-badge">{{ item.date.slice(5) }}</span>
                </div>
              </div>
              
              <button @click="addNewItemIntoColumn(col.id)" class="add-card-btn">+ 新建碎片</button>
            </div>
          </div>

          <div v-if="localColumns.length === 0" class="empty-columns-placeholder">
            <span>干净如初。点击右上角追加一个演化状态。</span>
          </div>
        </div>
      </section>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted, nextTick } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';

const props = defineProps<{
  title: string;
  noteId: string;
  blocks?: any[];
  extraData?: string;
}>();

const emit = defineEmits(['update:title', 'change']);
const { activeNote } = useSpiritData();

interface ColumnItem { id: string; name: string; color: string; sortOrder: number; }
interface ScheduleItem { id: string; title: string; isDone: boolean; themeColor: string; columnId: string | null; date: string | null; }

const localColumns = ref<ColumnItem[]>([]);
const localItems = ref<ScheduleItem[]>([]);

let isInitialized = false;
let saveTimer: any = null;

const draggedItemId = ref<string | null>(null);
const draggedColumnIndex = ref<number | null>(null);

const currentYear = ref(new Date().getFullYear());
const currentMonth = ref(new Date().getMonth());

const getItemsByColumn = (colId: string) => localItems.value.filter(i => i.columnId === colId);
const getItemsByDate = (dateStr: string) => localItems.value.filter(i => i.date === dateStr);

const calendarDays = computed(() => {
  const year = currentYear.value;
  const month = currentMonth.value;

  const firstDayInstance = new Date(year, month, 1);
  let firstDayOfWeek = firstDayInstance.getDay(); 
  if (firstDayOfWeek === 0) firstDayOfWeek = 7; 

  const lastDateOfCurrentMonth = new Date(year, month + 1, 0).getDate();
  const daysArray = [];

  const paddingDaysCount = firstDayOfWeek - 1; 
  const prevMonthLastInstance = new Date(year, month, 0);
  const prevMonthLastDate = prevMonthLastInstance.getDate();
  const prevMonthYear = prevMonthLastInstance.getFullYear();
  const prevMonth = prevMonthLastInstance.getMonth();

  for (let i = paddingDaysCount - 1; i >= 0; i--) {
    const dayNum = prevMonthLastDate - i;
    daysArray.push({
      dayNum,
      dateStr: `${prevMonthYear}-${String(prevMonth + 1).padStart(2, '0')}-${String(dayNum).padStart(2, '0')}`,
      isCurrentMonth: false,
      isToday: false
    });
  }

  const today = new Date();
  for (let i = 1; i <= lastDateOfCurrentMonth; i++) {
    const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(i).padStart(2, '0')}`;
    const isToday = today.getFullYear() === year && today.getMonth() === month && today.getDate() === i;
    daysArray.push({ dayNum: i, dateStr, isCurrentMonth: true, isToday });
  }

  const remainingCells = 42 - daysArray.length;
  const nextMonthInstance = new Date(year, month + 1, 1);
  const nextMonthYear = nextMonthInstance.getFullYear();
  const nextMonth = nextMonthInstance.getMonth();

  for (let i = 1; i <= remainingCells; i++) {
    daysArray.push({
      dayNum: i,
      dateStr: `${nextMonthYear}-${String(nextMonth + 1).padStart(2, '0')}-${String(i).padStart(2, '0')}`,
      isCurrentMonth: false,
      isToday: false
    });
  }

  return daysArray;
});

const adjustMonth = (step: number) => {
  const newDate = new Date(currentYear.value, currentMonth.value + step, 1);
  currentYear.value = newDate.getFullYear();
  currentMonth.value = newDate.getMonth();
};

const handleItemDragStart = (e: DragEvent, itemId: string) => {
  draggedItemId.value = itemId;
  draggedColumnIndex.value = null;
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
};

const handleColumnDragStart = (e: DragEvent, index: number) => {
  draggedColumnIndex.value = index;
  draggedItemId.value = null;
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
};

const handleDropOnColumnZone = (e: DragEvent, targetColumnId: string) => {
  if (!draggedItemId.value) return;
  const item = localItems.value.find(i => i.id === draggedItemId.value);
  if (item) {
    item.columnId = targetColumnId;
    compileAndDispatchAll(); 
  }
  draggedItemId.value = null;
};

const handleDropOnDate = (e: DragEvent, targetDateStr: string) => {
  if (!draggedItemId.value) return;
  const item = localItems.value.find(i => i.id === draggedItemId.value);
  if (item) {
    item.date = targetDateStr;
    compileAndDispatchAll();
  }
  draggedItemId.value = null;
};

const handleDropOnColumnContainer = (e: DragEvent, targetIndex: number) => {
  if (draggedColumnIndex.value === null) return;
  
  const sourceIndex = draggedColumnIndex.value;
  if (sourceIndex !== targetIndex) {
    const movedColumn = localColumns.value.splice(sourceIndex, 1)[0];
    localColumns.value.splice(targetIndex, 0, movedColumn);
    
    localColumns.value.forEach((col, idx) => {
      col.sortOrder = idx;
    });
    
    compileAndDispatchAll();
  }
  draggedColumnIndex.value = null;
};

const loadFromBlocks = () => {
  if (!activeNote.value || !Array.isArray(activeNote.value.blocks)) return;

  const colBlocks = activeNote.value.blocks.filter(b => b.type === 'schedule-column');
  const itemBlocks = activeNote.value.blocks.filter(b => b.type === 'schedule-item');

  if (colBlocks.length > 0 || itemBlocks.length > 0) {
    localColumns.value = colBlocks.map(b => ({ id: b.id, ...JSON.parse(b.data) })).sort((a,b) => a.sortOrder - b.sortOrder);
    localItems.value = itemBlocks.map(b => ({ id: b.id, ...JSON.parse(b.data) }));
  } else {
    localColumns.value = [];
    localItems.value = [];
  }
};

const compileAndDispatchAll = () => {
  if (!isInitialized || !activeNote.value) return;

  const finalBlocks: any[] = [];

  localColumns.value.forEach((col, idx) => {
    finalBlocks.push({
      id: col.id,
      ownerId: props.noteId,
      ownerType: 'schedule',
      type: 'schedule-column',
      sortOrder: idx,
      data: JSON.stringify({ name: col.name, color: col.color, sortOrder: idx })
    });
  });

  localItems.value.forEach((item, idx) => {
    finalBlocks.push({
      id: item.id,
      ownerId: props.noteId,
      ownerType: 'schedule',
      type: 'schedule-item',
      sortOrder: localColumns.value.length + idx,
      data: JSON.stringify({ title: item.title, isDone: item.isDone, themeColor: item.themeColor, columnId: item.columnId, date: item.date })
    });
  });

  activeNote.value.blocks = finalBlocks;

  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    emit('change', { blocks: finalBlocks });
  }, 400);
};

const addNewColumn = () => {
  const newId = `col_${Date.now()}`;
  localColumns.value.push({ id: newId, name: '', color: '#a1a1a6', sortOrder: localColumns.value.length });
  compileAndDispatchAll();
  
  nextTick(() => {
    const inputs = document.querySelectorAll('.column-name-input');
    const lastInput = inputs[inputs.length - 1] as HTMLInputElement;
    if (lastInput) lastInput.focus();
  });
};

const removeColumn = (idx: number) => {
  const colToDelete = localColumns.value[idx];
  if (colToDelete) {
    localItems.value.forEach(item => {
      if (item.columnId === colToDelete.id) item.columnId = null;
    });
  }
  localColumns.value.splice(idx, 1);
  compileAndDispatchAll();
};

const addNewItemIntoColumn = (colId: string) => {
  const newItemId = `item_${Date.now()}`;
  localItems.value.push({ id: newItemId, title: '', isDone: false, themeColor: '#0066cc', columnId: colId, date: null });
  compileAndDispatchAll();

  nextTick(() => {
    const cardInputs = document.querySelectorAll('.card-inline-input');
    const lastCardInput = cardInputs[cardInputs.length - 1] as HTMLInputElement;
    if (lastCardInput) lastCardInput.focus();
  });
};

const addNewItemIntoDate = (dateStr: string) => {
  localItems.value.push({ id: `item_${Date.now()}`, title: '', isDone: false, themeColor: '#86868b', columnId: null, date: dateStr });
  compileAndDispatchAll();

  nextTick(() => {
    const stripInputs = document.querySelectorAll('.strip-inline-input');
    const lastStripInput = stripInputs[stripInputs.length - 1] as HTMLInputElement;
    if (lastStripInput) lastStripInput.focus();
  });
};

const onItemBlur = (item: ScheduleItem) => {
  if (item.title.trim() === '') {
    localItems.value = localItems.value.filter(i => i.id !== item.id);
  }
  compileAndDispatchAll();
};

const onTitleInput = (e: Event) => { emit('update:title', (e.target as HTMLInputElement).value); };

watch(() => activeNote.value?.id, (newId) => {
  if (newId && activeNote.value?.blocks !== undefined) {
    isInitialized = false;
    loadFromBlocks();
    isInitialized = true;
  }
}, { immediate: true });

onUnmounted(() => { if (saveTimer) clearTimeout(saveTimer); });
</script>

<style scoped>
/* ================= 🌌 灵脉高级极简主义排版引擎 ================= */
.workspace-schedule-frame { 
  width: 100%; 
  height: 100vh; 
  display: flex; 
  flex-direction: column; 
  background: #ffffff; 
  color: #1d1d1f;
  -webkit-font-smoothing: antialiased;
}

.schedule-header { 
  padding: 40px 48px 16px; 
  background: #ffffff; 
  flex-shrink: 0; 
}
.schedule-title-input { 
  width: 100%; 
  font-size: 2.6rem; 
  font-weight: 800; 
  border: none; 
  background: transparent; 
  outline: none; 
  color: #1d1d1f; 
  letter-spacing: -0.04em;
}
.schedule-subtitle { 
  font-size: 11px; 
  color: #86868b; 
  margin-top: 6px; 
  letter-spacing: 0.05em;
}

.schedule-main-stage { 
  flex: 1; 
  display: flex; 
  flex-direction: column; 
  gap: 32px; 
  padding: 0 48px 40px; 
  overflow-y: auto; 
}

/* 🟢 极简空气感日历 */
.calendar-section { 
  display: flex; 
  flex-direction: column; 
  flex-shrink: 0; 
}
.section-top-bar { 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  padding-bottom: 12px; 
  border-bottom: 1px solid #f2f2f7; 
  margin-bottom: 12px;
}
.view-title { 
  font-size: 12px; 
  font-weight: 700; 
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: #86868b; 
}

.calendar-controls { display: flex; align-items: center; gap: 8px; }
.current-month-label { font-size: 13px; font-weight: 700; font-family: monospace; min-width: 70px; text-align: center; }
.spirit-arrow-btn { background: transparent; border: none; font-size: 10px; color: #a1a1a6; cursor: pointer; padding: 4px; transition: color 0.2s; }
.spirit-arrow-btn:hover { color: #0066cc; }

.grid-header-row { 
  display: grid; 
  grid-template-columns: repeat(7, 1fr); 
  text-align: center; 
  font-size: 11px; 
  font-weight: 600; 
  color: #c7c7cc; 
  margin-bottom: 12px; 
}

.grid-days-container { 
  display: grid; 
  grid-template-columns: repeat(7, 1fr); 
  grid-auto-rows: minmax(54px, auto); 
  gap: 6px; 
}

.calendar-day-cell { 
  background: #fbfbfd; 
  border-radius: 8px; 
  padding: 6px 8px; 
  display: flex; 
  flex-direction: column; 
  position: relative; 
  border: 1px solid transparent;
  transition: background 0.15s ease, border-color 0.15s ease;
}
.calendar-day-cell:hover { 
  background: #ffffff; 
  border-color: #e5e5ea;
}

.calendar-day-cell.is-sibling-month { opacity: 0.25; }
.calendar-day-cell.is-today { background: rgba(0, 102, 204, 0.03); border-color: rgba(0, 102, 204, 0.08); }
.calendar-day-cell.is-today .day-number { color: #0066cc; font-weight: 800; }

.day-cell-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.day-number { font-size: 11px; font-weight: 500; font-family: monospace; color: #86868b; }

.quick-add-event-btn { background: transparent; border: none; color: #a1a1a6; font-size: 12px; cursor: pointer; opacity: 0; transition: opacity 0.2s; padding: 0; line-height: 1; }
.calendar-day-cell:hover .quick-add-event-btn { opacity: 1; }

.day-events-list { flex: 1; display: flex; flex-direction: column; gap: 4px; }

.calendar-mini-strip { 
  font-size: 12px; 
  font-weight: 600; 
  padding: 4px 6px; 
  border-radius: 5px; 
  cursor: grab; 
  overflow: hidden; 
  border-left: 3.5px solid #86868b; 
  display: flex;
  align-items: center;
  gap: 6px;
}

.strip-checkbox-wrapper { 
  position: relative; 
  display: inline-block; 
  width: 12px; 
  height: 12px; 
  flex-shrink: 0; 
}
.strip-checkbox-wrapper input { opacity: 0; width: 0; height: 0; padding: 0; margin: 0; }
.strip-checkmark { 
  position: absolute; 
  top: 0; left: 0; 
  height: 12px; width: 12px; 
  background-color: transparent; 
  border: 1.5px solid #d2d2d7; 
  border-radius: 50%; 
  transition: all 0.15s; 
  cursor: pointer; 
}
.strip-checkbox-wrapper input:checked + .strip-checkmark { 
  background-color: currentColor; 
}

.strip-inline-input {
  border: none;
  background: transparent;
  font-size: 12px; 
  font-weight: 600;
  color: inherit;
  outline: none;
  width: 100%;
  padding: 0;
}

.strip-mini-color-picker {
  border: none;
  background: transparent;
  width: 12px;
  height: 12px;
  cursor: pointer;
  padding: 0;
  opacity: 0;
  transition: opacity 0.2s;
  flex-shrink: 0;
}
.calendar-mini-strip:hover .strip-mini-color-picker { opacity: 1; }

.strip-done { opacity: 0.35 !important; }
.strip-done .strip-inline-input { text-decoration: line-through; }

/* 🔵 极简对齐矩阵看板 */
.kanban-section { 
  display: flex; 
  flex-direction: column; 
  flex: 1; 
  min-height: 320px; 
}

/* 🌟【核心重构】：让看板列容器完美对齐日历的 7 列等分比例 */
.kanban-columns-container { 
  flex: 1; 
  display: grid; 
  /* 🚀 采用 7 列等分布局，间距 6px 与上面的日历完全对齐 */
  grid-template-columns: repeat(7, 1fr); 
  gap: 6px; 
  padding-bottom: 8px; 
  align-items: flex-start; 
}

/* 🌟【高级容错网关】：如果用户增加了 7 列以上，为了防止宽度崩溃，自动平滑退化到横向弹性滑动槽 */
.kanban-columns-container:has(> .kanban-column:nth-child(8)) {
  display: flex;
  overflow-x: auto;
  grid-template-columns: none;
}
/* 弹性滑动模式下，每一列的基础宽度强行绑定为 7 等分时的完美计算值 */
.kanban-columns-container:has(> .kanban-column:nth-child(8)) .kanban-column {
  width: calc((100% - 36px) / 7);
  flex-shrink: 0;
}

.kanban-column { 
  /* 拿掉固定的 260px，交由外层 Grid 容器进行等分比例延伸控制 */
  width: 100%; 
  max-height: 100%; 
  display: flex; 
  flex-direction: column; 
  background: transparent; 
  cursor: grab; 
  transition: transform 0.2s;
}
.kanban-column:active { cursor: grabbing; }

.column-header { 
  display: flex; 
  align-items: center; 
  gap: 8px; 
  margin-bottom: 14px; 
  padding: 0 4px;
  position: relative;
}
.column-drag-indicator {
  font-size: 11px;
  color: #c7c7cc;
  user-select: none;
  padding-right: 2px;
  opacity: 0.5;
}
.kanban-column:hover .column-drag-indicator { opacity: 1; }

.column-color-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
.column-name-input { 
  border: none; 
  background: transparent; 
  font-weight: 700; 
  font-size: 13px; 
  outline: none; 
  color: #1d1d1f; 
  width: 50%;
  cursor: text;
}

.column-actions-trigger { display: flex; align-items: center; gap: 6px; margin-left: auto; opacity: 0; transition: opacity 0.2s; }
.column-header:hover .column-actions-trigger { opacity: 1; }
.column-color-picker { border: none; background: transparent; width: 14px; height: 14px; cursor: pointer; padding: 0; }
.del-col-btn { background: transparent; border: none; color: #ff3b30; font-size: 10px; cursor: pointer; }

.kanban-cards-dropzone { 
  flex: 1; 
  display: flex; 
  flex-direction: column; 
  gap: 10px; 
  min-height: 100px;
  border-top: 1px dashed #f2f2f7;
  padding-top: 10px;
  cursor: default; 
}

/* 看板卡片 */
.schedule-card { 
  background: #ffffff; 
  border-radius: 8px; 
  padding: 10px 12px; 
  border: 1px solid #e5e5ea;
  border-left: 3px solid #86868b;
  cursor: grab; 
  display: flex; 
  flex-direction: column; 
  gap: 6px; 
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.schedule-card:hover { 
  border-color: #d2d2d7;
  box-shadow: 0 4px 12px rgba(0,0,0,0.03);
}
.schedule-card.is-done { opacity: 0.4; }

.card-body { display: flex; align-items: center; gap: 8px; width: 100%; }
.card-inline-input {
  border: none;
  background: transparent;
  font-size: 12px;
  font-weight: 600;
  color: #1d1d1f;
  outline: none;
  flex: 1;
  padding: 0;
  cursor: text;
}
.is-done .card-inline-input { text-decoration: line-through; }

.card-meta-row { display: flex; justify-content: space-between; align-items: center; margin-top: 2px; }
.mini-color-picker { border: none; background: transparent; width: 12px; height: 12px; cursor: pointer; padding: 0; opacity: 0; transition: opacity 0.2s; }
.schedule-card:hover .mini-color-picker { opacity: 1; }

.card-date-badge { 
  font-size: 9px; 
  color: #86868b; 
  font-family: monospace;
  background: #f5f5f7; 
  padding: 1px 4px; 
  border-radius: 3px; 
}

/* 优雅小圆点复选框 */
.custom-checkbox-wrapper { position: relative; display: inline-block; width: 14px; height: 14px; flex-shrink: 0; }
.custom-checkbox-wrapper input { opacity: 0; width: 0; height: 0; }
.checkmark { position: absolute; top: 0; left: 0; height: 14px; width: 14px; background-color: transparent; border: 1.5px solid #d2d2d7; border-radius: 50%; transition: all 0.2s; cursor: pointer; }
.custom-checkbox-wrapper input:checked + .checkmark { background-color: #1d1d1f; border-color: #1d1d1f; }

/* 极简按钮 */
.spirit-mini-btn { background: #1d1d1f; color: #ffffff; border: none; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; cursor: pointer; transition: opacity 0.2s; }
.spirit-mini-btn:hover { opacity: 0.8; }

.add-card-btn { background: transparent; border: none; color: #a1a1a6; padding: 6px; font-size: 11px; font-weight: 600; cursor: pointer; text-align: left; width: 100%; opacity: 0; transition: opacity 0.2s; }
.kanban-column:hover .add-card-btn { opacity: 1; }
.add-card-btn:hover { color: #0066cc; }

.empty-columns-placeholder, .empty-columns-placeholder span { font-size: 11px; color: #c7c7cc; letter-spacing: 0.05em; text-align: center; margin-top: 24px; width: 100%; }
</style>