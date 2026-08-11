namespace Cleansia.Tests.Logging;

/// <summary>
/// A request body that counts the bytes actually pulled out of it — the only observable that separates a
/// bounded read from an unbounded one, since both emit the identical log line and
/// <c>GC.GetAllocatedBytesForCurrentThread()</c> is per-thread while the read's continuation hops
/// thread-pool threads.
///
/// <see cref="CanSeek"/> / <see cref="Position"/> / <see cref="Seek"/> forward to an inner
/// <see cref="MemoryStream"/> so the middleware takes its production seekable branch rather than the
/// <c>EnableBuffering</c> fallback. The counter is deliberately NOT reset by a seek: it accumulates
/// everything the middleware read, which is exactly the quantity under test.
/// </summary>
internal sealed class CountingRequestBodyStream(byte[] payload) : Stream
{
    private readonly MemoryStream _inner = new(payload, writable: false);

    public long BytesRead { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override int Read(byte[] buffer, int offset, int count) =>
        Counted(_inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Counted(_inner.Read(buffer));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Counted(_inner.Read(buffer, offset, count)));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Counted(_inner.Read(buffer.Span)));

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Counted(int read)
    {
        BytesRead += read;
        return read;
    }
}
