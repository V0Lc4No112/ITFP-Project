using System;
using System.IO;
using System.Linq;
using Rental.App.Io;
using Rental.Domain.Results;
using Xunit;

namespace Rental.Tests;

public class ResourceTests
{
    private const string Text =
        "ItemCode,Available\nBIKE-01,3\nSKI-02,1";

    private sealed class ProbeReader(
        string text,
        int failOnLine = 0) : StringReader(text)
    {
        public int Reads { get; private set; }
        public bool Disposed { get; private set; }

        public override string? ReadLine()
        {
            Reads++;

            if (Reads == failOnLine)
                throw new IOException("disk read error");

            return base.ReadLine();
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ProbeWriter(
        bool failOnWrite) : StringWriter
    {
        public bool Disposed { get; private set; }

        public override void WriteLine(string? value)
        {
            if (failOnWrite)
                throw new IOException("disk full");

            base.WriteLine(value);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void StreamLines_NotEnumerated_DoesNotOpenTheSource()
    {
        int opens = 0;

        var lines = DataFile.StreamLines(() =>
        {
            opens++;
            return new StringReader(Text);
        });

        Assert.Equal(0, opens);

        Assert.Equal(3, lines.Count());
        Assert.Equal(1, opens);
    }

    [Fact]
    public void StreamLines_FullEnumeration_DisposesReader()
    {
        var reader = new ProbeReader(Text);

        var lines = DataFile.StreamLines(() => reader).ToList();

        Assert.Equal(3, lines.Count);
        Assert.Equal("ItemCode,Available", lines[0]);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void StreamLines_EarlyStop_DisposesReaderAndStopsReading()
    {
        var reader = new ProbeReader(Text);

        var first = DataFile.StreamLines(() => reader).First();

        Assert.Equal("ItemCode,Available", first);
        Assert.Equal(1, reader.Reads);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void ReadLines_ReaderFailsMidway_FailsAndDisposesReader()
    {
        var reader = new ProbeReader(Text, failOnLine: 2);

        var result = DataFile.ReadLines(
            "stock",
            "stock.csv",
            () => reader);

        var fail =
            Assert.IsType<Result<IReadOnlyList<string>>.Fail>(result);

        Assert.Equal(ErrorCode.FileUnreadable, fail.Error.Code);
        Assert.Equal(
            "Cannot load stock file 'stock.csv': disk read error",
            fail.Error.Message);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void WriteLines_Success_WritesEveryLineAndReleasesTheFile()
    {
        using var temp = new TempFolder();

        string path = temp.PathOf("report.csv");
        string[] lines = ["Header", "Row1"];

        var result = DataFile.WriteLines(
            "report", path, lines);

        var ok = Assert.IsType<Result<int>.Ok>(result);

        Assert.Equal(2, ok.Value);
        Assert.Equal(lines, File.ReadAllLines(path));

        using var stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void WriteLines_WriterFails_FailsAndDisposesWriter()
    {
        var writer = new ProbeWriter(failOnWrite: true);

        var result = DataFile.WriteLines(
            "report",
            "report.csv",
            new[] { "Header" },
            () => writer);

        var fail = Assert.IsType<Result<int>.Fail>(result);

        Assert.Equal(ErrorCode.FileUnwritable, fail.Error.Code);
        Assert.True(fail.Error.IsOperational);
        Assert.True(writer.Disposed);
    }

    [Fact]
    public void WriteLines_MissingFolder_FailsAndCreatesNothing()
    {
        using var temp = new TempFolder();

        string path = Path.Combine(
            temp.PathOf("no-folder"),
            "report.csv");

        var result = DataFile.WriteLines(
            "report",
            path,
            new[] { "Header" });

        var fail = Assert.IsType<Result<int>.Fail>(result);

        Assert.Equal(ErrorCode.FileUnwritable, fail.Error.Code);
        Assert.Equal(
            $"Cannot write report file '{path}': folder not found",
            fail.Error.Message);
        Assert.False(File.Exists(path));
    }
}
