// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Windows.Forms;

/// <summary>
/// The small internal helpers of dotnet/winforms that the vendored data binding code calls (EnumExtensions,
/// ExceptionExtensions, CollectionHelper, TypeExtensions of System.Private.Windows.Core), in one place.
/// </summary>
internal static class VendorHelpers
{
    /// <summary>ComponentModel is used as is (no trimming): WinForms' feature switch, off by default.</summary>
    internal static bool UseComponentModelRegisteredTypes => false;

    /// <summary>Sets the <paramref name="flags"/> if <paramref name="set"/> is true, otherwise clears them.</summary>
    public static void ChangeFlags<T>(ref this T value, T flags, bool set) where T : unmanaged, Enum
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
                {
                    ref byte v = ref Unsafe.As<T, byte>(ref value);
                    byte f = Unsafe.As<T, byte>(ref flags);
                    v = set ? (byte)(v | f) : (byte)(v & ~f);
                    break;
                }
            case 2:
                {
                    ref ushort v = ref Unsafe.As<T, ushort>(ref value);
                    ushort f = Unsafe.As<T, ushort>(ref flags);
                    v = set ? (ushort)(v | f) : (ushort)(v & ~f);
                    break;
                }
            case 4:
                {
                    ref uint v = ref Unsafe.As<T, uint>(ref value);
                    uint f = Unsafe.As<T, uint>(ref flags);
                    v = set ? v | f : v & ~f;
                    break;
                }
            default:
                {
                    ref ulong v = ref Unsafe.As<T, ulong>(ref value);
                    ulong f = Unsafe.As<T, ulong>(ref flags);
                    v = set ? v | f : v & ~f;
                    break;
                }
        }
    }

    /// <summary>An exception that is not recoverable, or a likely bug in the implementation.</summary>
    internal static bool IsCriticalException(this Exception ex)
        => ex is NullReferenceException
            or StackOverflowException
            or OutOfMemoryException
            or ThreadAbortException
            or IndexOutOfRangeException
            or AccessViolationException;

    /// <summary>If <paramref name="type"/> is a nullable type, the underlying type; otherwise <paramref name="type"/>.</summary>
    internal static Type UnwrapIfNullable(this Type type) =>
        type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? type.GetGenericArguments()[0]
            : type;

    /// <summary>Copies to arrays matching <c>Hashtable.CopyTo</c>'s behavior.</summary>
    public static void HashtableCopyTo<TKey, TValue>(this IDictionary<TKey, TValue> source, Array target, int index)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Rank != 1)
            throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(target));
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)target.Length, nameof(index));
        if (target.GetLowerBound(0) != 0)
            throw new ArgumentException("The lower bound of target array must be zero.", nameof(target));
        if (target.Length - index < source.Count)
            throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.");

        if (target is KeyValuePair<TKey, TValue>[] pairsTarget)
        {
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                pairsTarget[index++] = kvp;
            }
        }
        else if (target is DictionaryEntry[] dictionaryTarget)
        {
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                if (kvp.Key is not null)
                {
                    dictionaryTarget[index++] = new DictionaryEntry(kvp.Key, kvp.Value);
                }
            }
        }
        else if (target is object[] objectTarget)
        {
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                if (kvp.Key is not null)
                {
                    objectTarget[index++] = new DictionaryEntry(kvp.Key, kvp.Value);
                }
            }
        }
        else
        {
            throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(target));
        }
    }
}
