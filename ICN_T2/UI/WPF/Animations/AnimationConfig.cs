using System;

namespace ICN_T2.UI.WPF.Animations
{
    /// <summary>
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// 🎨 ModernModWindow 애니메이션 & 레이아웃 설정
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// 
    /// 이 파일에서 모든 UI 애니메이션과 레이아웃을 조정할 수 있습니다.
    /// 디자이너 뷰처럼 직관적으로 값을 수정하면 XAML과 CS 양쪽에 모두 반영됩니다.
    /// 
    /// ⚠️ 동적 계산이 필요한 변수들은 ModernModWindow.xaml.cs에 남아있습니다:
    ///    → _sidebarStartX, _sidebarTargetX (StepProgress 기반 보간)
    ///    → _bgShakeOffset (배경 흔들림 계산)
    ///    → _riserMaxHeight (현재 미사용)
    ///    → _medalHeaderXOffset (동적 헤더 위치 계산)
    /// 
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// </summary>
    public static class AnimationConfig
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // 📍 내비게이션 스텝별 레이아웃 설정
        // ═══════════════════════════════════════════════════════════════════════════
        // 메인메뉴 → 모딩메뉴 → 도구메뉴 각 단계별 창 크기, 패널 위치, 트랜지션 설정
        // ═══════════════════════════════════════════════════════════════════════════

        #region ┌─────────────────────────────────────────────────────────────────────┐
        #region │ 🏠 STEP 1: 메인메뉴 (프로젝트 목록) - Project List                    │
        #region └─────────────────────────────────────────────────────────────────────┘

        // ━━━ 메인 패널 크기 (MainContentPanel) ━━━
        // [변경] MarginAll 대신 상하좌우 개별 설정으로 변경
        // [최적화] 모든 스텝에서 동일한 크기 유지 (통일)
        public const double MainPanel_ProjectMenu_MarginTop = 30.0;
        public const double MainPanel_ProjectMenu_MarginBottom = 40.0;
        public const double MainPanel_ProjectMenu_MarginLeft = 40.0;
        public const double MainPanel_ProjectMenu_MarginRight = 40.0;

        // [NEW] 유리창(배경) 내부 크기 미세 조절 (컨테이너 크기는 유지하되, 유리만 작게 그리기)
        public const double Glass_MarginTop = 20.0;    // 값 ↑: 유리가 위쪽에서 더 아래로 내려옴
        public const double Glass_MarginBottom = 20.0; // 값 ↑: 유리가 아래쪽에서 더 위로 올라감
        public const double Glass_MarginLeft = 50.0;   // 값 ↑: 유리가 왼쪽(사이드바 쪽)에서 더 오른쪽으로 밀림
        public const double Glass_MarginRight = 20.0;  // 값 ↑: 유리가 오른쪽에서 더 안쪽으로 들어옴

        // → 위/아래 값을 늘리면 창의 높이가 줄어듭니다.
        // → 왼쪽/오른쪽 값을 늘리면 창의 너비가 줄어듭니다.
        // ※ 주의: 창이 줄어도 내부 비율은 유지되지만, 글자 크기나 고정 여백(40px)은 변하지 않습니다.
        //    (창을 많이 줄일 경우 아래 MainContentRootGrid_Margin 값도 줄이는 것을 추천합니다.)

        public const double MainPanel_CornerRadius = 40.0;
        // → 패널 모서리 둥글기 (px)

        public const double MainContentRootGrid_Margin = 40.0;
        // → 패널 내부 콘텐츠 여백 (px)
        // → 값 ↑: 내부 콘텐츠가 작아짐

        // ━━━ 사이드바 크기 ━━━
        public const double Sidebar_ProjectMenu_Width = 220.0;
        // → 프로젝트 메뉴에서 사이드바 너비 (px)

        // ━━━ 오른쪽 콘텐츠 영역 (RightContentArea) ━━━
        public const double RightContent_MarginRight = -2.0;
        // → 오른쪽 여백 (px) | 값 ↑: 콘텐츠 영역 작아짐 (

        public const double RightContent_MarginBottom = -14.0;
        // → 아래쪽 여백 (px) | 값 ↑: 콘텐츠 영역 작아짐 (10 -> 80)

        public const double RightContent_SpacerWidth = 20.0;
        // → 사이드바 ↔ 콘텐츠 사이 간격 (px)

        public const double ProjectListView_Margin = 45.0;
        public const double ProjectListView_MarginBottom = 38.0;
        // → 프로젝트 목록 내부 여백 (px)
        // → 값 ↑: 목록이 작아짐

        #endregion
        #endregion
        #endregion

        #region ┌─────────────────────────────────────────────────────────────────────┐
        #region │ 📖 STEP 2: 모딩메뉴 (책 아이콘 그리드) - Modding Menu                │
        #region └─────────────────────────────────────────────────────────────────────┘

        // ━━━ 메인 패널 트랜지션 (프로젝트 → 모딩) ━━━
        public const double MainPanel_ModdingMenu_MarginLeft = 40.0;     // 메인메뉴와 동일하게 통일
        // → 모딩 메뉴 진입 시 왼쪽 마진 (px)
        // → 사이드바 축소에 맞춰서 왼쪽 여백도 조정

        public const double MainPanel_ModdingMenu_MarginTop = 40.0;      // 메인메뉴와 동일
        public const double MainPanel_ModdingMenu_MarginRight = 40.0;    // 메인메뉴와 동일
        public const double MainPanel_ModdingMenu_MarginBottom = 40.0;   // 메인메뉴와 동일
        // → 모딩 메뉴에서 위/오른쪽/아래 여백 (복귀 시 사용)

        // ━━━ 사이드바 트랜지션 ━━━
        public const double Sidebar_ModdingMenu_Width = 80.0;
        // → 모딩 메뉴에서 사이드바 너비 (축소됨)

        // ━━━ 배경 확장 애니메이션 ━━━
        public const double Background_SidebarGap = 10.0;
        // → 사이드바와 배경 왼쪽 끝 사이의 간격 (px)
        // → 값 ↑: 사이드바와 배경 사이가 넓어짐
        // → 배경 왼쪽 끝 = Sidebar_ModdingMenu_Width + 이 값

        public const double Background_StepProgress_ModdingMenu = 0.5;
        // → 모딩 메뉴에서 배경 확장 진행도 (0.0~1.0)
        // → 0.5 = 왼쪽만 확장 (위쪽은 확장 안 됨)

        // ━━━ 책 애니메이션 타이밍 ━━━
        public const int Book_OpenDuration = 250;           // 책 열리는 속도 (ms) [0.2초 더 빠르게 도착]
        public const int Book_CloseDuration = 250;         // 책 닫히는 속도 (ms)
        public const int Book_OpenDelay = 0;               // 책 열기 전 대기 (ms) [0.2초 더 빠르게 조정]
        public const int Book_ExtraDelay = 0;            // 책 열기 후 추가 대기 (ms)
        public const int Book_MoveDuration = 400;          // 책 이동 속도 (ms) - 배경 확장보다 빠름
        public static readonly int Background_ExpandDelay = Book_OpenDuration + 200;
        // → 배경 확장 시작 딜레이 (ms)
        // → 책 열기(250ms) + 시선 여유(200ms) = 450ms
        // → 책을 먼저 보고, 그 뒤에 배경이 움직이는 연출
        public const int Book_SlideDuration = 350;         // 책 슬라이드 속도 (ms)
        public const int Book_CloseFadeOutDuration = 150;  // 책 닫기 페이드 아웃 (ms)
        public const int Book_CloseSyncFadeDuration = Fade_Duration; // 책 표지/속지 동시 페이드 시간 (ms)

        // ━━━ 책 위치/크기 ━━━
        public const double Book_SlideOffset = 10.0;       // 책 슬라이드 거리 (px)
        public const double Book_CoverInitialScale = 1.05; // 책 표지 초기 스케일
        public const double Book_ModdingMenu_MarginLeft = 0.0;
        // → 모딩 메뉴에서 책의 왼쪽 최종 위치 (px)
        // → Sidebar_ModdingMenu_Width(80) + Background_SidebarGap(10) = 90px
        // → 사이드바 바로 옆에 위치

        // ━━━ 책 표지(MenuOpen1) 기본 마진 ━━━
        public const double Book_BaseMarginLeft = 0.0;    // 책 기본 왼쪽 마진 (px)
        public const double Book_BaseMarginTop = 0.0;      // 책 기본 위쪽 마진 (px)
        public const double Book_BaseMarginRight = 0.0;    // 책 기본 오른쪽 마진 (px)
        public const double Book_BaseMarginBottom = 0.0;   // 책 기본 아래쪽 마진 (px)

        // ━━━ 속지(MenuOpen2) 오프셋 ━━━
        public const double Book_Open2OffsetX = 30.0;      // 속지 X 오프셋 (px) — 책장과 속지 정렬용
        public const double Book_Open2OffsetY = 32.0;       // 속지 Y 오프셋 (px)
        public const double Book_Page_LeftNudge = 0.0;      // 속지 전용 추가 X 오프셋 미세조정 (px)
        public const double Book_SidebarFollowFactor = 0.35; // 사이드바 이동량을 책에 얼마나 반영할지 (0~1)
        public const double Book_ModdingMenu_LeftNudge = 24.0; // 모딩 메뉴 단계에서 책 전체 X 보정 (px)
        public const double Book_ToolMenu_LeftNudge = 18.0; // 도구 메뉴 단계에서 책 전체 X 보정 (px)
        public const double Book_GlobalCloseOffsetX = 20.0; // 책 전체 X 보정 (오른쪽 +20px)

        #endregion
        #endregion
        #endregion

        #region ┌─────────────────────────────────────────────────────────────────────┐
        #region │ 🛠️ STEP 3: 도구메뉴 (캐릭터 정보 등) - Tool Menu                     │
        #region └─────────────────────────────────────────────────────────────────────┘

        // ━━━ 메인 패널 트랜지션 (모딩 → 도구) ━━━
        public const double MainPanel_ToolMenu_CompactMargin = 40.0;     // 10 → 40 (비율 유지)
        // → 도구 메뉴 진입 시 전체 마진 (px)
        // → 화면을 최대한 활용하되 비율 유지

        public const double MainContentRootGrid_ToolMenu_CompactMargin = 20.0;  // 10 → 20 (비율 유지)
        // → 도구 메뉴에서 내부 그리드 마진 (px)

        // ━━━ 배경 확장 애니메이션 ━━━
        public const double Background_StepProgress_ToolMenu = 1.0;
        // → 도구 메뉴에서 배경 확장 진행도 (최대)
        // → 1.0 = 왼쪽 + 위쪽 모두 확장됨

        public const double Background_TopRiseHeight = 80.0;
        // → 배경 상단이 위로 올라가는 최대 높이 (px)
        // → 값 ↑: 도구 메뉴에서 더 높이 올라감

        // iOS-style dark glass tuning for main content.
        // Base tone requested: #1E1E1E
        public const string MainContent_GlassTint = "#26DFF6FF"; // cool bluish-white glass tint
        public const string MainContent_GlassDarkTint = "#00000000";
        public const string MainContent_GlassOverlayTint = "#12182630";
        public const double MainContent_GlassBlurRadius = 10.0;

        // === Hierarchy A: Global Backdrop (얇고 투명한 대기) ===
        public const double MainContent_GlassRefractionStrength = 0.06;   // 기존 0.12 대비 50% 축소
        public const double MainContent_GlassNoiseScale = 1.80;           // 더 미세한 입자
        public const double MainContent_GlassSpecular = 0.10;
        public const double MainContent_GlassInnerShadow = 0.026;
        public const double MainContent_GlassDensity = 0.24;
        public const double MainContent_GlassMouseRadius = 0.30;
        public const double MainContent_GlassMouseFalloffPower = 1.60;
        public const double MainContent_GlassMouseOffsetStrength = 0.08;
        public const double MainContent_GlassEdgeHighlightStrength = 0.08;

        // === Hierarchy B: The Book (단단한 크리스탈) ===
        public const string Book_GlassTag = "BookGlassBackplate";
        public const double Book_GlassRefractionStrength = 0.045;
        public const double Book_GlassNoiseScale = 1.35;
        public const double Book_GlassSpecular = 0.10;
        public const double Book_GlassInnerShadow = 0.022;
        public const double Book_GlassDensity = 0.18;
        public const double Book_GlassMouseRadius = 0.24;
        public const double Book_GlassMouseFalloffPower = 1.85;
        public const double Book_GlassMouseOffsetStrength = 0.10;
        public const double Book_GlassEdgeHighlightStrength = 0.05;

        // === Sidebar policy: Book 프로필과 동일 ===
        public const double Sidebar_GlassRefractionStrength = 0.045;
        public const double Sidebar_GlassNoiseScale = 1.35;
        public const double Sidebar_GlassSpecular = 0.14;
        public const double Sidebar_GlassInnerShadow = 0.035;
        public const double Sidebar_GlassDensity = 0.48;
        public const double Sidebar_GlassMouseRadius = 0.22;
        public const double Sidebar_GlassMouseFalloffPower = 2.40;
        public const double Sidebar_GlassMouseOffsetStrength = 0.10;
        public const double Sidebar_GlassEdgeHighlightStrength = 0.11;

        // === Hierarchy C/D: Modding Medal Backplate (책 위 12개만) ===
        public const string ModdingMedal_GlassTag = "ModdingMedalBackplateGlass";
        public const double ModdingMenu_ButtonRefractionStrength = 0.075;
        public const double ModdingMedal_GlassRefractionStrength = 0.075;
        public const double ModdingMedal_GlassNoiseScale = 1.45;
        public const double ModdingMedal_GlassSpecular = 0.11;
        public const double ModdingMedal_GlassInnerShadow = 0.0;
        public const double ModdingMedal_GlassDensity = 0.62;
        public const double ModdingMedal_GlassMouseRadius = 1.50; // 약 1.5 버튼 반경
        public const double ModdingMedal_GlassMouseFalloffPower = 3.20;
        public const double ModdingMedal_GlassMouseOffsetStrength = 0.12;
        public const double ModdingMedal_GlassEdgeHighlightStrength = 0.03;

        // Tool panel glass matching tuning.
        public const string ToolPanel_GlassTag = "ToolGlassPanel";
        public const string ToolPanel_BackdropTag = "ToolPanelBackdropGlass";
        public const double ToolPanel_BackdropBlurRadius = 24.0;
        public const string ToolPanel_BackdropTint = "#A8EAF4FA";
        public const double ToolPanel_GlassRefractionStrength = 0.18;
        public const double ToolPanel_GlassNoiseScale = 1.25;

        // Interactive element unified glass (single-area hover/input).
        public const string ToolInteractive_GlassTag = "ToolInteractiveGlass";
        public static readonly bool ToolInteractive_EnableRefraction = false;
        public const double ToolInteractive_GlassRefractionStrength = 0.14;
        public const double ToolInteractive_GlassNoiseScale = 4.2;

        public const double Background_StepXPosition = 400.0;
        public const double Background_SidebarStartX = 240.0;
        public const double Background_RiserMaxHeight = 80.0;
        // → 배경 상단 꺾임 시작 X 좌표 (px)
        // → 이 지점부터 오른쪽이 위로 올라감

        public const double Background_CornerRadius = 40.0;
        // → 배경 모서리 둥글기 (px)

        // ━━━ 헤더 & 콘텐츠 간격 ━━━
        public const double Tool_HeaderContentSpacing = 22.0;
        // → 도구 메뉴에서 헤더 ↔ 콘텐츠 간격 (px)

        public const double CharacterInfo_HeaderSpacingNormal = 80.0;
        // → 일반 모드(비-도구) 헤더 ↔ 콘텐츠 간격 (px)

        public const double CharacterInfo_MarginBottom = 20.0;

        // Tool host layout tuning (tool views sync with stepped glass expansion).
        public const double ToolHost_MoveUpPx = 100.0;
        public const double ToolHost_ExtraHeightPx = 0.0;
        public const double ToolHost_LeftPadding = 14.0;
        public const double ToolHost_RightPadding = 18.0;
        public const double ToolHost_BottomPadding = -8.0;
        public const double ToolHost_TopPadding = 5.0;
        // → CharacterInfo 아래 여백 (px)

        // ━━━ 캐릭터 정보창 내부 레이아웃 (CharacterInfoV3) ━━━
        public const double CharacterList_WidthRatio = 30.0;    // 왼쪽 목록 너비 비율 (30*)
        public const double CharacterDetail_WidthRatio = 70.0;  // 오른쪽 상세 너비 비율 (70*)
        // ※ 주의: 두 값을 동시에 줄이면 비율이 같아져서 변화가 없습니다.
        // 왼쪽을 넓히려면 List를 늘리고 Detail을 줄이세요. (예: 40 대 60)

        public const double CharacterDetail_VerticalMargin = 0.0;   // 오른쪽 상세 상하 여백 (px)
        public const double CharacterDetail_HorizontalMargin = 0.0; // 오른쪽 상세 좌우 여백 (px)

        // CharacterInfoV3 panel layout overrides (applied in code-behind too).
        public const double CharacterListPanel_TopMargin = 90.0;
        public const double CharacterListPanel_BottomMargin = 14.0;   // shorten character list height by 10px
        public const double CharacterDetailPanel_TopMargin = -2.0;
        public const double CharacterDetailPanel_BottomMargin = 16.0; // shorten right panel bottom length by additional 20px
        public const double CharacterDetailPanel_CornerRadius = 34.0;  // rounder right panel corners
        public const double CharacterListBackdrop_Expand = 8.0;
        public const double CharacterDetailBackdrop_Expand = 10.0;
        public const double CharacterListBackdrop_RadiusBoost = 6.0;
        public const double CharacterDetailBackdrop_RadiusBoost = 8.0;

        // ━━━ 도구 콘텐츠 페이드인 ━━━
        public const int Tool_ContentFadeDuration = 300;
        // → 도구 창 내부 패널들 페이드인 속도 (ms)
        // → 빠르면 즉각 반응, 느리면 부드러움

        public const int Tool_HeaderBeforeBackgroundDelay = 100;
        // → 헤더 표시 후 배경 확장 시작 전 대기 (ms)
        // → 헤더가 먼저 나타나고, 이 시간만큼 대기한 뒤 배경이 확장됨

        // ━━━ 메달 애니메이션 (도구 선택 시) ━━━
        public const int Medal_PopDuration = 300;          // 메달 팝업 속도 (ms)
        public const int Medal_FlyDuration = 600;          // 메달 비행 속도 (ms)
        public const int Medal_LandDuration = 600;         // 메달 착지 속도 (ms)
        public const int Medal_FlyExtraDelay = 50;         // 메달 비행 후 추가 대기 (ms)
        public const int Medal_AfterBookReadyDelay = 200;  // 책이 완전히 열린 후 메달 등장 시작 지연 (ms)

        public const double Medal_PopScale = 2.64;         // 메달 팝업 최종 스케일 (+60%)
        public const double Medal_PopYOffset = -88.0;      // 메달 팝업 Y 오프셋 (px)

        #endregion

        // ━━━ 디버그 로그 제어 ━━━
        public static readonly bool EnableVerboseLayoutLogs = false;
        public static readonly bool EnableVerboseLayoutFileLog = false;
        #endregion
        // ━━━ 버튼 진입 애니메이션 (Spring) ━━━
        public const double Button_SpringDuration = 800;      // 0.8초
        public const double Button_SpringBounce = 0.4;        // 탄력성
        public const double Button_InitialDelay = 100;        // 0.1초
        public const double Button_StaggerDelay = 40;         // 0.04초
        public const double Button_FromScale = 0.99;          // 초기 스케일 (+65%)
        public const double Button_ToScale = 1.65;            // 최종 스케일 (+65%)
        public const double Button_FromOpacity = 0;           // 투명
        public const double Button_ToOpacity = 1;             // 불투명

        // ━━━ 리스트/패널 진입 애니메이션 (경량 Drop-In Bounce) ━━━
        public static readonly bool ListEntrance_Enable = true;
        public const double ListEntrance_DurationMs = 320.0;
        public const double ListEntrance_StaggerDelayMs = 36.0;
        public const double ListEntrance_OffsetY = -16.0;
        public const double ListEntrance_FromScale = 0.985;
        public const double ListEntrance_ToScale = 1.0;
        public const double ListEntrance_FromOpacity = 0.0;
        public const double ListEntrance_ToOpacity = 1.0;
        public const double ListEntrance_BounceAmplitude = 0.22;
        // Modding menu icon entrance budget:
        // totalWindow = first start -> last completed
        public const double ModdingToolsEntrance_TotalWindowMs = 400.0;
        public const double ModdingToolsEntrance_ItemDurationMs = 220.0;

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 🎬 트랜지션 타이밍 (각 스텝 전환 시 대기 시간)
        // ═══════════════════════════════════════════════════════════════════════════

        #region 트랜지션 대기 시간

        // ━━━ 프로젝트 메뉴 ↔ 모딩 메뉴 ━━━
        public const int Transition_LayoutDuration = 600;
        // → 레이아웃 확장/축소 애니메이션 속도 (ms)

        public const int Transition_RiserDuration = 600;
        // → 배경 위로 올라가는 속도 (ms)

        // ━━━ 모딩 메뉴 ↔ 도구 메뉴 ━━━
        public const int Transition_MedalPopDelay = 100;
        // → 메달 팝업 후 배경 확장 시작 전 대기 (ms)
        // → 메달 팝업(300ms) + 이 값 = 배경 확장 시작 시간
        // → 현재: 300 + 100 = 400ms (0.4초 ±0.05초 요구사항 충족)

        public const int Transition_ToolRevealDelay = 100;
        // → 도구 콘텐츠 표시 전 대기 (ms)

        public const int Transition_ToolFinalDelay = 100;
        // → 도구 창 오픈 후 최종 대기 (ms)

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 📝 헤더 & 텍스트 애니메이션
        // ═══════════════════════════════════════════════════════════════════════════

        #region 헤더 애니메이션

        public const int Header_FadeOutDuration = 300;     // 헤더 페이드 아웃 속도 (ms)
        public const int Header_FadeInDuration = 450;      // 헤더 페이드 인 속도 (ms) — 모딩 메뉴 등장 체감
        public const int Header_SlideDuration = 400;       // 헤더 슬라이드 속도 (ms) — 모딩 메뉴 등장 체감 속도
        public const double Header_SlideStartX = -120.0;   // 헤더 슬라이드 시작 X 위치 (px)
        public const double Header_MinHeight = 40.0;       // 헤더 최소 높이 (px)
        public const double Header_ContentSpacing = 30.0;  // 헤더 ↔ 콘텐츠 간격 (px)
        public const double Header_MarginLeft = 10.0;
        public const double Header_MarginTop = 0.0;
        public const double Header_MarginRight = 0.0;
        public const double Header_MarginBottom = 30.0;
        public const int TitleBar_SlideDuration = 250;
        public const double TitleBar_HiddenOffsetY = -38.0;

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 🎨 페이드 효과
        // ═══════════════════════════════════════════════════════════════════════════

        #region 페이드 효과

        public const int Fade_Duration = 250;              // 기본 페이드 인/아웃 속도 (ms)
        public const int Fade_MainMenuAppearDelay = 200;   // 메인 메뉴 등장 딜레이 (ms)

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 🌟 타이틀 화면 애니메이션 (최초 로딩)
        // ═══════════════════════════════════════════════════════════════════════════

        #region 타이틀 화면

        // ━━━ 타이밍 ━━━
        public const double Title_SqueezeDuration = 0.2;   // Squeeze 애니메이션 속도 (초)
        public const int Title_SqueezeDelay = 300;         // Squeeze 후 대기 (ms)
        public const double Title_SnapDuration = 0.3;      // Snap 애니메이션 속도 (초)
        public const int Title_FlashStartDelay = 260;      // Flash 시작 전 대기 (ms)
        public const double Title_FlashDuration = 0.1;     // Flash 페이드인 속도 (초)
        public const int Title_FlashCompleteDelay = 100;   // Flash 완료 후 대기 (ms)
        public const double Title_WakeUpDuration = 2.0;    // Awakening 페이드 아웃 속도 (초)
        public const int Title_WakeUpDelay = 2000;         // Awakening 후 대기 (ms)
        public const double Title_SlideInDuration = 1.2;   // 대시보드 슬라이드인 속도 (초)

        // ━━━ 스케일 & 위치 ━━━
        public const double Title_SqueezeScaleX = 0.85;    // Squeeze X 스케일
        public const double Title_SqueezeScaleY = 1.15;    // Squeeze Y 스케일
        public const double Title_SnapScale = 2.5;         // Snap(폭발) 스케일
        public const double Title_SlideStartX = -50.0;     // 대시보드 슬라이드 시작 X (px)

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 🎭 Z-Index (레이어 순서)
        // ═══════════════════════════════════════════════════════════════════════════

        #region Z-Index

        public const int ZIndex_MedalProxy = 9999;
        public const int ZIndex_Header = 10000;
        public const int ZIndex_MedalProxyBelowHeader = 5000;
        public const int ZIndex_BookCover = 999;
        public const int ZIndex_ModdingMenuContent = 1;

        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // 🔧 배경 슬라이드 키프레임 (고급 설정)
        // ═══════════════════════════════════════════════════════════════════════════

        #region 배경 슬라이드

        public const double Background_SlideFirstKeyTime = 0.2;   // 첫 번째 키프레임 (초)
        public const double Background_SlideSecondKeyTime = 0.45; // 두 번째 키프레임 (초)

        #endregion

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 💡 빠른 참조 가이드
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //
        // 🏠 메인메뉴 크기 조절:
        //    → MainPanel_ProjectMenu_MarginAll (창 전체 여백)
        //    → RightContent_MarginRight/Bottom (오른쪽 콘텐츠 여백)
        //    → ProjectListView_Margin (프로젝트 목록 여백)
        //
        // 📖 모딩메뉴 트랜지션 속도:
        //    → Transition_LayoutDuration (레이아웃 변경 속도)
        //    → Book_OpenDuration (책 열리는 속도)
        //
        // 🛠️ 도구메뉴 확장 조절:
        //    → Background_TopRiseHeight (위쪽 확장 높이)
        //    → Tool_ContentFadeDuration (콘텐츠 페이드인 속도)
        //    → Tool_HeaderBeforeBackgroundDelay (헤더→배경 확장 시간 간격)
        //    → Transition_MedalPopDelay (메달 팝업→헤더 전환 시간)
        //
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    }
}

