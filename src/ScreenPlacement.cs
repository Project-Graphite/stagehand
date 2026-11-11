using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Forms = System.Windows.Forms;

namespace Stagehand
{
    static class ScreenPlacement
    {
        const int GWL_EXSTYLE = -20;
        const long WS_EX_TOOLWINDOW = 0x80;
        const long WS_EX_NOACTIVATE = 0x08000000;
        const long WS_EX_APPWINDOW = 0x40000;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOACTIVATE = 0x10;
        const uint SWP_SHOWWINDOW = 0x40;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        const uint GW_OWNER = 4;
        const int SW_SHOWMAXIMIZED = 3, SW_RESTORE = 9;
        const int DWMWA_CLOAKED = 14;
        const uint SWP_NOZORDER = 0x4;

        public static volatile bool CursorGuardEnabled = true;
        public static volatile string PreferredDisplay = "";

        public static Forms.Screen FindLyricsScreen()
        {
            var screens = Forms.Screen.AllScreens.Where(s => !s.Primary).ToList();
            return screens.FirstOrDefault(s => s.DeviceName == PreferredDisplay)
                ?? screens.FirstOrDefault(s => (double)s.Bounds.Width / s.Bounds.Height > 3.5);
        }

        public static void ApplyWindowStyles(IntPtr hwnd, bool preview)
        {
            if (preview) return;
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            style = (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        }

        public static void PlaceOn(IntPtr hwnd, System.Drawing.Rectangle bounds)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void StartScreenGuard()
        {
            var thread = new Thread(() =>
            {
                var tick = 0;
                while (true)
                {
                    Thread.Sleep(8);
                    var lyrics = FindLyricsScreen();
                    if (lyrics == null) continue;
                    if (++tick % 30 == 0) MoveWindowsOff(lyrics);
                    if (!CursorGuardEnabled || !GetCursorPos(out var p) || !lyrics.Bounds.Contains(p.X, p.Y)) continue;
                    var home = Forms.Screen.AllScreens.Where(s => s != lyrics).OrderBy(s => Distance(s.Bounds, p.X, p.Y)).FirstOrDefault();
                    if (home == null) continue;
                    var b = home.Bounds;
                    SetCursorPos(Math.Max(b.Left, Math.Min(b.Right - 1, p.X)), Math.Max(b.Top, Math.Min(b.Bottom - 1, p.Y)));
                }
            }) { IsBackground = true, Name = "screen guard" };
            thread.Start();
        }

        static void MoveWindowsOff(Forms.Screen lyrics)
        {
            var home = Forms.Screen.PrimaryScreen;
            if (home.Bounds == lyrics.Bounds) return;
            var ownProcess = (uint)Process.GetCurrentProcess().Id;
            var className = new StringBuilder(64);
            EnumWindows((hwnd, param) =>
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindow(hwnd, GW_OWNER) != IntPtr.Zero || GetWindowTextLength(hwnd) == 0) return true;
                if ((GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0) return true;
                if (GetWindowThreadProcessId(hwnd, out var processId) == 0 || processId == ownProcess) return true;
                if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
                GetClassName(hwnd, className, className.Capacity);
                if (new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW" }.Contains(className.ToString())) return true;
                if (!GetWindowRect(hwnd, out var rect) || !lyrics.Bounds.Contains((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2)) return true;

                var maximized = IsZoomed(hwnd);
                if (maximized) ShowWindow(hwnd, SW_RESTORE);
                GetWindowRect(hwnd, out rect);
                var area = home.WorkingArea;
                var width = Math.Min(rect.Right - rect.Left, area.Width);
                var height = Math.Min(rect.Bottom - rect.Top, area.Height);
                SetWindowPos(hwnd, IntPtr.Zero, area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
                if (maximized) ShowWindow(hwnd, SW_SHOWMAXIMIZED);
                return true;
            }, IntPtr.Zero);
        }

        static double Distance(System.Drawing.Rectangle r, int x, int y)
        {
            var dx = Math.Max(Math.Max(r.Left - x, 0), x - r.Right + 1);
            var dy = Math.Max(Math.Max(r.Top - y, 0), y - r.Bottom + 1);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
