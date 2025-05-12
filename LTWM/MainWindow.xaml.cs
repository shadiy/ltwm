using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LTWM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public HwndSource source;
        Rect[] areas;

        public const int WM_HOTKEY = 0x0312;

        public MainWindow()
        {
            InitializeComponent();

            //SetToolWindow(this);

            this.Left = 0;
            this.Top = 0;
            this.Topmost = true;

            var helper = new WindowInteropHelper(this);
            var handle = helper.EnsureHandle();

            source = HwndSource.FromHwnd(handle);

            /*
            for (int i = 0; i < count; i++)
            {
                Button button = new Button();
                button.Width = 30;
                button.Height = 30;
                button.Margin = new Thickness(5, 5, 5, 5);
                button.Background = Brushes.Gray;
                button.FontSize = 20;
                button.Content = i + 1;
                MainPanel.Children.Add(button);
            }

            // GLOBAL HOOKS NOT SUPPORTED IN C#
            // EATHER START NEW PROCCESS OR FIND ANOTHER WAY
            //IntPtr hhook = SetWindowsHookExW(5, (int nCode, IntPtr wParam, IntPtr lParam) =>
            //{
            //    Console.WriteLine("HOOKERSSS!!");
            //
            //    // Move / Resize
            //    if (nCode == 0)
            //    {
            //        // Resize other windows
            //        Console.WriteLine("WINDOW RESIZED!!");
            //        IntPtr handle = wParam;
            //        ResizeWindows(handle, manager, helper.Handle);
            //    }
            //
            //    return CallNextHookEx(0, nCode, wParam, lParam);
            //}, 0, 0);
            //

            Application.Current.Exit += (object sender, ExitEventArgs e) =>
            {
                source.RemoveHook(HwndHook);
                UnregisterHotKey(helper.Handle, 111);
                UnregisterHotKey(helper.Handle, 112);
                Console.WriteLine("APPLICATION CLOSING!!");
            };


            double monitorWidth = SystemParameters.PrimaryScreenWidth;
            double monitorHeight = SystemParameters.PrimaryScreenHeight;


            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 2);
            timer.Tick += (object? sender, EventArgs e) =>
            {
                if (!manager.IsWindowOnCurrentVirtualDesktop(helper.Handle))
                {
                    manager.MoveWindowToCurrentDesktop(helper.Handle);
                }

                var windows = GetWindowsInCurrentDesktop(manager, helper.Handle);
                Console.WriteLine(windows.Count.ToString());

                foreach (IntPtr window in windows)
                {
                    Console.WriteLine(GetWindowName(window));
                }

                foreach (IntPtr window in windows)
                {
                    var foundWindow = FindWindowInListByHandle(window, nativeWindows);

                    if (foundWindow != null)
                    {
                        Rect outRect = new Rect();
                        GetWindowRect(window, ref outRect);

                        if (outRect.left != foundWindow.rect.left || outRect.top != foundWindow.rect.top || outRect.right != foundWindow.rect.right || outRect.bottom != foundWindow.rect.bottom)
                        {
                            //ResizeWindows(window, nativeWindows, helper.Handle);

                            foundWindow.rect = outRect;
                        }
                    }
                    else
                    {
                        NativeWindow nativeWindow = new NativeWindow();
                        nativeWindow.handle = window;
                        nativeWindow.rect = new Rect();
                        nativeWindows.Add(nativeWindow);
                        //ResizeWindows(window, nativeWindows, helper.Handle);
                    }
                }

                areas = GenerateWindowAreas(nativeWindows);
                ResizeWindowsBasedOnAreas(nativeWindows, areas);

                //int windowWidth = ((int)monitorWidth - (15 * windows.Count)) / windows.Count;
                //int windowHeight = (int)monitorHeight - 40 - 60;
                //for (int i = 0; i < windows.Count; i++)
                //{
                //    var hwnd = windows[i];
                //    MoveWindow(hwnd, 15 + (i * windowWidth), 40, windowWidth, windowHeight, true);
                //}
            };
            timer.Start();
            */
        }


        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        internal static extern IntPtr GetForegroundWindow();


        [DllImport("user32.dll", EntryPoint = "SetWinEventHook")]
        internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        public delegate void WinEventProc(IntPtr hWinEventHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);


        [DllImport("user32.dll", EntryPoint = "RegisterHotKey")]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);


        [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
        internal static extern bool GetWindowRect(IntPtr hWnd, ref Win32.Rect lpRect);


        [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "EnumWindows")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        // Delegate to filter which windows to include 
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        static List<IntPtr> GetWindowsInCurrentDesktop(VirtualDesktopManager manager, IntPtr myHwnd)
        {
            List<IntPtr> windowsId = new List<IntPtr>();

            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                if (manager.IsWindowOnCurrentVirtualDesktop(hWnd))
                {
                    //var id = manager.GetWindowDesktopId(hWnd);

                    if (hWnd == myHwnd) return true;
                    if (!IsAltTabWindow(hWnd)) return true;

                    // Filter out other invisible windows

                    string windowName = GetWindowName(hWnd);
                    if (windowName == "") return true;
                    if (windowName == " ") return true;
                    if (windowName == "Settings") return true;
                    if (windowName == "Microsoft Text Input Application") return true;
                    if (windowName == "Windows Shell Experience Host") return true;

                    windowsId.Add(hWnd);
                }

                return true;
            }, 0);

            return windowsId;
        }

        static string GetWindowName(IntPtr hwnd)
        {
            int textLength = GetWindowTextLength(hwnd);
            StringBuilder outText = new StringBuilder(textLength + 1);
            int a = GetWindowText(hwnd, outText, outText.Capacity);
            return outText.ToString();
        }


        [DllImport("user32.dll", EntryPoint = "MoveWindow")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        private static extern bool IsWindowVisible(IntPtr hWnd);



        [DllImport("user32.dll", EntryPoint = "GetTitleBarInfo")]
        internal static extern bool GetTitleBarInfo(IntPtr hwnd, ref TITLEBARINFO pti);



        [StructLayout(LayoutKind.Sequential)]
        internal struct TITLEBARINFO
        {
            public uint cbSize;
            public Rect rcTitleBar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public uint[] rgstate;
        }

        [DllImport("user32.dll", EntryPoint = "GetAncestor")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", EntryPoint = "GetLastActivePopup")]
        internal static extern IntPtr GetLastActivePopup(IntPtr hWnd);


        internal const uint GA_ROOTOWNER = 3;
        internal const uint STATE_SYSTEM_INVISIBLE = 0x00008000;
        internal const long WS_EX_TOOLWINDOW = 0x00000080L;

        internal static bool IsAltTabWindow(IntPtr hwnd)
        {
            TITLEBARINFO ti = new TITLEBARINFO();
            IntPtr hwndTry, hwndWalk = 0;

            if (!IsWindowVisible(hwnd)) return false;

            hwndTry = GetAncestor(hwnd, GA_ROOTOWNER);
            while (hwndTry != hwndWalk)
            {
                hwndWalk = hwndTry;
                hwndTry = GetLastActivePopup(hwndWalk);
                if (IsWindowVisible(hwndTry)) break;
            }

            if (hwndWalk != hwnd) return false;

            // the following removes some task tray programs and "Program Manager"
            ti.cbSize = (uint)Marshal.SizeOf(typeof(TITLEBARINFO));
            GetTitleBarInfo(hwnd, ref ti);
            if ((ti.rgstate[0] & STATE_SYSTEM_INVISIBLE) > 0) return false;

            // Tool windows should not be displayed either, these do not appear in the
            // task bar.
            if ((GetWindowLongPtr(hwnd, GWL.GWL_EXSTYLE) & WS_EX_TOOLWINDOW) > 0) return false;

            return true;
        }




        // DIDNT WORK
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, GWL nIndex, IntPtr dwNewLong);

        const long WS_EX_TOPMOST = 0x00000008L;

        public enum GWL : int
        {
            GWL_WNDPROC = (-4),
            GWL_HINSTANCE = (-6),
            GWL_HWNDPARENT = (-8),
            GWL_STYLE = (-16),
            GWL_EXSTYLE = (-20),
            GWL_USERDATA = (-21),
            GWL_ID = (-12)
        }

        public static void SetToolWindow(Window window)
        {
            var wih = new WindowInteropHelper(window);
            var style = GetWindowLongPtr(wih.Handle, GWL.GWL_EXSTYLE);
            style = new IntPtr(style.ToInt64() | WS_EX_TOPMOST);
            SetWindowLongPtr(wih.Handle, GWL.GWL_EXSTYLE, style);
        }
    }
}