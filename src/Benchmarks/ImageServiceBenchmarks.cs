using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using ImageMagick;
using SkiaSharp;

namespace Benchmarks;

[ShortRunJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[NativeMemoryProfiler]
[HideColumns("Error", "StdDev", "RatioSD")]
public class ImageServiceBenchmarks
{
    // Keep the underlying streams open for reuse in consecutive benchmarks
    private static readonly NonClosableMemoryStream SourceStream = new();

    // Pre-allocate a large buffer to avoid resizing during the benchmark
    private static readonly NonClosableMemoryStream DestinationStream = new(capacity: 4 * 1024 * 1024);

    // Ensure consistent output quality across all libraries
    private const int OutputQuality = 75;

    private const int OutputWidth = 256;
    private const int OutputHeight = 256;

    private static readonly MagickGeometry MagickOutputSize = new(OutputWidth, OutputHeight);

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await using var stream = File.OpenRead("SampleJpeg-10.jpg");

        if (stream == null)
        {
            throw new InvalidOperationException("Resource not found.");
        }

        await stream.CopyToAsync(SourceStream);
    }

    [Benchmark(Baseline = true)]
    public void SkiaSharpResize()
    {
        ResetStreams();

        using var image = SKBitmap.Decode(SourceStream);
        using var resized = new SKBitmap(OutputWidth, OutputHeight);
        using var canvas = new SKCanvas(resized);

        canvas.DrawBitmap(image, new SKRect(0, 0, OutputWidth, OutputHeight));

        resized.Encode(DestinationStream, SKEncodedImageFormat.Jpeg, OutputQuality);
    }

    [Benchmark]
    public async Task MagickNetResize()
    {
        ResetStreams();

        using var image = new MagickImage(SourceStream);

        image.Quality = OutputQuality;
        image.Resize(MagickOutputSize);
        image.Strip();

        await image.WriteAsync(DestinationStream);
    }

    private static void ResetStreams()
    {
        SourceStream.Seek(0, SeekOrigin.Begin);
        DestinationStream.Seek(0, SeekOrigin.Begin);
    }
}