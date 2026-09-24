using System;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing.Imaging;

public enum ColorMatrixFlag
{
    Default = 0,
    SkipGrays = 1,
    AltGrays = 2,
}

public enum ColorAdjustType
{
    Default = 0,
    Bitmap = 1,
    Brush = 2,
    Pen = 3,
    Text = 4,
    Count = 5,
    Any = 6,
}

/// <summary>A 5×5 matrix over (R, G, B, A, 1) in the 0..1 range, applied as a row vector times the matrix (GDI+).</summary>
public sealed class ColorMatrix
{
    private readonly float[,] _m = new float[5, 5];

    public ColorMatrix()
    {
        for (int i = 0; i < 5; i++) _m[i, i] = 1;
    }

    public ColorMatrix(float[][] newColorMatrix)
    {
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                _m[i, j] = i < newColorMatrix.Length && j < newColorMatrix[i].Length ? newColorMatrix[i][j] : 0;
    }

    public float this[int row, int column]
    {
        get => _m[row, column];
        set => _m[row, column] = value;
    }

    public float Matrix00 { get => _m[0, 0]; set => _m[0, 0] = value; }
    public float Matrix01 { get => _m[0, 1]; set => _m[0, 1] = value; }
    public float Matrix02 { get => _m[0, 2]; set => _m[0, 2] = value; }
    public float Matrix03 { get => _m[0, 3]; set => _m[0, 3] = value; }
    public float Matrix04 { get => _m[0, 4]; set => _m[0, 4] = value; }
    public float Matrix10 { get => _m[1, 0]; set => _m[1, 0] = value; }
    public float Matrix11 { get => _m[1, 1]; set => _m[1, 1] = value; }
    public float Matrix12 { get => _m[1, 2]; set => _m[1, 2] = value; }
    public float Matrix13 { get => _m[1, 3]; set => _m[1, 3] = value; }
    public float Matrix14 { get => _m[1, 4]; set => _m[1, 4] = value; }
    public float Matrix20 { get => _m[2, 0]; set => _m[2, 0] = value; }
    public float Matrix21 { get => _m[2, 1]; set => _m[2, 1] = value; }
    public float Matrix22 { get => _m[2, 2]; set => _m[2, 2] = value; }
    public float Matrix23 { get => _m[2, 3]; set => _m[2, 3] = value; }
    public float Matrix24 { get => _m[2, 4]; set => _m[2, 4] = value; }
    public float Matrix30 { get => _m[3, 0]; set => _m[3, 0] = value; }
    public float Matrix31 { get => _m[3, 1]; set => _m[3, 1] = value; }
    public float Matrix32 { get => _m[3, 2]; set => _m[3, 2] = value; }
    public float Matrix33 { get => _m[3, 3]; set => _m[3, 3] = value; }
    public float Matrix34 { get => _m[3, 4]; set => _m[3, 4] = value; }
    public float Matrix40 { get => _m[4, 0]; set => _m[4, 0] = value; }
    public float Matrix41 { get => _m[4, 1]; set => _m[4, 1] = value; }
    public float Matrix42 { get => _m[4, 2]; set => _m[4, 2] = value; }
    public float Matrix43 { get => _m[4, 3]; set => _m[4, 3] = value; }
    public float Matrix44 { get => _m[4, 4]; set => _m[4, 4] = value; }

    /// <summary>
    /// Skia's 4×5 row-major form (each output channel a row: R' = m0·R + m1·G + m2·B + m3·A + m4),
    /// i.e. the transpose of GDI+'s, translation already in the 0..1 range both use.
    /// </summary>
    internal float[] ToSkia()
    {
        var r = new float[20];
        for (int channel = 0; channel < 4; channel++)
        {
            for (int input = 0; input < 4; input++) r[channel * 5 + input] = _m[input, channel];
            r[channel * 5 + 4] = _m[4, channel];
        }
        return r;
    }
}

public sealed class ColorMap
{
    public Color OldColor { get; set; }
    public Color NewColor { get; set; }
}

/// <summary>
/// How DrawImage recolors: a color matrix, a gamma, a transparent color key range, a remap table, a
/// threshold. Bitmap adjustments are applied (the Default/Bitmap kind); brush, pen and text ones are kept.
/// </summary>
public sealed class ImageAttributes : ICloneable, IDisposable
{
    internal ColorMatrix? Matrix { get; private set; }
    internal float Gamma { get; private set; } = 1f;
    internal (Color Low, Color High)? ColorKey { get; private set; }
    internal ColorMap[]? RemapTable { get; private set; }
    internal float? Threshold { get; private set; }
    internal bool NoOp { get; private set; }
    internal WrapMode WrapMode { get; private set; } = WrapMode.Clamp;

    private static bool Applies(ColorAdjustType type) => type is ColorAdjustType.Default or ColorAdjustType.Bitmap or ColorAdjustType.Any;

    public void SetColorMatrix(ColorMatrix newColorMatrix) => SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag flags) => SetColorMatrix(newColorMatrix, flags, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type)
    {
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        if (Applies(type)) Matrix = newColorMatrix;
    }

    public void SetColorMatrices(ColorMatrix newColorMatrix, ColorMatrix? grayMatrix) => SetColorMatrix(newColorMatrix);

    public void SetColorMatrices(ColorMatrix newColorMatrix, ColorMatrix? grayMatrix, ColorMatrixFlag flags) => SetColorMatrix(newColorMatrix, flags);

    public void SetColorMatrices(ColorMatrix newColorMatrix, ColorMatrix? grayMatrix, ColorMatrixFlag mode, ColorAdjustType type) => SetColorMatrix(newColorMatrix, mode, type);

    public void ClearColorMatrix() => ClearColorMatrix(ColorAdjustType.Default);

    public void ClearColorMatrix(ColorAdjustType type)
    {
        if (Applies(type)) Matrix = null;
    }

    public void SetGamma(float gamma) => SetGamma(gamma, ColorAdjustType.Default);

    public void SetGamma(float gamma, ColorAdjustType type)
    {
        if (Applies(type)) Gamma = gamma;
    }

    public void ClearGamma() => ClearGamma(ColorAdjustType.Default);

    public void ClearGamma(ColorAdjustType type)
    {
        if (Applies(type)) Gamma = 1f;
    }

    public void SetColorKey(Color colorLow, Color colorHigh) => SetColorKey(colorLow, colorHigh, ColorAdjustType.Default);

    public void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type)
    {
        if (Applies(type)) ColorKey = (colorLow, colorHigh);
    }

    public void ClearColorKey() => ClearColorKey(ColorAdjustType.Default);

    public void ClearColorKey(ColorAdjustType type)
    {
        if (Applies(type)) ColorKey = null;
    }

    public void SetRemapTable(ColorMap[] map) => SetRemapTable(map, ColorAdjustType.Default);

    public void SetRemapTable(ColorMap[] map, ColorAdjustType type)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (Applies(type)) RemapTable = map;
    }

    public void ClearRemapTable() => ClearRemapTable(ColorAdjustType.Default);

    public void ClearRemapTable(ColorAdjustType type)
    {
        if (Applies(type)) RemapTable = null;
    }

    public void SetBrushRemapTable(ColorMap[] map) => SetRemapTable(map, ColorAdjustType.Brush);

    public void ClearBrushRemapTable() => ClearRemapTable(ColorAdjustType.Brush);

    public void SetThreshold(float threshold) => SetThreshold(threshold, ColorAdjustType.Default);

    public void SetThreshold(float threshold, ColorAdjustType type)
    {
        if (Applies(type)) Threshold = threshold;
    }

    public void ClearThreshold() => ClearThreshold(ColorAdjustType.Default);

    public void ClearThreshold(ColorAdjustType type)
    {
        if (Applies(type)) Threshold = null;
    }

    public void SetNoOp() => SetNoOp(ColorAdjustType.Default);

    public void SetNoOp(ColorAdjustType type)
    {
        if (Applies(type)) NoOp = true;
    }

    public void ClearNoOp() => ClearNoOp(ColorAdjustType.Default);

    public void ClearNoOp(ColorAdjustType type)
    {
        if (Applies(type)) NoOp = false;
    }

    public void SetWrapMode(WrapMode mode) => WrapMode = mode;

    public void SetWrapMode(WrapMode mode, Color color) => WrapMode = mode;

    public void SetWrapMode(WrapMode mode, Color color, bool clamp) => WrapMode = mode;

    public void SetOutputChannelColorProfile(string colorProfileFilename) { }

    public void SetOutputChannelColorProfile(string colorProfileFilename, ColorAdjustType type) { }

    public void ClearOutputChannelColorProfile() { }

    public void ClearOutputChannelColorProfile(ColorAdjustType type) { }

    public void ClearOutputChannel() { }

    public void ClearOutputChannel(ColorAdjustType type) { }

    public object Clone() => MemberwiseClone();

    public void Dispose() => GC.SuppressFinalize(this);

    ~ImageAttributes() { }

    /// <summary>The image as these attributes recolor it (per-pixel steps first, the matrix as a paint filter).</summary>
    internal SKBitmap Apply(SKBitmap source, out SKColorFilter? filter)
    {
        filter = NoOp || Matrix == null ? null : SKColorFilter.CreateColorMatrix(Matrix.ToSkia());
        if (NoOp || (ColorKey == null && RemapTable == null && Threshold == null && Gamma == 1f)) return source;
        var copy = source.Copy();
        for (int y = 0; y < copy.Height; y++)
        {
            for (int x = 0; x < copy.Width; x++)
            {
                var c = copy.GetPixel(x, y); // unpremultiplied
                if (RemapTable != null)
                    foreach (var map in RemapTable)
                        if (map.OldColor.ToArgb() == (int)(uint)c) { c = new SKColor((uint)map.NewColor.ToArgb()); break; }
                if (ColorKey is { } key && c.Red >= key.Low.R && c.Red <= key.High.R && c.Green >= key.Low.G && c.Green <= key.High.G && c.Blue >= key.Low.B && c.Blue <= key.High.B)
                    c = SKColors.Transparent;
                if (Gamma != 1f && c.Alpha != 0)
                    c = new SKColor(GammaOf(c.Red), GammaOf(c.Green), GammaOf(c.Blue), c.Alpha);
                if (Threshold is { } t && c.Alpha != 0)
                    c = new SKColor(c.Red / 255f > t ? (byte)255 : (byte)0, c.Green / 255f > t ? (byte)255 : (byte)0, c.Blue / 255f > t ? (byte)255 : (byte)0, c.Alpha);
                copy.SetPixel(x, y, c);
            }
        }
        return copy;
    }

    private byte GammaOf(byte v) => (byte)Math.Clamp(Math.Round(Math.Pow(v / 255.0, Gamma) * 255), 0, 255);
}
