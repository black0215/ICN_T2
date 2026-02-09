namespace ICN_T2.Logic.Level5.Binary;

// 변수 타입 정의
// (주의: System.Type과 이름 충돌을 피하기 위해 EntryType으로 변경)
public enum EntryType : byte
{
    // Unknown = 0, // Removed or remapped if needed, but standard Level-5 typically uses 0 for String, 1 for Int, 2 for Float
    String = 0,
    Int = 1,
    Float = 2,
    Unknown = 0xFF // Or typically not used in binary
}