<template>
  <div 
    class="spirit-render-engine" 
    v-html="renderedHtml"
  ></div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { generateHTML } from '@tiptap/html';
import { spiritExtensions } from '../../utils/editorConfig'; // 🌟 复用你的扩展蓝图

interface Props {
  modelValue: any; // 接收后端的 JSON 对象或字符串
}

const props = defineProps<Props>();

// SpiritPreview.vue 中的计算属性修改

const renderedHtml = computed(() => {
  if (!props.modelValue) return '';
  
  let json = props.modelValue;
  if (typeof json === 'string') {
    try {
      json = JSON.parse(json);
    } catch (e) {
      return json; // 纯文本兜底
    }
  }

  try {
    // 🌟 核心修复逻辑：确保数据符合 Tiptap 的规范
    let finalJson = json;

    // 1. 如果节点缺失 type，根据你的后端逻辑，它大概率是个段落 (paragraph)
    if (json && !json.type) {
      finalJson = {
        type: 'paragraph',
        ...json
      };
    }

    // 2. 如果数据不是以 'doc' 开头的完整文档，强制套上一层 doc 外壳
    // generateHTML 必须接收 type: 'doc' 的根节点才能正确转换
    if (finalJson.type !== 'doc') {
      finalJson = {
        type: 'doc',
        content: [finalJson]
      };
    }

    return generateHTML(finalJson, spiritExtensions);
  } catch (e) {
    console.error('灵脉预览渲染失败:', e);
    // 🌟 增加一个更有意义的报错显示，方便你调试
    return `<span style="color: #d2d2d7; font-size: 12px;">(灵感解析异常: 结构不完整)</span>`;
  }
});
</script>

<style scoped>
/* 🌟 引入刚剥离出来的灵魂样式 */
@import "./spirit-typography.css";

.spirit-render-engine {
  width: 100%;
  text-align: left;
  /* 解决可能出现的长单词/代码溢出问题 */
  overflow-wrap: break-word;
  word-break: break-word;
}

/* 如果在百科列表（Index）中使用，通常需要限制图片的最大高度 */
:deep(img) {
  max-height: 300px;
  object-fit: cover;
}

/* 针对预览模式的特殊处理：隐藏 Tiptap 默认可能出现的边框 */
:deep(.ProseMirror) {
  outline: none;
}
</style>