using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ICN_T2.Logic.VirtualFileSystem;

/// <summary>
/// [최적화 완료] 가상 디렉토리 구조 검증기
/// - 문자열 할당 없는(Zero-Allocation) 순회 검사
/// - 순환 참조(Cycle) 감지 기능 추가
/// - 저장 전 필수 무결성 체크 수행
/// </summary>
public static class VirtualDirectoryDebugger
{
    // 파일명에 들어가면 안 되는 문자들 (OS + 레벨5 포맷 기준)
    private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars()
        .Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) // 명시적 차단
        .Distinct()
        .ToArray();

    public static void Validate(VirtualDirectory root)
    {
        var errors = new List<string>();
        var visited = new HashSet<VirtualDirectory>(); // 순환 참조 방지용
        var pathStack = new Stack<string>(); // 현재 경로 추적용 (문자열 결합 방지)

        // 루트 이름이 비어있으면 (보통 루트는 이름이 없지만, 명시적 확인)
        pathStack.Push(string.IsNullOrEmpty(root.Name) ? "[ROOT]" : root.Name);

        try
        {
            ValidateRecursive(root, pathStack, visited, errors);
        }
        finally
        {
            pathStack.Pop();
        }

        // 에러 보고
        if (errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ [VirtualDirectory Validation FAILED]");
            Console.WriteLine("========================================");
            foreach (var e in errors)
            {
                Console.WriteLine($" - {e}");
            }
            Console.WriteLine("========================================");
            Console.ResetColor();

            throw new InvalidOperationException(
                $"파일 구조에 치명적인 오류가 {errors.Count}건 발견되어 저장을 중단합니다.\n(자세한 내용은 콘솔 로그 확인)"
            );
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ [VirtualDirectory Integrity OK]");
        Console.ResetColor();
    }

    private static void ValidateRecursive(
        VirtualDirectory dir,
        Stack<string> pathStack,
        HashSet<VirtualDirectory> visited,
        List<string> errors)
    {
        // 1. 순환 참조 검사 (무한 루프 방지)
        if (!visited.Add(dir))
        {
            errors.Add($"[CRITICAL] Circular Reference detected at: {BuildPath(pathStack)}");
            return;
        }

        // 2. 파일 검사 (이름 규칙)
        if (dir.Files != null)
        {
            foreach (var fileName in dir.Files.Keys)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    errors.Add($"[FILE] Empty filename at: {BuildPath(pathStack)}");
                }
                else if (HasInvalidChars(fileName))
                {
                    errors.Add($"[FILE] Invalid characters in '{fileName}' at: {BuildPath(pathStack)}");
                }

                // 파일 데이터가 null인 경우 체크 (데이터 유실 방지)
                var stream = dir.Files[fileName];
                if (stream == null)
                {
                    errors.Add($"[FILE] Stream is NULL for '{fileName}' at: {BuildPath(pathStack)}");
                }
            }
        }

        // 3. 하위 폴더 검사 (재귀)
        if (dir.Folders != null)
        {
            foreach (var subDir in dir.Folders.Values)
            {
                string folderName = subDir.Name;

                // 폴더 이름 유효성 검사
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    errors.Add($"[DIR] Empty folder name inside: {BuildPath(pathStack)}");
                    // 이름이 없어도 일단 내부는 검사 시도
                    pathStack.Push("???");
                }
                else if (HasInvalidChars(folderName))
                {
                    errors.Add($"[DIR] Invalid characters in folder '{folderName}' at: {BuildPath(pathStack)}");
                    pathStack.Push(folderName);
                }
                else
                {
                    pathStack.Push(folderName);
                }

                ValidateRecursive(subDir, pathStack, visited, errors);

                pathStack.Pop(); // Backtrack
            }
        }
    }

    // 에러 발생 시에만 호출되어 전체 경로 문자열을 생성 (최적화 핵심)
    private static string BuildPath(Stack<string> pathStack)
    {
        // Stack은 LIFO이므로 뒤집어서 결합해야 올바른 경로가 됨
        return string.Join("/", pathStack.Reverse());
    }

    // Span을 사용한 고성능 문자 검사
    private static bool HasInvalidChars(string name)
    {
        return name.IndexOfAny(_invalidChars) >= 0;
    }
}