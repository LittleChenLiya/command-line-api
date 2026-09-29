// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

namespace System.CommandLine
{
    /// <summary>
    /// Describes a single value that will be supplied to an option or argument when a
    /// <see cref="ParseResult"/> is invoked.
    /// </summary>
    public sealed class ParsedValue
    {
        internal ParsedValue(
            string? rawValue,
            object? convertedValue,
            ParseValueOrigin origin,
            TokenRange tokenRange,
            string? conversionError)
        {
            RawValue = rawValue;
            ConvertedValue = convertedValue;
            Origin = origin;
            TokenRange = tokenRange;
            ConversionError = conversionError;
        }

        /// <summary>
        /// Gets the value exactly as it appeared on the command line, or <see langword="null"/> for a default value.
        /// </summary>
        public string? RawValue { get; }

        /// <summary>
        /// Gets the converted value that will be supplied to the handler.
        /// </summary>
        /// <remarks>
        /// Accessing this property does not perform any conversion and does not invoke any
        /// user code. The value is <see langword="null"/> when conversion failed; the failure
        /// message is available from <see cref="ConversionError"/>. The object's
        /// <see cref="object.ToString"/> method is never called while building the description.
        /// </remarks>
        public object? ConvertedValue { get; }

        /// <summary>
        /// Gets a value indicating whether the value was provided explicitly or produced from a default.
        /// </summary>
        public ParseValueOrigin Origin { get; }

        /// <summary>
        /// Gets the range of input arguments associated with this value.
        /// </summary>
        public TokenRange TokenRange { get; }

        /// <summary>
        /// Gets the error message if this value could not be converted to the target type; otherwise <see langword="null"/>.
        /// </summary>
        public string? ConversionError { get; }

        /// <summary>
        /// Gets a value indicating whether the value was converted successfully.
        /// </summary>
        public bool IsSuccess => ConversionError is null;

        /// <inheritdoc />
        /// <remarks>
        /// This override never calls <see cref="ConvertedValue"/>.<see cref="object.ToString"/>,
        /// so values whose <see cref="object.ToString"/> throws can be reported safely.
        /// </remarks>
        public override string ToString() =>
            Origin == ParseValueOrigin.Default
                ? "<default>"
                : RawValue ?? "";
    }
}
