using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ICN_T2.Tools.IO;

public class BinaryDataWriter : IDisposable
{
    private Stream _stream;

    public bool BigEndian { get; set; } = false;

    public long Length => _stream.Length;

    public Stream BaseStream => _stream;

    public long Position => _stream.Position;

    public BinaryDataWriter(byte[] data)
    {
        _stream = new MemoryStream(data);
    }

    public BinaryDataWriter(Stream stream)
    {
        _stream = stream;
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }

    /* --- 위치 제어 --- */

    public void Skip(uint size)
    {
        _stream.Seek(size, SeekOrigin.Current);
    }

    public void Seek(uint position)
    {
        _stream.Seek(position, SeekOrigin.Begin);
    }

    public void PrintPosition()
    {
        Console.WriteLine($"Write Position: 0x{_stream.Position:X}");
    }

    /* --- 쓰기 메서드 (엔디안 지원 추가) --- */

    public void Write(byte[] data)
    {
        _stream.Write(data, 0, data.Length);
    }

    public void Write(byte value)
    {
        _stream.WriteByte(value);
    }

    public void Write(short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void Write(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, bytes.Length);
    }

    /* --- 고급 쓰기 기능 --- */

    public void WriteAlignment(int alignment = 16, byte alignmentByte = 0x0)
    {
        long remainder = _stream.Position % alignment;
        if (remainder == 0) return;

        int bytesToWrite = alignment - (int)remainder;
        for (int i = 0; i < bytesToWrite; i++)
        {
            _stream.WriteByte(alignmentByte);
        }
    }

    // 기본 패딩 (0x00 후 0xFF로 채움 - 요괴워치 포맷 특성)
    public void WriteAlignment()
    {
        Write((byte)0x00);
        WriteAlignment(16, 0xFF);
    }

    public void WriteStruct<T>(T structure) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free(); // 안전하게 메모리 해제
        }

        // Reader와 대칭되도록 전체 구조체 바이트 반전
        if (BigEndian)
        {
            Array.Reverse(bytes);
        }

        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteMultipleStruct<T>(IEnumerable<T> structures) where T : struct
    {
        foreach (T structure in structures)
        {
            WriteStruct(structure);
        }
    }

    // [추가 제안] 문자열 쓰기 기능 (없으면 나중에 불편할 수 있음)
    public void WriteString(string value, Encoding encoding)
    {
        if (string.IsNullOrEmpty(value)) return;
        byte[] bytes = encoding.GetBytes(value);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.WriteByte(0x00); // Null Terminator
    }
}