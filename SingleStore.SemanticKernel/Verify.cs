using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SingleStore.SemanticKernel;

internal static class Verify
{
    /// <summary>
    /// Equivalent of ArgumentNullException.ThrowIfNull
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNull([NotNull] object? obj,
        [CallerArgumentExpression(nameof(obj))]
        string? paramName = null)
    {
#if NET
        ArgumentNullException.ThrowIfNull(obj, paramName);
#else
        if (obj is null) ThrowArgumentNullException(paramName);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNullOrWhiteSpace([NotNull] string? str,
        [CallerArgumentExpression(nameof(str))]
        string? paramName = null)
    {
#if NET
        ArgumentException.ThrowIfNullOrWhiteSpace(str, paramName);
#else
        NotNull(str, paramName);
        if (string.IsNullOrWhiteSpace(str)) ThrowArgumentWhiteSpaceException(paramName);
#endif
    }

    [DoesNotReturn]
    internal static void ThrowArgumentNullException(string? paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    internal static void ThrowArgumentWhiteSpaceException(string? paramName)
    {
        throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.",
            paramName);
    }

    internal static void NotLessThan(int value, int limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < limit)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be greater than or equal to {limit}.");
        }
    }
}
