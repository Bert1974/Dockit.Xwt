﻿using System;
using System.Diagnostics;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace BaseLib.Xwt.Controls.DockPanel.Internals
{
    using Xwt = global::Xwt;

    class FloatWindow : Xwt.Window, IDockFloatWindow
    {
        public static IDockFloatWindow Create(DockPanel dock, IDockContent[] docs, Point formpos, out IDockPane panefloat)
        {
            return Create(dock, docs, new Rectangle(formpos, new Size(200, 200)), out panefloat);
        }
        public static IDockFloatWindow Create(DockPanel dock, IDockContent[] docs, Rectangle formpos, out IDockPane panefloat)
        {
            var r = new FloatWindow(dock, docs, formpos);

            r.Show();

            r.SetParent();
            panefloat = r.DockPanel.Current as IDockPane;

            r.maindock.AddFloat(r);

            return r;
        }

        public DockPanel/*IDockFloatForm.*/ DockPanel { get; private set; }
        private readonly ResizeAndTitleBaranvas Canvas;
        private DockPanel maindock;
        private bool titlebarvisible;
        private bool isclosing = false;

        Window IDockFloatWindow.Window => this;
        DockPanel IDockFloatWindow.MainDockPanel => maindock;

        enum DragModes
        {
            None,
            Move,
            LeftTop,
            Top,
            RightTop,
            Left,
            Right,
            LeftBottom,
            Bottom,
            RightBottom
        }

        #region ResizeCanvas
        class ResizeAndTitleBaranvas : Xwt.Canvas
        {
            public int dragsize = 4;

            private readonly FloatWindow owner;
            private DragModes captured = DragModes.None;
            private Point orgpt;
            private Rectangle orgpos;

            public ResizeAndTitleBaranvas(FloatWindow owner)
            {
                this.owner = owner;
                this.Margin = 0;
                this.HorizontalPlacement = WidgetPlacement.Fill;
                this.VerticalPlacement = WidgetPlacement.Fill;
                this.ExpandHorizontal = this.ExpandVertical = true;
                this.AddChild(owner.DockPanel);
            }
            protected override void OnBoundsChanged()
            {
                MoveWindows();
            }
            internal void MoveWindows()
            {
                //base.OnBoundsChanged()
                var size = this.Size;
                this.SetChildBounds(owner.DockPanel, new Rectangle(
                            dragsize, dragsize + (this.owner.titlebarvisible ? TitleBar.TitleBarHeight+dragsize : 0),
                            size.Width - dragsize * 2, size.Height - dragsize * 2 - (this.owner.titlebarvisible ? TitleBar.TitleBarHeight+dragsize : 0)));
            }
            protected override void OnDraw(Context ctx, Rectangle dirtyRect)
            {
                base.OnDraw(ctx, dirtyRect);

                if (this.owner.titlebarvisible)
                {
                    var r = new Rectangle(dragsize, dragsize, this.Bounds.Width - dragsize * 2, TitleBar.TitleBarHeight);

                    ctx.SetColor(DockPanel.TitlebarColor);
                    ctx.Rectangle(r);
                    ctx.Fill();

                    var tl = new TextLayout(this) { Text = this.owner.Title };

                    ctx.SetColor(Colors.Black);
                    ctx.DrawTextLayout(tl, new Point(dragsize+4, dragsize + 2));
                }
                ctx.SetColor(Colors.Black);
                ctx.Rectangle(this.Bounds);
                ctx.Stroke();
            }
            protected override void OnButtonPressed(ButtonEventArgs args)
            {
                args.Handled = true;
                if (args.Button == PointerButton.Left)
                {
                    var hit = HitTest(args.Position);

               /*     if (hit == DragModes.Move) // hit titlebar
                    {
                        // todo check small move first
                        DockItDragDrop.StartDrag(this.owner, this.ConvertToScreenCoordinates(args.Position));
                        return;
                    }
                    else */ 

                    if (hit != DragModes.None)
                    {
                        this.captured = hit;
                        owner.DockPanel.xwt.SetCapture(this);
                        
                        this.orgpt = base.ConvertToScreenCoordinates(args.Position);
                        this.orgpos = new Rectangle(this.owner.Location, base.Size);

                        return;
                    }
                }
            //   base.OnButtonPressed(args);
            }
            protected override void OnButtonReleased(ButtonEventArgs args)
            {
                args.Handled = true;
                if (captured != DragModes.None)
                {
                    if (captured != DragModes.Move)
                    {
                        Rectangle r = GetDragPos(args.Position);
                        if (r.Width >= 0 && r.Height >= 0)
                        {
                            /*  if (this.captured == DragModes.Move)
                              {
                                  if (this.drophit.HasValue)
                                  {
                                      Debug.Assert(this.captured==DragModes.Move);

                                      ClrCapture();

                                      this.droppane.DockPanel.DockFloatform(this.owner, this.droppane, this.drophit.Value);

                                      return;
                                  }
                              }*/
                            SetNewPos(r);
                        }
                    }
                    ClrCapture();
                    return;
                }
         //       base.OnButtonReleased(args);
            }

            private void ClrCapture()
            {
             //   DockPanel.ClrHightlight();

                owner.DockPanel.xwt.ReleaseCapture(this);
                this.captured = DragModes.None;
            }

            protected override void OnMouseMoved(MouseMovedEventArgs args)
            {
                args.Handled = true;
                var scrpt = ConvertToScreenCoordinates(args.Position);
                if (captured == DragModes.Move)
                {
                    if (!DockPanel.DragRectangle.Contains(scrpt.X - this.orgpt.X, scrpt.Y - this.orgpt.Y))
                    {
                        ClrCapture();
                        
                        DockItDragDrop.StartDrag(this.owner, scrpt);
                    }
                    base.Cursor = GetCursor(captured, scrpt);
                    return;
                }
                else if (captured != DragModes.None)
                {
                    Rectangle r = GetDragPos(args.Position);
                    if (r.Width >= 0 && r.Height >= 0)
                    {
                        SetNewPos(r);
                    }
                    base.Cursor = GetCursor(captured, scrpt);
                    return;
                }
                else
                {
                    base.OnMouseMoved(args);
                }
                base.Cursor = GetCursor(HitTest(args.Position), args.Position);
            }

            private void SetNewPos(Rectangle pos)
            {
                (owner.DockPanel.xwt as XwtImpl).SetPos(this.owner, pos);
           //     owner.DockPanel.xwt.SetCapture(this);
            }

            private CursorType GetCursor(DragModes mode, Point pt)
            {
                CursorType c = CursorType.Arrow;
                switch (mode)
                {
                    case DragModes.LeftTop: c = CursorType.ResizeNW; break;
                    case DragModes.Top: c = CursorType.ResizeUp; break;
                    case DragModes.RightTop: c = CursorType.ResizeNE; break;
                    case DragModes.Left: c = CursorType.ResizeLeft; break;
                    case DragModes.Right: c = CursorType.ResizeRight; break;
                    case DragModes.LeftBottom: c = CursorType.ResizeSW; break;
                    case DragModes.Bottom: c = CursorType.ResizeDown; break;
                    case DragModes.RightBottom: c = CursorType.ResizeSE; break;

                    case DragModes.Move: c = CursorType.Move;    break;
                }
                return c;
            }
            private DragModes HitTest(Point pt)
            {
                if (pt.Y >= 0 && pt.X >= 0)
                {
                    if (this.owner.titlebarvisible)
                    {
                        if (pt.Y >= dragsize && pt.Y < TitleBar.TitleBarHeight && pt.X >= dragsize && pt.X < this.Bounds.Width - dragsize)
                        {
                            return DragModes.Move;
                        }
                    }
                    if (pt.Y < dragsize)
                    {
                        if (pt.X < dragsize) // left top
                        {
                            return DragModes.LeftTop;
                        }
                        else if (pt.X < this.Bounds.Width - dragsize) // top
                        {
                            return DragModes.Top;
                        }
                        else if (pt.X < this.Bounds.Width) // right top
                        {
                            return DragModes.RightTop;
                        }
                    }
                    else if (pt.Y < base.Bounds.Height - dragsize)
                    {
                        if (pt.X < dragsize) // center left
                        {
                            return DragModes.Left;
                        }
                        else if (pt.X >= base.Bounds.Width - dragsize) // center right
                        {
                            return DragModes.Right;
                        }
                    }
                    else if (pt.Y < base.Bounds.Height)
                    {
                        if (pt.X < dragsize) // left bottom
                        {
                            return DragModes.LeftBottom;
                        }
                        else if (pt.X < this.Bounds.Width - dragsize) // bottom
                        {
                            return DragModes.Bottom;
                        }
                        else if (pt.X < this.Bounds.Width) // right bottom
                        {
                            return DragModes.RightBottom;
                        }
                    }
                }
                return DragModes.None;
            }
            private Rectangle GetDragPos(Point pt)
            {
                pt = base.ConvertToScreenCoordinates(pt);

                const int miw = 64, mih = 64;

                switch (captured)
                {
                    case DragModes.Move:
                        {
                            double nx = pt.X + (this.orgpos.X - this.orgpt.X), ny = pt.Y + (this.orgpos.Y - this.orgpt.Y);
                            return new Rectangle(new Point(nx, ny), this.orgpos.Size);
                        }
                    case DragModes.LeftTop:
                        {
                            double nx = pt.X + (this.orgpos.X - this.orgpt.X), ny = pt.Y + (this.orgpos.Y - this.orgpt.Y);
                            nx = Math.Min(this.orgpos.Right - miw, nx); ny = Math.Min(ny, this.orgpos.Bottom - mih);
                            return new Rectangle(nx, ny, this.orgpos.Right - nx, this.orgpos.Bottom - ny);
                        }
                    case DragModes.Top:
                        {
                            double ny = pt.Y + (this.orgpos.Y - this.orgpt.Y);
                            ny = Math.Min(ny, this.orgpos.Bottom - mih);
                            return new Rectangle(this.orgpos.Left, ny, this.orgpos.Width, this.orgpos.Bottom - ny);
                        }
                    case DragModes.RightTop:
                        {
                            double nx = pt.X + (this.orgpos.Right - this.orgpt.X), ny = pt.Y + (this.orgpos.Y - this.orgpt.Y);
                            nx = Math.Max(this.orgpos.Left + miw, nx); ny = Math.Min(ny, this.orgpos.Bottom - mih);
                            return new Rectangle(this.orgpos.X, ny, nx - this.orgpos.X, this.orgpos.Bottom - ny);
                        }
                    case DragModes.Left:
                        {
                            double nx = pt.X + (this.orgpos.X - this.orgpt.X);
                            nx = Math.Min(this.orgpos.Right - miw, nx);
                            return new Rectangle(nx, this.orgpos.Y, this.orgpos.Right - nx, this.orgpos.Height);
                        }
                    case DragModes.Right:
                        {
                            double nx = pt.X + (this.orgpos.Right - this.orgpt.X);
                            nx = Math.Max(this.orgpos.Left + miw, nx);
                            return new Rectangle(this.orgpos.X, this.orgpos.Y, nx - this.orgpos.X, this.orgpos.Height);
                        }
                    case DragModes.LeftBottom:
                        {
                            double nx = pt.X + (this.orgpos.X - this.orgpt.X), ny = pt.Y + (this.orgpos.Bottom - this.orgpt.Y);
                            nx = Math.Min(this.orgpos.Right - miw, nx); ny = Math.Max(ny, this.orgpos.Top + mih);
                            return new Rectangle(nx, this.orgpos.Y, this.orgpos.Right - nx, ny - this.orgpos.Y);
                        }
                    case DragModes.Bottom:
                        {
                            double ny = pt.Y + (this.orgpos.Bottom - this.orgpt.Y);
                            ny = Math.Max(ny, this.orgpos.Top + miw);
                            return new Rectangle(this.orgpos.Left, this.orgpos.Y, this.orgpos.Width, ny - this.orgpos.Y);
                        }
                    case DragModes.RightBottom:
                        {
                            double nx = pt.X + (this.orgpos.Right - this.orgpt.X), ny = pt.Y + (this.orgpos.Bottom - this.orgpt.Y);
                            nx = Math.Max(this.orgpos.Left + miw, nx); ny = Math.Max(ny, this.orgpos.Top + mih);
                            return new Rectangle(this.orgpos.X, this.orgpos.Y, nx - this.orgpos.X, ny - this.orgpos.Y);
                        }
                }
                return new Rectangle(0, 0, -1, -1);
            }
        }
        #endregion

        private FloatWindow(DockPanel dock, IDockContent[] docs, Rectangle formpos)
        {
            while (dock.FloatForm != null) { dock = (dock.FloatForm as FloatWindow).maindock; }

            this.BackgroundColor = Colors.White;
            this.maindock = dock;
            this.Location = formpos.Location;
            this.Width = formpos.Size.Width;
            this.Height = formpos.Size.Height;
            //      this.Resizable = false;
            this.Padding = 0;
            this.Title = "Properties";

            this.DockPanel = new DockPanel(this, this.maindock.xwt);
            this.Content = this.Canvas = new ResizeAndTitleBaranvas(this);

            if (!DockPanel.CustomTitleBar)
            {
                this.Canvas.dragsize = 0;
                this.Resizable = true;
                this.titlebarvisible = false;
                this.Decorated = true;
            }
            else
            {
                this.Canvas.dragsize = 4;
                this.Resizable = false;
                this.titlebarvisible = true;
                this.Decorated = false;
            }
            this.DockPanel.Dock(docs, DockPosition.Center);

            this.DockPanel.DocumentsChanged += DockPanel_DocumentsChanged;
          //  this.SetTitleBarVisble();

            /*       if (Toolkit.CurrentEngine.Type == ToolkitType.Wpf)
                    {
                        var wpfwin = (this.GetBackend() as IWindowFrameBackend).Window;
                        wpfwin.GetType().SetPropertyValue(wpfwin, "AllowsTransparency", true);
                    }*/
        }
        private void SetParent()
        {
            if (this.maindock.ParentWindow.Visible)
            {
                this.DockPanel.xwt.SetParent(this, this.maindock.ParentWindow);
            }
            else
            {
                this.maindock.ParentWindow.Shown += ParentWindow_Shown;
            }
        }

        private void ParentWindow_Shown(object sender, EventArgs e)
        {
            this.maindock.ParentWindow.Shown -= ParentWindow_Shown;
            this.DockPanel.xwt.SetParent(this, this.maindock.ParentWindow);
        }

        private void DockPanel_DocumentsChanged(object sender, EventArgs e)
        {
       //     this.SetTitleBarVisble();
        }
        /*     private void SetTitleBarVisble()
             {
              /*   if (this.DockPanel.Current is IDockPane) // not splitted?
                 {
                     if (this.titlebarvisible)
                     {
                         this.titlebarvisible = false;
                         this.Canvas.MoveWindows();
                         this.Canvas.QueueDraw();
                     }
                 }
                 else
                 {
                     if (!this.titlebarvisible)
                     {
                         this.titlebarvisible = true;
                 //     this.Canvas.MoveWindows();
              //      this.Canvas.QueueDraw();
                     }
                 }
             }*/
        protected override bool OnCloseRequested()
        {
            if (!this.isclosing)
            {
                this.DockPanel.Reset();
                return true;
            }
            return true;// base.OnCloseRequested();
        }
        protected override void OnClosed()
        {
            this.maindock.RemoveFloat(this);
            base.OnClosed();
        }
        void IDockFloatWindow.Close()
        {
            this.DockPanel.Reset();
            this.isclosing = true;
            base.Close();
            this.Dispose();
        }
        IDockPane IDockFloatWindow.DockToolbar(IDockContent[] controls, DockPosition pos, IDockPane destination)
        {
            return DockPanel.Dock(controls, pos, destination);
        }
        void IDockFloatWindow.Invalidate()
        {
        }
    }
}
