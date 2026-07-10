using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;  // 用于 Dispatcher
using System.Windows.Threading;

namespace TaiChuHuanYu_DeskTop_V1.Service
{
    public class FileIntegrationService
    {
        private readonly FileService _fileService;
        private readonly string _vaultPath;
        private CoreWebView2 _coreWebView2;
        private ViewMode _currentMode = ViewMode.Remote;
        private readonly Dispatcher _uiDispatcher;

        public enum ViewMode { Remote, Local }

        public FileIntegrationService(FileService fileService, string vaultPath)
        {
            _fileService = fileService;
            _vaultPath = vaultPath;
            // 获取 UI 线程的 Dispatcher，确保在主线程上操作 WebView2
            _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _fileService.OnFileChanged += OnFileSystemChanged;
        }

        public void SetCoreWebView2(CoreWebView2 coreWebView2)
        {
            _coreWebView2 = coreWebView2;
        }

        public void SetMode(ViewMode mode)
        {
            _currentMode = mode;
        }

        public void HandleWebMessage(string webMessageAsJson)
        {
            // 此方法通常从 UI 线程调用，但为了安全，我们仍可确保在 UI 线程执行
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => HandleWebMessage(webMessageAsJson));
                return;
            }

            if (_coreWebView2 == null) return;

            using var doc = JsonDocument.Parse(webMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("cmd", out var cmdEl)) return;
            string cmd = cmdEl.GetString();

            switch (cmd)
            {
                case "FILE_MOVE":
                    string oldPath = root.GetProperty("oldPath").GetString();
                    string newPath = root.GetProperty("newPath").GetString();
                    _fileService.MoveFile(Path.Combine(_vaultPath, oldPath), Path.Combine(_vaultPath, newPath));
                    break;

                case "CREATE_NOTE":
                    string title = root.GetProperty("title").GetString();
                    _fileService.CreateEmptyNote(title);
                    // 通知前端创建成功
                    var noteMsg = new { cmd = "NOTE_CREATED" };
                    _coreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(noteMsg));
                    break;

                case "IMPORT_FILES":
                    // 触发导入事件
                    OnImportFilesRequested?.Invoke();
                    break;

                case "GET_FILE_LIST":
                    SendFileListToWebView();
                    break;

                case "GET_FILE_CONTENT":
                    string filePath = root.GetProperty("path").GetString();
                    SendFileContent(filePath);
                    break;

                case "SAVE_FILE_CONTENT":
                    string savePath = root.GetProperty("path").GetString();
                    string saveContent = root.GetProperty("content").GetString();
                    SaveFileContent(savePath, saveContent);
                    break;

                case "OPEN_LINK":
                    string linkPath = root.GetProperty("path").GetString();
                    SendFileContent(linkPath);
                    break;

                case "INSERT_IMAGE":
                    string currentDocPath = root.GetProperty("currentDocPath").GetString();
                    string fileName = root.GetProperty("fileName").GetString();
                    string base64Data = root.GetProperty("base64Data").GetString();
                    HandleInsertImage(currentDocPath, fileName, base64Data);
                    break;

                case "GET_BACKLINKS":
                    string targetPath = root.GetProperty("path").GetString();
                    SendBacklinks(targetPath);
                    break;
            }
        }

        public event Action OnImportFilesRequested;

        // ========== 文件变化通知转发（从 FileSystemWatcher 线程调用） ==========
        private async void OnFileSystemChanged(string type, string absolutePath)
        {
            // 此方法在非 UI 线程调用，需要使用 Dispatcher 调度到 UI 线程
            await _uiDispatcher.InvokeAsync(async () =>
            {
                if (_coreWebView2 == null) return;

                // 将绝对路径转为相对路径
                var relativePath = Path.GetRelativePath(_vaultPath, absolutePath).Replace('\\', '/');

                if (_currentMode == ViewMode.Local)
                {
                    // 本地模式：通知页面刷新文件列表
                    var msg = new { cmd = "FILE_CHANGED", type, path = relativePath };
                    string json = JsonSerializer.Serialize(msg);
                    _coreWebView2.PostWebMessageAsJson(json);
                }
                else
                {
                    // 远程模式：调用前端 window.onFileChange
                    string script = $"if(window.onFileChange) window.onFileChange('{type}', '{relativePath}');";
                    await _coreWebView2.ExecuteScriptAsync(script);
                }
            });
        }

        // ========== 发送文件列表到 WebView（本地模式） ==========
        private void SendFileListToWebView()
        {
            // 确保在 UI 线程
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => SendFileListToWebView());
                return;
            }
            if (_coreWebView2 == null) return;
            var files = GetFileList();
            var response = new { cmd = "FILE_LIST", files };
            string json = JsonSerializer.Serialize(response);
            _coreWebView2.PostWebMessageAsJson(json);
        }
        private void HandleInsertImage(string currentDocPath, string fileName, string base64Data)
        {
            if (_coreWebView2 == null) return;
            try
            {
                // 解码 base64
                byte[] imageData = Convert.FromBase64String(base64Data);
                // 复制到 Assets
                string relativePath = _fileService.CopyImageToAssets(fileName, imageData);
                // 构建插入的 Markdown 文本
                string insertedText = $"![]({relativePath})";
                // 通知前端
                var response = new { cmd = "IMAGE_INSERTED", path = relativePath, insertedText };
                string json = JsonSerializer.Serialize(response);
                _coreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                var errorResponse = new { cmd = "ERROR", message = $"插入图片失败: {ex.Message}" };
                _coreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(errorResponse));
            }
        }
        // ========== 发送文件内容 ==========
        private void SendFileContent(string relativePath)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => SendFileContent(relativePath));
                return;
            }
            if (_coreWebView2 == null) return;
            string fullPath = Path.Combine(_vaultPath, relativePath);
            string content = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    content = File.ReadAllText(fullPath);
                }
                catch { }
            }
            var response = new { cmd = "FILE_CONTENT", path = relativePath, content = content ?? "" };
            string json = JsonSerializer.Serialize(response);
            _coreWebView2.PostWebMessageAsJson(json);
        }

        // ========== 保存文件内容 ==========
        private void SaveFileContent(string relativePath, string content)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => SaveFileContent(relativePath, content));
                return;
            }
            if (_coreWebView2 == null) return;
            string fullPath = Path.Combine(_vaultPath, relativePath);
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, content);
                var response = new { cmd = "FILE_SAVED", path = relativePath };
                _coreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                var errorResponse = new { cmd = "ERROR", message = $"保存失败: {ex.Message}" };
                _coreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(errorResponse));
            }
        }

        // ========== 获取文件列表 ==========
        private List<FileInfoDto> GetFileList()
        {
            var list = new List<FileInfoDto>();
            try
            {
                if (!Directory.Exists(_vaultPath))
                {
                    Console.WriteLine($"[GetFileList] Vault 目录不存在: {_vaultPath}");
                    return list;
                }

                var files = Directory.GetFiles(_vaultPath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"[GetFileList] 扫描到 {files.Length} 个文件");

                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        // 手动构建相对路径（确保不为空）
                        string relativePath = file.Substring(_vaultPath.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            // 如果文件就在根目录，则直接使用文件名
                            relativePath = info.Name;
                        }
                        list.Add(new FileInfoDto
                        {
                            Path = relativePath,
                            Name = info.Name,
                            Extension = info.Extension.TrimStart('.'),
                            Modified = info.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            Size = info.Length
                        });
                        Console.WriteLine($"[GetFileList] 添加文件: {relativePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GetFileList] 处理文件 {file} 时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetFileList] 致命错误: {ex.Message}");
            }
            Console.WriteLine($"[GetFileList] 最终返回 {list.Count} 个条目");
            return list;
        }


        private void SendBacklinks(string targetPath)
        {
            if (_coreWebView2 == null) return;
            var backlinks = new List<string>();
            string targetFileName = Path.GetFileName(targetPath);
            string targetNameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);

            var mdFiles = Directory.GetFiles(_vaultPath, "*.md", SearchOption.AllDirectories);
            foreach (var file in mdFiles)
            {
                string relativePath = Path.GetRelativePath(_vaultPath, file).Replace('\\', '/');
                if (relativePath == targetPath) continue;

                try
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains($"[[{targetFileName}]]") ||
                        content.Contains($"[[{targetNameWithoutExt}]]") ||
                        content.Contains($"[[{targetPath}]]"))
                    {
                        backlinks.Add(relativePath);
                    }
                }
                catch { }
            }

            var response = new { cmd = "BACKLINKS", path = targetPath, links = backlinks };
            string json = JsonSerializer.Serialize(response);
            _coreWebView2.PostWebMessageAsJson(json);
        }
    }

    public class FileInfoDto
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public string Modified { get; set; }
        public long Size { get; set; }
    }
}