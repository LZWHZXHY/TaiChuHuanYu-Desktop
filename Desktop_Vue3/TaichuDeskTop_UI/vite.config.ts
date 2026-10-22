import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import VueDevTools from 'vite-plugin-vue-devtools'
import path from 'node:path'

export default defineConfig(({ command }) => ({
  plugins: [vue(), command === 'serve' && VueDevTools()],
  resolve: {
    alias: [
      { find: '@', replacement: path.resolve(__dirname, './src') },
    ],
  },
  build: {
    rolldownOptions: {
      preserveEntrySignatures: false,
      output: {
        // UI/编辑器共享循环依赖，拆包后仍按原模块导入顺序初始化。
        strictExecutionOrder: true,
        chunkFileNames: chunk => `assets/${chunk.name.split('~')[0]}-[hash].js`,
        codeSplitting: {
          groups: [
            { name: 'three', test: /node_modules[\\/]three[\\/]/ },
            { name: 'charts', test: /node_modules[\\/](echarts|zrender)[\\/]/ },
            { name: 'editor', test: /node_modules[\\/](@tiptap|prosemirror-[^\\/]+)[\\/]/ },
            { name: 'math', test: /node_modules[\\/]katex[\\/]/ },
            { name: 'network', test: /node_modules[\\/]vis-(network|data)[\\/]/ },
            { name: 'ui', test: /node_modules[\\/]element-plus[\\/]/ },
          ].map(group => ({ ...group, entriesAware: true, maxSize: 600_000 })),
        },
      },
    },
  },
}))
