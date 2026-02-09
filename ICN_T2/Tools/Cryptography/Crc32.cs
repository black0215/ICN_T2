using System;
using System.Security.Cryptography;

namespace ICN_T2.Tools;

/// <summary>
/// [최적화 완료] CRC32 해시 계산기
/// - Span<byte> 지원으로 제로 카피 계산 가능
/// - 루프 전개(Loop Unrolling) 및 최적화된 테이블 접근
/// - HashAlgorithm 상속 유지 (호환성)
/// </summary>
public sealed class Crc32 : HashAlgorithm
{
    public const uint DefaultPolynomial = 0xedb88320u;
    public const uint DefaultSeed = 0xffffffffu;

    private static uint[]? _defaultTable;
    private readonly uint _seed;
    private readonly uint[] _table;
    private uint _hash;

    public Crc32() : this(DefaultPolynomial, DefaultSeed) { }

    public Crc32(uint polynomial, uint seed)
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Big Endian 시스템은 지원하지 않습니다.");

        _table = InitializeTable(polynomial);
        _seed = _hash = seed;
    }

    public override void Initialize()
    {
        _hash = _seed;
    }

    protected override void HashCore(byte[] array, int ibStart, int cbSize)
    {
        // 배열을 Span으로 변환하여 고속 처리 메서드로 전달
        HashCore(new ReadOnlySpan<byte>(array, ibStart, cbSize));
    }

    // .NET Core / .NET 5+ 최적화: Span 기반 해시 코어
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    protected override void HashCore(ReadOnlySpan<byte> source)
    {
        _hash = CalculateHash(_table, _hash, source);
    }
#else
    // 구형 .NET 호환용 오버로드 (필요시 사용)
    private void HashCore(ReadOnlySpan<byte> source)
    {
        _hash = CalculateHash(_table, _hash, source);
    }
#endif

    protected override byte[] HashFinal()
    {
        var hashBuffer = BitConverter.GetBytes(~_hash);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(hashBuffer); // 해시는 보통 Big-Endian으로 출력

        HashValue = hashBuffer;
        return hashBuffer;
    }

    public override int HashSize => 32;

    // ---------------------------------------------------------
    // Static Convenience Methods (가장 많이 쓰임)
    // ---------------------------------------------------------

    public static uint Compute(byte[] buffer)
    {
        return Compute(DefaultSeed, buffer);
    }

    public static uint Compute(uint seed, byte[] buffer)
    {
        return Compute(DefaultPolynomial, seed, new ReadOnlySpan<byte>(buffer));
    }

    // [최적화] Span을 직접 받는 고속 계산 메서드
    public static uint Compute(ReadOnlySpan<byte> buffer)
    {
        return Compute(DefaultPolynomial, DefaultSeed, buffer);
    }

    public static uint Compute(uint polynomial, uint seed, ReadOnlySpan<byte> buffer)
    {
        return ~CalculateHash(InitializeTable(polynomial), seed, buffer);
    }

    // ---------------------------------------------------------
    // Core Logic
    // ---------------------------------------------------------

    private static uint[] InitializeTable(uint polynomial)
    {
        if (polynomial == DefaultPolynomial)
        {
            if (_defaultTable != null) return _defaultTable;
            // 레이스 컨디션 발생해도 결과는 같으므로 락 없이 진행 (성능 우선)
        }

        var createTable = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var j = 0; j < 8; j++)
                if ((entry & 1) == 1)
                    entry = (entry >> 1) ^ polynomial;
                else
                    entry >>= 1;
            createTable[i] = entry;
        }

        if (polynomial == DefaultPolynomial)
            _defaultTable = createTable;

        return createTable;
    }

    // [핵심 최적화 구간] JIT가 이 루프를 최대한 최적화하도록 유도
    private static uint CalculateHash(uint[] table, uint seed, ReadOnlySpan<byte> buffer)
    {
        var hash = seed;

        // 1. 작은 데이터는 그냥 처리
        if (buffer.Length < 16)
        {
            foreach (byte b in buffer)
            {
                hash = (hash >> 8) ^ table[(hash ^ b) & 0xff];
            }
            return hash;
        }

        // 2. 대량 데이터 처리 (루프 언롤링 효과 기대)
        int i = 0;
        int length = buffer.Length;

        // 8바이트씩 처리 (CPU 파이프라인 최적화)
        while (i + 8 <= length)
        {
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
        }

        // 남은 바이트 처리
        while (i < length)
        {
            hash = (hash >> 8) ^ table[(hash ^ buffer[i++]) & 0xff];
        }

        return hash;
    }
}