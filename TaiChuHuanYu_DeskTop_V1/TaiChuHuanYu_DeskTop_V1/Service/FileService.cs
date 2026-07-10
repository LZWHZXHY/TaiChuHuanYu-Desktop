using System;
using System.IO;

namespace TaiChuHuanYu_DeskTop_V1.Service
{
    public class FileService
    {
        private FileSystemWatcher _watcher;
        public string VaultPath { get; private set; }

        /// <summary>
        /// 文件变化事件：参数 (变化类型, 文件绝对路径)
        /// </summary>
        public event Action<string, string> OnFileChanged;

        public FileService(string vaultPath)
        {
            VaultPath = vaultPath;
            if (!Directory.Exists(VaultPath)) Directory.CreateDirectory(VaultPath);
            Console.WriteLine($"=== [监控] {VaultPath} ===");

            _watcher = new FileSystemWatcher(VaultPath)
            {
                Filter = "*.*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            // ---- 文件变化监听（立即通知，无延迟） ----
            _watcher.Created += (s, e) =>
            {
                if (File.Exists(e.FullPath))
                {
                    Console.WriteLine($"[Created] {e.FullPath}");
                    OnFileChanged?.Invoke("CREATED", e.FullPath);
                }
            };

            _watcher.Changed += (s, e) =>
            {
                Console.WriteLine($"[Changed] {e.FullPath}");
                OnFileChanged?.Invoke("CHANGED", e.FullPath);
            };

            _watcher.Deleted += (s, e) =>
            {
                Console.WriteLine($"[Deleted] {e.FullPath}");
                OnFileChanged?.Invoke("DELETED", e.FullPath);
            };

            _watcher.Renamed += (s, e) =>
            {
                Console.WriteLine($"[Renamed] {e.OldFullPath} -> {e.FullPath}");
                OnFileChanged?.Invoke("RENAMED", e.FullPath);
            };
        }

        /// <summary>
        /// 移动文件（供前端手动调用）
        /// </summary>
        public void MoveFile(string oldPath, string newPath)
        {
            if (!File.Exists(oldPath)) return;

            string destDir = Path.GetDirectoryName(newPath);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            // 目标文件已存在则加时间戳
            if (File.Exists(newPath))
            {
                string name = Path.GetFileNameWithoutExtension(newPath);
                string ext = Path.GetExtension(newPath);
                newPath = Path.Combine(destDir, $"{name}_{DateTime.Now.Ticks}{ext}");
            }

            File.Move(oldPath, newPath);
        }

        /// <summary>
        /// 创建空白笔记（支持相对路径，自动创建目录）
        /// </summary>
        public void CreateEmptyNote(string title, string relativePath = "")
        {
            relativePath = relativePath?.Trim('/').Trim('\\') ?? "";
            string dir = string.IsNullOrEmpty(relativePath)
                ? VaultPath
                : Path.Combine(VaultPath, relativePath);

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{title}.md");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, $"---\ntitle: {title}\n---\n");
            }
        }
        /// <summary>
        /// 复制图片到 20_Assets 目录，返回相对路径
        /// </summary>
        public string CopyImageToAssets(string fileName, byte[] imageData)
        {
            string assetsDir = Path.Combine(VaultPath, "20_Assets");
            if (!Directory.Exists(assetsDir))
                Directory.CreateDirectory(assetsDir);

            // 如果文件名已存在，添加时间戳
            string destPath = Path.Combine(assetsDir, fileName);
            if (File.Exists(destPath))
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destPath = Path.Combine(assetsDir, $"{name}_{DateTime.Now.Ticks}{ext}");
            }
            File.WriteAllBytes(destPath, imageData);
            // 返回相对路径（相对于 VaultPath）
            string relativePath = Path.GetRelativePath(VaultPath, destPath).Replace('\\', '/');
            return relativePath;
        }
        /// <summary>
        /// 创建文件夹（支持相对路径，自动创建多级目录）
        /// </summary>
        public void CreateFolder(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            string fullPath = Path.Combine(VaultPath, relativePath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}