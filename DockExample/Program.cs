﻿using BaseLib.DockIt_Xwt;
using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Drawing;

namespace DockExample
{
    class Program
    {
        static readonly List<mainwindow> openwindows = new List<mainwindow>();

        static class UIHelpers
        {
            public static MenuItem NewMenuItem(string text, EventHandler click)
            {
                var r = new MenuItem(text);
                r.Clicked += click;
                return r;
            }
            public static void NewWindow()
            {
                var mainWindow = new mainwindow()
                {
                };
                Program.AddWindow(mainWindow);
                mainWindow.Show();
            }
        }

        class mainwindow : Window
        {
            DockPanel dock;
            bool closing = false;

            public mainwindow()
            {
                this.Title = $"Xwt Demo Application {Xwt.Toolkit.CurrentEngine.Type}";
                this.Width = 150;this.Height = 150;
                this.Padding = 0;

                this.CloseRequested += (s, e) => { if (!closing) { e.AllowClose = this.close(); } };

                var menu = new Menu();
                var file = new MenuItem("_File");
                file.SubMenu = new Menu();
                file.SubMenu.Items.Add(UIHelpers.NewMenuItem("New window", new_mainwindow));
                file.SubMenu.Items.Add(new MenuItem("_Open"));
                file.SubMenu.Items.Add(new MenuItem("_New"));
                var mi = new MenuItem("_Close");
                mi.Clicked += (s, e) => { if (this.close()) { base.Close(); }; };
                file.SubMenu.Items.Add(mi);
                menu.Items.Add(file);

                var edit = new MenuItem("_Edit");
                edit.SubMenu = new Menu();
                edit.SubMenu.Items.Add(new MenuItem("_Copy"));
                edit.SubMenu.Items.Add(new MenuItem("Cu_t"));
                edit.SubMenu.Items.Add(new MenuItem("_Paste"));
                menu.Items.Add(edit);
                
                var dockmenu = new MenuItem("_Dock") { SubMenu = new Menu() };
                dockmenu.SubMenu.Items.Add(UIHelpers.NewMenuItem("save layout to disk", save_layout));
                dockmenu.SubMenu.Items.Add(UIHelpers.NewMenuItem("load layout from disk", load_layout));
                menu.Items.Add(dockmenu);

                this.MainMenu = menu;
                this.Content = dock = new DockPanel();

                dock.Dock(new testdockitem());
                dock.Dock(new testtoolitem(), DockPosition.Top);
                dock.Dock(new IDockContent[] { new testtoolitem(), new testtoolitem(), new testtoolitem(), new testtoolitem(), new testtoolitem() }, DockPosition.Bottom);
            }
            protected override void OnClosed()
            {
                base.OnClosed();

                if (Program.RemoveWindow(this))
                {
                    Application.Exit();
                }
            }
            bool close()
            {
                this.closing = true;         
                return true;
            }

            void new_mainwindow(object sender, EventArgs e)
            {
                UIHelpers.NewWindow();
            }
            void save_layout(object sender, EventArgs e)
            {
                //    dock.SaveXml();
            }
            void load_layout(object sender, EventArgs e)
            {
                //    dock.LoadXml();
            }
        }

        [STAThread()]
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                Application.Initialize(ToolkitType.Gtk);
            }
            else
            {
                Application.Initialize(ToolkitType.Wpf);
            }
            UIHelpers.NewWindow();
            Application.Run();
        }
        private static void AddWindow(mainwindow window)
        {
            openwindows.Add(window);
        }
        private static bool RemoveWindow(mainwindow window)
        {
            openwindows.Remove(window);
            return openwindows.Count == 0;
        }
    }
    class testdockitem : Canvas, IDockDocument
    {
        Widget IDockContent.Widget => this;
        string IDockContent.TabText => "testdoc";

        public testdockitem()
        {
            this.MinWidth = this.MinHeight = 100;
            this.BackgroundColor = Colors.Aquamarine;
        }
    }
    class testtoolitem : Canvas, IDockToolbar
    {
        Widget IDockContent.Widget => this;
        string IDockContent.TabText => "tool";

        public testtoolitem()
        {
            this.MinWidth = this.MinHeight = 100;
            this.BackgroundColor = Colors.Aquamarine;
        }
    }
}