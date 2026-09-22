// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Abstracts the file reads performed during response file expansion,
    /// allowing in-memory substitutes to be injected for testing.
    /// </summary>
    public interface IResponseFileReader
    {
        /// <summary>
        /// Determines whether the specified response file exists.
        /// </summary>
        /// <param name="path">The normalized full path of the response file.</param>
        bool Exists(string path);

        /// <summary>
        /// Reads the contents of the specified response file.
        /// </summary>
        /// <param name="path">The normalized full path of the response file.</param>
        /// <returns>The raw bytes of the response file.</returns>
        byte[] ReadAllBytes(string path);
    }

    /// <summary>
    /// The default <see cref="IResponseFileReader"/>, which reads from the file system.
    /// </summary>
    public sealed class FileResponseFileReader : IResponseFileReader
    {
        /// <summary>
        /// Gets a shared instance of <see cref="FileResponseFileReader"/>.
        /// </summary>
        public static FileResponseFileReader Instance { get; } = new();

        private FileResponseFileReader()
        {
        }

        /// <inheritdoc />
        public bool Exists(string path) => File.Exists(path);

        /// <inheritdoc />
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    }
}
