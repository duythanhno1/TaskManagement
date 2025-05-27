using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Windows;

namespace TaskManagerWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async();
            // Cấu hình WebView2: tắt các chức năng mặc định
            var settings = webView.CoreWebView2.Settings;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsBuiltInErrorPageEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsPinchZoomEnabled = false;
            settings.IsReputationCheckingRequired = false;
            settings.IsStatusBarEnabled = false;
            settings.IsSwipeNavigationEnabled = false;
            settings.IsZoomControlEnabled = false;

            // Đường dẫn tuyệt đối tới thư mục "bin\Debug\"
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UI Management");

            // Kiểm tra tồn tại thư mục để tránh lỗi
            if (!Directory.Exists(basePath))
            {
                MessageBox.Show("Không tìm thấy thư mục: " + basePath);
                return;
            }

            // Ánh xạ thư mục test vào tên miền ảo app.local
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local",
                basePath,
                CoreWebView2HostResourceAccessKind.Allow
            );

            // Điều hướng tới file index.html
            webView.Source = new Uri("https://app.local/index.html");
        }
    }
}
