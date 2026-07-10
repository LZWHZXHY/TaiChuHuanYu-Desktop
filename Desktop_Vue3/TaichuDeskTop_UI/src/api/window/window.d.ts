//src/window/window.d.ts

// 确保这是一个模块文件
export {};

declare global {
  interface Window {
    /** * 桌面端 C# FileService 触发的回调 
     * 当本地文件发生增删改重命名时会被调用
     */
    onFileChange?: (type: string, path: string) => void;

    /** * 可选：如果后续有其他通信（例如获取配置、读取本地文件内容等）
     * 可以在这里统一扩展
     */
    // onFileActionResponse?: (data: any) => void;
  }
}