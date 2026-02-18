using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ICN_T2.UI.WPF.Services
{
    /// <summary>
    /// Windows 11 Mica Backdrop 효과를 WPF Window에 적용하는 헬퍼 클래스
    /// - MicaBackdrop: 시스템 배경화면 색상과 동기화되는 반투명 효과
    /// - Acrylic: iOS 제어센터 스타일의 유리 효과
    /// - Fallback: Windows 10 이하에서는 일반 배경 사용
    /// </summary>
    public static class MicaBackdropHelper
    {
        #region Windows API Constants

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // SystemBackdropType values
        private const int DWMSBT_AUTO = 0;           // 자동
        private const int DWMSBT_NONE = 1;           // 없음
        private const int DWMSBT_MAINWINDOW = 2;     // Mica (메인 윈도우)
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Mica Alt (일시적 윈도우)
        private const int DWMSBT_TABBEDWINDOW = 4;   // Mica Tabbed

        #endregion

        #region DllImport

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref bool attrValue,
            int attrSize);

        #endregion

        #region Public Methods

        /// <summary>
        /// Window에 Mica Backdrop 효과 적용 (Windows 11+)
        /// </summary>
        /// <param name="window">대상 Window</param>
        /// <param name="useDarkMode">다크 모드 사용 여부 (기본값: false)</param>
        /// <returns>성공 여부</returns>
        public static bool ApplyMicaBackdrop(Window window, bool useDarkMode = false)
        {
            try
            {
                // Windows 11 이상에서만 동작
                if (!IsWindows11OrGreater())
                {
                    System.Diagnostics.Debug.WriteLine("[MicaBackdrop] Windows 11 미만 - Mica 미적용");
                    return false;
                }

                // Window Handle 가져오기
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;

                if (hwnd == IntPtr.Zero)
                {
                    // Window가 아직 로드되지 않았으면 Loaded 이벤트에서 재시도
                    window.Loaded += (s, e) => ApplyMicaBackdrop(window, useDarkMode);
                    return false;
                }

                // 1. Dark Mode 설정
                int darkMode = useDarkMode ? 1 : 0;
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref darkMode,
                    sizeof(int));

                // 2. Mica Backdrop 적용 (Windows 11 22H2+)
                int backdropType = DWMSBT_MAINWINDOW;  // Mica
                int result = DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType,
                    sizeof(int));

                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[MicaBackdrop] Mica 적용 성공");
                    return true;
                }
                else
                {
                    // Fallback: 구형 Windows 11 (21H2)에서는 DWMWA_MICA_EFFECT 사용
                    System.Diagnostics.Debug.WriteLine("[MicaBackdrop] SYSTEMBACKDROP_TYPE 실패, MICA_EFFECT 시도");
                    int micaEnabled = 1;
                    result = DwmSetWindowAttribute(
                        hwnd,
                        DWMWA_MICA_EFFECT,
                        ref micaEnabled,
                        sizeof(int));

                    if (result == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[MicaBackdrop] Mica (레거시 방식) 적용 성공");
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[MicaBackdrop] 적용 실패: HRESULT={result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MicaBackdrop] 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mica Backdrop 제거
        /// </summary>
        /// <param name="window">대상 Window</param>
        public static void RemoveMicaBackdrop(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;

                if (hwnd == IntPtr.Zero)
                    return;

                int backdropType = DWMSBT_NONE;
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType,
                    sizeof(int));

                System.Diagnostics.Debug.WriteLine("[MicaBackdrop] Mica 제거됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MicaBackdrop] 제거 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Window에 Acrylic Backdrop 효과 적용 (Windows 11+)
        /// </summary>
        public static bool ApplyAcrylicBackdrop(Window window, bool useDarkMode = false)
        {
            try
            {
                if (!IsWindows11OrGreater())
                    return false;

                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;

                if (hwnd == IntPtr.Zero)
                {
                    window.Loaded += (s, e) => ApplyAcrylicBackdrop(window, useDarkMode);
                    return false;
                }

                // Dark Mode
                int darkMode = useDarkMode ? 1 : 0;
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref darkMode,
                    sizeof(int));

                // Acrylic Backdrop (Transient Window)
                int backdropType = DWMSBT_TRANSIENTWINDOW;
                int result = DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType,
                    sizeof(int));

                System.Diagnostics.Debug.WriteLine($"[MicaBackdrop] Acrylic 적용: result={result}");
                return result == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MicaBackdrop] Acrylic 오류: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Windows 11 이상인지 확인 (빌드 22000+)
        /// </summary>
        private static bool IsWindows11OrGreater()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                // Windows 11 = NT 10.0.22000+
                return version.Major >= 10 && version.Build >= 22000;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
