<template>
  <div class="workspace-excel-frame" ref="excelRootRef">
    <header class="excel-header">
      <input 
        :value="props.title" 
        @input="onTitleInput" 
        class="excel-title-input" 
        placeholder="未命名电子表格 / Spreadsheet" 
      />
      <div class="excel-actions">
        <button class="action-btn import-btn" @click="triggerImport">📥 导入 Excel</button>
        <input ref="fileInput" type="file" accept=".xlsx, .xls" style="display: none" @change="handleFileImport" />
        
        <button class="action-btn export-btn" @click="handleExport">📤 导出 Excel</button>
      </div>
    </header>

    <div class="excel-sheet-wrapper" ref="wrapperRef">
      <div 
        id="luckysheet-core-container" 
        :style="{ width: containerWidth + 'px', height: containerHeight + 'px' }"
        class="luckysheet-pixel-snapped">
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue';
import luckysheet from "luckysheet";
import * as XLSX from "xlsx";

const props = defineProps<{
  title: string;
  noteId: string;
  blocks?: any[]; 
  extraData?: string; // 🌟 补齐多态对接契约：声明接收可选属性，完全释放通道给右侧面板无污染使用
}>();

const emit = defineEmits(['update:title', 'change']);

const excelRootRef = ref<HTMLElement | null>(null);
const wrapperRef = ref<HTMLElement | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);

// 动态绑定容器的绝对整数像素高宽
const containerWidth = ref<number>(100);
const containerHeight = ref<number>(100);
let resizeObserver: ResizeObserver | null = null;

const onTitleInput = (e: Event) => {
  emit('update:title', (e.target as HTMLInputElement).value);
};

// 只记录有真正内容部分的“稀疏矩阵清洗同步器”
const handleDataChange = () => {
  if (!luckysheet) return;
  
  const rawSheets = luckysheet.getluckysheetfile();
  const cleanSheets = JSON.parse(JSON.stringify(rawSheets));

  if (!cleanSheets || cleanSheets.length === 0) return;

  const compressedSheets = cleanSheets.map((sheet: any) => {
    let celldata = sheet.celldata || [];

    if (sheet.data && sheet.data.length > 0) {
      celldata = [];
      sheet.data.forEach((row: any, rIdx: number) => {
        if (!row) return;
        row.forEach((cell: any, cIdx: number) => {
          if (cell && (cell.v !== undefined && cell.v !== null && String(cell.v).trim() !== "")) {
            celldata.push({
              r: rIdx,
              c: cIdx,
              v: cell
            });
          }
        });
      });
    } else if (Array.isArray(celldata)) {
      celldata = celldata.filter((item: any) => {
        return item && item.v && (item.v.v !== undefined && item.v.v !== null && String(item.v.v).trim() !== "");
      });
    }

    const validRows = celldata.map((item: any) => item.r);
    const validCols = celldata.map((item: any) => item.c);
    
    const maxRow = validRows.length > 0 ? Math.max(...validRows) + 1 : 20;  
    const maxCol = validCols.length > 0 ? Math.max(...validCols) + 1 : 15;  

    return {
      name: sheet.name || 'Sheet1',
      status: sheet.status,
      order: sheet.order,
      row: maxRow,       
      column: maxCol,    
      celldata: celldata, 
      config: sheet.config || {},
      color: sheet.color || ""
    };
  });

  emit('change', {
    blocks: [{
      id: `excel_grid_${props.noteId}`,
      ownerId: props.noteId,
      ownerType: 'excel',
      type: 'excel-grid',
      data: JSON.stringify({
        sheetData: compressedSheets,
        cells: extractPreviewCells(compressedSheets) 
      })
    }]
  });
};

const extractPreviewCells = (sheetData: any[]) => {
  try {
    const firstSheet = sheetData[0];
    const previewMatrix: string[][] = [
      ['', '', ''],
      ['', '', ''],
      ['', '', '']
    ];

    if (firstSheet?.data && firstSheet.data.length > 0) {
      const dataMatrix = firstSheet.data;
      for (let r = 0; r < 3; r++) {
        for (let c = 0; c < 3; c++) {
          previewMatrix[r][c] = dataMatrix[r]?.[c]?.m || dataMatrix[r]?.[c]?.v || '';
        }
      }
    } 
    else if (firstSheet?.celldata && Array.isArray(firstSheet.celldata)) {
      firstSheet.celldata.forEach((item: any) => {
        const r = item.r;
        const c = item.c;
        if (r < 3 && c < 3) {
          previewMatrix[r][c] = item.v?.m || item.v?.v || '';
        }
      });
    }
    return previewMatrix;
  } catch (e) { 
    return [["(空表格)"]]; 
  }
};

const triggerImport = () => fileInput.value?.click();
const handleFileImport = (e: Event) => {
  const files = (e.target as HTMLInputElement).files;
  if (!files || files.length === 0) return;
  const reader = new FileReader();
  reader.onload = (event) => {
    const data = new Uint8Array(event.target?.result as ArrayBuffer);
    const workbook = XLSX.read(data, { type: 'array' });
    
    const convertedSheets: any[] = [];
    workbook.SheetNames.forEach((sheetName, index) => {
      const worksheet = workbook.Sheets[sheetName];
      const celldata: any[] = [];
      const range = XLSX.utils.decode_range(worksheet['!ref'] || 'A1:A1');

      for (let r = range.s.r; r <= range.e.r; r++) {
        for (let c = range.s.c; c <= range.e.c; c++) {
          const cellAddress = XLSX.utils.encode_cell({ r, c });
          const cell = worksheet[cellAddress];
          if (cell && cell.v !== undefined) {
            celldata.push({
              r: r,
              c: c,
              v: {
                v: cell.v,
                m: cell.w || String(cell.v),
                ct: { fa: "@", t: "s" }
              }
            });
          }
        }
      }

      convertedSheets.push({
        name: sheetName,
        status: index === 0 ? 1 : 0,
        order: index,
        column: Math.max(range.e.c + 1, 15),
        row: Math.max(range.e.r + 1, 20),
        celldata: celldata,
        config: {}
      });
    });

    if (convertedSheets.length > 0) {
      luckysheet.destroy();
      initLuckysheetEngine(convertedSheets);
      handleDataChange();
    }
  };
  reader.readAsArrayBuffer(files[0]);
  if (fileInput.value) fileInput.value.value = '';
};

const handleExport = () => {
  const sheets = luckysheet.getluckysheetfile();
  const workbook = XLSX.utils.book_new();

  sheets.forEach((sheet: any) => {
    const sheetName = sheet.name || 'Sheet1';
    const celldata = sheet.celldata || [];
    if(celldata.length === 0) return;
    
    const maxRow = Math.max(...celldata.map((item: any) => item.r), 0);
    const maxCol = Math.max(...celldata.map((item: any) => item.c), 0);
    const aoa: any[][] = Array.from({ length: maxRow + 1 }, () => Array(maxCol + 1).fill(''));

    celldata.forEach((item: any) => {
      aoa[item.r][item.c] = item.v?.m || item.v?.v || '';
    });

    for (let i = 0; i < aoa.length; i++) {
      if (!aoa[i]) aoa[i] = [];
    }

    const ws = XLSX.utils.aoa_to_sheet(aoa);
    XLSX.utils.book_append_sheet(workbook, ws, sheetName);
  });

  XLSX.writeFile(workbook, `${props.title || '太初寰宇长卷编织表格'}.xlsx`);
};

const initLuckysheetEngine = (dataPayload: any[]) => {
  luckysheet.create({
    container: 'luckysheet-core-container',
    title: props.title || '太初寰宇长卷编织',
    lang: 'zh',
    data: dataPayload,
    showinfobar: false,       
    showtoolbar: true,        
    showsheetbar: true,       
    showstatisticBar: false,  
    enableAddRow: true,       

    hook: {
      cellUpdated: handleDataChange,
      sheetChanged: handleDataChange
    }
  });
};

onMounted(() => {
  if (typeof window !== 'undefined') {
    (window as any).luckysheet = luckysheet;
  }

  let initialData = [{ 
    name: "Sheet1", 
    status: 1, 
    order: 0, 
    column: 16, 
    row: 25,    
    celldata: [], 
    config: {},
    defaultRowHeight: 22,
    defaultColWidth: 78
  }];

  const excelBlock = props.blocks?.find(b => b.type === 'excel-grid');
  if (excelBlock?.data) {
    try {
      const parsed = typeof excelBlock.data === 'string' ? JSON.parse(excelBlock.data) : excelBlock.data;
      if (parsed.sheetData) initialData = parsed.sheetData;
    } catch (e) {}
  }

  nextTick(() => {
    initLuckysheetEngine(initialData);

    // 🌟 物理抗锯齿自适应监听器：拦截 Flex 突变，消除小数点像素
    if (wrapperRef.value) {
      resizeObserver = new ResizeObserver((entries) => {
        for (let entry of entries) {
          const { width, height } = entry.contentRect;
          
          // 向下取整锁死，彻底杜绝模糊
          containerWidth.value = Math.floor(width);
          containerHeight.value = Math.floor(height);
          
          nextTick(() => {
            if (luckysheet) luckysheet.resize();
          });
        }
      });
      resizeObserver.observe(wrapperRef.value);
    }
  });
});

onUnmounted(() => {
  if (resizeObserver) resizeObserver.disconnect();
});
</script>

<style scoped>
.workspace-excel-frame { 
  width: 100%; 
  height: 100%; 
  display: flex; 
  flex-direction: column; 
  background: #ffffff; 
  overflow: hidden; 
}

.excel-header { 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  padding: 14px 40px; 
  border-bottom: 1px solid #f2f2f7; 
  background: #ffffff; 
  flex-shrink: 0; 
  z-index: 100; 
}

.excel-title-input { 
  font-size: 22px; 
  font-weight: 700; 
  border: none; 
  outline: none; 
  color: #1d1d1f; 
  background: transparent; 
  width: 40%; 
}

.excel-actions { 
  display: flex; 
  gap: 12px; 
}

.action-btn { 
  padding: 8px 16px; 
  border-radius: 8px; 
  font-size: 13px; 
  font-weight: 600; 
  cursor: pointer; 
  border: 1px solid #d2d2d7; 
  background: #ffffff; 
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.action-btn:hover {
  background: #f5f5f7;
  transform: translateY(-0.5px);
}

.export-btn { 
  background: #0066cc; 
  color: #ffffff; 
  border-color: #0066cc; 
}
.export-btn:hover { 
  background: #005bb8; 
  border-color: #005bb8;
}

.excel-sheet-wrapper { 
  flex: 1; 
  width: 100%; 
  position: relative; 
  overflow: hidden; 
  background: #f5f5f7; 
}

/* 纠正亚像素对齐的渲染画布容器 */
.luckysheet-pixel-snapped {
  margin: 0px;
  padding: 0px;
  position: absolute;
  left: 0px;
  top: 0px;
  image-rendering: -webkit-optimize-contrast;
  image-rendering: crisp-edges;
}

:deep(#luckysheet-core-container) {
  box-sizing: border-box;
}

:deep(.luckysheet-wa-editor) {
  border-top: none !important; 
  background: #fcfcfd !important; 
}

:deep(.luckysheet-work-area) {
  height: 100% !important;
}
</style>

<style>
body .luckysheet-cols-menu,
body .luckysheet-wa-editor-insert-menu,
body .luckysheet-font-size-menu,
body .luckysheet-font-family-menu,
body .luckysheet-filter-menu {
  z-index: 2005 !important;
}

body .universal-color-picker,
body .luckysheet-color-picker,
body .luckysheet-spectrum-container,
body .spectrum-container,
body .sp-container {
  z-index: 2010 !important;
}

body .luckysheet-cols-rows-shift-menu,
body .luckysheet-rightclick-menu {
  z-index: 2020 !important;
}

body .luckysheet-modal-dialog-slider,
body .luckysheet-modal-dialog-mask {
  z-index: 2015 !important;
}
</style>