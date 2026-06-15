<template>
  <div 
    class="spirit-render-engine" 
    v-html="renderedHtml"
  ></div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { generateHTML } from '@tiptap/html';
import { spiritExtensions } from '../../utils/editorConfig'; // 复用您的扩展蓝图
import StarterKit from '@tiptap/starter-kit';

interface Props {
  modelValue: any; // 接收后端的 JSON 对象或字符串
}

const props = defineProps<Props>();

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
    // 1. 深拷贝，防止污染原响应式数据
    let finalJson = JSON.parse(JSON.stringify(json));

    if (finalJson && !finalJson.type) {
      finalJson = { type: 'paragraph', ...finalJson };
    }

    if (finalJson.type !== 'doc') {
      finalJson = { type: 'doc', content: [finalJson] };
    }

    // 2. 🌟 升级版防御器：精准白名单清洗
    const stripAndCleanEngine = (nodes: any[]) => {
      if (!nodes || !Array.isArray(nodes)) return;

      for (let i = 0; i < nodes.length; i++) {
        const node = nodes[i];
        if (!node || typeof node !== 'object') continue;

        // 🛡️ 核心修复点 1：精准过滤 marks 数组，只保留系统支持的样式（防止颜色丢失，同时防御未知样色崩溃）
        if (node.marks && Array.isArray(node.marks)) {
          // 这里是您当前系统（StarterKit + 独立扩展）注册的所有合法 Mark
          const allowedMarkTypes = ['bold', 'italic', 'underline', 'strike', 'code', 'textStyle', 'highlight'];
          
          node.marks = node.marks.filter((mark: any) => {
            if (!mark || typeof mark !== 'object') return false;
            // 兼容标准字符串或带有 type.name 结构的脏数据
            const markTypeName = typeof mark.type === 'string' ? mark.type : mark.type?.name;
            return allowedMarkTypes.includes(markTypeName);
          });

          // 如果过滤完空了，直接删掉属性保持节点纯净
          if (node.marks.length === 0) {
            delete node.marks;
          }
        }

        // 🛡️ 核心修复点 2：放行所有在 editorConfig 中存在的合法业务节点（包含折叠、任务列表、提及等）
        const allowedNodeTypes = [
          'doc', 'paragraph', 'text', 'heading', 'blockquote', 
          'bulletList', 'orderedList', 'listItem', 'codeBlock', 
          'image', 'spirit-link', 'details', 'summary', 
          'taskList', 'taskItem', 'mention', 'horizontalRule', 'hardBreak'
        ];
        
        if (node.type && !allowedNodeTypes.includes(node.type)) {
          node.type = 'paragraph'; // 遇到完全无法识别的磁场异动节点，才纠正为段落
        }

        // 递归深层清洗子节点
        if (node.content && Array.isArray(node.content)) {
          stripAndCleanEngine(node.content);
        }
      }
    };

    if (finalJson.content) {
      stripAndCleanEngine(finalJson.content);
    }

    // 3. 显式引入纯净的 StarterKit 依赖包并结合您的业务扩展进行纯静态 HTML 转换
    return generateHTML(finalJson, [
      StarterKit.configure({
        heading: { levels: [1, 2, 3] },
        codeBlock: { HTMLAttributes: { class: 'spirit-code-block' } },
      }),
      ...spiritExtensions.filter(ext => ext && ext.name !== 'starter-kit' && ext.name !== 'starterKit')
    ]);

  } catch (e: any) {
    console.error('【灵脉高级清洗失败】:', e);
    return `<div style="color: #ff3b30; font-size: 13px; padding: 10px; border: 1px dashed #ff3b30; border-radius: 4px;">
              ⚠️ 百科词条局部发生极强磁场干扰 (${e.message})
            </div>`;
  }
});
</script>

<style>
@import "./spirit-typography.css";
</style>


<style scoped>


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