using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


// https://stackoverflow.com/a/32417530

namespace LTWM
{
    internal static class Guids
    {
        public static readonly Guid CLSID_ImmersiveShell = new Guid(0xC2F03A33, 0x21F5, 0x47FA, 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39);
        public static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid(0xC5E0CDCA, 0x7B6E, 0x41B2, 0x9F, 0xC4, 0xD9, 0x39, 0x75, 0xCC, 0x46, 0x7B);
        public static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        public static readonly Guid IID_IVirtualDesktopManagerInternal = new Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6");
        public static readonly Guid IID_IVirtualDesktop = new Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4");
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
    internal interface IVirtualDesktop
    {
        void notimpl1(); // void IsViewVisible(IApplicationView view, out int visible);
        Guid GetId();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6")]
    internal interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void notimpl1();  // void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
        void notimpl2();  // void CanViewMoveDesktops(IApplicationView view, out int itcan);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        IVirtualDesktop CreateDesktop();
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop([In] IntPtr TopLevelWindow, [Out] out int OnCurrentDesktop);
        [PreserveSig]
        int GetWindowDesktopId([In] IntPtr TopLevelWindow, [Out] out Guid CurrentDesktop);
        [PreserveSig]
        int MoveWindowToDesktop([In] IntPtr TopLevelWindow, [MarshalAs(UnmanagedType.LPStruct)][In] Guid CurrentDesktop);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    public class CVirtualDesktopManager
    {
    }

    public class VirtualDesktopManager
    {
        internal static IVirtualDesktopManagerInternal VirtualDesktopManagerInternal;

        internal static IVirtualDesktopManager _manager;

        public VirtualDesktopManager()
        {
            var shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell));
            VirtualDesktopManagerInternal = (IVirtualDesktopManagerInternal)shell.QueryService(Guids.CLSID_VirtualDesktopManagerInternal, typeof(IVirtualDesktopManagerInternal).GUID);

            _manager = (IVirtualDesktopManager)new CVirtualDesktopManager();
        }

        public int GetCount()
        {
            return VirtualDesktopManagerInternal.GetCount();
        }

        public bool IsWindowOnCurrentVirtualDesktop(IntPtr TopLevelWindow)
        {
            int hr = _manager.IsWindowOnCurrentVirtualDesktop(TopLevelWindow, out int result);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return result != 0;
        }

        public Guid GetWindowDesktopId(IntPtr TopLevelWindow)
        {
            int hr = _manager.GetWindowDesktopId(TopLevelWindow, out Guid result);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return result;
        }

        public void MoveWindowToDesktop(IntPtr TopLevelWindow, Guid CurrentDesktop)
        {
            int hr = _manager.MoveWindowToDesktop(TopLevelWindow, CurrentDesktop);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public void MoveWindowToCurrentDesktop(IntPtr TopLevelWindow)
        {
            Guid currentDesktop = VirtualDesktopManagerInternal.GetCurrentDesktop().GetId();
            MoveWindowToDesktop(TopLevelWindow, currentDesktop);
        }

        public int GetCurrentDesktopindex()
        {
            return GetDesktopIndex(VirtualDesktopManagerInternal.GetCurrentDesktop());
        }

        internal static int GetDesktopIndex(IVirtualDesktop desktop)
		{ // get index of desktop
			int index = -1;
			Guid IdSearch = desktop.GetId();
			IObjectArray desktops;
			VirtualDesktopManagerInternal.GetDesktops(out desktops);
			object objdesktop;
			for (int i = 0; i < VirtualDesktopManagerInternal.GetCount(); i++)
			{
				desktops.GetAt(i, typeof(IVirtualDesktop).GUID, out objdesktop);
				if (IdSearch.CompareTo(((IVirtualDesktop)objdesktop).GetId()) == 0)
				{ index = i;
					break;
				}
			}
			Marshal.ReleaseComObject(desktops);
			return index;
		}
    }
}
