using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace ICN_T2.Tools;

/// <summary>
/// [최적화 완료] Enum 처리 도우미
/// - 리플렉션 캐싱 적용 (최초 1회만 연산, 이후 조회 속도 O(1))
/// - 확장 메서드 지원 (value.GetDescription())
/// </summary>
public static class EnumHelper
{
    // [핵심 기술] 제네릭 정적 클래스를 이용한 캐싱
    // T(Enum 타입)마다 별도의 정적 메모리 공간이 생성되는 C#의 특성을 이용함
    private static class Cache<T> where T : struct, Enum
    {
        public static readonly (T Value, string Name)[] Values;
        public static readonly Dictionary<T, string> Descriptions;

        // 정적 생성자: 이 타입(T)을 처음 건드릴 때 딱 한 번 실행됨
        static Cache()
        {
            var enumValues = Enum.GetValues<T>();
            var type = typeof(T);

            Values = new (T, string)[enumValues.Length];
            Descriptions = new Dictionary<T, string>(enumValues.Length);

            for (int i = 0; i < enumValues.Length; i++)
            {
                T val = enumValues[i];
                string name = val.ToString();

                // Description 속성(Attribute) 가져오기
                var field = type.GetField(name);
                if (field != null)
                {
                    var attr = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attr != null)
                    {
                        name = attr.Description;
                    }
                }

                Values[i] = (val, name);
                Descriptions[val] = name; // 딕셔너리에 저장
            }
        }
    }

    /// <summary>
    /// Enum의 모든 값과 이름(Description)을 가져옵니다. (캐싱됨)
    /// UI 콤보박스 바인딩에 최적화되어 있습니다.
    /// </summary>
    public static (T Value, string Name)[] GetValues<T>() where T : struct, Enum
    {
        // 캐시에서 즉시 반환 (Zero-Allocation)
        return Cache<T>.Values;
    }

    /// <summary>
    /// Enum 값의 Description을 가져옵니다. (확장 메서드)
    /// 예: YokaiRank.S.GetDescription() -> "S랭크"
    /// </summary>
    public static string GetDescription<T>(this T value) where T : struct, Enum
    {
        // 딕셔너리에서 즉시 조회 (O(1))
        if (Cache<T>.Descriptions.TryGetValue(value, out var description))
        {
            return description;
        }
        return value.ToString();
    }
}