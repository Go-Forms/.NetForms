using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum ColorDepth
{
    Depth4Bit = 4,
    Depth8Bit = 8,
    Depth16Bit = 16,
    Depth24Bit = 24,
    Depth32Bit = 32,
}

/// <summary>
/// A keyed list of same-sized images shared by ToolStrip, ListView and TreeView. Win32 keeps
/// one bitmap strip and a colour depth; we keep the images as they are and only scale on draw,
/// so <see cref="ImageSize"/> is the size everything is drawn at, not a re-encode of the source.
/// </summary>
[DefaultProperty(nameof(Images))]
public sealed class ImageList : Component
{
    private Size _imageSize = new Size(16, 16);

    public ImageList() => Images = new ImageCollection(this);

    public ImageList(System.ComponentModel.IContainer container) : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    [Category("Appearance")]
    [Description("The images stored in this ImageList.")]
    [DefaultValue(null)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ImageCollection Images { get; }

    [Category("Behavior")]
    [Description("The size of individual images in the ImageList.")]
    [Localizable(true)]
    public Size ImageSize
    {
        get => _imageSize;
        set
        {
            if (value.Width <= 0 || value.Height <= 0 || value.Width > 256 || value.Height > 256)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_imageSize == value) return;
            _imageSize = value;
            _version++;
            OnRecreateHandle(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [Description("The number of colors to use to render images.")]
    public ColorDepth ColorDepth { get; set; } = ColorDepth.Depth32Bit;

    [Category("Behavior")]
    [Description("The color that is treated as transparent.")]
    public Color TransparentColor { get; set; } = Color.Transparent;

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    /// <summary>
    /// All the images at once, as the designer keeps them in the .resx: null for an empty list; setting it
    /// replaces the images, size and colour depth (null empties the list), keeping the order the designer's
    /// <c>Images.SetKeyName(i, ...)</c> calls that follow rely on.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DefaultValue(null)]
    public ImageListStreamer? ImageStream
    {
        get
        {
            if (Images.Count == 0) return null;
            // The same object while the images are what it holds: the designer sees an unchanged value.
            if (_imageStream == null || _imageStreamVersion != _version)
            {
                _imageStream = new ImageListStreamer(this);
                _imageStreamVersion = _version;
            }
            return _imageStream;
        }
        set
        {
            Images.Clear();
            _imageStream = null;
            if (value == null) return;
            _imageSize = value.ImageSize;
            ColorDepth = value.ColorDepth;
            foreach (var image in value.Images) Images.Add((Image)image.Clone());
            _imageStream = value;
            _imageStreamVersion = _version;
            OnRecreateHandle(EventArgs.Empty);
        }
    }

    // As in WinForms: with images, the stream carries size and depth, so only an empty list writes them;
    // TransparentColor is written unless it is the reset value LightGray (a new list has Transparent).
    internal bool ShouldSerializeColorDepth() => Images.Count == 0;

    internal void ResetColorDepth() => ColorDepth = ColorDepth.Depth32Bit;

    internal bool ShouldSerializeImageSize() => Images.Count == 0;

    internal void ResetImageSize() => ImageSize = new Size(16, 16);

    internal bool ShouldSerializeTransparentColor() => !TransparentColor.Equals(Color.LightGray);

    internal void ResetTransparentColor() => TransparentColor = Color.LightGray;

    private ImageListStreamer? _imageStream;
    private int _imageStreamVersion;

    /// <summary>Bumped by every change of <see cref="Images"/>.</summary>
    private int _version;

    [Description("The native handle of the ImageList.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IntPtr Handle => IntPtr.Zero;

    [Description("Indicates if the native handle has been created for this ImageList.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HandleCreated => true;

    [Description("Occurs when the ImageList needs to recreate its handle.")]
    [Browsable(false)]
    public event EventHandler? RecreateHandle;

    private void OnRecreateHandle(EventArgs e) => RecreateHandle?.Invoke(this, e);

    public void Draw(Graphics g, Point pt, int index) => Draw(g, pt.X, pt.Y, index);

    public void Draw(Graphics g, int x, int y, int index) => Draw(g, x, y, _imageSize.Width, _imageSize.Height, index);

    public void Draw(Graphics g, int x, int y, int width, int height, int index)
    {
        ArgumentNullException.ThrowIfNull(g);
        var image = Images.GetImage(index);
        if (image == null) return;
        g.DrawImage(image, new Rectangle(x, y, width, height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Images.Clear();
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + " Images.Count: " + Images.Count + ", ImageSize: " + ImageSize;

    public sealed class ImageCollection : IList, IList<Image>
    {
        private readonly ImageList _owner;
        private readonly List<Image> _images = new();
        private readonly List<string> _keys = new();

        internal ImageCollection(ImageList owner) => _owner = owner;

        public int Count => _images.Count;
        public bool Empty => _images.Count == 0;
        public bool IsReadOnly => false;

        public Image this[int index]
        {
            get => _images[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _images[index] = value;
                _owner._version++;
            }
        }

        public Image? this[string? key]
        {
            get
            {
                int i = IndexOfKey(key);
                return i >= 0 ? _images[i] : null;
            }
        }

        public StringCollection Keys => new StringCollection(_keys);

        internal Image? GetImage(int index) => index >= 0 && index < _images.Count ? _images[index] : null;

        public void Add(Image value) => Add(string.Empty, value);

        public void Add(string? key, Image image)
        {
            ArgumentNullException.ThrowIfNull(image);
            _images.Add(image);
            _owner._version++;
            _keys.Add(key ?? string.Empty);
        }

        public void Add(Image value, Color transparentColor)
        {
            // The transparent colour is a source-bitmap convention; our images already carry alpha.
            Add(value);
        }

        public int Add(Icon value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Add(value.ToBitmap());
            return _images.Count - 1;
        }

        public void AddRange(Image[] images)
        {
            ArgumentNullException.ThrowIfNull(images);
            foreach (var image in images) Add(image);
        }

        /// <summary>Slices a horizontal strip into <paramref name="count"/> images of <see cref="ImageSize"/>.</summary>
        public void AddStrip(Image value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var size = _owner.ImageSize;
            if (value.Width % size.Width != 0) throw new ArgumentException("The strip width must be a multiple of ImageSize.Width.", nameof(value));
            int count = value.Width / size.Width;
            for (int i = 0; i < count; i++)
            {
                var frame = new Bitmap(size.Width, size.Height);
                using (var g = Graphics.FromImage(frame))
                {
                    g.DrawImage(value, new Rectangle(0, 0, size.Width, size.Height), new Rectangle(i * size.Width, 0, size.Width, size.Height), GraphicsUnit.Pixel);
                }
                Add(frame);
            }
        }

        public void Clear()
        {
            _images.Clear();
            _owner._version++;
            _keys.Clear();
        }

        public bool Contains(Image image) => _images.Contains(image);

        public bool ContainsKey(string? key) => IndexOfKey(key) >= 0;

        public int IndexOf(Image image) => _images.IndexOf(image);

        public int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _keys.Count; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public void SetKeyName(int index, string? name)
        {
            if (index < 0 || index >= _keys.Count) throw new IndexOutOfRangeException();
            _keys[index] = name ?? string.Empty;
        }

        public bool Remove(Image image)
        {
            int i = IndexOf(image);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        public void RemoveAt(int index)
        {
            _images.RemoveAt(index);
            _owner._version++;
            _keys.RemoveAt(index);
        }

        public void RemoveByKey(string? key)
        {
            int i = IndexOfKey(key);
            if (i >= 0) RemoveAt(i);
        }

        public void Insert(int index, Image image) => throw new NotSupportedException();

        public void CopyTo(Image[] array, int arrayIndex) => _images.CopyTo(array, arrayIndex);

        public IEnumerator<Image> GetEnumerator() => _images.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _images.GetEnumerator();

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _images[index]; set => this[index] = (Image)value!; }
        int IList.Add(object? value) { Add((Image)value!); return _images.Count - 1; }
        bool IList.Contains(object? value) => value is Image i && Contains(i);
        int IList.IndexOf(object? value) => value is Image i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) { if (value is Image i) Remove(i); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_images).CopyTo(array, index);

        /// <summary>The read-only view of the image keys that WinForms hands out from Images.Keys.</summary>
        public sealed class StringCollection : IList
        {
            private readonly List<string> _items;

            internal StringCollection(List<string> items) => _items = items;

            public int Count => _items.Count;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool IsSynchronized => false;
            public object SyncRoot => this;

            public object? this[int index] { get => _items[index]; set => throw new NotSupportedException(); }

            public bool Contains(object? value) => value is string s && _items.Contains(s);
            public int IndexOf(object? value) => value is string s ? _items.IndexOf(s) : -1;
            public IEnumerator GetEnumerator() => _items.GetEnumerator();
            public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
            public int Add(object? value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, object? value) => throw new NotSupportedException();
            public void Remove(object? value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
    }
}
