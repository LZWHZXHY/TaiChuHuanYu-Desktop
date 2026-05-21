using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;

namespace TaiChuHuanYu_DeskTop_V1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var dir = System.IO.Path.GetDirectoryName(tokenFilePath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            InitBrowser();
        }

        private async void InitBrowser()
        {
            await MainWebView.EnsureCoreWebView2Async(null);


            string originalUserAgent = MainWebView.CoreWebView2.Settings.UserAgent;
            MainWebView.CoreWebView2.Settings.UserAgent = originalUserAgent + " TaiChuDesktop/1.0";

            // 1. 监听网页消息
            MainWebView.WebMessageReceived += (s, e) =>
            {
                var json = e.WebMessageAsJson;
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("cmd", out var cmdEl))
                {
                    string cmd = cmdEl.GetString();
                    if (cmd == "SAVE_AUTH_TOKEN")
                    {
                        string token = doc.RootElement.GetProperty("token").GetString();
                        // 【新增】尝试获取用户名，让本地化更完整
                        string username = doc.RootElement.TryGetProperty("username", out var uEl) ? uEl.GetString() : "";
                        SaveTokenAndReload(token, username);
                    }
                }
            };

            //MainWebView.Source = new Uri("http://localhost:5173");
            MainWebView.Source = new Uri("https://bianyuzhou.com");

            // 2. 导航完成时的逻辑（核心修改）
            MainWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                string token = GetLocalToken();
                bool isLogin = !string.IsNullOrWhiteSpace(token);

                // --- 【核心修改】自动同步 Token 到 Vue 的 localStorage ---
                if (isLogin)
                {
                    // 将本地存储的 JWT 注入到网页中，这样 Vue 的 Axios 拦截器就能直接用了
                    await MainWebView.CoreWebView2.ExecuteScriptAsync($"localStorage.setItem('token', '{token}');");
                }

                // 3. 构建插件菜单
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
            };
        }

        private string tokenFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaiChuHuanYu",
            "auth.token"
        );

        // 修改：直接返回 Token 内容，方便后续注入
        private string GetLocalToken()
        {
            if (System.IO.File.Exists(tokenFilePath))
            {
                return System.IO.File.ReadAllText(tokenFilePath);
            }
            return null;
        }

        private bool CheckLocalToken() => !string.IsNullOrWhiteSpace(GetLocalToken());

        private async void SaveTokenAndReload(string token, string username)
        {
            
            await System.IO.File.WriteAllTextAsync(tokenFilePath, token);

           
            MainWebView.CoreWebView2.Reload();
        }

        public class PluginInfo
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
        }
    }
}