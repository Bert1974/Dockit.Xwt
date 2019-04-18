﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xwt;

namespace BaseLib.Xwt.Interop
{
    static class Win32
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        public struct POINT
        {
            public int X, Y;
            public static implicit operator Point(POINT pt)
            {
                return new Point(pt.X, pt.Y);
            }
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);


        public static Type swc_panel = PlatForm.GetType("System.Windows.Controls.Panel");
        public static Type swi_wininterophelper = PlatForm.GetType("System.Windows.Interop.WindowInteropHelper");
        public static Type swi_hwndsource = PlatForm.GetType("System.Windows.Interop.HwndSource");

    }
}