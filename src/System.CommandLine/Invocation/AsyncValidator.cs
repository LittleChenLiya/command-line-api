// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine.Invocation;

/// <summary>
/// Represents an asynchronous validator that runs after parsing and synchronous validation have
/// completed without errors, and before the command's action is invoked.
/// </summary>
/// <remarks>
/// Asynchronous validators are only executed by <see cref="ParseResult.InvokeAsync"/>.
/// The synchronous <see cref="ParseResult.Invoke"/> method does not block on them and throws
/// an <see cref="InvalidOperationException"/> instead.
/// </remarks>
public sealed class AsyncValidator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncValidator"/> class.
    /// </summary>
    /// <param name="name">The name of the validator. It is included in the diagnostic produced when the validator throws an exception.</param>
    /// <param name="validate">
    /// The validation logic. It receives the <see cref="ParseResult"/> and a <see cref="CancellationToken"/>,
    /// and returns a <see cref="ValidationDiagnostic"/> when validation fails, or <see langword="null"/> when it succeeds.
    /// </param>
    public AsyncValidator(string name, Func<ParseResult, CancellationToken, Task<ValidationDiagnostic?>> validate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        Validate = validate ?? throw new ArgumentNullException(nameof(validate));
    }

    /// <summary>
    /// Gets the name of the validator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the validation logic that is invoked before the command's action runs.
    /// </summary>
    public Func<ParseResult, CancellationToken, Task<ValidationDiagnostic?>> Validate { get; }
}
