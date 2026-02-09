using System.Text;
// using Albatross; // Removed Legacy Namespace

namespace ICN_T2
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            #region agent log
            void AgentLog(string hypothesisId, string location, string message, object? data = null)
            {
                try
                {
                    var logPath = @"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log";
                    var dir = System.IO.Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
                    var payload = new
                    {
                        sessionId = "debug-session",
                        runId = "run1",
                        hypothesisId,
                        location,
                        message,
                        data,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(payload) + System.Environment.NewLine);
                }
                catch
                {
                }
            }

            AgentLog("H8", "Program.cs:Main:entry", "Program.Main entry", new { pid = System.Diagnostics.Process.GetCurrentProcess().Id });

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                AgentLog("H9", "Program.cs:Main:UnhandledException", "Unhandled exception", new { exception = e.ExceptionObject?.ToString(), isTerminating = e.IsTerminating });
            };
            #endregion

            // 1. 인코딩 프로바이더 등록 (가장 중요!)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 2. WPF 요괴워치 스타일 메인 윈도우 실행
            var wpfApp = new System.Windows.Application();
            #region agent log
            AgentLog("H8", "Program.cs:Main:beforeRun", "About to run ModernModWindow");
            #endregion
            wpfApp.Run(new ICN_T2.UI.WPF.ModernModWindow());
        }
    }
}