using System;
using ICN_T2.Logic.VirtualFileSystem;

namespace ICN_T2.Logic.Level5.Archives;

/// <summary>
/// Level-5 아카이브(ARC0, XPCK 등) 공통 인터페이스
/// - UI 의존성(Windows.Forms) 제거
/// - IDisposable 상속으로 리소스 관리 강화
/// </summary>
public interface IArchive : IDisposable
{
    /// <summary>
    /// 아카이브 형식 이름 (예: "ARC0", "XPCK")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 가상 파일 시스템의 루트 디렉토리
    /// </summary>
    VirtualDirectory Directory { get; set; }

    /// <summary>
    /// 아카이브를 파일로 저장합니다.
    /// </summary>
    /// <param name="path">저장할 파일 경로</param>
    /// <param name="progressCallback">진행률 콜백 (선택 사항)</param>
    void Save(string path);
    // 주의: 원래 progressCallback이 있었지만, 
    // 현재 XPCK/ARC0 구현체들이 기본 Save(string)만 구현하고 있으므로
    // 인터페이스도 일단 단순화하거나, 구현체에 오버로딩을 추가해야 합니다.
    // 여기서는 가장 기본적인 형태인 Save(path)로 통일합니다.

    /// <summary>
    /// 스트림을 닫고 리소스를 해제합니다.
    /// (Dispose()와 같은 역할)
    /// </summary>
    void Close();
}