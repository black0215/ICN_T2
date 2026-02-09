using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ICN_T2.Tools.External;

/* * 외부 도구(XtractQuery) 실행 래퍼
 * - 비동기 실행(Async) 지원으로 UI 프리징 방지
 * - UI 의존성 제거 (MessageBox 삭제)
 */
public static class XtractQueryWrapper
{
    // 실행 파일 경로 (캐싱)
    private static readonly string _toolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "XtractQuery.exe");

    /// <summary>
    /// XtractQuery를 비동기로 실행하여 파일을 컴파일/디컴파일합니다.
    /// </summary>
    /// <param name="filePath">대상 파일 경로</param>
    /// <param name="mode">"d" (Decompile) 또는 "c" (Compile)</param>
    /// <param name="type">포맷 타입 (예: "xq32")</param>
    /// <returns>성공 시 출력 파일 경로, 실패 시 null</returns>
    public static async Task<string?> RunAsync(string filePath, string mode = "d", string type = "xq32")
    {
        if (!File.Exists(_toolPath))
        {
            throw new FileNotFoundException($"XtractQuery.exe를 찾을 수 없습니다.\n경로: {_toolPath}");
        }

        string workingDir = Path.GetDirectoryName(_toolPath)!;
        string fileName = Path.GetFileName(filePath);
        string targetPath = Path.Combine(workingDir, fileName);

        // 1. 도구가 있는 폴더로 파일 복사 (외부 툴의 상대 경로 의존성 해결)
        // 이미 같은 곳에 있다면 복사 건너뜀
        bool tempCopyCreated = false;
        if (!string.Equals(filePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(filePath, targetPath, true);
            tempCopyCreated = true;
        }

        try
        {
            // 2. 프로세스 실행 설정
            var psi = new ProcessStartInfo
            {
                FileName = _toolPath,
                // 예: -o d -t xq32 -f "filename.bin"
                Arguments = $"-o {mode} -t {type} -f \"{fileName}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // 비동기로 출력 스트림 읽기 (데드락 방지)
                var outputTask = p.StandardOutput.ReadToEndAsync();
                var errorTask = p.StandardError.ReadToEndAsync();

                // 프로세스 종료 대기 (비동기) - UI가 멈추지 않음!
                await p.WaitForExitAsync();

                string output = await outputTask;
                string error = await errorTask;

                if (p.ExitCode != 0)
                {
                    // 로그에 에러 기록 (콘솔 혹은 파일)
                    Console.WriteLine($"[XtractQuery Error] {error}\n{output}");
                    return null; // 실패 시 null 반환
                }
            }

            // 3. 예상 출력 파일명 추론
            // 디컴파일(d) -> .txt 붙음 / 컴파일(c) -> .txt 떼짐 (툴 특성에 따라 조정 필요)
            string resultFile;
            if (mode == "d")
            {
                resultFile = targetPath + ".txt";
            }
            else
            {
                // 컴파일의 경우 보통 확장자가 바뀜. 여기선 단순하게 처리
                resultFile = targetPath.Replace(".txt", "");
                if (!File.Exists(resultFile) && File.Exists(targetPath + ".bin"))
                    resultFile = targetPath + ".bin";
            }

            return File.Exists(resultFile) ? resultFile : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Wrapper Exception] {ex.Message}");
            return null;
        }
        finally
        {
            // (옵션) 임시로 복사했던 원본 파일 정리
            // 컴파일 결과물이 필요해서 남겨야 한다면 이 부분은 주석 처리하세요.
            if (tempCopyCreated && File.Exists(targetPath))
            {
                // File.Delete(targetPath); 
            }
        }
    }
}