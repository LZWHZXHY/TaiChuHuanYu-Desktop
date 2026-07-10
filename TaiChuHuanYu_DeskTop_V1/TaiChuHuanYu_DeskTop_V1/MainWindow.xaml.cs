using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using TaiChuHuanYu_DeskTop_V1.Service;

namespace TaiChuHuanYu_DeskTop_V1
{
    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        private FileService _fileService;
        private FileIntegrationService _integrationService;
        private string vaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TaiChuVault");
        private string tokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaiChuHuanYu",
            "auth.token"
        );

        private FileIntegrationService.ViewMode _currentMode = FileIntegrationService.ViewMode.Remote;

        public MainWindow()
        {
            AllocConsole();
            InitializeComponent();

            // 初始化文件服务
            _fileService = new FileService(vaultPath);
            // 初始化集成服务（负责桥接 FileService 和 WebView2）
            _integrationService = new FileIntegrationService(_fileService, vaultPath);
            // 订阅导入文件请求事件，以弹出对话框
            _integrationService.OnImportFilesRequested += ImportFilesFromDialog;

            // 确保 token 目录存在
            var dir = Path.GetDirectoryName(tokenFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            InitBrowser();
        }

        private async void InitBrowser()
        {
            await MainWebView.EnsureCoreWebView2Async(null);
            //MainWebView.CoreWebView2.OpenDevToolsWindow();
            MainWebView.CoreWebView2.Settings.UserAgent += " TaiChuDesktop/1.0";

            // ========== 虚拟映射 1：app.local -> 前端资源（HTML, CSS, JS） ==========
            string resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "LocalFileBrowser");
            if (Directory.Exists(resourcesPath))
            {
                MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    resourcesPath,
                    CoreWebView2HostResourceAccessKind.Allow
                );
                Console.WriteLine($"[虚拟映射] app.local -> {resourcesPath}");
            }
            else
            {
                Console.WriteLine($"[警告] 资源目录不存在: {resourcesPath}");
            }

            // ========== 虚拟映射 2：vault.local -> 笔记库根目录 ==========
            string vaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TaiChuVault");
            if (Directory.Exists(vaultPath))
            {
                MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "vault.local",
                    vaultPath,
                    CoreWebView2HostResourceAccessKind.Allow
                );
                Console.WriteLine($"[虚拟映射] vault.local -> {vaultPath}");
            }
            else
            {
                Console.WriteLine($"[警告] Vault 目录不存在: {vaultPath}");
            }

            // 将 CoreWebView2 注入集成服务（用于发送消息到前端）
            _integrationService.SetCoreWebView2(MainWebView.CoreWebView2);

            // 注册消息处理：Token 相关留在本窗口，其余转发给集成服务
            MainWebView.WebMessageReceived += (s, e) =>
            {
                var json = e.WebMessageAsJson;
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("cmd", out var cmdEl))
                {
                    string cmd = cmdEl.GetString();
                    if (cmd == "SAVE_AUTH_TOKEN")
                    {
                        string token = root.GetProperty("token").GetString();
                        string username = root.TryGetProperty("username", out var uEl) ? uEl.GetString() : "";
                        SaveTokenAndReload(token, username);
                        return;
                    }
                }
                // 非 Token 操作，全部交给集成服务
                _integrationService.HandleWebMessage(e.WebMessageAsJson);
            };

            // 导航完成事件（远程模式专用）
            MainWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            // 默认加载远程模式
            SwitchToRemoteMode();
        }

        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_currentMode == FileIntegrationService.ViewMode.Local) return;

            string token = GetLocalToken();
            bool isLogin = !string.IsNullOrWhiteSpace(token);
            if (isLogin)
            {
                await MainWebView.CoreWebView2.ExecuteScriptAsync($"localStorage.setItem('token', '{token}');");
            }

            var plugins = new List<PluginInfo>();
            if (!isLogin)
            {
                plugins.Add(new PluginInfo { Name = "身份认证", Url = "/LoginRegister", Icon = "Lock" });
            }
            else
            {
                plugins.Add(new PluginInfo { Name = "推送首页", Url = "/", Icon = "Home" });
                plugins.Add(new PluginInfo { Name = "太初灵脉", Url = "/lingmai", Icon = "Bolt" });
            }
            string json = System.Text.Json.JsonSerializer.Serialize(plugins);
            await MainWebView.CoreWebView2.ExecuteScriptAsync($"window.receivePlugins({json})");
        }

        // ========== 模式切换 ==========
        private void ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == FileIntegrationService.ViewMode.Remote)
                SwitchToLocalMode();
            else
                SwitchToRemoteMode();
        }

        private void SwitchToRemoteMode()
        {
            _currentMode = FileIntegrationService.ViewMode.Remote;
            _integrationService.SetMode(_currentMode);
            ToggleModeBtn.Content = "切换到本地模式";
            ModeStatus.Text = "当前：远程模式";
            MainWebView.Source = new Uri("http://localhost:5173");
        }

        private void SwitchToLocalMode()
        {
            _currentMode = FileIntegrationService.ViewMode.Local;
            _integrationService.SetMode(_currentMode);
            ToggleModeBtn.Content = "切换到远程模式";
            ModeStatus.Text = "当前：本地模式";
            LoadLocalFileBrowser();
        }

        private void LoadLocalFileBrowser()
        {
            MainWebView.Source = new Uri("https://app.local/index.html");
        }

        // ========== Token 管理 ==========
        private string GetLocalToken()
        {
            if (File.Exists(tokenFilePath))
                return File.ReadAllText(tokenFilePath);
            return null;
        }

        private async void SaveTokenAndReload(string token, string username)
        {
            await File.WriteAllTextAsync(tokenFilePath, token);
            MainWebView.CoreWebView2.Reload();
        }

        // ========== 导入文件（由集成服务触发） ==========
        private void ImportFilesFromDialog()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
            if (dialog.ShowDialog() == true)
            {
                foreach (string file in dialog.FileNames)
                {
                    string dest = Path.Combine(vaultPath, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }
        }

        // ========== DTO（与集成服务中定义保持一致，可共用） ==========
        public class PluginInfo
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
        }
    }
}