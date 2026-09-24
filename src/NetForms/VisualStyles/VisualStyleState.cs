// Vendored from dotnet/winforms (MIT, see THIRD-PARTY-NOTICES.md); Win32 constants replaced by their values.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.VisualStyles;

/// <summary>
///  Determines whether visual styles are enabled.
/// </summary>
[Flags]
public enum VisualStyleState
{
    /// <summary>
    ///  Visual styles are not enabled.
    /// </summary>
    NoneEnabled = 0,

    /// <summary>
    ///  Visual styles enabled only for client area.
    /// </summary>
    ClientAreaEnabled = 2,

    /// <summary>
    ///  Visual styles enabled only for non-client area.
    /// </summary>
    NonClientAreaEnabled = 1,

    /// <summary>
    ///  Visual styles enabled only for client and non-client areas.
    /// </summary>
    ClientAndNonClientAreasEnabled = 3
}
