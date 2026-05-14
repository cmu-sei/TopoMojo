// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TopoMojo.Hypervisor.vSphere;

/// <summary>
/// Wraps a stream to provide progress callbacks during read operations
/// </summary>
public class ProgressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Action<long> _progressCallback;
    private long _bytesRead;

    public ProgressStream(Stream baseStream, Action<long> progressCallback)
    {
        _baseStream = baseStream;
        _progressCallback = progressCallback;
        _bytesRead = 0;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytesRead;
        _progressCallback?.Invoke(_bytesRead);
        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _baseStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;
        _progressCallback?.Invoke(_bytesRead);
        return bytesRead;
    }

    // Delegate all other operations to base stream
    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }
    public override void Flush() => _baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => _baseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _baseStream.Dispose();
        base.Dispose(disposing);
    }
}
