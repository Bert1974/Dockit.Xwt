﻿using BaseLib.XwtPlatForm;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Xwt;
using Xwt.Backends;

namespace BaseLib.DockIt_Xwt
{
    partial class XwtImpl
    {
        class WPF : IXwtImpl
        {
            [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            public void DoEvents()
            {
                var t1 = PlatForm.GetType("System.Windows.Threading.DispatcherFrame");
                var frame = Activator.CreateInstance(t1);

                var t = PlatForm.GetType("System.Windows.Threading.Dispatcher");
                var current = t.GetPropertyValueStatic("CurrentDispatcher");

                var t3 = PlatForm.GetType("System.Windows.Threading.DispatcherPriority");
                var t4 = PlatForm.GetType("System.Windows.Threading.DispatcherOperationCallback");

                var callback = Delegate.CreateDelegate(t4, typeof(WPF), "ExitFrame");

                var mi = current.GetType().GetMethod("BeginInvoke", new Type[] { typeof(Delegate), t3, typeof(object[]) });
                mi.Invoke(current, new object[] { (Delegate)callback, Enum.Parse(t3, "Background"), new object[] { frame } });

                mi = t.GetMethod("PushFrame", BindingFlags.Static | BindingFlags.Public, null, new Type[] { frame.GetType() }, new ParameterModifier[] { new ParameterModifier(1) });
                mi.Invoke(null, new object[] { frame });

                /*     DispatcherFrame frame = new DispatcherFrame();
                     Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                         new DispatcherOperationCallback(ExitFrame), frame);
                     Dispatcher.PushFrame(frame);*/
            }

            public static object ExitFrame(object f)
            {
                f.GetType().SetPropertyValue(f, "Continue", false);
                //  ((DispatcherFrame)f).Continue = false;
                return null;
            }
            void IXwt.ReleaseCapture(Widget widget)
            {
                if (widget != null)
                {
                    var backend = Toolkit.CurrentEngine.GetSafeBackend(widget);
                    var w = backend.GetType().GetPropertyValue(backend, "Widget");
                    w.GetType().Invoke(w, "ReleaseMouseCapture");
                }
            }
            void IXwt.SetCapture(Widget widget)
            {
                if (widget != null)
                {
                    var backend = Toolkit.CurrentEngine.GetSafeBackend(widget);
                    var w = backend.GetType().GetPropertyValue(backend, "Widget");
                    w.GetType().Invoke(w, "CaptureMouse");
                }
            }
            void IXwt.SetParent(WindowFrame r, WindowFrame parentWindow)
            {
                //     IntPtr hwnd = GetHwnd(r);
                //      IntPtr hwndmain = GetHwnd(parentWindow);

                var w = (r.GetBackend() as IWindowFrameBackend).Window;
           //     var te = PlatForm.GetType("System.Windows.WindowStyle");

                w.GetType().SetPropertyValue(w, "Owner", (parentWindow.GetBackend() as IWindowFrameBackend).Window);

                //      SetParent(hwnd, hwndmain);
            }

            private IntPtr GetHwnd(WindowFrame r)
            {
                Type t = PlatForm.GetType("System.Windows.Interop.WindowInteropHelper");
                var wh = Activator.CreateInstance(t, new object[] { (r.GetBackend() as IWindowFrameBackend).Window });
                return (IntPtr)wh.GetType().GetPropertyValue(wh, "Handle");
            }
        }
    }
}
