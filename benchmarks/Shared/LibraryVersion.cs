// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;

namespace MarkView.Benchmarks.Shared;

public static class LibraryVersion
{
    /// <summary>"Name version" of the assembly defining <paramref name="type"/>, from its informational version (which carries the commit for a local build).</summary>
    public static string Of(Type type, string name)
    {
        var assembly = type.Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        var plus = version?.IndexOf('+') ?? -1;
        if (plus >= 0 && version!.Length > plus + 8)
            version = version[..(plus + 8)];
        return $"{name} {version}";
    }
}
