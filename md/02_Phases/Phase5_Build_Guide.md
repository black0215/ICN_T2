# 🚀 Phase 5 빌드 및 테스트 가이드

## ✅ Phase 5 완료 체크리스트

### 파일 생성 확인:
- ✅ `UI/WPF/Effects/GlassRefraction.fx` (HLSL 소스)
- ✅ `UI/WPF/Effects/GlassRefraction.ps` (컴파일된 바이너리 - 2408 bytes)
- ✅ `UI/WPF/Effects/GlassRefractionEffect.cs` (WPF 래퍼)

### 코드 수정 확인:
- ✅ `ModernModWindow.xaml.cs` - Shader 통합 코드 추가
  - `InitializeGlassRefractionShader()` 메서드
  - `Window_MouseMove_ShaderUpdate()` 이벤트 핸들러
  - `UpdateShaderAnimation()` 60 FPS 루프
- ✅ `ICN_T2.csproj` - Shader 리소스 등록

---

## 🛠️ 빌드 방법

### Visual Studio 사용:
```
1. Visual Studio에서 솔루션 열기
2. Ctrl+Shift+B (빌드)
3. F5 (디버그 실행) 또는 Ctrl+F5 (릴리스 실행)
```

### CLI 사용:
```bash
cd C:\Users\home\Desktop\ICN_T2
dotnet build
dotnet run --project ICN_T2\ICN_T2.csproj
```

---

## 🧪 테스트 순서

### 1단계: 빌드 성공 확인
```
빌드 출력 확인:
- [Resource] UI\WPF\Effects\GlassRefraction.ps
- 빌드 성공 메시지
```

### 2단계: 실행 및 로그 확인
```
디버그 출력 창에서 다음 로그 확인:
[GlassShader] 초기화 시작 (한글)
[GlassShader] ✅ CharacterInfoContent에 shader 적용 완료 (한글)
[GlassShader] ✅ 초기화 완료 - 60 FPS 애니메이션 시작 (한글)
```

### 3단계: UI 테스트
```
1. 프로젝트 선택 (아무 프로젝트나 선택 또는 새로 생성)
2. 모딩 메뉴 진입 (프로젝트 클릭)
3. "캐릭터 정보" 버튼 클릭
4. CharacterInfoV3 화면 표시 확인
5. 마우스 움직이기
6. **유리 굴절 효과 확인!** 🎉
```

---

## 🐛 문제 해결

### Shader 로드 실패
**증상**: `[GlassShader] Shader 로드 실패` 로그
**원인**: .ps 파일이 리소스로 임베드되지 않음
**해결**:
```bash
# .csproj 파일 확인
<Resource Include="UI\WPF\Effects\GlassRefraction.ps" />

# 리빌드
dotnet clean
dotnet build
```

### CharacterInfoContent null
**증상**: `[GlassShader] ⚠️ CharacterInfoContent가 null입니다`
**원인**: XAML에 x:Name="CharacterInfoContent" 없음
**해결**: 이미 XAML에 정의되어 있음 (ModernModWindow.xaml 라인 703)

### 효과가 보이지 않음
**증상**: 빌드 성공했지만 화면에 변화 없음
**원인**:
1. GPU 렌더링 비활성화
2. Shader Model 3.0 미지원 그래픽 카드
3. Software 렌더링 모드

**해결**:
```csharp
// App.xaml.cs 또는 ModernModWindow 생성자에서:
RenderOptions.ProcessRenderMode = RenderMode.Default;

// 하드웨어 가속 확인:
System.Windows.Media.RenderCapability.Tier
// 값이 2 (0x00020000)이면 Pixel Shader 3.0 지원
```

### 성능 저하
**증상**: UI가 느려짐
**원인**: Shader 연산 과부하
**해결**:
```csharp
// ModernModWindow.xaml.cs - InitializeGlassRefractionShader()에서:
RefractionStrength = 0.1;  // 왜곡 강도 낮춤
NoiseScale = 3.0;          // 노이즈 스케일 낮춤

// 또는 애니메이션 속도 낮춤:
_shaderAnimationTimer.Interval = TimeSpan.FromMilliseconds(33); // 30 FPS
```

---

## 🎯 효과 파라미터 조정

### 왜곡 강도 조절:
```csharp
// ModernModWindow.xaml.cs - InitializeGlassRefractionShader()
_glassRefractionEffect = new GlassRefractionEffect
{
    RefractionStrength = 0.5,  // 0.0 ~ 1.0 (기본 0.3)
    NoiseScale = 5.0,          // 1.0 ~ 10.0 (기본 5.0)
    // ...
};
```

### 애니메이션 속도 조절:
```csharp
// ModernModWindow.xaml.cs - UpdateShaderAnimation()
_shaderTime += 0.02;  // 0.001 ~ 0.05 (기본 0.01)
```

---

## 📊 성능 벤치마크

### 예상 성능:
```
CPU: 거의 없음 (1% 이하)
GPU: 낮음~중간 (5-15% 사용률)
FPS: 60 FPS 유지 (대부분의 환경)
메모리: +2~5 MB (Shader 캐싱)
```

### 최소 요구사항:
```
GPU: DirectX 9.0c 지원
Shader Model: 3.0 이상
드라이버: 최신 그래픽 드라이버 권장
```

---

## 🎨 전체 효과 조합

### 현재 적용된 모든 효과:
```
1. [Phase 1] Edge Glow
   - 마우스 위치 기반 반사광
   - LinearGradientBrush 동적 생성

2. [Phase 2] Spring Animation
   - ElasticEase 탄력 효과
   - 0.8초 부드러운 전환

3. [Phase 3] Top-Only Expansion
   - StepProgress 0.5 → 1.0
   - 80px 위쪽 상승

4. [Phase 4] Mica Backdrop
   - Windows 11 시스템 배경 동기화
   - DWM API 통합

5. [Phase 5] Glass Refraction ← 새로 추가!
   - HLSL Pixel Shader 3.0
   - 실시간 UV 왜곡
   - 60 FPS 애니메이션
```

---

## ✅ 최종 체크리스트

실행 전 확인:
- [ ] 빌드 성공 (0 errors, 0 warnings 권장)
- [ ] GlassRefraction.ps 파일 존재 (2408 bytes)
- [ ] ICN_T2.csproj에 Resource 등록됨
- [ ] Windows 10 이상 (DirectX 9.0c+)
- [ ] 최신 그래픽 드라이버 설치

실행 후 확인:
- [ ] Shader 로드 성공 로그
- [ ] CharacterInfoContent 적용 로그
- [ ] 60 FPS 애니메이션 시작 로그
- [ ] 마우스 이동 시 유리 왜곡 효과 확인

---

## 🎉 성공!

모든 항목이 체크되었다면:
```
🎊 Phase 5 완료!
🌟 iOS 26 제어센터 스타일 완성!
💫 HLSL Glass Refraction 작동 중!
```

---

**빌드 성공을 기원합니다!** 🚀

**완료일**: 2026-02-10
**프로젝트**: ICN_T2 - Nexus Mod Studio (Puni Edition)
