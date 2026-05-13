import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import VueDevTools from 'vite-plugin-vue-devtools'
import path from 'node:path' // 🌟 1. 引入 path 模块

// https://vite.dev/config/
export default defineConfig({
  plugins: [VueDevTools(), vue()],
  // 🌟 2. 添加 resolve 配置
  resolve: {
    alias: {
      // 这里的 '@' 将指向项目的 'src' 目录
      '@': path.resolve(__dirname, './src')
    }
  }
})