using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ICN_T2.Tools.IO;

public class BinaryDataReader : IDisposable
{
    private Stream _stream;

    public bool BigEndian { get; set; } = false;

    public long Length => _stream.Length;

    public Stream BaseStream => _stream;

    public long Position => _stream.Position;

    public BinaryDataReader(byte[] data)
    {
        _stream = new MemoryStream(data);
    }

    public BinaryDataReader(Stream stream)
    {
        _stream = stream;
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }

    /* --- 읽기 핵심 메서드 --- */

    public T ReadValue<T>() where T : struct
    {
        // byte 타입에 대한 특수 처리 유지
        if (typeof(T) == typeof(byte))
        {
            return (T)(object)(byte)_stream.ReadByte();
        }

        return ReadStruct<T>();
    }

    public T[] ReadMultipleValue<T>(int count) where T : struct
    {
        return Enumerable.Range(0, count).Select(_ => ReadValue<T>()).ToArray();
    }

    public T ReadStruct<T>() where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];
        _stream.ReadExactly(bytes, 0, size);

        if (BigEndian)
        {
            Array.Reverse(bytes);
        }

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public T[] ReadMultipleStruct<T>(int count) where T : struct
    {
        return Enumerable.Range(0, count).Select(_ => ReadStruct<T>()).ToArray();
    }

    public string ReadString(Encoding encoding)
    {
        List<byte> bytes = new List<byte>();
        int b;

        // 0x00(Null)을 만날 때까지 읽음
        while ((b = _stream.ReadByte()) != 0x0 && _stream.Position < _stream.Length)
        {
            bytes.Add((byte)b);
        }

        return encoding.GetString(bytes.ToArray());
    }

    /* --- 탐색 및 위치 제어 --- */

    public void Skip(uint size)
    {
        _stream.Seek(size, SeekOrigin.Current);
    }

    public void Seek(uint position)
    {
        _stream.Seek(position, SeekOrigin.Begin);
    }

    public byte[] GetSection(int size)
    {
        byte[] data = new byte[size];
        _stream.ReadExactly(data, 0, data.Length);
        return data;
    }

    public byte[] GetSection(uint offset, int size)
    {
        long temp = _stream.Position;
        Seek(offset);
        byte[] data = new byte[size];
        _stream.ReadExactly(data, 0, data.Length);
        Seek((uint)temp);
        return data;
    }

    public long Find<T>(T search, uint start) where T : struct, IEquatable<T>
    {
        int sizeOfT = Marshal.SizeOf<T>();
        int count = (int)(_stream.Length - start) / sizeOfT;

        long temp = _stream.Position;
        Seek(start);

        T[] tableSearch = ReadMultipleStruct<T>(count);
        int foundIndex = Array.IndexOf(tableSearch, search);

        Seek((uint)temp);

        return foundIndex != -1 ? start + (foundIndex * sizeOfT) : -1;
    }

    public void SeekOf<T>(T search, uint start) where T : struct, IEquatable<T>
    {
        long pos = Find(search, start);

        if (pos != -1)
        {
            Seek((uint)pos);
        }
        else
        {
            throw new IndexOutOfRangeException($"Could not find the specified value of type {typeof(T).Name}");
        }
    }

    public void PrintPosition()
    {
        Console.WriteLine($"Current Position: 0x{_stream.Position:X}");
    }
}