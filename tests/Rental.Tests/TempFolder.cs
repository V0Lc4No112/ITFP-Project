using System;
using System.IO;

namespace Rental.Tests;

public sealed class TempFolder : IDisposable
{
    public string Path { get; } =
        Directory.CreateTempSubdirectory("rental-tests-").FullName;

    public string Write(string name, params string[] lines)
    {
        string path = PathOf(name);
        File.WriteAllLines(path, lines);
        return path;
    }

    public string PathOf(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
