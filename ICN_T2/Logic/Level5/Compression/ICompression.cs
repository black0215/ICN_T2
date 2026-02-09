namespace ICN_T2.Logic.Level5.Compression;

/// <summary>
/// 모든 압축 알고리즘이 따라야 할 표준 인터페이스
/// </summary>
public interface ICompression
{
    byte[] Compress(byte[] data);
    byte[] Decompress(byte[] data);
}