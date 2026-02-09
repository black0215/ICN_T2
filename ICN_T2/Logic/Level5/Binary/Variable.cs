using System;

namespace ICN_T2.Logic.Level5.Binary;
//CfgBin 엔트리 내의 개별 변수 값
public class Variable
{
    public EntryType Type { get; set; }
    public object Value { get; set; }

    public Variable(EntryType type, object value)
    {
        Type = type;
        Value = value;
    }

    // [편의 기능] 원하는 타입으로 값 꺼내기 (예: var i = variable.GetValue<int>();)
    public T GetValue<T>()
    {
        return (T)Convert.ChangeType(Value, typeof(T));
    }

    public override string ToString()
    {
        return $"{Type}: {Value}";
    }
}