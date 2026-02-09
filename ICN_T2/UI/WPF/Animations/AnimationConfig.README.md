# 🎨 Animation Config 사용 가이드

## 📁 파일 위치
`ICN_T2/UI/WPF/Animations/AnimationConfig.cs`

---

## ✨ 개요

**AnimationConfig.cs**는 ModernModWindow의 모든 UI 애니메이션과 레이아웃 설정을 한 곳에서 관리하는 외부 설정 파일입니다.

디자이너 뷰처럼 직관적으로 값을 수정하면 XAML과 CS 양쪽에 모두 반영됩니다!

---

## 🏗️ 구조

### 📍 **스텝별 레이아웃 설정** (가장 중요!)

각 화면 전환 단계별로 창 크기, 패널 위치, 트랜지션을 조절할 수 있습니다:

#### 🏠 STEP 1: 메인메뉴 (프로젝트 목록)
```csharp
MainPanel_ProjectMenu_MarginAll = 50.0;     // 창 전체 여백
RightContent_MarginRight = 25.0;            // 오른쪽 콘텐츠 여백
ProjectListView_Margin = 35.0;              // 프로젝트 목록 내부 여백
```

**예시: 오른쪽 콘텐츠 영역을 넓히고 싶다면?**
```csharp
// 여백을 줄이면 콘텐츠 영역이 커집니다
RightContent_MarginRight = 10.0;   // 25 → 10으로 변경
RightContent_MarginBottom = 5.0;   // 10 → 5로 변경
```

#### 📖 STEP 2: 모딩메뉴 (책 & 아이콘 그리드)
```csharp
MainPanel_ModdingMenu_MarginLeft = 20.0;    // 왼쪽 마진 축소
Sidebar_ModdingMenu_Width = 80.0;           // 사이드바 축소 너비
Book_OpenDuration = 250;                     // 책 열리는 속도
```

**예시: 책 애니메이션을 더 빠르게 하고 싶다면?**
```csharp
Book_OpenDuration = 150;        // 250 → 150 (빨라짐)
Book_SlideDuration = 200;       // 350 → 200 (빨라짐)
```

#### 🛠️ STEP 3: 도구메뉴 (캐릭터 정보 등)
```csharp
Background_TopRiseHeight = 100.0;           // 배경 위로 확장 높이
Tool_ContentFadeDuration = 300;             // 콘텐츠 페이드인 속도
Transition_MedalPopDelay = 100;             // 배경 확장 시작 타이밍
```

**예시: 배경 확장을 더 높이 올리고 싶다면?**
```csharp
Background_TopRiseHeight = 150.0;   // 100 → 150 (더 높이 올라감)
```

**예시: 도구 콘텐츠가 더 빠르게 나타나게 하려면?**
```csharp
Tool_ContentFadeDuration = 200;     // 300 → 200 (빠른 페이드인)
Transition_MedalPopDelay = 50;      // 100 → 50 (빠른 확장 시작)
```

---

### 🎬 **트랜지션 타이밍**

화면 전환 시 대기 시간을 조절:

```csharp
Transition_LayoutDuration = 600;        // 레이아웃 변경 속도
Transition_ToolRevealDelay = 600;       // 도구 콘텐츠 표시 전 대기
```

**예시: 전체 트랜지션을 빠르게 하려면?**
```csharp
Transition_LayoutDuration = 400;        // 600 → 400 (40% 빠름)
Transition_ToolRevealDelay = 400;       // 600 → 400 (40% 빠름)
```

---

### 🎨 **기타 설정**

#### 헤더 애니메이션
```csharp
Header_FadeOutDuration = 300;
Header_SlideDuration = 150;
Header_SlideStartX = -120.0;
```

#### 페이드 효과
```csharp
Fade_Duration = 250;
```

#### Z-Index (레이어 순서)
```csharp
ZIndex_Header = 10000;          // 헤더를 항상 최상단에
ZIndex_MedalProxy = 9999;       // 메달 애니메이션
```

---

## 🚀 사용 방법

### 1️⃣ AnimationConfig.cs 파일 열기
```
ICN_T2/UI/WPF/Animations/AnimationConfig.cs
```

### 2️⃣ 원하는 값 수정
```csharp
// 예: 오른쪽 콘텐츠 영역 크기를 키우고 싶다면
public const double RightContent_MarginRight = 10.0;  // 기존: 25.0
public const double RightContent_MarginBottom = 5.0;  // 기존: 10.0
```

### 3️⃣ 저장하고 빌드
- 파일 저장 (`Ctrl + S`)
- 프로젝트 빌드 (`Ctrl + Shift + B`)
- 실행 (`F5`)

### 4️⃣ 실시간 확인
변경사항이 모든 화면에 즉시 반영됩니다!

---

## ⚠️ 주의사항

### 💡 동적 계산 변수는 CS에 남아있습니다

다음 변수들은 런타임 계산이 필요해서 `ModernModWindow.xaml.cs`에 남아있습니다:

- `_bookBaseMargin*` - 책 위치 계산
- `_menuOpen2Offset*` - 책 속지 오프셋
- `_sidebarStartX`, `_sidebarTargetX` - StepProgress 기반 보간
- `_bgShakeOffset` - 배경 흔들림 계산
- `_medalHeaderXOffset` - 동적 헤더 위치

이 변수들은 **ModernModWindow.xaml.cs** 상단의 `동적 계산 (CS 전용)` region에서 찾을 수 있습니다.

---

## 📊 빠른 참조

### 오른쪽 콘텐츠 영역 크기 조절
```csharp
RightContent_MarginRight        // 오른쪽 여백 (↓ 값 = 넓어짐)
RightContent_MarginBottom       // 아래 여백 (↓ 값 = 길어짐)
RightContent_SpacerWidth        // 사이드바와의 간격
ProjectListView_Margin          // 목록 내부 여백
```

### 메인 패널 크기 조절
```csharp
MainPanel_ProjectMenu_MarginAll // 전체 창 여백 (↓ 값 = 커짐)
MainContentRootGrid_Margin      // 내부 콘텐츠 여백
```

### 배경 확장 높이 조절
```csharp
Background_TopRiseHeight        // 도구 메뉴에서 위로 확장 높이
Background_StepXPosition        // 확장 시작 X 좌표
```

### 트랜지션 속도 조절
```csharp
Transition_LayoutDuration       // 레이아웃 변경 속도 (↓ 값 = 빠름)
Book_OpenDuration               // 책 열리는 속도 (↓ 값 = 빠름)
Tool_ContentFadeDuration        // 도구 페이드인 속도 (↓ 값 = 빠름)
```

---

## 🎯 실전 예제

### 예제 1: 오른쪽 콘텐츠 영역을 최대한 넓히기
```csharp
// AnimationConfig.cs에서
public const double RightContent_MarginRight = 5.0;     // 25 → 5
public const double RightContent_MarginBottom = 5.0;    // 10 → 5
public const double RightContent_SpacerWidth = 10.0;    // 20 → 10
public const double ProjectListView_Margin = 15.0;      // 35 → 15
```

### 예제 2: 모든 애니메이션 속도 2배 빠르게
```csharp
// AnimationConfig.cs에서
public const int Transition_LayoutDuration = 300;       // 600 → 300
public const int Book_OpenDuration = 125;               // 250 → 125
public const int Tool_ContentFadeDuration = 150;        // 300 → 150
public const int Medal_PopDuration = 150;               // 300 → 150
```

### 예제 3: 도구 메뉴 배경을 더 높이 확장
```csharp
// AnimationConfig.cs에서
public const double Background_TopRiseHeight = 150.0;   // 100 → 150
public const double Background_StepXPosition = 350.0;   // 400 → 350 (더 왼쪽부터 시작)
```

---

## 🔄 변경 전후 비교 예시

### Before (기본값)
- 오른쪽 여백: 25px
- 프로젝트 목록 여백: 35px
- 배경 확장 높이: 100px

### After (수정 후)
```csharp
public const double RightContent_MarginRight = 10.0;
public const double ProjectListView_Margin = 20.0;
public const double Background_TopRiseHeight = 120.0;
```
- 오른쪽 여백: 10px (콘텐츠 15px 더 넓어짐)
- 프로젝트 목록 여백: 20px (목록 내용 15px 더 넓어짐)
- 배경 확장 높이: 120px (20px 더 높이 올라감)

---

## 💬 문의사항

값을 수정했는데 변경이 안 보인다면?
1. 프로젝트를 완전히 리빌드 (`Ctrl + Shift + B`)
2. 솔루션 클린 후 다시 빌드 (`Build > Clean Solution` → `Build > Rebuild Solution`)
3. Visual Studio 재시작

여전히 문제가 있다면 `ModernModWindow.xaml.cs`의 `OnWindowLoaded` 메서드에서 값이 제대로 적용되는지 확인하세요.

---

## 📝 개발자 노트

### XAML에서 직접 사용하기 (고급)

XAML에서도 AnimationConfig를 참조할 수 있습니다:

```xml
<Grid Margin="{x:Static animations:AnimationConfig.RightContent_MarginRight}">
    <!-- ... -->
</Grid>
```

하지만 현재는 대부분 CS 코드에서 값을 읽어서 적용하는 방식을 사용합니다.

---

**이제 AnimationConfig.cs 파일 하나만 수정해서 모든 UI를 자유자재로 조절할 수 있습니다! 🎉**
