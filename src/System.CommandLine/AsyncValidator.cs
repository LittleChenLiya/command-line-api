// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine
{
    /// <summary>
    /// Defines a named asynchronous validation check that runs after parsing and synchronous
    /// validation have completed, and before the command's action is invoked.
    /// </summary>
    /// <remarks>
    /// Asynchronous validators are only executed by <see cref="ParseResult.InvokeAsync"/>.
    /// The synchronous <see cref="ParseResult.Invoke"/> method does not support them and throws
    /// an <see cref="InvalidOperationException"/> when any are registered, rather than blocking
    /// on asynchronous work (which can deadlock).
    /// </remarks>
    public sealed class AsyncValidator
    {
        private readonly Func<ParseResult, CancellationToken, Task<IEnumerable<ValidationDiagnostic>?>> _validate;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncValidator"/> class.
        /// </summary>
        /// <param name="name">The name of the validator, used in diagnostics produced when the validator throws.</param>
        /// <param name="validate">
        /// The validation logic. It receives the <see cref="ParseResult"/> and a <see cref="CancellationToken"/>,
        /// and returns the diagnostics found, or <see langword="null"/> or an empty sequence when validation succeeds.
        /// </param>
        public AsyncValidator(string name, Func<ParseResult, CancellationToken, Task<IEnumerable<ValidationDiagnostic>?>> validate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            Name = name;
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        /// <summary>
        /// Gets the name of the validator.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Runs the validation logic.
        /// </summary>
        /// <param name="parseResult">Provides the parse results.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The diagnostics found, or <see langword="null"/> or an empty sequence when validation succeeds.</returns>
        public Task<IEnumerable<ValidationDiagnostic>?> ValidateAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
            => _validate(parseResult, cancellationToken);
    }
}
