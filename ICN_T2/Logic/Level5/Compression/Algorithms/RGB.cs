using System;
using System.Runtime.CompilerServices;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// RGB 색상 구조체
/// - Target-typed new 적용으로 코드 단순화
/// - 불필요한 패딩 제거 및 인라인 최적화
/// </summary>
public readonly struct RGB
{
    public readonly byte R, G, B;
    
    public RGB(int r, int g, int b)
    {
        R = Clamp(r);
        G = Clamp(g);
        B = Clamp(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Clamp(int val) 
        => (byte)((val < 0) ? 0 : (val > 255) ? 255 : val);

    // [단순화] new RGB(...) -> new(...)
    public static RGB operator +(RGB c, int mod) 
        => new(c.R + mod, c.G + mod, c.B + mod);

    public static int operator -(RGB c1, RGB c2) 
        => ErrorRGB(c1.R - c2.R, c1.G - c2.G, c1.B - c2.B);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ErrorRGB(int r, int g, int b) 
        => (r * r) + (g * g) + (b * b);

    // [단순화] if-else를 삼항 연산자 + 식 본문(Expression Body)으로 압축
    public RGB Scale(int limit) => limit == 16
        ? new(R * 17, G * 17, B * 17)
        : new((R << 3) | (R >> 2), (G << 3) | (G >> 2), (B << 3) | (B >> 2));

    public RGB Unscale(int limit) 
        => new(R * limit / 256, G * limit / 256, B * limit / 256);

    public override string ToString() => $"RGB({R},{G},{B})";
}