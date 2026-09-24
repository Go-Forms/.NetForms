// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
namespace System.Windows.Forms;

public class ConvertEventArgs : EventArgs
{
    public ConvertEventArgs(object? value, Type? desiredType)
    {
        Value = value;
        DesiredType = desiredType;
    }

    public object? Value { get; set; }

    public Type? DesiredType { get; }
}
