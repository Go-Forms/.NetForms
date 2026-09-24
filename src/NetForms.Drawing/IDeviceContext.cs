using System;

namespace System.Drawing;

/// <summary>
/// WinForms' handle to "something you can draw on" (Graphics implements it). There is no
/// HDC here; <see cref="GetHdc"/> returns <see cref="IntPtr.Zero"/> so code that only
/// passes the context along keeps compiling.
/// </summary>
public interface IDeviceContext : IDisposable
{
    IntPtr GetHdc();
    void ReleaseHdc();
}
