﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xwt;
using Xwt.Drawing;

namespace BaseLib.DockIt_Xwt
{
    internal class DockPane : Canvas, IDockPane, IDockNotify
    {
        class DropTarget : Label
        {
            private DockPane dockPane;
            public readonly DockPosition pos;

            public DropTarget(DockPane dockPane, DockPosition pos)
            {
                this.dockPane = dockPane;
                this.pos = pos;
                this.WidthRequest = dockPane.wh;
                this.HeightRequest = dockPane.wh;
                this.Opacity = .8f;
                this.BackgroundColor = Colors.LightYellow;
            }

            internal void SetHighLight(bool highlighted)
            {
                if (highlighted)
                {
                    this.BackgroundColor = Colors.OrangeRed;
                }
                else
                {
                    this.BackgroundColor = Colors.LightYellow;
                }
            }
        }
        private List<IDockContent> _docs = new List<IDockContent>();
        private IDockContent _activedoc;

        public DockPanel DockPanel { get; private set; }
        public IDockContent Document
        {
            get => _activedoc;
            private set
            {
                if (!object.ReferenceEquals(this._activedoc, value))
                {
                    if (this._activedoc != null)
                    {
                        (this as IDockNotify).OnUnloading();
                        this.RemoveChild(this._activedoc.Widget);
                    }
                    if ((this._activedoc = value) != null)
                    {
                        this.AddChild(this._activedoc.Widget);
                        this.SetChildBounds(this._activedoc.Widget, DocumentRectangle);

                        if (this.ParentWindow?.Visible ?? false)
                        {
                            (this as IDockNotify).OnLoaded(this);
                        }
                    }
                }
            }
        }

        public IEnumerable<IDockContent> Documents => this._docs;

        TitleBar topbar, bottombar;

        public Point Location
        {
            get
            {
                var b = this.DockPanel.GetChildBounds(this);
                return b.Location;//todo this.bounds??
            }
        }

        public Size WidgetSize { get; private set; }

        public Size MinimumSize { get; private set; }

        public Size MaximumSize { get; private set; }

        public Canvas Widget => this;

        public Rectangle DocumentRectangle
        {
            get
            {
                var r = this.Bounds;

                if (this.topbar.Visible)
                {
                    r = new Rectangle(r.Left, r.Top + 22, r.Width, r.Height - 22);
                }
                if (this.bottombar.Visible)
                {
                    r = new Rectangle(r.Left, r.Top, r.Width, r.Height - 22);
                }
                if (r.Width < 0 || r.Height < 0) return Rectangle.Zero;
                return r;
            }
        }
        internal DockPane(DockPanel dockPanel, IDockContent[] testdoc)
        {
            this.MinWidth = this.MinHeight = 0;
            this.Margin = 0;
            this.DockPanel = dockPanel;
            this.DockPanel.ActiveContentChanged += DockPanel_ActiveContentChanged;
            //   base.BackgroundColor = Colors.Yellow;

            this.topbar = TitleBar.CreateHeader(this);
            this.AddChild(this.topbar);
            this.bottombar = TitleBar.CreateTabs(this);
            this.AddChild(this.bottombar);

          //  this._docs.AddRange(testdoc);

            GetSize(false);

//            this.Document = this._docs.FirstOrDefault();
  //          this.DockPanel.SetActive(this.Document ?? this.DockPanel.ActiveDocument ?? this.DockPanel.DefaultDocument);
            //  this.ActiveDocChanged(); 

            this.DockPanel.AddChild(this);

            Add(testdoc);

            MoveWindows();
        }

        private void MoveWindows()
        {
            if (this.topbar.Visible)
            {
                this.SetChildBounds(this.topbar, new Rectangle(0, 0, this.Bounds.Width, 22));
            }
            if (this.bottombar.Visible)
            {
                this.SetChildBounds(this.bottombar, new Rectangle(0, this.Bounds.Height - 22, this.Bounds.Width, 22));
            }
            //  base.Bounds = new Rectangle(pos, size);
            if (_activedoc != null)
            {
                this.SetChildBounds(this._activedoc.Widget, DocumentRectangle);
            }
        }
        public void OnHidden()
        {
            this.DockPanel.ActiveContentChanged -= DockPanel_ActiveContentChanged;
        }
        protected override void Dispose(bool disposing)
        {
            //   this.Document = null;

            base.Dispose(disposing);
        }
        private void DockPanel_ActiveContentChanged(object sender, EventArgs e)
        {
            this.topbar.SetDocuments(this._docs);
            this.bottombar.SetDocuments(this._docs);
         //   this.bottombar.Update();
        }

        public void Add(IDockContent[] docs)
        {
            this._docs.AddRange(docs);

            if (this.Document == null)
            {
                this.Document = docs.FirstOrDefault();
            }
            this.topbar.SetDocuments(this._docs);
            this.bottombar.SetDocuments(this._docs);

            this.DockPanel.SetActive(this.DockPanel.ActiveDocument ?? this.Document ?? this.DockPanel.DefaultDocument);
        }
        public bool Remove(IDockContent[] docs)
        {
            IDockDocument activedoc = this.DockPanel.ActiveDocument;
            if (docs.Any(_d => object.ReferenceEquals(_d, this.Document)))
            {
                this.Document = null;
            }
            if (docs.Any(_d => object.ReferenceEquals(_d, activedoc)))
            {
                activedoc = null;
            }
            foreach (var doc in docs)
            {
                this._docs.Remove(doc);
            }
            if (this.Document == null)
            {
                this.Document = this._docs.FirstOrDefault();
            }
            this.DockPanel.SetActive(activedoc ?? this.Document ?? this.DockPanel.DefaultDocument);
            //this.topbar.SetDocuments(this._docs);
            //this.bottombar.SetDocuments(this._docs);

            return !this.Documents.Any();
        }
        public void Layout(Point pt, Size size)
        {
            this.WidgetSize = size;
            this.DockPanel.SetChildBounds(this, new Rectangle(pt, size));

            MoveWindows();
        }
        protected override Size OnGetPreferredSize(SizeConstraint widthConstraint, SizeConstraint heightConstraint)
        {
            return new Size(widthConstraint.AvailableSize, heightConstraint.AvailableSize);
        }

        public void RemoveWidget()
        {
            this.DockPanel.RemoveChild(this);
        }

        void IDockNotify.OnLoaded(IDockPane pane)
        {
            (this._activedoc as IDockNotify)?.OnLoaded(this);
        }

        void IDockNotify.OnUnloading()
        {
            (this._activedoc as IDockNotify)?.OnUnloading();
        }

        public void GetSize(bool setsize)
        {
            double miw = 64, mih = 48;

            if (_docs.Any())
            {
                foreach (var doc in this._docs)
                {
                    miw = Math.Max(miw, doc.Widget.MinWidth);
                    mih = Math.Max(miw, doc.Widget.MinHeight);
                }
            }
            this.MinimumSize = new Size(miw, mih+44);
            //   (this as Canvas).MinWidth = miw; // fails with WPF
            //   (this as Canvas).MinHeight =mih;
        }

        public bool HitTest(Point position, out IDockSplitter splitter, out int ind)
        {
            splitter = null;
            ind = -1;


            if (position.X >= this.Location.X && position.X < this.Location.X + this.WidgetSize.Width &&
                position.Y >= this.Location.Y && position.Y < this.Location.Y + this.WidgetSize.Height)
            {
                return true;
            }
            return false;
        }

        internal void SetActive(IDockContent value)
        {
            this.Document = value;
            this.DockPanel.SetActive(value);
        }
      /*  public void ActiveDocChanged()
        {
            this.topbar.SetDocuments(this._docs);
            this.bottombar.Update();
        }*/

        int wh = 16;

        public void SetDrop(DockPosition? hit)
        {
            var r = new Rectangle(
                0,
                this.topbar.Visible ? this.topbar.Bounds.Height : 0,
                this.Bounds.Width,
                this.Bounds.Height - (this.topbar.Visible ? this.topbar.HeightRequest : 0) - (this.bottombar.Visible ? this.bottombar.Bounds.Height : 0));

            this.Document?.Widget.Hide();

            AddDrop((r.Width - wh) / 2, r.Top, DockPosition.Top);
            AddDrop((r.Width - wh) / 2, r.Top + r.Height - wh, DockPosition.Bottom);
            AddDrop(0, r.Top + (r.Height - wh) / 2, DockPosition.Left);
            AddDrop(r.Right - wh, r.Top + (r.Height - wh) / 2, DockPosition.Right);
            AddDrop((r.Width - wh) / 2, r.Top + (r.Height - wh) / 2, DockPosition.Center);
        }


        private void AddDrop(double x, double y, DockPosition pos)
        {
            var widget = new DropTarget(this, pos);
            this.AddChild(widget);
            this.SetChildBounds(widget, new Rectangle(x, y, wh, wh));
        }

        public void ClearDrop()
        {
            var toremove = this.Children.OfType<DropTarget>().ToArray();

            foreach (var dt in toremove)
            {
                this.RemoveChild(dt);
                dt.Dispose();
            }
            this.Document?.Widget.Show();
        }

        public DockPosition? HitTest(Point position)
        {
            DockPosition? r = null;
            foreach (var ctl in this.Children.OfType<DropTarget>().Reverse())
            {
                if (this.GetChildBounds(ctl).Contains(position))
                {
                    r = ctl.pos;
                }
            }
            return r;
        }

        public void Update(DockPosition? hit)
        {
            foreach (var ctl2 in this.Children.OfType<DropTarget>())
            {
                ctl2.SetHighLight(hit.HasValue&&ctl2.pos == hit.Value);
            }
        }
    }
}