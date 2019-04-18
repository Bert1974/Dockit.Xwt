﻿using BaseLib.Xwt.Interop;
using System;
using System.Reflection;
using System.Security.Permissions;
using Xwt;
using Xwt.Backends;

namespace BaseLib.Xwt
{
    partial class XwtImpl
    {
        class WPF : RealXwt
        {
            [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            public override void DoEvents()
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
            public override void ReleaseCapture(Widget widget)
            {
                if (widget != null)
                {
                    var backend = widget.GetBackend();
                    var w = backend.GetType().GetPropertyValue(backend, "Widget");
                    w.GetType().Invoke(w, "ReleaseMouseCapture");
                }
            }
            public override void SetCapture(XwtImpl xwt, Widget widget)
            {
                if (widget != null)
                {
                    var backend = widget.GetBackend();
                    var w = backend.GetType().GetPropertyValue(backend, "Widget");
                    w.GetType().Invoke(w, "CaptureMouse");
                }
            }
            public override void SetParent(WindowFrame r, WindowFrame parentWindow)
            {
                var w = r.GetBackend().Window;
                w.GetType().SetPropertyValue(w, "Owner", (parentWindow.GetBackend() as IWindowFrameBackend).Window);
            }

            private IntPtr GetHwnd(WindowFrame r)
            {
                var wh = Activator.CreateInstance(Win32.swi_wininterophelper, new object[] { r.GetBackend().Window });
                return (IntPtr)wh.GetType().GetPropertyValue(wh, "Handle");
            }

            public override void GetMouseInfo(WindowFrame window, out int mx, out int my, out uint buttons)
            {
                throw new NotImplementedException();
            }
        }
    }
}