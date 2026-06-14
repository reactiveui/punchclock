// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using PublicApiGenerator;
using VerifyTUnit;

namespace Punchclock.APITests;

/// <summary>A helper for doing API approvals.</summary>
public static class ApiExtensions
{
    /// <summary>API approval members for assemblies.</summary>
    /// <param name="assembly">The assembly that is being checked.</param>
    extension(Assembly assembly)
    {
        /// <summary>Checks to make sure the API is approved.</summary>
        /// <param name="namespaces">The namespaces.</param>
        /// <param name="filePath">The caller file path.</param>
        /// <returns>
        /// A Task.
        /// </returns>
        public async Task CheckApproval(string[] namespaces, [CallerFilePath] string filePath = "")
        {
            var generatorOptions = new ApiGeneratorOptions { AllowNamespacePrefixes = namespaces };
            var apiText = assembly.GeneratePublicApi(generatorOptions);
            await Verifier.Verify(apiText, null, filePath)
                .UniqueForRuntimeAndVersion()
                .ScrubEmptyLines()
                .ScrubLines(l =>
                    l.StartsWith("[assembly: AssemblyVersion(", StringComparison.InvariantCulture) ||
                    l.StartsWith("[assembly: AssemblyFileVersion(", StringComparison.InvariantCulture) ||
                    l.StartsWith("[assembly: AssemblyInformationalVersion(", StringComparison.InvariantCulture) ||
                    l.StartsWith("[assembly: System.Reflection.AssemblyMetadata(", StringComparison.InvariantCulture));
        }
    }
}
