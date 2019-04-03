﻿using BaseLib.XwtPlatForm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Xml;
using Xwt;
using Xwt.Drawing;

namespace BaseLib.DockIt_Xwt
{
    public class DockPanel : Canvas
    {
        public static bool DefaultFloat { get; set; } = true;
        public static Color TitlebarColor { get; set; } = Colors.LightBlue;

        public event EventHandler DocumentsChanged, ActiveDocumentChanged, ActiveContentChanged;

        private IDockLayout _content;
        internal int busy;
        private Size _size;
        private IDockSplitter dragsplit;
        private Point dragpt;
        private int dragind;
        private bool capture;
        public IXwt xwt { get; }

        private Window mainwindow;
        internal IDockFloatForm FloatForm = null;

        internal static readonly List<DockPanel> AllDockPanels = new List<DockPanel>();
        private static IDockPane droptarget = null;
        private bool firedocschanged;

        private readonly List<IDockFloatForm> floating = new List<IDockFloatForm>();

        private bool onloadedfired = false;

        internal static void SetHighLight(DockPanel dockPanel, Point pt, out IDockPane target, out DockPosition? hit)
        {
            target = null;
            hit = null;
            foreach (var pane in dockPanel.AllLayouts.OfType<IDockPane>())
            {
                var bounds = dockPanel.GetChildBounds(pane.Widget);

                if (bounds.Contains(pt))
                {
                    hit = pane.HitTest(new Point(pt.X - bounds.X, pt.Y - bounds.Y));
                    target = pane;
                    break;
                }
            }
            if (!object.ReferenceEquals(target, DockPanel.droptarget))
            {
                ClrHightlight();

                if ((DockPanel.droptarget = target) != null)
                {
                    DockPanel.droptarget?.SetDrop(hit);
                }
            }
            else
            {
                DockPanel.droptarget?.Update(hit);
            }

            // ClrHightlight();

        }
        internal static void ClrHightlight()
        {
            DockPanel.droptarget?.ClearDrop();
            DockPanel.droptarget = null;
        }

        public bool Contains(IDockLayout item)
        {
            return this.AllContent.Any(_l => object.ReferenceEquals(_l, item));
        }

        internal IDockContent DefaultDocument
        {
            get
            {
                return this.AllLayouts.OfType<IDockPane>().FirstOrDefault(_p => _p.Document != null)?.Document;
            }
        }
        public IDockDocument ActiveDocument
        {
            get; private set;
        }

        public IDockLayout Current
        {
            get => this._content;
            private set => this._content = value;
        }

        public IEnumerable<IDockContent> AllContent
        {
            get
            {
                return _AllLayouts(this.Current).OfType<IDockPane>().SelectMany(_p => _p.Documents);
            }
        }
        public IEnumerable<IDockLayout> AllLayouts
        {
            get
            {
                return _AllLayouts(this.Current);
            }
        }
        IEnumerable<IDockLayout> _AllLayouts(IDockLayout content)
        {
            if (content != null)
            {
                yield return content as IDockLayout;

                if (content is IDockSplitter)
                {
                    foreach (var c in (content as IDockSplitter).Layouts.SelectMany(_l => _AllLayouts(_l)))
                    {
                        yield return c;
                    }
                }
            }
        }

        public void OnLoaded()
        {
            foreach (var pane in this.AllLayouts.OfType<IDockPane>())
            {
                (pane as IDockNotify).OnLoaded(pane);
            }
        }
        public void OnUnloading()
        {
            foreach (var pane in this.AllLayouts.OfType<IDockPane>())
            {
                (pane as IDockNotify).OnUnloading();
            }
        }
        internal DockPanel(FloatWindow floatwindow, IXwt xwt)
            : this((Window)floatwindow, xwt)
        {
            this.FloatForm = floatwindow;
        }
        
        public void LoadXml(string filename, DeserializeDockContent deserializeDockContent = null)
        {
            using (var stream = File.OpenRead(filename))
            {
                LoadXml(stream, deserializeDockContent);
            }
        }
        public bool LoadXml(Stream stream, DeserializeDockContent deserializeDockContent = null)
        {
            var xmlReader = XmlReader.Create(stream,
                new XmlReaderSettings()
                {
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreWhitespace = true
                });

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(DockSave), DockState.SerializeTypes);
            var data = (DockSave)serializer.Deserialize(xmlReader);
            
            BeginLayout();

            this.Current.RemoveWidget();
            
            this.Current = null;

            this.floating.ToList().ForEach(_f => _f.Close());

            IDockLayout pane = null;
            try
            {
                if (data != null)
                {
                    pane = data.Restore(this, deserializeDockContent) as IDockLayout;
                }
            }
            catch { }
            this.Current = pane ?? new DockPane(this, new IDockContent[0]);
            this.Current.AddWidget();

            EndLayout();

            return pane != null;
        }
        public void SaveXml(string filename)
        {
            using (var stream = File.Create(filename))
            {
                SaveXml(stream);
            }
        }
        public void SaveXml(Stream stream)
        {
            var data = DockState.SaveState(this);
            
            var xmlWriter = XmlWriter.Create(stream,
                new XmlWriterSettings()
                {
                    Encoding = new UTF8Encoding(false, true),
                    ConformanceLevel = ConformanceLevel.Document,
                    Indent = true,
                    IndentChars = " ",
                    NewLineHandling = NewLineHandling.Entitize
                });

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(DockSave), DockState.SerializeTypes);
            serializer.Serialize(xmlWriter, data);
        }


        public DockPanel(Window mainwindow, IXwt xwt = null)
        {
            this.xwt = xwt ?? XwtImpl.Create();
            this.mainwindow = mainwindow; // ParentWindow
            this.busy++;
            this.Margin = 0;
            this.MinWidth = this.MinHeight = 0;
            base.BackgroundColor = Colors.Chocolate;
            base.ExpandHorizontal = base.ExpandVertical = true;
            base.SetDragDropTarget(new TransferDataType[] { TransferDataType.Text });

            this.Current = new DockPane(this, new IDockContent[0]);
            this.Current.AddWidget();

            this.busy--;

            DockPanel.AllDockPanels.Add(this);

            if (Toolkit.CurrentEngine.Type == ToolkitType.Gtk3)
            {
                if (!mainwindow.Visible)
                {
                    this.mainwindow.Shown += showfunc;
                }
            }
        }
        private void showfunc(object sender, EventArgs e)
        {
            this.mainwindow.Shown -= showfunc;

            var t = PlatForm.GetType("GLib.Idle");
            var t2 = PlatForm.GetType("GLib.IdleHandler");
            var mi = t.GetMethod("Add", new Type[] { t2 });
            mi.Invoke(null, new object[] { Delegate.CreateDelegate(t2, this, "dolayoutnow") });
        }
        private bool dolayoutnow()
        {
            this._size = this.Bounds.Size;
            _DoLayout();
            return false;
        }
        protected override void Dispose(bool disposing)
        {
            DockPanel.AllDockPanels.Remove(this);

            base.Dispose(disposing);
        }

        protected override void OnChildPreferredSizeChanged()
        {
            //   base.OnChildPreferredSizeChanged();
        }
        public new void QueueDraw()
        {
            foreach (var pane in this.AllLayouts.OfType<IDockPane>().Where(_p => _p.Document != null))
            {
                (pane.Document as Canvas)?.QueueDraw();
            }
            base.QueueDraw();
        }
        protected override void OnBoundsChanged()
        {
            base.OnBoundsChanged();
            this._size = this.Bounds.Size;
            this._DoLayout();
        }
        protected override Size OnGetPreferredSize(SizeConstraint widthConstraint, SizeConstraint heightConstraint)
        {
            Size r = new Size(widthConstraint.AvailableSize, heightConstraint.AvailableSize);//??base.OnGetPreferredSize(widthConstraint, heightConstraint);

            this._size = r;
            return r;
        }

        internal static DockPanel[] GetHits(int x, int y)
        {
            return DockPanel.AllDockPanels.Where(_dock =>
            {
                if (_dock.ParentWindow != null)
                {
                    var wp = _dock.ConvertToScreenCoordinates(_dock.Bounds.Location);

                    if (x >= wp.X && x < wp.X + _dock.Size.Width &&
                        y >= wp.Y && y < wp.Y + _dock.Size.Height)
                    {
                        return true;
                    }
                }
                return false;
            }).ToArray();
        }
        internal static DockPanel CheckHit(object handle, double x, double y)
        {
            return DockPanel.AllDockPanels.FirstOrDefault(_dp =>
            {
                var backend = typeof(Xwt.Window).GetPropertyValuePrivate(_dp.ParentWindow, "Backend") as Xwt.Backends.IWindowFrameBackend;

                if (object.ReferenceEquals(handle, backend.Window))// == backend.NativeHandle) // check if window to check
                {
                    var wp = _dp.ConvertToScreenCoordinates(_dp.Bounds.Location);

                    if (x >= wp.X && x < wp.X + _dp.Size.Width &&
                        y >= wp.Y && y < wp.Y + _dp.Size.Height)
                    {
                        return true; // hit within dockpane
                    }
                }
                return false;
            });
        }
        private void CheckOnloaded()
        {
           /* if (this.Current!=null&&!onloadedfired &&(this.ParentWindow?.Visible??false))
            {
                if (Xwt.Toolkit.CurrentEngine.Type == ToolkitType.Gtk3)
                {
                    this._size = this.Bounds.Size;
                    this.Current.GetSize(false);
                    this.Current.Layout(this.Bounds.Location, this.Bounds.Size);
                }
                this.OnLoaded();
                onloadedfired = true;
            }*/
        }
        public IDockPane Dock(IDockContent testdoc, DockPosition pos = DockPosition.Center, IDockPane destination = null)
        {
            return Dock(new IDockContent[] { testdoc }, pos, destination);
        }
        public IDockPane Dock(IDockContent[] testdoc, DockPosition pos = DockPosition.Center, IDockPane destination = null)
        {
            if (pos == DockPosition.Float)
            {
                throw new NotImplementedException();
            }

            IDockPane result;

            BeginLayout();

            if (this.Current == null)
            {
                this.Current = new DockPane(this, testdoc);
                this.Current.AddWidget();
            }
            if (destination == null)
            {
                destination = AllLayouts.OfType<IDockPane>().FirstOrDefault(_l => _l.Documents.OfType<IDockDocument>().Any()) ??
                              AllLayouts.OfType<IDockPane>().FirstOrDefault(_l => _l.Documents.OfType<IDockContent>().Count() == 0) ??
                              AllLayouts.OfType<IDockPane>().FirstOrDefault();
            }
            Debug.Assert(destination != null);
            Debug.Assert(this.AllLayouts.Contains(destination));

            if (pos == DockPosition.Center)
            {
                destination.Add(testdoc);
                result = destination;
            }
            else
            {
                var split = FindSplitter(destination, out int ind);// this dw.Parent as DockSplitter;

                result = (IDockPane)new DockPane(this, testdoc);
                _DoDock(result, destination, pos);
            }
            EndLayout(true);

            return result;
        }
        public IDockLayout Dock(IDockLayout todock, DockPosition pos = DockPosition.Center, IDockPane destination = null)
        {
            IDockLayout result;

            Debug.Assert(todock != null);

            if (pos == DockPosition.Float) //  create new float window?
            {
                throw new NotImplementedException();
            }
         /*   else if (todock.DockPanel.Current is IDockPane) // dock documents?
            {
                // remove docs
                var panesrc = todock.DockPanel.Current as IDockPane;
                var docs = panesrc?.Documents.ToArray() ?? new IDockContent[0];
                panesrc?.Remove(docs);

                result = this.Dock(docs, pos, destination);
            }*/
            else // dock idocksplitter
            {
          //..      var splitsrc = todock.DockPanel.Current as IDockSplitter;

                BeginLayout();

                if (this.Current == null)
                {
                    this.Current = new DockPane(this, new IDockContent[0]);
                    this.Current.AddWidget();
                }
                if (destination == null)
                {
                    destination = this.AllLayouts.OfType<IDockPane>().FirstOrDefault(_l => _l.Documents.OfType<IDockDocument>().Any()) ??
                                  this.AllLayouts.OfType<IDockPane>().FirstOrDefault(_l => _l.Documents.OfType<IDockContent>().Count() == 0) ??
                                  this.AllLayouts.OfType<IDockPane>().FirstOrDefault();
                }
                Debug.Assert(destination != null);
                Debug.Assert(AllLayouts.Contains(destination));

                _DoDock(todock, destination, pos);
                result= todock;

              /*  if (pos == DockPosition.Center)
                {
                    if (destination.Documents.Count() == 0)
                    {
                        var split = FindSplitter(destination, out int ind);// this dw.Parent as DockSplitter;

                        if (split == null) // singlepane
                        {
                            Debug.Assert(object.ReferenceEquals(destination, this.Current));

                            ClearContent();

                            splitsrc.AddWidget();
                            this.Current = splitsrc;

                            result = splitsrc;
                        }
                        else
                        {
                            split.Remove(destination, true);

                            if (splitsrc.Orientation == split.Orientation) // merge splitters?
                            {

                            }
                            else // insert new splitter
                            {
                                split.Insert(ind, splitsrc);
                            }
                        }
                    }
                    else // can't dock splitter in center on pane
                    {
                        result = null;
                        SystemSounds.Asterisk.Play();
                    }
                }
                else
                {
             //       var split = FindSplitter(destination, out int ind);// this dw.Parent as DockSplitter;

             //      result = (IDockPane)new DockPane(this, testdoc);
             //       _DoDock(result, destination, pos);

                    splitsrc.AddWidget();
                    
                    _DoDock(splitsrc, destination, pos);
                }*/
         //   done:
                EndLayout(true);
            }
            return result;
        }

        private void _DoDock(IDockLayout todock, IDockPane destination, DockPosition pos)
        {
            Debug.Assert(todock != null);
            Debug.Assert(destination != null);
            Debug.Assert(pos !=DockPosition.Float);

            if (object.ReferenceEquals(destination, this.Current)) // single pane?
            {
                var oldpane = destination;
                this.Current = null;

                if (pos == DockPosition.Center)
                {
                    if (todock is IDockSplitter)
                    {
                        var splitsrc = todock as IDockSplitter;
                        //      var todock = r as IDockSplitter;

                        if (destination.Documents.Count() == 0) // empty single pane? -> replace
                        {
                            ClearContent();

                            splitsrc.NewDockPanel(this);
                            splitsrc.AddWidget();
                            this.Current = splitsrc;
                            return;
                        }
                        else // can't dock splitter on pane with documents
                        {
                            SystemSounds.Asterisk.Play();
                            return;
                        }
                    }
                    else // should be handled by caller (Dock)
                    {
                        var panesrc = todock as IDockPane;

                        Debug.Assert(false);
                    }
                }
                else // pos != center&float, single pane -> new splitter or use todock if splitter
                {
                    if (todock is IDockSplitter)
                    {
                        var splitsrc = todock as IDockSplitter;

                        destination.RemoveWidget();

                        switch (pos)
                        {
                            case DockPosition.Top:
                                splitsrc.Insert(1, destination);
                                this.Current = splitsrc;
                                break;
                            case DockPosition.Bottom:
                                this.Current = new DockSplitter(this, oldpane, todock, Orientation.Vertical);
                                break;
                            case DockPosition.Left:
                                this.Current = new DockSplitter(this, todock, oldpane, Orientation.Horizontal);
                                break;
                            case DockPosition.Right:
                                this.Current = new DockSplitter(this, oldpane, todock, Orientation.Horizontal);
                                break;
                        }
                        todock.AddWidget();
                        this.Current = todock;
                        return;
                    }
                    else
                    {
                        todock.AddWidget();
                        switch (pos)
                        {
                            case DockPosition.Top:
                                this.Current = new DockSplitter(this, todock, oldpane, Orientation.Vertical);
                                break;
                            case DockPosition.Bottom:
                                this.Current = new DockSplitter(this, oldpane, todock, Orientation.Vertical);
                                break;
                            case DockPosition.Left:
                                this.Current = new DockSplitter(this, todock, oldpane, Orientation.Horizontal);
                                break;
                            case DockPosition.Right:
                                this.Current = new DockSplitter(this, oldpane, todock, Orientation.Horizontal);
                                break;
                        }
                        return;
                    }
                }
            }
            else // Current is IockSpliter
            {
                if (todock is IDockSplitter)
                {
                    var splitsrc = todock as IDockSplitter;
                    var split = FindSplitter(destination, out int ind);// this dw.Parent as DockSplitter;

                    splitsrc.NewDockPanel(this);
                    splitsrc.AddWidget();

                    Merge(split, ind, splitsrc, pos);
                    
                    return;
                }
                else
                {
                    var dw = destination as IDockLayout;//.Control.Parent as DockWindow;
                    var split = FindSplitter(destination, out int ind);// this dw.Parent as DockSplitter;

                    todock.AddWidget();

                    switch (pos)
                    {
                        case DockPosition.Left:
                        case DockPosition.Right:
                            {
                                if (split.Orientation == Orientation.Horizontal)
                                {
                                    switch (pos)
                                    {
                                        case DockPosition.Left:
                                            split.Insert(ind, todock as IDockLayout);
                                            break;
                                        case DockPosition.Right:
                                            split.Insert(ind + 1, todock as IDockLayout);
                                            break;
                                    }
                                }
                                else
                                {
                                    split.Remove(destination, false);
                                    switch (pos)
                                    {
                                        case DockPosition.Left:
                                            split.Insert(ind, new DockSplitter(this, todock, dw, Orientation.Horizontal));
                                            break;
                                        case DockPosition.Right:
                                            split.Insert(ind, new DockSplitter(this, dw, todock, Orientation.Horizontal));
                                            break;
                                    }
                                }
                            }
                            break;
                        case DockPosition.Top:
                        case DockPosition.Bottom:
                            {
                                if (split.Orientation == Orientation.Vertical)
                                {
                                    switch (pos)
                                    {
                                        case DockPosition.Top:
                                            split.Insert(ind, todock as IDockLayout);
                                            break;
                                        case DockPosition.Bottom:
                                            split.Insert(ind + 1, todock as IDockLayout);
                                            break;
                                    }
                                }
                                else
                                {
                                    split.Remove(destination, false);
                                    switch (pos)
                                    {
                                        case DockPosition.Top:
                                            split.Insert(ind, new DockSplitter(this, todock as IDockLayout, dw, Orientation.Vertical));
                                            break;
                                        case DockPosition.Bottom:
                                            split.Insert(ind, new DockSplitter(this, dw, todock as IDockLayout, Orientation.Vertical));
                                            break;
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void Merge(IDockSplitter split, int ind, IDockSplitter splitsrc, DockPosition pos)
        {
            var curcontent = split.Get(ind);

            if (curcontent is IDockPane)
            {
                if (split.Orientation == Orientation.Vertical)
                {
                    switch (pos)
                    {
                        case DockPosition.Left:
                            {
                                if (splitsrc.Orientation == Orientation.Horizontal)
                                {
                                    // remove curcontent and recplace for splitsrc with curcontent
                                    split.Remove(curcontent, false);
                                    splitsrc.Insert(splitsrc.Layouts.Count(), curcontent);
                                    split.Insert(ind, splitsrc);
                                }
                                else
                                {
                                    split.Remove(curcontent, false);
                                    var newsplit = new DockSplitter(this, splitsrc, curcontent, Orientation.Horizontal);
                                    split.Insert(ind, newsplit);
                                }
                                return;
                            }
                        case DockPosition.Right:
                            {
                                if (splitsrc.Orientation == Orientation.Horizontal)
                                {
                                    split.Remove(curcontent, false);
                                    splitsrc.Insert(0, curcontent);
                                    split.Insert(ind, splitsrc);
                                }
                                else
                                {
                                    split.Remove(curcontent, false);
                                    var newsplit = new DockSplitter(this, curcontent, splitsrc, Orientation.Horizontal);
                                    split.Insert(ind, newsplit);
                                }
                                return;
                            }
                        case DockPosition.Top:
                        case DockPosition.Bottom:
                            break;
                        default: throw new NotImplementedException();
                    }
                }
                else // split.Orientation == Orientation.Horizontal
                {
                    switch (pos)
                    {
                        case DockPosition.Left:
                        case DockPosition.Right:
                            break;
                        case DockPosition.Top:
                            {
                                if (splitsrc.Orientation == Orientation.Vertical)
                                {
                                    // remove curcontent and recplace for splitsrc with curcontent
                                    split.Remove(curcontent, false);
                                    splitsrc.Insert(splitsrc.Layouts.Count(), curcontent);
                                    split.Insert(ind, splitsrc);
                                }
                                else
                                {
                                    split.Remove(curcontent, false);
                                    var newsplit = new DockSplitter(this, splitsrc, curcontent, Orientation.Horizontal);
                                    split.Insert(ind, newsplit);
                                }
                            }
                            break;
                        case DockPosition.Bottom:
                            {
                                if (splitsrc.Orientation == Orientation.Vertical)
                                {
                                    split.Remove(curcontent, false);
                                    splitsrc.Insert(0, curcontent);
                                    split.Insert(ind, splitsrc);
                                }
                                else
                                {
                                    split.Remove(curcontent, false);
                                    var newsplit = new DockSplitter(this, curcontent, splitsrc, Orientation.Horizontal);
                                    split.Insert(ind, newsplit);
                                }
                            }
                            break;
                        default: throw new NotImplementedException();
                    }
                }
            }
            if (splitsrc.Orientation == split.Orientation) // merge splitters?
            {
                //splitsrc.RemoveWidget();
                var panes = splitsrc.Layouts.ToArray();

                for (int nit = 0; nit < panes.Length; nit++)
                {
                    split.Insert(ind + nit, panes[nit]);
                }
                return;
            }
            else // insert new splitter
            {
                split.Insert(ind, splitsrc);
                return;
            }
        }
        internal void RemoveFloat(IDockFloatForm window)
        {
            this.floating.Remove(window);
        }

        internal void AddFloat(IDockFloatForm window)
        {
            this.floating.Add(window);
        }

        internal void BeginLayout()
        {
            if (this.busy++ == 0)
            {
                this.firedocschanged = false;
            }
        }

        internal void EndLayout(bool firedocschanged=false)
        {
            this.firedocschanged |= firedocschanged;
            if (--this.busy == 0)
            {
                this._DoLayout();

                if (this.firedocschanged)
                {
                    OnDocumentChanged();
                }
            }
        }

        private void OnDocumentChanged()
        {
            this.DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void SetActive(IDockContent value)
        {
          //  if (!object.ReferenceEquals(value, this.ActiveDocument))
            {
                if (value is IDockDocument)
                {
                    this.ActiveDocument = value as IDockDocument;

                    this.ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
                }
                this.ActiveContentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private IDockSplitter FindSplitter(IDockLayout searchfor, out int ind)
        {
            ind = -1;
            return FindSplitter(this.Current, searchfor, ref ind);
        }

        private IDockSplitter FindSplitter(IDockLayout content, IDockLayout searchfor, ref int ind)
        {
            if (content is IDockSplitter)
            {
                int cnt = 0;
                foreach (var l in (content as IDockSplitter).Layouts)
                {
                    if (object.ReferenceEquals(searchfor, l))
                    {
                        ind = cnt;
                        return content as IDockSplitter;
                    }
                    var r = FindSplitter(l, searchfor, ref ind);
                    if (r != null)
                    {
                        return r;
                    }
                    cnt++;
                }
            }
            return null;
        }

        private void _DoLayout()
        {
            if (this.busy == 0)
            {
                CheckOnloaded();
                if (!Size.Zero.Equals(_size) && this.Current != null)
                {
                    this.Current.GetSize(false);
                    this.Current.Layout(this.Bounds.Location, this.Bounds.Size);
                }
            }
        }

        protected override void OnButtonPressed(ButtonEventArgs args)
        {
            if (args.Button == PointerButton.Left)
            {
                if (this.Current.HitTest(args.Position, out IDockSplitter splitter, out int ind) && splitter != null)
                {
                    this.dragsplit = splitter;
                    this.dragpt = args.Position;
                    this.dragind = ind;

                    this.capture = true;

                    this.xwt.SetCapture(this);
                    return;
                }
            }
            base.OnButtonPressed(args);
        }
        private void DragSplit(IDockSplitter dragsplit, int ind, double d)
        {
            if (d == 0) { return; }
            Size s;
            Point pt;
            int e = 4;
            var panes = dragsplit.Layouts.ToArray();

            busy++;

            switch (dragsplit.Orientation)
            {
                case Orientation.Horizontal:
                    {

                        //      if ((Control.ModifierKeys & Keys.Shift) != 0)
                        {
                            s = panes[ind + 0].WidgetSize;
                            pt = panes[ind + 0].Location;
                            panes[ind + 0].Layout(pt, new Size(s.Width + d, s.Height));

                            pt.Offset(s.Width - d + e, 0);
                            s = panes[ind + 1].WidgetSize;
                            panes[ind + 1].Layout(pt, new Size(s.Width - d, s.Height));
                        }
                        /*     else
                             {
                                 var mi = panes.Take(ind + 1).Select(_p => (_p as Control).MinimumSize.Width).ToArray();
                                 var ci = panes.Take(ind + 1).Select(_p => (_p as Control).Width).ToArray();

                                 int e = 4;

                                 int ct = ci.Sum() - mi.Sum() + (ind + 1) * e;
                                 int nt = ct + d;

                                 double ee = 0;

                                 for (int nit = 0; nit <= ind; nit++)
                                 {
                                     var ne = ee + e + mi[nit] + (ci[nit] - mi[nit]) * nt / (double)ct;
                                     int w = (int)ne - (int)ee;
                                     s = (panes[ind] as Control).Size;
                                     (panes[ind] as Control).Location = new Point((int)ee, 0);
                                     (panes[ind] as Control).Size = new Size(w - e, s.Height);
                                     panes[ind].DoLayout();

                                     ee = ne;
                                 }
                                 mi = panes.Skip(ind + 1).Select(_p => (_p as Control).MinimumSize.Width).ToArray();
                                 ci = panes.Skip(ind + 1).Select(_p => (_p as Control).Width).ToArray();

                                 ct = ci.Sum()-mi.Sum()+((panes.Length-(ind+1))-1)*e;
                                 nt = ct - d;

                                 for (int nit = ind+1; nit <= ind; nit++)
                                 {
                                     var ne = ee + e + mi[nit] + (ci[nit] - mi[nit]) * nt / (double)ct;
                                     int w = (int)ne - (int)ee;
                                     s = (panes[ind] as Control).Size;
                                     (panes[ind] as Control).Location = new Point((int)ee, 0);
                                     (panes[ind] as Control).Size = new Size(w - e, s.Height);
                                     panes[ind].DoLayout();

                                     ee = ne;
                                 }
                             }*/

                    }
                    break;
                case Orientation.Vertical:
                    {
                        s = panes[ind + 0].WidgetSize;
                        pt = panes[ind + 0].Location;
                        panes[ind + 0].Layout(pt, new Size(s.Width, s.Height + d));

                        pt.Offset(0, s.Height - d + e);
                        s = panes[ind + 1].WidgetSize;
                        panes[ind + 1].Layout(pt, new Size(s.Width, s.Height - d));
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
            busy--;
            _DoLayout();
        }

        protected override void OnDragStarted(DragStartedEventArgs args)
        {
        }
        protected override void OnButtonReleased(ButtonEventArgs e)
        {
            if (capture)
            {
                switch (this.dragsplit.Orientation)
                {
                    case Orientation.Horizontal:
                        this.DragSplit(this.dragsplit, this.dragind, e.Position.X - this.dragpt.X);
                        this.dragpt = e.Position;
                        break;
                    case Orientation.Vertical:
                        this.DragSplit(this.dragsplit, this.dragind, e.Position.Y - this.dragpt.Y);
                        this.dragpt = e.Position;
                        break;
                }
                this.capture = false;
                this.xwt.ReleaseCapture(this);
            }
            else
            {
                base.OnButtonReleased(e);
            }
        }
        protected override void OnGotFocus(EventArgs args)
        {
            base.OnGotFocus(args);
        }
        protected override void OnKeyPressed(KeyEventArgs args)
        {
            base.OnKeyPressed(args);
        }
        protected override void OnMouseEntered(EventArgs args)
        {
            base.OnMouseEntered(args);
        }
        protected override void OnMouseExited(EventArgs args)
        {
            base.OnMouseExited(args);
        }
        protected override void OnMouseMoved(MouseMovedEventArgs e)
        {
            if (this.capture)
            {
                switch (this.dragsplit.Orientation)
                {
                    case Orientation.Horizontal:
                        this.DragSplit(this.dragsplit, this.dragind, e.Position.X - this.dragpt.X);
                        this.dragpt = e.Position;
                        break;
                    case Orientation.Vertical:
                        this.DragSplit(this.dragsplit, this.dragind, e.Position.Y - this.dragpt.Y);
                        this.dragpt = e.Position;
                        break;
                }
            }
            else
            {
                CheckCursor(e.Position);
            }
        }

        private void CheckCursor(Point position)
        {
            if (this.Current==null || !this.Current.HitTest(position, out IDockSplitter splitter, out int ind) || splitter == null)
            {
                base.Cursor = CursorType.Arrow;
            }
            else
            {
                switch (splitter.Orientation)
                {
                    case Orientation.Horizontal:
                        base.Cursor = CursorType.ResizeLeftRight;
                        return;
                    case Orientation.Vertical:
                        base.Cursor = CursorType.ResizeUpDown;
                        return;
                }
            }
        }
        internal void RemovePane(IDockLayout pane)
        {
            var split = FindSplitter(pane, out int ind);

            if (split != null) // should be there if not top level
            {
                RemovePane(split, pane);
            }
            else if (object.ReferenceEquals(pane, this.Current))
            {
                ClearContent();
            }
            else
            {
                Debug.Assert(false);
            }
        }
        private void ClearContent()
        {
            this.Current?.RemoveWidget();
            this.Current?.OnHidden();
            this.Current = null;
        }
        internal void RemovePane(IDockSplitter split, IDockLayout panesrc)
        {
            // nottoplevel? -> remove from splitter
            if (!object.ReferenceEquals(this.Current, panesrc))
            {
                split.Remove(panesrc, true);
            }
            else // reset toplevel
            {
                split = null;
                this.ClearContent();

                if (this.FloatForm != null)
                {
                    this.FloatForm.Close();
                }
                ////     OnDocumentsChange(EventArgs.Empty);
                return;
            }
            panesrc.OnHidden();
            // splitter now empty?
            if (split.Layouts.Count() == 0)
            {
                var split2 = FindSplitter(split, out int ind2);

                if (!(split2 is DockPanel))
                {
                    split2.Remove(split, true);
                }
                else
                {
                    Debug.Assert(false);
                }
            }
            else if (split.Layouts.Count() == 1) // splitter now containg 1 item?  ->cleanup
            {
                //find splitter containing split
                var split2 = FindSplitter(split, out int ind2);

                if (split2 == null) // if split is toplevel, add 
                {
                    Debug.Assert(object.ReferenceEquals(this.Current, split));
                    this.Current = split.Layouts.First();
                }
                else if (split2 is DockSplitter) // should be
                {
                    //replace split in split2 with the item in split
                    var oldcontent = split.Layouts.First();

                    split.Remove(oldcontent, false);

                    split2.Insert(ind2, oldcontent);
                    split2.Remove(split, true);
                }
                else
                {
                    Debug.Assert(false);
                }
                ////       OnDocumentsChange(EventArgs.Empty);
            }
            else
            {
                split.GetSize(false);
                ////        OnDocumentsChange(EventArgs.Empty);
            }
        }
        public IDockLayout DockFloatform(IDockFloatForm window, IDockPane panedst, DockPosition dockat)
        {
            IDockLayout result = null;
            BeginLayout();

            Debug.Assert(this.AllLayouts.Contains(panedst));

            //move docnum (-1=all) in pansrc to panedst at dockat (panesrc is assumed to be child in this._content)

            // check if not moving to same floatform
            if (!window.DockPanel.Contains(panedst))
            {
                if (window.DockPanel.Current is IDockPane) // single pane?
                {
                    var srcpane = window.DockPanel.Current as IDockPane;
                    var docs = srcpane.Documents.ToArray();

                    srcpane.Remove(docs); //removes widgets

                    //add
                    if (dockat == DockPosition.Center)
                    {
                        panedst.Add(docs);
                        result = panedst;
                    }
                    else
                    {
                        result = panedst.DockPanel.Dock(docs, dockat, panedst);
                    }
                    window.Close();
                }
                else //multiple panes -> splitter
                {
                    Debug.Assert(window.DockPanel.Current is IDockSplitter);

                    var srcsplit = window.DockPanel.Current as IDockSplitter;

                    if (dockat != DockPosition.Center ||  panedst.Documents.Count() == 0)
                    {
                        srcsplit.RemoveWidget();

                        srcsplit.NewDockPanel(this);

                        result = panedst.DockPanel.Dock(srcsplit, dockat, panedst);

                        window.Close();
                    }
                    else
                    {
                        SystemSounds.Exclamation.Play();
                    }
                }
            }
            EndLayout(true);

            return result;
        }
        public void MovePane(IDockPane panesrc, IDockContent[] doc, IDockPane panedst, DockPosition dockat)
        {
            BeginLayout();

            //move docnum (-1=all) in pansrc to panedst at dockat (panesrc is assumed to be child in this._content)

            var split = FindSplitter(panesrc, out int ind);

            // check if not moving to same place
            if (!object.ReferenceEquals(panesrc, panedst) || dockat != DockPosition.Center)
            {
                // get document to move
                if (panedst?.DockPanel.FloatForm != null)
                {
                    doc = doc.Where(_doc => (_doc as IDockCustomize)?.CanFloat ?? DockPanel.DefaultFloat).ToArray();
                }
                // remove
                bool delsrc = panesrc.Remove(doc);

                //add
                if (dockat == DockPosition.Center)
                {
                    panedst.Add(doc);
                }
                else
                {
                    panedst = panedst.DockPanel.Dock(doc, dockat, panedst);
                }
                // if empty check remove pane
                if (delsrc)
                {
                    _CheckRemovePane(panesrc, panedst);
                }
                ////             this.ActiveControl = doc.FirstOrDefault();
            }
            EndLayout(true);

            //     OnDocumentsChange(EventArgs.Empty);
        }
        internal void _CheckRemovePane(IDockLayout pane, IDockPane panedst)
        {
            bool checkdocs = false;
            //  Debug.Assert(!pane.Controls.Any());

        //    DockPanel dp = this;

              if (this.FloatForm != null)
              {
                //  dp = this.FloatForm.MainDockPanel;

                  this.RemovePane(pane);
                  pane = null;

                  if (this.Current == null)
                  {
                      this.FloatForm.Close();
                  }
                  /*if (panedst != null && panedst.DockPanel.FloatForm == null) // check docs only for mdi
                  {
                      checkdocs = true;
                  }*/
              }
              else 
            {
               // dp = this;
                if (panedst != null &&object.ReferenceEquals(panedst.DockPanel,pane.DockPanel))//&& panedst.DockPanel.FloatForm == null)
                {
                    //    Debug.Assert(panedst.GetDocuments(-1).Any());

                    /*  if (pane.DockPanel.FloatForm == null)
                      {
                          checkdocs = true; // main->main
                      }
                      else */
               /*     if (panedst.Documents.Any())
                    {
                        this.RemovePane(pane);
                    }
                    else*/
                    {
                        if (this.AllLayouts.OfType<IDockPane>().Where(_t => !object.ReferenceEquals(pane, _t) && _t.Documents.OfType<IDockDocument>().Any()).Count() > 0)
                        {
                            this.RemovePane(pane);
                        }
                    }
                }
                else
                {
                    checkdocs = true;
                }
            }
            if (checkdocs)
            {
                var empty = this.AllLayouts.OfType<IDockPane>().Where(_t => !_t.Documents.Any());
                var docs = this.AllLayouts.OfType<IDockPane>().Where(_t => _t.Documents.OfType<IDockDocument>().Any());

                if (!docs.Any()) // -> no docs, keep 1
                {
                    if (empty.Count() > 1)
                    {
                        if (empty.Any(_p => object.ReferenceEquals(_p, pane)))
                        {
                            RemovePane(pane);
                        }
                        // keep 1
                        if (empty.Count() > 1) // query, actual value
                        {
                            empty = this.AllLayouts.OfType<IDockPane>().Where(_t => !_t.Documents.Any()).OrderByDescending(_p => _p.WidgetSize.Width * _p.WidgetSize.Height);

                            if (empty.Any()) // keep biggest
                            {
                                foreach (var l in empty.Skip(1).ToArray())
                                {
                                    this.RemovePane(l);
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var l in empty.ToArray())
                    {
                        this.RemovePane(l);
                    }
                }
            }
        }
        public void FloatPane(IDockPane panesrc, IDockContent[] doc, Point formpos)
        {
            BeginLayout();

            //move docnum (-1=all) in pansrc to panedst at dockat

            // could be mdi-child-window, so lookup
            var split = FindSplitter(panesrc, out int ind);
            if (split == null)
            { 
                Debug.Assert(object.ReferenceEquals(panesrc, this.Current));
            }
            if (doc.Length > 0) // only float floatable controls
            {
                bool delsrc = panesrc.Remove(doc);

                var form = FloatWindow.Create(this, doc, formpos, out IDockPane panefloat);

                if (delsrc)
                {
                    split = FindSplitter(panesrc, out ind);
                    // RemovePane(split, _panesrc);

                    _CheckRemovePane(panesrc, panefloat);
                }
             //   this.ActiveControl = doc.FirstOrDefault();
            }
            EndLayout(true);

        //    OnDocumentsChange(EventArgs.Empty);
        }
    }
}