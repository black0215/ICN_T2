using System;
using System.IO;
using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace ICN_T2.Tools.IO;

/* * 파일 소스 인터페이스
 * 로우 레벨 최적화 적용: Span<byte> 및 RandomAccess 지원
 */
public interface IFileSource : IDisposable
{
    long Length { get; }
    void CopyTo(Stream destination);
    int ReadToSpan(Span<byte> buffer);
    byte[] ToArray();
}

/* * 메모리 소스 (작은 파일용) 
 */
public sealed class MemorySource : IFileSource
{
    private readonly ReadOnlyMemory<byte> _memory;

    public long Length => _memory.Length;

    public MemorySource(byte[]? data)
    {
        _memory = new ReadOnlyMemory<byte>(data ?? Array.Empty<byte>());
    }

    public MemorySource(byte[] data, int offset, int count)
    {
        _memory = new ReadOnlyMemory<byte>(data, offset, count);
    }

    public void CopyTo(Stream destination)
    {
        if (_memory.IsEmpty) return;
        destination.Write(_memory.Span);
    }

    public int ReadToSpan(Span<byte> buffer)
    {
        int toCopy = Math.Min(buffer.Length, _memory.Length);
        _memory.Span.Slice(0, toCopy).CopyTo(buffer);
        return toCopy;
    }

    public byte[] ToArray()
    {
        return _memory.ToArray();
    }

    public void Dispose()
    {
        // GC가 관리하므로 처리 불필요
    }
}

/* * 파일 세그먼트 소스 (대용량 파일용 최적화)
 * 경고 해결: 필드 초기화 및 Nullable 처리 완료
 */
public sealed class FileSegmentSource : IFileSource
{
    private readonly SafeFileHandle _handle;
    private readonly long _offset;
    private readonly long _length;
    private readonly bool _ownsHandle;

    public long Length => _length;

    public FileSegmentSource(string filePath, long offset, long length)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

        // RandomAccess 옵션으로 파일 핸들 열기
        var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);

        _handle = fs.SafeFileHandle;
        _offset = offset;
        _length = length;
        _ownsHandle = true;

        // FileStream 변수 fs는 여기서 사라지지만, _handle이 살아있어 파일은 유지됩니다.
    }

    public void CopyTo(Stream destination)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920); // 80KB 버퍼
        try
        {
            long remaining = _length;
            long currentFileOffset = _offset;

            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                int read = RandomAccess.Read(_handle, buffer.AsSpan(0, toRead), currentFileOffset);

                if (read == 0) break;

                destination.Write(buffer, 0, read);
                remaining -= read;
                currentFileOffset += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public int ReadToSpan(Span<byte> buffer)
    {
        int toRead = (int)Math.Min(buffer.Length, _length);
        return RandomAccess.Read(_handle, buffer.Slice(0, toRead), _offset);
    }

    public byte[] ToArray()
    {
        byte[] buffer = new byte[_length];
        RandomAccess.Read(_handle, buffer, _offset);
        return buffer;
    }

    public void Dispose()
    {
        if (_ownsHandle && _handle != null && !_handle.IsInvalid)
        {
            _handle.Dispose();
        }
    }
}