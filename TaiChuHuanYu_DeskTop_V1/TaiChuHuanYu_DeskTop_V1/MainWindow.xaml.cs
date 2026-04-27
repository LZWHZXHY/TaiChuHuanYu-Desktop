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
    //这里处理互动逻辑
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitBrowser();
        }

        private async void InitBrowser()
        {
            await MainWebView.EnsureCoreWebView2Async(null);

            MainWebView.WebMessageReceived += (s, e) =>
            {
                
                var json = e.WebMessageAsJson;
                using var doc = System.Text.Json.JsonDocument.Parse(json);

               
                if (doc.RootElement.TryGetProperty("cmd", out var cmdEl) && cmdEl.GetString() == "SAVE_AUTH_TOKEN")
                {
                    string token = doc.RootElement.GetProperty("token").GetString();
                    SaveTokenAndReload(token); 
                }
            };


            MainWebView.Source = new Uri("http://localhost:5173"); //加载网页内容 Vue3

            MainWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                bool isLogin = CheckLocalToken();




                var plugins = new List<PluginInfo>();
                if (!isLogin)
                {
                    plugins.Add(new PluginInfo { Name = "身份认证", Url = "/LoginRegister" });
                }
                else
                {
                    // 已登录，加载全部灵脉插件
                    plugins.Add(new PluginInfo { Name = "推送首页", Url = "/" });
                    plugins.Add(new PluginInfo { Name = "太初灵脉", Url = "/lingmai" });
                }

                string json = System.Text.Json.JsonSerializer.Serialize(plugins);
                await MainWebView.CoreWebView2.ExecuteScriptAsync($"window.receivePlugins({json})");
            };




            

        }

        private string tokenFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.token");
        private bool CheckLocalToken()
        {
            // 如果文件存在且里面有内容，就认为已登录
            return System.IO.File.Exists(tokenFilePath) && !string.IsNullOrWhiteSpace(System.IO.File.ReadAllText(tokenFilePath));
        }

        private async void SaveTokenAndReload(string token)
        {
            // 将 Token 写入本地文件
            await System.IO.File.WriteAllTextAsync(tokenFilePath, token);
            // 关键：刷新 WebView2，这会重新触发 NavigationCompleted，从而加载完整菜单
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