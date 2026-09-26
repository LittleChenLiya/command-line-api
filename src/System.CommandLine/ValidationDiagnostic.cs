// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine
{
    /// <summary>
    /// Describes a single diagnostic produced by an <see cref="AsyncValidator"/>.
    /// </summary>
    public sealed class ValidationDiagnostic
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationDiagnostic"/> class.
        /// </summary>
        /// <param name="message">A message that explains the diagnostic to the user.</param>
        /// <param name="validatorName">The name of the validator that produced the diagnostic, when known.</param>
        public ValidationDiagnostic(string message, string? validatorName = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
            }

            Message = message;
            ValidatorName = validatorName;
        }

        /// <summary>
        /// A message that explains the diagnostic to the user.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The name of the validator that produced the diagnostic, when known.
        /// </summary>
        public string? ValidatorName { get; }

        /// <inheritdoc />
        public override string ToString() => Message;
    }
}
