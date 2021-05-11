// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.CommandLine
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    internal class VersionCommand : ICommandLineCommand
    {
        public bool ShowLogo => true;

        public bool StopParsing => true;

        public Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine(ThisAssembly.AssemblyInformationalVersion);

            return Task.FromResult(0);
        }

        public bool TryParseArgument(ICommandLineParser parseHelper, string argument) => true; // eat any arguments
    }
}
