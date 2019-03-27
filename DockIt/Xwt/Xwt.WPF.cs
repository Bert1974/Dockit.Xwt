﻿using System;
using System.Linq;
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
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

            [DllImport("user32.dll", SetLastError = true)]
            static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

            class DragWindow : XwtImpl.DragWindow
            {
                private bool doexit;

                public DragWindow(IXwt xwt, Canvas widget, Point position)
                    : base(xwt, widget, position)
                {
                    this.Content = new Canvas()
                    {
                        ExpandHorizontal = true,
                        ExpandVertical = true,
                        CanGetFocus = true
                    };
                    this.Content.MouseMoved += Content_MouseMoved;
                    this.Content.ButtonPressed += Content_ButtonPressed;
                    this.Content.ButtonReleased += Content_ButtonReleased;
                }
                protected override bool OnCloseRequested()
                {
                    if (!this.doexit)
                    {
                        close(false);
                        return false;
                    }
                    return base.OnCloseRequested();
                }
                private void Content_ButtonReleased(object sender, ButtonEventArgs e)
                {
                    close(e.Button == PointerButton.Left);
                }
                private void Content_ButtonPressed(object sender, ButtonEventArgs e)
                {
                    close(false);
                }
                private void close(bool apply)
                {
                    this.result = apply;
                    this.doexit = true;
                }
                private void Content_MouseMoved(object sender, MouseMovedEventArgs e)
                {
                    var pt = (sender as Widget).ConvertToScreenCoordinates(e.Position);
                    this.Location = pt.Offset(-5, -5);

                    var hits = BaseLib.DockIt_Xwt.PlatForm.Instance.Search(IntPtr.Zero, pt); // all hit window-handle son system

                    foreach (var w in hits)
                    {
                        if (BackendHost.Backend.NativeHandle == w.Item2)
                        {
                            continue;// hit through dragwindow
                        }
                        var hit = DockPanel.CheckHit(w.Item2, pt.X, pt.Y);

                        if (hit != null)
                        {
                            var b = hit.ConvertToScreenCoordinates(hit.Bounds.Location);

                            DockPanel.SetHighLight(hit, new Point(pt.X - b.X, pt.Y - b.Y), out this.droppane, out this.drophit);
                            return;
                        }
                        //       break; // don't know enumerated strange window with wpf
                    }
                    DockPanel.ClrHightlight();
                }
                public override void Show()
                {
                    this.doexit = false;

                    (this as Window).Show();
                    this.Content.SetFocus();

                    this.xwt.SetCapture(this.Content);

                    while (!this.doexit)
                    {
                        this.xwt.DoEvents();
                    }
                    this.xwt.ReleaseCapture(this.Content);

                    DockPanel.ClrHightlight();

                    base.Close();
                }
            }

            [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            public void DoEvents()
            {
                var t1 = XwtImpl.GetType("System.Windows.Threading.DispatcherFrame");
                var frame = Activator.CreateInstance(t1);

                var t = XwtImpl.GetType("System.Windows.Threading.Dispatcher");
                var current = t.GetPropertyValueStatic("CurrentDispatcher");

                var t3 = XwtImpl.GetType("System.Windows.Threading.DispatcherPriority");
                var t4 = XwtImpl.GetType("System.Windows.Threading.DispatcherOperationCallback");

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
                var backend = Toolkit.CurrentEngine.GetSafeBackend(widget);
                var w = backend.GetType().GetPropertyValue(backend, "Widget");
                w.GetType().Invoke(w, "ReleaseMouseCapture");
            }

            void IXwt.SetCapture(Widget widget)
            {
                var backend = Toolkit.CurrentEngine.GetSafeBackend(widget);
                var w = backend.GetType().GetPropertyValue(backend, "Widget");
                w.GetType().Invoke(w, "CaptureMouse");
            }

            public XwtImpl.DragWindow Create(Canvas widget, Point position)
            {
                return new DragWindow(this, widget, position);
            }
            bool IXwtImpl.SetPos(WindowFrame window, Rectangle pos)
            {
                IntPtr hwnd = GetHwnd(window);

                const short SWP_NOZORDER = 0X4;
                const short SWP_NOCOPYBITS = 0x0100;
                const short SWP_NOACTIVATE = 0x0010;
                SetWindowPos(hwnd, IntPtr.Zero, Convert.ToInt32(pos.X), Convert.ToInt32(pos.Y), Convert.ToInt32(pos.Width), Convert.ToInt32(pos.Height), SWP_NOZORDER | SWP_NOCOPYBITS | SWP_NOACTIVATE);

                return true;
            }

            void IXwtImpl.SetParent(WindowFrame r, WindowFrame parentWindow)
            {
                //     IntPtr hwnd = GetHwnd(r);
                //      IntPtr hwndmain = GetHwnd(parentWindow);

                var w = (r.GetBackend() as IWindowFrameBackend).Window;
           //     var te = XwtImpl.GetType("System.Windows.WindowStyle");

                w.GetType().SetPropertyValue(w, "Owner", (parentWindow.GetBackend() as IWindowFrameBackend).Window);

                //      SetParent(hwnd, hwndmain);
            }

            private IntPtr GetHwnd(WindowFrame r)
            {
                Type t = XwtImpl.GetType("System.Windows.Interop.WindowInteropHelper");
                var wh = Activator.CreateInstance(t, new object[] { (r.GetBackend() as IWindowFrameBackend).Window });
                return (IntPtr)wh.GetType().GetPropertyValue(wh, "Handle");
            }
        }
    }
}