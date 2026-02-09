using System;
using System.Security.Cryptography;

namespace ICN_T2.Tools;

/// <summary>
/// [최적화 완료] 고성능 난수 생성기
/// - 일반 용도: Random.Shared 사용 (가장 빠름, 게임 로직용)
/// - 보안 용도: RandomNumberGenerator + Span 최적화 (암호화용)
/// </summary>
public static class RandomNumber
{
    // 1. [초고속] 일반적인 게임 로직, 아이템 드랍, 확률 계산용
    // .NET 최신 버전의 표준 방식입니다. 별도의 인스턴스 생성이 없습니다.
    public static int Next()
    {
        return Random.Shared.Next();
    }

    public static int Next(int maxValue)
    {
        return Random.Shared.Next(maxValue);
    }

    public static int Next(int minValue, int maxValue)
    {
        return Random.Shared.Next(minValue, maxValue);
    }

    // 2. [보안] 예측 불가능해야 하는 경우 (예: 암호화 키, 세션 ID)
    // 기존 코드의 문제점(매번 인스턴스 생성 + 힙 할당)을 제거하고
    // 스택 메모리(stackalloc)를 사용하여 가비지 컬렉터 부담을 0으로 만들었습니다.
    public static int NextSecure()
    {
        // 힙(Heap)에 메모리를 만들지 않고 스택(Stack)에서 즉시 처리 (Zero-Allocation)
        Span<byte> buffer = stackalloc byte[4];

        // 시스템의 암호학적 난수 생성기로 채움
        RandomNumberGenerator.Fill(buffer);

        // 정수로 변환
        return BitConverter.ToInt32(buffer);
    }
}