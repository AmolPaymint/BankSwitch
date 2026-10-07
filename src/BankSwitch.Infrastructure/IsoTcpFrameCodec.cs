using System.Buffers.Binary;

namespace BankSwitch.Infrastructure;

public static class IsoTcpFrameCodec
{
    public static async Task<byte[]> ReadFrameAsync(Stream stream, int headerBytes, int maxMessageBytes, TimeSpan idleTimeout, CancellationToken cancellationToken)
    {
        if (headerBytes is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(headerBytes), "Header must be 2 or 4 bytes.");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(idleTimeout);
        var header = await ReadExactAsync(stream, headerBytes, timeoutCts.Token).ConfigureAwait(false);
        var length = headerBytes == 2 ? BinaryPrimitives.ReadUInt16BigEndian(header) : (int)BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length <= 0) throw new InvalidDataException("ISO frame length must be positive.");
        if (length > maxMessageBytes) throw new InvalidDataException($"ISO frame length {length} exceeds max {maxMessageBytes}.");
        return await ReadExactAsync(stream, length, timeoutCts.Token).ConfigureAwait(false);
    }

    public static async Task WriteFrameAsync(Stream stream, byte[] payload, int headerBytes, CancellationToken cancellationToken)
    {
        if (payload.Length <= 0) throw new InvalidDataException("Cannot send an empty ISO frame.");
        if (headerBytes is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(headerBytes), "Header must be 2 or 4 bytes.");
        var header = new byte[headerBytes];
        if (headerBytes == 2)
        {
            if (payload.Length > ushort.MaxValue) throw new InvalidDataException("Payload too large for two-byte header.");
            BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)payload.Length);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
        }
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException("Peer closed the socket before a complete ISO frame was read.");
            read += count;
        }
        return buffer;
    }
}
