using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ICN_T2.UI
{
    public class ModdingWindow_Web : Form
    {
        private WebView2 _webView;

        public ModdingWindow_Web()
        {
            // Initial Form Setup (Borderless if desired, but let's keep it simple first)
            this.Text = "Nexus Mod Studio";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(10, 10, 15); // Match web bg

            // Initialize WebView2
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            this.Controls.Add(_webView);

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Ensure Runtime is ready
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Application.StartupPath, "TopLevel_WebView2_Data"));
                await _webView.EnsureCoreWebView2Async(env);

                // Configure Settings (Disable unwanted browser features)
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Hook up Message Receiver (JS -> C#)
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Navigate to local index.html
                string htmlPath = Path.Combine(Application.StartupPath, "Resources", "Web", "index.html");
                if (File.Exists(htmlPath))
                {
                    _webView.CoreWebView2.Navigate(htmlPath);
                }
                else
                {
                    MessageBox.Show($"Web resource not found at: {htmlPath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 Initialization Failed:\n" + ex.Message);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();

            if (message == "save_data")
            {
                // Placeholder for Save Logic
                MessageBox.Show("C# received 'save_data' command from Web UI!", "Bridge Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
