namespace System.Drawing.Drawing2D;

public sealed class GraphicsContainer
{
    internal GraphicsContainer(GraphicsState state) => State = state;

    internal GraphicsState State { get; }
}

public enum CompositingMode
{
    SourceOver = 0,
    SourceCopy = 1,
}

public enum CoordinateSpace
{
    World = 0,
    Page = 1,
    Device = 2,
}
