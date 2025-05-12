using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LTWM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        WindowTree tree = new WindowTree();

        VirtualDesktopManager manager = new VirtualDesktopManager();

        static uint LeftArrow = 0x25;
        static uint RightArrow = 0x27;
        const int MOD_ALT = 0x0001;
        const int MOD_CONTROL = 0x0002;
        const int WM_HOTKEY = 0x0312;

        MainWindow mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            Console.WriteLine("LTWM started");

            mainWindow = new MainWindow();
            mainWindow.Show();

            Console.WriteLine("MainWindow created");

            mainWindow.source.AddHook(HwndHook);

            Win32.RegisterHotKey(mainWindow.source.Handle, 111, MOD_ALT | MOD_CONTROL, LeftArrow);
            Win32.RegisterHotKey(mainWindow.source.Handle, 112, MOD_ALT | MOD_CONTROL, RightArrow);

            Console.WriteLine("Registered hotkeys");


            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += (object? sender, EventArgs e) =>
            {
                if (manager.GetCurrentDesktopindex() != 3) return;

                var windows = GetWindowsInCurrentDesktop(manager, mainWindow.source.Handle);

                //tree.head = null;

                //Console.WriteLine("Tree: " + tree.ToString());
                foreach (IntPtr window in windows)
                {
                    if (tree.head == null)
                    {
                        tree.addWindow(window, null);
                        continue;
                    }


                    // if window is already in the tree
                    var existing = tree.FindWindow(window);
                    if (existing != null)
                    {
                        existing.wasVisitedLastTick = true;

                        // get window position
                        Win32.Rect win_rect;
                        var res = Win32.GetWindowRect(window, out win_rect);

                        //Console.WriteLine("Existing Window Pos: {0}, {1}, {2}, {3}", existing.rect.left, existing.rect.top, existing.rect.right, existing.rect.bottom);
                        //Console.WriteLine("New Window Pos: {0}, {1}, {2}, {3}", win_rect.left, win_rect.top, win_rect.right, win_rect.bottom);

                        var resOrMov = tree.WasResizedOrMoved(existing, win_rect);

                        if (resOrMov == WindowTree.ResizedOrMoved.Moved)
                        {
                            Console.WriteLine("Moved Window Name: " + GetWindowName(window));
                            //Console.WriteLine("New Window Pos: {0}, {1}, {2}, {3}", win_rect.left, win_rect.top, win_rect.right, win_rect.bottom);

                            // find the closest node to the window
                            var closest = tree.FindClosestWindow(win_rect);

                            if (closest != null)
                            {
                                //Console.WriteLine("Closest: " + GetWindowName(closest.handle.Value));
                                var temp_handle = closest.handle;
                                closest.handle = existing.handle;
                                existing.handle = temp_handle;
                            }
                        }
                        else if (resOrMov == WindowTree.ResizedOrMoved.Resized)
                        {
                            Console.WriteLine("Resized Window Name: " + GetWindowName(window));

                            if (existing.rect.left != win_rect.left)
                            {
                                var ver_split = tree.FindParentHorizontalSplit(existing);

                                ver_split.leftRatio += (float)(win_rect.left - existing.rect.left) / existing.rect.left;
                                Console.WriteLine($" Change: {(float)(win_rect.left - existing.rect.left) / existing.rect.left}");
                            }
                        }
                    }
                    else
                    {
                        var node = tree.head;
                        while (true)
                        {
                            if (node == null) break;
                            if (node.left == null && node.right == null) break;
                            if (node.left != null && node.right == null) node = node.left;

                            node = node.right;
                        }

                        tree.addWindow(window, node);
                    }
                }

                tree.RemoveUnvisitedNodes();

                var rect = SystemParameters.WorkArea;
                var win32Rect = new Win32.Rect((int)rect.Left + 4, (int)rect.Top + 4, (int)rect.Right - 4, (int)rect.Bottom - 4);

                ResizeNode(tree.head, win32Rect);
            };
            timer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Console.WriteLine("LTWM exiting");
            mainWindow.source.RemoveHook(HwndHook);

            Win32.UnregisterHotKey(mainWindow.source.Handle, 111);
            Win32.UnregisterHotKey(mainWindow.source.Handle, 112);

            Console.WriteLine("Unregistered hotkeys");

            Console.WriteLine("LTWM exited");
        }

        public void ResizeNode(WindowTree.Node node, Win32.Rect remainingArea)
        {
            if (node.type == WindowTree.NodeType.Window)
            {
                if (node.handle == null) return;
                Win32.MoveWindow(node.handle.Value, remainingArea.left, remainingArea.top, remainingArea.right - remainingArea.left, remainingArea.bottom - remainingArea.top, true);

                node.rect = new Win32.Rect(remainingArea.left, remainingArea.top, remainingArea.right, remainingArea.bottom);
            }
            else if (node.type == WindowTree.NodeType.HorizontalSplit)
            {
                int width = (int)MathF.Floor((remainingArea.right - remainingArea.left) * node.leftRatio);

                Win32.Rect leftRect = new Win32.Rect(remainingArea.left, remainingArea.top, remainingArea.left + width, remainingArea.bottom);
                Win32.Rect rightRect = new Win32.Rect(remainingArea.left + width, remainingArea.top, remainingArea.right, remainingArea.bottom);

                if (node.left != null) ResizeNode(node.left, leftRect);
                if (node.right != null) ResizeNode(node.right, rightRect);
            }
            else if (node.type == WindowTree.NodeType.VerticalSplit)
            {
                int height = (int)MathF.Floor((remainingArea.bottom - remainingArea.top) * node.leftRatio);

                Win32.Rect leftRect = new Win32.Rect(remainingArea.left, remainingArea.top, remainingArea.right, remainingArea.top + height);
                Win32.Rect rightRect = new Win32.Rect(remainingArea.left, remainingArea.top + height, remainingArea.right, remainingArea.bottom);

                if (node.left != null) ResizeNode(node.left, leftRect);
                if (node.right != null) ResizeNode(node.right, rightRect);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_HOTKEY:
                    IntPtr focused = Win32.GetForegroundWindow();
                    WindowTree.Node? window = tree.FindWindow(focused);
                    if (window == null) return IntPtr.Zero;

                    switch (wParam.ToInt32())
                    {
                        case 111:
                            WindowTree.Node? leftWindow = tree.FindWindowLeftOf(window);
                            if (leftWindow == null) return IntPtr.Zero;

                            WindowTree.Node leftWindowParent = leftWindow.Parent;
                            if (window.Parent.left == window)
                            {
                                leftWindow.Parent = window.Parent;
                                window.Parent.left = leftWindow;
                            }
                            else
                            {
                                leftWindow.Parent = window.Parent;
                                window.Parent.right = leftWindow;
                            }

                            if (leftWindow.Parent.left == leftWindow)
                            {
                                window.Parent = leftWindowParent;
                                leftWindowParent.left = window;
                            }
                            else
                            {
                                window.Parent = leftWindowParent;
                                leftWindowParent.right = window;
                            }

                            ResizeNode(tree.head, new Win32.Rect(10, 60, 1910, 1030));

                            handled = true;
                            break;

                        case 112:
                            WindowTree.Node? rightWindow = tree.FindWindowRightOf(window);
                            if (rightWindow == null) return IntPtr.Zero;

                            WindowTree.Node rightWindowParent = rightWindow.Parent;
                            if (window.Parent.left == window)
                            {
                                rightWindow.Parent = window.Parent;
                                window.Parent.left = rightWindow;
                            }
                            else
                            {
                                rightWindow.Parent = window.Parent;
                                window.Parent.right = rightWindow;
                            }

                            if (rightWindow.Parent.left == rightWindow)
                            {
                                window.Parent = rightWindowParent;
                                rightWindowParent.left = window;
                            }
                            else
                            {
                                window.Parent = rightWindowParent;
                                rightWindowParent.right = window;
                            }

                            ResizeNode(tree.head, new Win32.Rect(10, 60, 1910, 1030));

                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        public static List<IntPtr> GetWindowsInCurrentDesktop(VirtualDesktopManager manager, IntPtr myHwnd)
        {
            List<IntPtr> windowsId = new List<IntPtr>();

            Win32.EnumWindows((IntPtr hWnd, IntPtr lParam) =>
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
                    if (windowName == "Task Manager") return true;

                    windowsId.Add(hWnd);
                }

                return true;
            }, 0);

            return windowsId;
        }

        public static string GetWindowName(IntPtr hwnd)
        {
            int textLength = Win32.GetWindowTextLength(hwnd);
            StringBuilder outText = new StringBuilder(textLength + 1);
            int a = Win32.GetWindowText(hwnd, outText, outText.Capacity);
            return outText.ToString();
        }

        internal static bool IsAltTabWindow(IntPtr hwnd)
        {
            Win32.TitleBarInfo ti = new Win32.TitleBarInfo();
            IntPtr hwndTry, hwndWalk = 0;

            if (!Win32.IsWindowVisible(hwnd)) return false;

            hwndTry = Win32.GetAncestor(hwnd, Win32.GA_ROOTOWNER);
            while (hwndTry != hwndWalk)
            {
                hwndWalk = hwndTry;
                hwndTry = Win32.GetLastActivePopup(hwndWalk);
                if (Win32.IsWindowVisible(hwndTry)) break;
            }

            if (hwndWalk != hwnd) return false;

            // the following removes some task tray programs and "Program Manager"
            ti.cbSize = (uint)Marshal.SizeOf(typeof(Win32.TitleBarInfo));
            Win32.GetTitleBarInfo(hwnd, ref ti);
            if ((ti.rgstate[0] & Win32.STATE_SYSTEM_INVISIBLE) > 0) return false;

            // Tool windows should not be displayed either, these do not appear in the
            // task bar.
            if ((Win32.GetWindowLongPtr(hwnd, Win32.GWL.GWL_EXSTYLE) & Win32.WS_EX_TOOLWINDOW) > 0) return false;

            return true;
        }
    }

    public class Win32
    {
        internal const uint GA_ROOTOWNER = 3;
        internal const uint STATE_SYSTEM_INVISIBLE = 0x00008000;
        internal const long WS_EX_TOOLWINDOW = 0x00000080L;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public Rect() { }

            public Rect(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TitleBarInfo
        {
            public uint cbSize;
            public Rect rcTitleBar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public uint[] rgstate;
        }

        public enum GWL : int
        {
            GWL_WNDPROC = -4,
            GWL_HINSTANCE = -6,
            GWL_HWNDPARENT = -8,
            GWL_STYLE = -16,
            GWL_EXSTYLE = -20,
            GWL_USERDATA = -21,
            GWL_ID = -12
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll", EntryPoint = "MoveWindow")]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", EntryPoint = "GetTitleBarInfo")]
        public static extern bool GetTitleBarInfo(IntPtr hwnd, ref TitleBarInfo pti);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "EnumWindows")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        // Delegate to filter which windows to include 
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetAncestor")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", EntryPoint = "GetLastActivePopup")]
        public static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, GWL nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "RegisterHotKey")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
