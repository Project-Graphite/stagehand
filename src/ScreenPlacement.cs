using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Forms = System.Windows.Forms;

namespace GraphiteLyrics
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
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

        public static volatile bool CursorGuardEnabled = true;

        public static Forms.Screen FindLyricsScreen()
        {
            return Forms.Screen.AllScreens.FirstOrDefault(s => s.Bounds.Width == 1920 && s.Bounds.Height == 464)
                ?? Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary && (double)s.Bounds.Width / s.Bounds.Height > 3.5);
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

        public static void StartCursorGuard()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(8);
                    if (!CursorGuardEnabled) continue;
                    var lyrics = FindLyricsScreen();
                    if (lyrics == null || !GetCursorPos(out var p) || !lyrics.Bounds.Contains(p.X, p.Y)) continue;
                    var home = Forms.Screen.AllScreens.Where(s => s != lyrics).OrderBy(s => Distance(s.Bounds, p.X, p.Y)).FirstOrDefault();
                    if (home == null) continue;
                    var b = home.Bounds;
                    SetCursorPos(Math.Max(b.Left, Math.Min(b.Right - 1, p.X)), Math.Max(b.Top, Math.Min(b.Bottom - 1, p.Y)));
                }
            }) { IsBackground = true, Name = "cursor guard" };
            thread.Start();
        }

        static double Distance(System.Drawing.Rectangle r, int x, int y)
        {
            var dx = Math.Max(Math.Max(r.Left - x, 0), x - r.Right + 1);
            var dy = Math.Max(Math.Max(r.Top - y, 0), y - r.Bottom + 1);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
