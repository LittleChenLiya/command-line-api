// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Reads the raw contents of a response file. Implementations can be injected into
    /// <see cref="ResponseFileExpansionOptions.FileReader"/> to customize how response files
    /// are read (for example, for testing or to read from a virtual file system).
    /// </summary>
    public interface IResponseFileReader
    {
        /// <summary>
        /// Reads the full contents of the response file at the specified (already normalized) path.
        /// </summary>
        /// <param name="path">The normalized full path of the response file.</param>
        /// <returns>The raw bytes of the response file.</returns>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="IOException">The file could not be read.</exception>
        byte[] ReadAllBytes(string path);
    }
}
