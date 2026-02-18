using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ICN_T2.Logic.Project;
using ICN_T2.UI.WPF.ViewModels;
using System.Collections.ObjectModel;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.UI.WPF.Animations;
using ICN_T2.UI.WPF.Services;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ICN_T2.UI.WPF.Effects;
using ICN_T2.UI.WPF.ViewModels.Contracts;
using System.Linq;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;

namespace ICN_T2.UI.WPF
{
    public partial class ModernModWindow : Window
    {
        private List<Project> _projects = new List<Project>();
        public ObservableCollection<ModdingToolViewModel> ModdingTools { get; set; } = new ObservableCollection<ModdingToolViewModel>();
        public IGame? CurrentGame { get; private set; }

        // Rx 기반 애니메이션 서비스
        private readonly AnimationService _animationService = new AnimationService();

        // [Phase 5] Glass Refraction Shader
        private GlassRefractionEffect? _glassRefractionEffect;
        private readonly Dictionary<Button, GlassRefractionEffect> _buttonRefractionEffects = new Dictionary<Button, GlassRefractionEffect>();
        private readonly Dictionary<FrameworkElement, GlassRefractionEffect> _moddingMedalRefractionEffects = new Dictionary<FrameworkElement, GlassRefractionEffect>();
        private readonly Dictionary<FrameworkElement, Effect> _toolPanelRefractionEffects = new Dictionary<FrameworkElement, Effect>();
        private readonly Dictionary<FrameworkElement, GlassRefractionEffect> _toolInteractiveRefractionEffects = new Dictionary<FrameworkElement, GlassRefractionEffect>();
        private VisualBrush? _toolPanelBackdropBrush;
        private ImageBrush? _glassNormalMapBrush;
        private GlassRefractionEffect? _fixedBackdropRefractionEffect;
        private GlassRefractionEffect? _tintLayerRefractionEffect;  // 틴트 레이어용 왜곡 효과
        private GlassRefractionEffect? _blurOverlayRefractionEffect;  // 블러 오버레이용 왜곡 효과 (약하게)
        private GlassRefractionEffect? _sidebarRefractionEffect;  // 사이드바 Liquid Glass 효과
        private GlassRefractionEffect? _bookRefractionEffect; // 책 배경 전용 굴절 효과
        private FrameworkElement? _bookGlassBackplate;
        private double _shaderTime = 0.0;
        private bool _isBookMarginAnimationRunning;
        private System.Threading.CancellationTokenSource? _activeToolTransitionCts;
        private int _activeToolTransitionVersion;
        private DateTime _bookOpenCompletedAtUtc = DateTime.MinValue;
        private DateTime _lastSteppedPathLogAtUtc = DateTime.MinValue;
        private bool _toolPanelAttachRetryPending;
        private int _toolPanelAttachRetryCount;
        private const int ToolPanelAttachMaxRetries = 6;
        private bool _isToolLayoutLocked;
        private bool _isToolLayoutFinalized;
        private System.Windows.Size _lastToolLayoutWindowSize = System.Windows.Size.Empty;
        private readonly ObservableCollection<SaveSelectionItemViewModel> _saveSelectionItems = new ObservableCollection<SaveSelectionItemViewModel>();
        private readonly Dictionary<string, object> _saveParticipantByToolId = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);


        // === Navigation Stack System (New) ===
        public enum NavState
        {
            ProjectList = 0,    // 프로젝트 목록 (Level 0)
            ModdingMenu = 1,    // 모딩 메뉴 (아이콘 그리드) (Level 1)
            ToolWindow = 2,     // 개별 도구 화면 (캐릭터 정보 등) (Level 2)
            DetailView = 3      // 도구 내 상세 화면 (Level 3 - Optional)
        }

        public class NavItem
        {
            public NavState State { get; set; }
            public object? Context { get; set; }
            public string? MethodName { get; set; } // 어디서 호출되었는지 기록
        }

        private sealed class SaveSelectionItemViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isChecked = true;
            private string? _errorMessage;

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

            public string ToolId { get; init; } = "";
            public string ToolDisplayName { get; init; } = "";
            public string ChangeId { get; init; } = "";
            public string DisplayName { get; init; } = "";
            public string? Description { get; init; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value) return;
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }

            public string? ErrorMessage
            {
                get => _errorMessage;
                set
                {
                    if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _errorMessage = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ErrorMessage)));
                }
            }
        }

        private Stack<NavItem> _navStack = new Stack<NavItem>();
        private bool _suppressSidebarNavCheckedEvents = true;

        public void NavigateTo(NavState target, object? context = null, [System.Runtime.CompilerServices.CallerMemberName] string? methodName = null)
        {
            // [FIX] Ensure root state is in stack if navigating away from start for the first time
            if (_navStack.Count == 0 && target != NavState.ProjectList)
            {
                _navStack.Push(new NavItem { State = NavState.ProjectList, MethodName = "AutoInit" });
            }

            _navStack.Push(new NavItem { State = target, Context = context, MethodName = methodName });
            UpdateUI(target, context);
        }

        public void GoBack()
        {
            if (_navStack.Count <= 1)
            {
                // Defensive fallback: if UI is already in modding/tool state but stack got flattened,
                // still allow back navigation to restore ProjectList.
                bool toolWindowVisible =
                    CharacterInfoContent?.Visibility == Visibility.Visible ||
                    CharacterScaleContent?.Visibility == Visibility.Visible ||
                    YokaiStatsContent?.Visibility == Visibility.Visible ||
                    EncounterEditorContent?.Visibility == Visibility.Visible ||
                    (_navStack.Count > 0 && _navStack.Peek().State == NavState.ToolWindow);

                if (ProjectMenuContent?.Visibility == Visibility.Collapsed &&
                    (ModdingMenuContent?.Visibility == Visibility.Visible || toolWindowVisible))
                {
                    _navStack.Clear();
                    _navStack.Push(new NavItem { State = NavState.ProjectList, MethodName = "GoBackFallback" });

                    if (toolWindowVisible)
                    {
                        HideAllToolContents();
                    }

                    TransitionBackToProjectList();
                }

                return;
            }

            var current = _navStack.Pop();
            var previous = _navStack.Peek();
            CancelRunningToolSelectionTransition(resetToModdingMenuState: false);
            if (current.State == NavState.ToolWindow)
            {
                ResetToolLayoutSession("GoBack from ToolWindow");
            }

            // [FIX] Layout State Management during Back Navigation
            if (previous.State == NavState.ModdingMenu)
            {
                // Returning to Modding Menu: 도구→모딩 복귀 (1.0 → 0.5)
                // [UPDATE] 2단계 확장: 위쪽만 내려옴 (StepProgress 1.0 → 0.5)
                System.Diagnostics.Debug.WriteLine("[ModWindow] 도구→모딩 복귀: StepProgress 1.0→0.5 (한글)");
                AnimateSteppedLayoutTo(0.5);

                // [FIX] ToolCompact 해제: 모딩 메뉴에서는 compact 안 보여야 함
                if (current.State == NavState.ToolWindow)
                {
                    /* Compact 확장 로직 비활성화 (요청 사항):
                     * 도구→모딩 복귀 시 MainContentPanel/MainContentRootGrid 마진 복원 애니메이션 사용 안 함.
                     * 유지 이유: 히스토리 보존 / 재활성 대비.
                     */
                    // AnimateToolCompactLayout(false);
                }
            }
            else if (previous.State == NavState.ProjectList)
            {
                // Returning to Project List: Reset Everything (0.5 → 0.0)
                // [FIX] Removed AnimateSteppedLayout(false) from here.
                // It is now handled inside TransitionBackToProjectList with a proper DELAY.
                // AnimateSteppedLayout(false); 

                // [NEW] ToolCompact Layout 비활성화: ProjectList로 복귀하므로 일반 레이아웃으로 복원
                if (current.State == NavState.ToolWindow)
                {
                    /* Compact 확장 로직 비활성화 (요청 사항):
                     * 도구→프로젝트 복귀에서도 동일하게 compact 마진 복원 로직을 사용하지 않음.
                     */
                    // AnimateToolCompactLayout(false);
                }

                // AnimateRiser 제거: ToolWindow에서 사용 안 함
            }

            RestoreUI(previous.State, current.State, previous.Context);
        }

        private async void UpdateUI(NavState target, object? context)
        {
            switch (target)
            {
                case NavState.ModdingMenu:
                    await TransitionToModdingMenu();
                    break;
                case NavState.ToolWindow:
                    if (context is System.Windows.Controls.Button btn)
                    {
                        await TransitionToToolWindow(btn);
                    }
                    else if (context is int toolIndex)
                    {
                        // Direct call without button animation?
                        // Handle if needed
                    }
                    break;
            }
        }

        private async System.Threading.Tasks.Task TransitionToToolWindow(System.Windows.Controls.Button btn)
        {
            var vm = btn.DataContext as ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel;
            if (vm == null) return;

            // Forward selection animation only:
            // interrupt current forward transition and restart from stable modding-menu baseline.
            // Also cancel any running RecoverFromSelection (back animation)
            if (_recoverCts != null)
            {
                _recoverCts.Cancel();
                _recoverCts.Dispose();
                _recoverCts = null;
            }
            CancelRunningToolSelectionTransition(resetToModdingMenuState: true);
            int transitionVersion = System.Threading.Interlocked.Increment(ref _activeToolTransitionVersion);
            var cts = new System.Threading.CancellationTokenSource();
            _activeToolTransitionCts = cts;

            // --- STEP 1: SETUP PROXY ---
            _activeTransitionButton = btn;

            // Setup Images
            ProxyBag.Source = new BitmapImage(new Uri(vm.BagIconPath, UriKind.Absolute));
            ProxyIcon.Source = new BitmapImage(new Uri(vm.IconBPath, UriKind.Absolute));

            var proxyTxt = ProxyIconContainer.FindName("ProxyText") as System.Windows.Controls.TextBlock;
            if (proxyTxt != null) proxyTxt.Text = vm.Title;

            // Keep the transition medal perfectly circular even if source button ratio changes.
            double proxySize = Math.Max(1.0, Math.Min(btn.ActualWidth, btn.ActualHeight));
            ProxyIconContainer.Width = proxySize;
            ProxyIconContainer.Height = proxySize;

            // Get Positions relative to Root
            var rootGrid = VisualTreeHelper.GetParent(TransitionProxy) as UIElement;
            if (rootGrid == null) return;

            var btnTransform = btn.TransformToVisual(rootGrid);
            var startPoint = btnTransform.Transform(new System.Windows.Point(
                (btn.ActualWidth - proxySize) / 2.0,
                (btn.ActualHeight - proxySize) / 2.0));

            // Initial Position
            TransitionProxy.Margin = new Thickness(startPoint.X, startPoint.Y, 0, 0);
            TransitionProxy.Visibility = Visibility.Visible;

            // Prepare Transforms
            var scaleTrans = new ScaleTransform(1.0, 1.0);
            var translateTrans = new TranslateTransform(0, 0);
            var transGroup = new TransformGroup();
            transGroup.Children.Add(scaleTrans);
            transGroup.Children.Add(translateTrans);
            TransitionProxy.RenderTransform = transGroup;

            // 프록시 아이콘/텍스트 세팅 후 원래 위치 요소를 즉시 숨겨 '붕 뜨는' 연출 고정
            _activeTransitionOriginElement = ResolveTransitionOriginElement(btn);
            SetTransitionOriginHidden(true);

            // Manual Trigger instead of Property Setter
            IsSelectionFinished = true;
            try
            {
                await PlaySelectionAnimation(cts.Token, transitionVersion);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] PlaySelectionAnimation 취소됨 (forward interrupt)");
            }
            finally
            {
                if (transitionVersion == _activeToolTransitionVersion)
                {
                    _activeToolTransitionCts?.Dispose();
                    _activeToolTransitionCts = null;
                }
            }
        }

        private async System.Threading.Tasks.Task TransitionToModdingMenu()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionToModdingMenu 시작 - Rx 기반 전환됨 (한글)");
            this.IsHitTestVisible = true;
            CancelRunningToolSelectionTransition(resetToModdingMenuState: false);
            #region agent log
            try
            {
                var log = new
                {
                    runId = "run1",
                    hypothesisId = "H2",
                    location = "ModernModWindow.xaml.cs:TransitionToModdingMenu:entry",
                    message = "TransitionToModdingMenu entry config snapshot",
                    data = new
                    {
                        sidebarWidthModding = AnimationConfig.Sidebar_ModdingMenu_Width,
                        sidebarWidthProject = AnimationConfig.Sidebar_ProjectMenu_Width,
                        marginLeftModding = AnimationConfig.MainPanel_ModdingMenu_MarginLeft,
                        stepProgressTarget = AnimationConfig.Background_StepProgress_ModdingMenu
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion
            #region agent log
            try
            {
                var log = new
                {
                    sessionId = "debug-session",
                    runId = "run1",
                    hypothesisId = "H4",
                    location = "ModernModWindow.xaml.cs:TransitionToModdingMenu:entry",
                    message = "TransitionToModdingMenu entry",
                    data = new { fadeDurationMs = AnimationConfig.Fade_Duration, bookOpenDelayMs = AnimationConfig.Book_OpenDelay, headerText = ViewModel.HeaderText },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion

            // [UPDATE] 유저 요청: 책 움직임을 0.2초 더 빠르게 - 프로젝트 페이드아웃과 병렬 처리
            // 1. Fade out current contents (백그라운드에서 실행, 대기 안 함)
            try
            {
                // 페이드아웃을 백그라운드에서 실행 (await 제거)
                Observable.Merge(
                    UIAnimationsRx.Fade(ProjectMenuButtons, 1, 0, AnimationConfig.Fade_Duration),
                    UIAnimationsRx.Fade(ProjectListView, 1, 0, AnimationConfig.Fade_Duration)
                ).DefaultIfEmpty().Subscribe();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] Fade 오류 무시: {ex.Message} (한글)");
            }

            // Switch Visibility
            ProjectMenuButtons.Visibility = Visibility.Collapsed;
            ProjectListView.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Collapsed;

            // [BUG FIX] Reset Opacity & Visibility explicitly
            UIAnimationsRx.ClearAnimation(BookCover, UIElement.OpacityProperty);
            BookCover.Opacity = 1;
            BookCover.Visibility = Visibility.Visible;

            // [FIX] 책장이 속지보다 위에 오도록 Z-Index 명확히 설정
            System.Windows.Controls.Panel.SetZIndex(BookCover, AnimationConfig.ZIndex_BookCover);

            UIAnimationsRx.ClearAnimation(ModdingMenuContent, UIElement.OpacityProperty);
            ModdingMenuContent.Opacity = 1;
            ModdingMenuContent.Visibility = Visibility.Visible;
            System.Windows.Controls.Panel.SetZIndex(ModdingMenuContent, AnimationConfig.ZIndex_ModdingMenuContent);
            AttachBookRefractionEffect();
            _ = Dispatcher.InvokeAsync(() =>
            {
                AttachBookRefractionEffect();
                AttachModdingMedalRefractionEffects();
            }, DispatcherPriority.Loaded);

            // [FIX] 애니메이션 초기화: 이전 애니메이션 제거 후 원래 위치로 명시적 설정
            _isBookMarginAnimationRunning = false;
            SetBookLayoutMargins(CalculateBookLeftForProgress(0.0), clearAnimations: true);

            // Reset Transforms for Cover
            BookCover.RenderTransformOrigin = new System.Windows.Point(0.0, 0.5);
            CoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            CoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            CoverScale.ScaleX = AnimationConfig.Book_CoverInitialScale;
            CoverScale.ScaleY = AnimationConfig.Book_CoverInitialScale;
            CoverSkew.AngleY = 0;
            CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            CoverTranslate.X = 0;

            ModMenuTranslate.X = 0;
            ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuSlideTranslate.X = -AnimationConfig.Book_SlideOffset;

            ModdingMenuContent.RenderTransformOrigin = new System.Windows.Point(0.0, 0.5);
            ModMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ModMenuSkew.BeginAnimation(SkewTransform.AngleYProperty, null);
            ModMenuScale.ScaleX = 1;
            ModMenuSkew.AngleY = 0;

            ModdingMenuButtons.Visibility = Visibility.Visible;
            ModdingMenuButtons.Opacity = 0;
            ModdingMenuButtons.IsHitTestVisible = true;

            // Ensure modding sidebar nav buttons stay interactive when entering via animated flow.
            if (NavProject != null) NavProject.Visibility = Visibility.Visible;
            if (NavTool != null) NavTool.Visibility = Visibility.Visible;
            if (NavOption != null) NavOption.Visibility = Visibility.Visible;

            // Transition Header (Rx 기반, ViewModel 사용)
            ViewModel.HeaderText = "모딩메뉴";
            TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);
            TxtMainHeader.Margin = GetHeaderDefaultMargin();
            var headerFadeTask = UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Header_FadeInDuration);

            var headerTranslate = TxtMainHeader.RenderTransform as TranslateTransform;
            if (headerTranslate != null)
                headerTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(AnimationConfig.Header_SlideStartX, 0, TimeSpan.FromMilliseconds(AnimationConfig.Header_SlideDuration)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            CoverScale.ScaleX = 1.0;
            CoverScale.ScaleY = 1.0;

            // [UPDATE] 책이 더 빨리 시작하도록 Book_OpenDelay 전에 0.2초 단축
            await System.Threading.Tasks.Task.Delay(Math.Max(0, AnimationConfig.Book_OpenDelay - 200));
            if (!IsLoaded)
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Phase 1: 책 열기 (사이드바/패널은 아직 안 움직임)
            System.Diagnostics.Debug.WriteLine("[ModWindow] Phase 1: 책 열기 시작 (한글)");

            var bookOpenTask = Observable.Merge(
                UIAnimationsRx.AnimateBook(BookCover, true, AnimationConfig.Book_OpenDuration),
                UIAnimationsRx.SlideX(ModdingMenuContent, -AnimationConfig.Book_SlideOffset, 0, AnimationConfig.Book_OpenDuration),
                UIAnimationsRx.Fade(ModdingMenuButtons, 0, 1, AnimationConfig.Fade_Duration)
            ).DefaultIfEmpty();
            SetModdingToolButtonsOpacity(AnimationConfig.ListEntrance_FromOpacity);

            var bgSlide = new DoubleAnimationUsingKeyFrames();
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(_bgShakeOffset, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(_bgSlideFirstKeyTimeSeconds))));
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(_bgSlideSecondKeyTimeSeconds))));
            ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, bgSlide);

            // 책 열기 완료 대기
            await bookOpenTask;
            _bookOpenCompletedAtUtc = DateTime.UtcNow;

            // Ensure book/page transforms are normalized before the next layout phase.
            CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            CoverTranslate.X = 0;
            ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuSlideTranslate.X = 0;
            AttachBookRefractionEffect();
            AttachModdingMedalRefractionEffects();

            // [UPDATE] 책이 완전히 펼쳐진 다음에 아이콘 버튼들이 등장
            AnimateModdingToolsEntrance();

            // Phase 2: 시선 여유 후 → 배경 확장 + 사이드바 축소 + 패널 마진 변경을 동시 시작
            // 책이 이미 펼쳐진 상태에서, 모든 레이아웃 변화가 함께 시작됨
            await System.Threading.Tasks.Task.Delay(AnimationConfig.Book_ExtraDelay);

            System.Diagnostics.Debug.WriteLine("[ModWindow] Phase 2: 배경 확장 + 사이드바 축소 + 책 이동 동시 시작 (한글)");

            // 모든 애니메이션을 동일 콜 스택에서 BeginAnimation → 같은 프레임에 시작
            var duration = TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // 1. 배경 확장 (StepProgress 0→0.5)
            AnimateSteppedLayoutTo(AnimationConfig.Background_StepProgress_ModdingMenu);
            var titleBarHideTask = AnimateGlobalTitleBarAsync(false);

            // 2. 사이드바 축소 (직접 BeginAnimation — Observable 지연 없음)
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            var sideAnim = new DoubleAnimation(LeftSidebarBorder.ActualWidth, AnimationConfig.Sidebar_ModdingMenu_Width, duration) { EasingFunction = easing };
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, sideAnim);
            UpdateSidebarClip();

            // 3. 패널 마진 변경 (직접 BeginAnimation — Observable 지연 없음)
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            var currentMargin = MainContentPanel.Margin;
            var targetMargin = new Thickness(
                AnimationConfig.MainPanel_ModdingMenu_MarginLeft,
                AnimationConfig.MainPanel_ModdingMenu_MarginTop,
                AnimationConfig.MainPanel_ModdingMenu_MarginRight,
                AnimationConfig.MainPanel_ModdingMenu_MarginBottom);
            var marginAnim = new ThicknessAnimation(currentMargin, targetMargin, duration) { EasingFunction = easing };
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);

            // [NEW] 4. 책 이동 애니메이션 (배경보다 빠르게 도착)
            // [FIX] StepProgress 보간식과 동일한 목표값으로 통일해 열기/닫기 오프셋 불일치 제거
            double targetBookLeft = CalculateBookLeftForProgress(AnimationConfig.Background_StepProgress_ModdingMenu);

            System.Diagnostics.Debug.WriteLine($"[ModWindow] 책 이동: {AnimationConfig.Book_BaseMarginLeft} → {targetBookLeft} (명시적 지정) (한글)");

            // 시작 위치는 원래 위치 (확장 전)
            var currentBookMargin = BookCover.Margin;
            var currentContentMargin = ModdingMenuContent.Margin;

            var bookDuration = TimeSpan.FromMilliseconds(AnimationConfig.Book_MoveDuration);
            var bookEasing = new CubicEase { EasingMode = EasingMode.EaseOut };

            _isBookMarginAnimationRunning = true;
            BookCover.BeginAnimation(FrameworkElement.MarginProperty, null);
            var bookMarginAnim = new ThicknessAnimation(
                currentBookMargin,  // From: 원래 위치
                GetBookCoverMargin(targetBookLeft),  // To: 목표 위치
                bookDuration)
            { EasingFunction = bookEasing };
            BookCover.BeginAnimation(FrameworkElement.MarginProperty, bookMarginAnim);

            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, null);
            var contentMarginAnim = new ThicknessAnimation(
                currentContentMargin,  // From: 원래 위치
                GetBookPageMargin(targetBookLeft),  // To: 목표 위치
                bookDuration)
            { EasingFunction = bookEasing };
            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, contentMarginAnim);

            // 완료 대기 (레이아웃 확장 시간만큼)
            await System.Threading.Tasks.Task.Delay((int)AnimationConfig.Transition_LayoutDuration);

            // [FIX] 애니메이션 완료 후 최종 위치로 명시적 설정 (재진입 시 올바른 초기화를 위해)
            SetBookLayoutMargins(targetBookLeft, clearAnimations: true);
            _isBookMarginAnimationRunning = false;
            AttachBookRefractionEffect();
            AttachModdingMedalRefractionEffects();

            await titleBarHideTask;

            // Final guard: keep sidebar controls interactive after transition completes.
            ModdingMenuButtons.IsHitTestVisible = true;
            if (BtnBackOnly != null)
            {
                BtnBackOnly.IsHitTestVisible = true;
                BtnBackOnly.Visibility = Visibility.Visible;
            }
            if (NavProject != null) NavProject.IsHitTestVisible = true;
            if (NavTool != null) NavTool.IsHitTestVisible = true;
            if (NavOption != null) NavOption.IsHitTestVisible = true;
            this.IsHitTestVisible = true;

            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionToModdingMenu 완료 (StepProgress=0.5) (한글)");
        }

        private void RestoreUI(NavState target, NavState from, object? context)
        {
            switch (target)
            {
                case NavState.ProjectList:
                    if (from == NavState.ModdingMenu)
                        TransitionBackToProjectList();
                    break;
                case NavState.ModdingMenu:
                    if (from == NavState.ToolWindow)
                    {
                        HideAllToolContents();
                        RecoverFromSelection();
                    }
                    break;
            }
        }

        private async void TransitionBackToProjectList()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionBackToProjectList 시작 - Rx 기반 전환됨 (한글)");
            CancelRunningToolSelectionTransition(resetToModdingMenuState: false);
            #region agent log
            try
            {
                var log = new
                {
                    sessionId = "debug-session",
                    runId = "run1",
                    hypothesisId = "H5",
                    location = "ModernModWindow.xaml.cs:TransitionBackToProjectList:entry",
                    message = "TransitionBackToProjectList entry",
                    data = new { fadeDurationMs = AnimationConfig.Fade_Duration, bookCloseDurationMs = AnimationConfig.Book_CloseDuration, headerText = ViewModel.HeaderText },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion

            // === Phase 0: 헤더 전환 시작 (슬라이드 우선) ===
            // [UPDATE] 유저 요청: 화면 전환 트렌지션보다 헤더 슬라이드가 먼저 나오게 설정
            ViewModel.HeaderText = "메인메뉴";
            var headerFadeOut = UIAnimationsRx.Fade(TxtMainHeader, 1, 0, AnimationConfig.Header_FadeOutDuration);
            headerFadeOut.Subscribe(_ =>
            {
                TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);
                UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Header_FadeInDuration).Subscribe();

                var headerTranslate = TxtMainHeader.RenderTransform as TranslateTransform;
                if (headerTranslate != null)
                    headerTranslate.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation(AnimationConfig.Header_SlideStartX, 0, TimeSpan.FromMilliseconds(AnimationConfig.Header_SlideDuration))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            });

            // === Phase 1: 책장 닫기 애니메이션 시작 ===
            await UIAnimationsRx.Fade(ModdingMenuButtons, 1, 0, AnimationConfig.Fade_Duration);

            // Normalize book/page anchors before close animation to avoid cumulative drift.
            _isBookMarginAnimationRunning = false;
            SetBookLayoutMargins(CalculateBookLeftForProgress(StepProgress), clearAnimations: true);
            CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            CoverTranslate.X = 0;
            ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuSlideTranslate.X = 0;
            ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuTranslate.X = 0;

            // 1. 책 닫기 (먼저 실행)
            await Observable.Merge(
                UIAnimationsRx.AnimateBook(BookCover, false, AnimationConfig.Book_CloseDuration),
                UIAnimationsRx.SlideX(ModdingMenuContent, 0, AnimationConfig.Book_SlideOffset, AnimationConfig.Book_CloseDuration)
            ).DefaultIfEmpty();

            // 2. 책 닫힘 완료 후 → 배경 축소 + 사이드바 확장 + 책 이동 + 페이드를 동시 시작
            System.Diagnostics.Debug.WriteLine("[ModWindow] 모딩→프로젝트 복귀: 배경 축소 + 사이드바 확장 동시 시작 (한글)");

            // 메인 메뉴 요소 준비 (미리 Visible로 설정하고 투명도 0으로 시작)
            ProjectMenuButtons.Visibility = Visibility.Visible;
            ProjectListView.Visibility = Visibility.Visible;
            RefreshProjectList();
            GlobalTitleBar.Visibility = Visibility.Collapsed;
            GlobalTitleBar.Opacity = 0;
            ProjectMenuButtons.Opacity = 0;
            ProjectListView.Opacity = 0;

            // [FIX] 책 페이드아웃 먼저 완료 (배경 경계면 문제 해결)
            int closeSyncFadeDuration = AnimationConfig.Book_CloseSyncFadeDuration;
            await Observable.Merge(
                UIAnimationsRx.Fade(ModdingMenuContent, 1, 0, closeSyncFadeDuration),
                UIAnimationsRx.Fade(BookCover, 1, 0, closeSyncFadeDuration),
                // 오른쪽으로 이동하며 사라짐
                UIAnimationsRx.SlideX(ModdingMenuContent, 0, AnimationConfig.Book_SlideOffset * 3, closeSyncFadeDuration),
                UIAnimationsRx.SlideX(BookCover, 0, AnimationConfig.Book_SlideOffset * 3, closeSyncFadeDuration)
            ).DefaultIfEmpty();

            // 모든 레이아웃 애니메이션을 동일 콜 스택에서 BeginAnimation → 같은 프레임에 시작
            var revDuration = TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration);
            var revEasing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // 배경 축소 (StepProgress 0.5→0)
            AnimateSteppedLayoutTo(0.0);

            // 사이드바 확장 (직접 BeginAnimation)
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            var revSideAnim = new DoubleAnimation(LeftSidebarBorder.ActualWidth, AnimationConfig.Sidebar_ProjectMenu_Width, revDuration) { EasingFunction = revEasing };
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, revSideAnim);
            UpdateSidebarClip();

            // 패널 마진 복원 (직접 BeginAnimation)
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            var revCurrentMargin = MainContentPanel.Margin;
            var revTargetMargin = new Thickness(
                AnimationConfig.MainPanel_ProjectMenu_MarginLeft,
                AnimationConfig.MainPanel_ProjectMenu_MarginTop,
                AnimationConfig.MainPanel_ProjectMenu_MarginRight,
                AnimationConfig.MainPanel_ProjectMenu_MarginBottom);
            var revMarginAnim = new ThicknessAnimation(revCurrentMargin, revTargetMargin, revDuration) { EasingFunction = revEasing };
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, revMarginAnim);

            // 메인 메뉴 요소 등장 (배경 축소와 병렬)
            var titleBarShowTask = ShowGlobalTitleBarWithDelayAsync(AnimationConfig.Fade_MainMenuAppearDelay);
            await Observable.Merge(
                Observable.FromAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(AnimationConfig.Fade_MainMenuAppearDelay);
                    return true;
                }).SelectMany(_ => UIAnimationsRx.Fade(ProjectMenuButtons, 0, 1, AnimationConfig.Fade_Duration)),
                Observable.FromAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(AnimationConfig.Fade_MainMenuAppearDelay);
                    return true;
                }).SelectMany(_ => BuildListEntranceAnimation(ProjectListView))
            ).DefaultIfEmpty();
            await titleBarShowTask;

            // Cleanup after parallel animations
            GlobalTitleBar.IsHitTestVisible = true;
            BookCover.Visibility = Visibility.Collapsed;
            ModdingMenuContent.Visibility = Visibility.Collapsed;
            ModdingMenuButtons.Visibility = Visibility.Collapsed;
            DetachModdingMedalRefractionEffects();
            DetachBookRefractionEffect();
            _isBookMarginAnimationRunning = false;
            CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            CoverTranslate.X = 0;
            ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuSlideTranslate.X = -AnimationConfig.Book_SlideOffset;
            ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuTranslate.X = 0;
            SetBookLayoutMargins(CalculateBookLeftForProgress(0.0), clearAnimations: true);

            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionBackToProjectList 완료 (한글)");
        }


        // 애니메이션 상태 저장용
        private System.Windows.Controls.Button? _activeTransitionButton;
        private FrameworkElement? _activeTransitionOriginElement;
        private Thickness _activeTransitionStartMargin;
        private double _activeTransitionWidth;
        private double _activeTransitionHeight;
        private bool _isSelectionFinished;

        // RecoverFromSelection 취소용 CTS (백 애니메이션 중 다른 버튼 누를 때 즉시 중단)
        private System.Threading.CancellationTokenSource? _recoverCts;

        #region ========================================
        #region === 🎬 애니메이션 설정 변수 ===
        #region ========================================
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ✅ 대부분의 변수는 AnimationConfig.cs 외부 파일로 이동했습니다!
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //
        // 📁 위치: UI/WPF/Animations/AnimationConfig.cs
        //
        // 이제 그 파일에서 모든 UI 설정을 디자이너 뷰처럼 편집할 수 있습니다:
        //   - 메인메뉴/모딩메뉴/도구메뉴 각 스텝별 창 크기
        //   - 패널 위치 & 여백
        //   - 트랜지션 타이밍
        //   - 애니메이션 속도
        //
        // ⚠️ 아래 변수들만 동적 계산이 필요해서 이곳에 남아있습니다:
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        #region 책 - 위치 설정 (AnimationConfig에서 참조)
        // → AnimationConfig.Book_BaseMargin*, AnimationConfig.Book_Open2Offset* 참조
        #endregion

        #region 메달 - 동적 위치 계산 (CS 전용)
        private double _medalHeaderXOffset = 20.0;       // 메달→헤더 비행 X 오프셋 (동적 계산)
        #endregion

        #region 레이아웃 - 동적 보간 계산 (CS 전용)
        // 배경 형태 보간용 (StepProgress 기반)
        private double _sidebarStartX = AnimationConfig.Background_SidebarStartX; // 프로젝트 메뉴: 사이드바 너비 (보간 시작점)
        private double _sidebarTargetX = 105.0;          // 모딩/도구 메뉴: 사이드바 너비 (보간 끝점)

        // 배경 외관 동적 계산
        private double _riserMaxHeight = AnimationConfig.Background_RiserMaxHeight; // 도구창 최대 상승 높이 (현재 미사용)
        private double _bgShakeOffset = -10.0;           // 배경 흔들림 거리 (동적 계산)
        #endregion

        #region 레이아웃 - 배경 슬라이드 키프레임 (고급 설정 - 동적 계산 필요)
        private double _bgSlideFirstKeyTimeSeconds = AnimationConfig.Background_SlideFirstKeyTime;
        private double _bgSlideSecondKeyTimeSeconds = AnimationConfig.Background_SlideSecondKeyTime;
        #endregion

        #endregion
        #endregion
        #endregion

        // === 🎬 애니메이션 설정 변수 끝 ===


        public bool IsSelectionFinished
        {
            get => _isSelectionFinished;
            set
            {
                if (_isSelectionFinished != value)
                {
                    _isSelectionFinished = value;
                    // Trigger moved to Manual Call in TransitionToToolWindow
                }
            }
        }

        private void CancelRunningToolSelectionTransition(bool resetToModdingMenuState)
        {
            var cts = _activeToolTransitionCts;
            if (cts == null) return;

            try
            {
                cts.Cancel();
            }
            catch
            {
            }

            if (resetToModdingMenuState)
            {
                ResetSelectionAnimationToModdingMenuState();
            }
        }

        private void ResetSelectionAnimationToModdingMenuState()
        {
            try
            {
                HideAllToolContents();

                SetTransitionOriginHidden(false);
                _activeTransitionOriginElement = null;

                TransitionProxy.BeginAnimation(UIElement.OpacityProperty, null);
                TransitionProxy.Opacity = 0;
                TransitionProxy.Visibility = Visibility.Collapsed;

                if (TransitionProxy.RenderTransform is TransformGroup grp)
                {
                    foreach (var child in grp.Children)
                    {
                        if (child is ScaleTransform s)
                        {
                            s.ScaleX = 1;
                            s.ScaleY = 1;
                        }
                        else if (child is TranslateTransform t)
                        {
                            t.X = 0;
                            t.Y = 0;
                        }
                    }
                }

                if (BookCover != null)
                {
                    BookCover.BeginAnimation(UIElement.OpacityProperty, null);
                    BookCover.Visibility = Visibility.Visible;
                    BookCover.Opacity = 1;
                }

                if (ModdingMenuContent != null)
                {
                    ModdingMenuContent.BeginAnimation(UIElement.OpacityProperty, null);
                    ModdingMenuContent.Visibility = Visibility.Visible;
                    ModdingMenuContent.Opacity = 1;
                }

                if (ModdingMenuButtons != null)
                {
                    ModdingMenuButtons.BeginAnimation(UIElement.OpacityProperty, null);
                    ModdingMenuButtons.Visibility = Visibility.Visible;
                    ModdingMenuButtons.Opacity = 1;
                }

                StepProgress = AnimationConfig.Background_StepProgress_ModdingMenu;
                _isBookMarginAnimationRunning = false;
                SetBookLayoutMargins(CalculateBookLeftForProgress(StepProgress), clearAnimations: true);

                CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                CoverTranslate.X = 0;
                ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuTranslate.X = 0;
                ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuSlideTranslate.X = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] Forward transition reset 오류: {ex.Message}");
            }
        }

        private void ThrowIfForwardTransitionInterrupted(System.Threading.CancellationToken token, int transitionVersion)
        {
            token.ThrowIfCancellationRequested();
            if (transitionVersion != _activeToolTransitionVersion)
            {
                throw new OperationCanceledException(token);
            }
        }

        private async System.Threading.Tasks.Task DelayForForwardTransitionAsync(int delayMs, System.Threading.CancellationToken token, int transitionVersion)
        {
            ThrowIfForwardTransitionInterrupted(token, transitionVersion);
            if (delayMs > 0)
            {
                await System.Threading.Tasks.Task.Delay(delayMs, token);
            }
            ThrowIfForwardTransitionInterrupted(token, transitionVersion);
        }

        private static string NormalizeHeaderText(string? text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }

        private static Thickness GetHeaderDefaultMargin()
        {
            return new Thickness(
                AnimationConfig.Header_MarginLeft,
                AnimationConfig.Header_MarginTop,
                AnimationConfig.Header_MarginRight,
                AnimationConfig.Header_MarginBottom);
        }

        private async System.Threading.Tasks.Task AnimateGlobalTitleBarAsync(bool show)
        {
            if (GlobalTitleBar == null) return;

            double fromY = show ? AnimationConfig.TitleBar_HiddenOffsetY : 0.0;
            double toY = show ? 0.0 : AnimationConfig.TitleBar_HiddenOffsetY;

            if (GlobalTitleBarTranslate != null)
            {
                GlobalTitleBarTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            }

            if (show)
            {
                GlobalTitleBar.Visibility = Visibility.Visible;
                GlobalTitleBar.IsHitTestVisible = true;
                if (GlobalTitleBarTranslate != null)
                {
                    GlobalTitleBarTranslate.Y = fromY;
                }

                var yAnim = new DoubleAnimation(fromY, toY, TimeSpan.FromMilliseconds(AnimationConfig.TitleBar_SlideDuration))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                GlobalTitleBarTranslate?.BeginAnimation(TranslateTransform.YProperty, yAnim);
                await UIAnimationsRx.Fade(GlobalTitleBar, GlobalTitleBar.Opacity, 1, AnimationConfig.Fade_Duration);
                GlobalTitleBar.Opacity = 1;
            }
            else
            {
                GlobalTitleBar.IsHitTestVisible = false;
                var yAnim = new DoubleAnimation(fromY, toY, TimeSpan.FromMilliseconds(AnimationConfig.TitleBar_SlideDuration))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                GlobalTitleBarTranslate?.BeginAnimation(TranslateTransform.YProperty, yAnim);
                await UIAnimationsRx.Fade(GlobalTitleBar, GlobalTitleBar.Opacity, 0, AnimationConfig.Fade_Duration);
                GlobalTitleBar.Opacity = 0;
                GlobalTitleBar.Visibility = Visibility.Collapsed;
                if (GlobalTitleBarTranslate != null)
                {
                    GlobalTitleBarTranslate.Y = AnimationConfig.TitleBar_HiddenOffsetY;
                }
            }
        }

        private async System.Threading.Tasks.Task ShowGlobalTitleBarWithDelayAsync(int delayMs)
        {
            if (delayMs > 0)
            {
                await System.Threading.Tasks.Task.Delay(delayMs);
            }

            await AnimateGlobalTitleBarAsync(true);
        }

        private static Thickness GetBookCoverMargin(double left)
        {
            return new Thickness(
                left,
                AnimationConfig.Book_BaseMarginTop,
                AnimationConfig.Book_BaseMarginRight,
                AnimationConfig.Book_BaseMarginBottom);
        }

        private static Thickness GetBookPageMargin(double left)
        {
            return new Thickness(
                left + AnimationConfig.Book_Open2OffsetX + AnimationConfig.Book_Page_LeftNudge,
                AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY,
                AnimationConfig.Book_BaseMarginRight,
                AnimationConfig.Book_BaseMarginBottom);
        }

        private void SetBookLayoutMargins(double left, bool clearAnimations = true)
        {
            if (BookCover == null || ModdingMenuContent == null) return;

            if (clearAnimations)
            {
                BookCover.BeginAnimation(FrameworkElement.MarginProperty, null);
                ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, null);
            }

            BookCover.Margin = GetBookCoverMargin(left);
            ModdingMenuContent.Margin = GetBookPageMargin(left);

            if (ModdingMenuContent.Visibility == Visibility.Visible)
            {
                AttachModdingMedalRefractionEffects();
            }
        }

        private void SetModdingToolButtonsOpacity(double opacity)
        {
            if (ModdingMenuContent == null || ModdingMenuContent.Items == null) return;

            int itemCount = ModdingMenuContent.Items.Count;
            for (int i = 0; i < itemCount; i++)
            {
                var container = ModdingMenuContent.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                if (container == null) continue;

                var button = FindVisualChild<Button>(container);
                if (button == null) continue;

                UIAnimationsRx.ClearAnimation(button, UIElement.OpacityProperty);
                button.Opacity = opacity;
                button.Visibility = Visibility.Visible;
            }
        }

        private double CalculateBookLeftForProgress(double progress)
        {
            double clamped = Math.Max(0.0, Math.Min(1.0, progress));
            double sidebarProgress = Math.Min(clamped * 2.0, 1.0);
            double targetSidebarX = AnimationConfig.Sidebar_ModdingMenu_Width + AnimationConfig.Background_SidebarGap;
            double expandedWidth = (_sidebarStartX - targetSidebarX) *
                sidebarProgress *
                Math.Max(0.0, Math.Min(1.0, AnimationConfig.Book_SidebarFollowFactor));
            double moddingNudge = AnimationConfig.Book_ModdingMenu_LeftNudge * sidebarProgress;
            double toolBlend = 0.0;
            if (clamped > AnimationConfig.Background_StepProgress_ModdingMenu)
            {
                toolBlend = Math.Min(
                    (clamped - AnimationConfig.Background_StepProgress_ModdingMenu) /
                    Math.Max(0.0001, AnimationConfig.Background_StepProgress_ToolMenu - AnimationConfig.Background_StepProgress_ModdingMenu),
                    1.0);
            }
            double toolNudge = (AnimationConfig.Book_ToolMenu_LeftNudge - AnimationConfig.Book_ModdingMenu_LeftNudge) * toolBlend;
            return AnimationConfig.Book_BaseMarginLeft - expandedWidth + moddingNudge + toolNudge + AnimationConfig.Book_GlobalCloseOffsetX;
        }

        private async System.Threading.Tasks.Task WaitForBookReadyForMedalAsync(System.Threading.CancellationToken token, int transitionVersion)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while ((_bookOpenCompletedAtUtc == DateTime.MinValue ||
                   StepProgress < AnimationConfig.Background_StepProgress_ModdingMenu - 0.01) &&
                   timer.ElapsedMilliseconds < 2000)
            {
                await DelayForForwardTransitionAsync(16, token, transitionVersion);
            }

            await DelayForForwardTransitionAsync(AnimationConfig.Medal_AfterBookReadyDelay, token, transitionVersion);
        }

        private async System.Threading.Tasks.Task PlaySelectionAnimation(System.Threading.CancellationToken token, int transitionVersion)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] PlaySelectionAnimation 시작 - Rx 기반 전환됨 (한글)");
                ThrowIfForwardTransitionInterrupted(token, transitionVersion);

                // 1. Setup Proxy Transform (Reset to Identity)
                var grp = new TransformGroup();
                var scaleT = new ScaleTransform(1, 1);
                var transT = new TranslateTransform(0, 0);
                grp.Children.Add(scaleT);
                grp.Children.Add(transT);

                TransitionProxy.RenderTransform = grp;
                TransitionProxy.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                TransitionProxy.Visibility = Visibility.Visible;
                TransitionProxy.Opacity = 1;

                // Set ZIndex to ensure it's on top
                System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxy);

                // Ensure medal bounce starts only after book-open sequence is fully settled.
                await WaitForBookReadyForMedalAsync(token, transitionVersion);

                // 2. Medal Popup Animation (Scale + Y movement)
                var duration = TimeSpan.FromMilliseconds(AnimationConfig.Medal_PopDuration);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var animScaleX = new DoubleAnimation(1.0, AnimationConfig.Medal_PopScale, duration)
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var animScaleY = new DoubleAnimation(1.0, AnimationConfig.Medal_PopScale, duration)
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var animMoveY = new DoubleAnimation(0, AnimationConfig.Medal_PopYOffset, duration)
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                };

                scaleT.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
                scaleT.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
                transT.BeginAnimation(TranslateTransform.YProperty, animMoveY);

                System.Diagnostics.Debug.WriteLine($"[ModWindow] Pop & Lift 시작 (한글): Margin={TransitionProxy.Margin}");

                // --- STEP 2: PAUSE & FLY TO HEADER ---
                await DelayForForwardTransitionAsync(AnimationConfig.Medal_PopDuration, token, transitionVersion);

                // [UPDATE] 유저 요청: 배경 확장 애니메이션 시작 시간을 0.4초±0.05초(350~450ms)로 조정
                // 메달 팝업이 300ms이므로, 추가 대기 시간 AnimationConfig 사용
                // 현재 시점: 300ms(팝업) + 100ms → 목표: 400ms 전후
                await DelayForForwardTransitionAsync(AnimationConfig.Transition_MedalPopDelay, token, transitionVersion);

                // Z-Index Management for "Behind Header" effect
                System.Windows.Controls.Panel.SetZIndex(TxtMainHeader, AnimationConfig.ZIndex_Header);
                System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxyBelowHeader);

                // Calculate Target (Header)
                var rootGrid = VisualTreeHelper.GetParent(TransitionProxy) as UIElement;
                if (rootGrid == null) return;

                var headerTransform = TxtMainHeader.TransformToVisual(rootGrid);
                var headerPos = headerTransform.Transform(new System.Windows.Point(0, 0));

                double targetX = headerPos.X - TransitionProxy.Margin.Left + _medalHeaderXOffset;
                double targetY = headerPos.Y - TransitionProxy.Margin.Top;

                System.Diagnostics.Debug.WriteLine($"[ModWindow] 헤더로 비행 시작 (한글): {targetX}, {targetY}");

                // Flight animation
                var flightDuration = TimeSpan.FromMilliseconds(AnimationConfig.Medal_FlyDuration);
                var flightEase = new SineEase { EasingMode = EasingMode.EaseIn };

                var animFlyX = new DoubleAnimation(0, targetX, flightDuration) { EasingFunction = flightEase, FillBehavior = FillBehavior.HoldEnd };
                var animFlyY = new DoubleAnimation(AnimationConfig.Medal_PopYOffset, targetY, flightDuration) { EasingFunction = flightEase, FillBehavior = FillBehavior.HoldEnd };
                var animScaleDownX = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, flightDuration) { EasingFunction = flightEase, FillBehavior = FillBehavior.HoldEnd };
                var animScaleDownY = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, flightDuration) { EasingFunction = flightEase, FillBehavior = FillBehavior.HoldEnd };
                var animFade = new DoubleAnimation(1.0, 0.0, flightDuration) { EasingFunction = flightEase, FillBehavior = FillBehavior.HoldEnd };

                transT.BeginAnimation(TranslateTransform.XProperty, animFlyX);
                transT.BeginAnimation(TranslateTransform.YProperty, animFlyY);
                scaleT.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleDownX);
                scaleT.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleDownY);
                TransitionProxy.BeginAnimation(UIElement.OpacityProperty, animFade);

                // Wait for animations to complete
                await DelayForForwardTransitionAsync(AnimationConfig.Medal_FlyDuration + AnimationConfig.Medal_FlyExtraDelay, token, transitionVersion);

                // --- STEP 3: TRANSITION TO TOOL ---

                // Update Header Text (Rx 기반, ViewModel 사용)
                if (_activeTransitionButton != null && _activeTransitionButton.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
                {
                    string cleanTitle = vm.Title.Replace("\r", "").Replace("\n", " ");
                    await UIAnimationsRx.Fade(TxtMainHeader, 1, 0, AnimationConfig.Header_FadeOutDuration);
                    ViewModel.HeaderText = cleanTitle;
                    TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);
                    TxtMainHeader.Margin = GetHeaderDefaultMargin();

                    await UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Fade_Duration);
                    ThrowIfForwardTransitionInterrupted(token, transitionVersion);
                }

                // [NEW] 유저 요청: 헤더 표시 후 0.1초(100ms) 대기
                System.Diagnostics.Debug.WriteLine("[ModWindow] 헤더 표시 완료, 0.1초 대기 후 배경 확장 시작 (한글)");
                await DelayForForwardTransitionAsync(AnimationConfig.Tool_HeaderBeforeBackgroundDelay, token, transitionVersion);

                // [UPDATE] 2단계 확장 시스템: 도구 진입 시 0.5 → 1.0 (위쪽 추가 확장)
                // 이미 모딩 메뉴에서 0.5까지 확장되어 있으므로, 여기서는 0.5 → 1.0만 애니메이션
                System.Diagnostics.Debug.WriteLine("[ModWindow] 도구 진입 2단계 확장 시작 (0.5→1.0, 위쪽 추가) (한글)");
                AnimateSteppedLayoutTo(1.0);
                /* Compact 확장 로직 비활성화 (요청 사항):
                 * StepProgress 0.5→1.0의 위쪽 확장 로직만 유지하고,
                 * MainContentPanel/MainContentRootGrid compact 마진 애니메이션은 제거.
                 * 유지 이유: 히스토리 보존 / 재활성 대비.
                 */
                // AnimateToolCompactLayout(true);

                // [FIX TIMING] Trigger Book Close HERE (Rx 기반)
                var bookCloseTask = UIAnimationsRx.AnimateBook(BookCover, false, AnimationConfig.Book_CloseDuration);

                // Fade out background & book cover (Rx 기반, 병렬)
                var fadeTask = Observable.Merge(
                    UIAnimationsRx.Fade(ModdingMenuContent, 1, 0, AnimationConfig.Fade_Duration),
                    UIAnimationsRx.Fade(BookCover, 1, 0, AnimationConfig.Fade_Duration)
                ).DefaultIfEmpty();

                // Book slide animation
                var animCoverRight = new DoubleAnimation(0, AnimationConfig.Book_SlideOffset, TimeSpan.FromMilliseconds(AnimationConfig.Book_SlideDuration))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                    FillBehavior = FillBehavior.HoldEnd
                };
                CoverTranslate.BeginAnimation(TranslateTransform.XProperty, animCoverRight);
                ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, animCoverRight);

                // Wait for fade out
                await fadeTask;
                ThrowIfForwardTransitionInterrupted(token, transitionVersion);

                // [UPDATE] 배경 확장 시작 전에 초기화 시작 (렉 방지)
                System.Diagnostics.Debug.WriteLine("[ModWindow] 콘텐츠 초기화 시작 (배경 확장과 병렬) (한글)");

                // Reset layout
                // [FIX] 배경 유지: Riser 초기화(0으로 설정)를 제거하여 기존 상태 유지
                // 도구 창이 열릴 때 ShowCharacterInfoContent에서 다시 Riser(true)를 호출하므로,
                // 여기서 0으로 끄면 "작아졌다가 커지는" 현상이 발생함.
                // AnimateRiser(false);
                // [FIX] 배경이 순간적으로 축소되는 현상을 방지하기 위해 주석 처리
                // AnimateSteppedLayout(false);

                // Show tool interface
                var hasTool = false;
                if (_activeTransitionButton?.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vmHasTool)
                    hasTool = HasConnectedTool(vmHasTool);

                // [NEW] 사이드바를 투명하게 준비
                SetToolEmptyToolbar(true, fadeIn: false);

                // [Reveal Effect] 확장 중에는 준비만 (초기화는 확장 후에 실행해 렉 방지)
                bool shouldShowCharacterInfo = false;
                bool shouldShowCharacterScale = false;
                bool shouldShowYokaiStats = false;
                bool shouldShowEncounterEditor = false;

                if (_activeTransitionButton?.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vmReveal)
                {
                    if (vmReveal.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.CharacterInfo)
                    {
                        shouldShowCharacterInfo = true;
                        PrepareCharacterInfoContentForReveal();
                    }
                    else if (vmReveal.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.CharacterScale)
                    {
                        shouldShowCharacterScale = true;
                        PrepareCharacterScaleContentForReveal();
                    }
                    else if (vmReveal.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.YokaiStats)
                    {
                        shouldShowYokaiStats = true;
                        PrepareYokaiStatsContentForReveal();
                    }
                    else if (vmReveal.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.EncounterEditor)
                    {
                        shouldShowEncounterEditor = true;
                        PrepareEncounterEditorContentForReveal();
                    }

                }

                // 배경 확장 완료 대기
                System.Diagnostics.Debug.WriteLine("[ModWindow] 배경 확장 완료 대기 (한글)");
                await DelayForForwardTransitionAsync(AnimationConfig.Transition_LayoutDuration, token, transitionVersion);
                RequestToolHostLayoutUpdate("Tool transition layout completed", force: true);
                FinalizeToolLayoutOnce("Tool transition layout completed");

                // 확장 완료 직후 즉시 페이드인만 재생 (초기화는 페이드인 완료 후 실행해 애니메이션 취소 방지)
                System.Diagnostics.Debug.WriteLine("[ModWindow] 사이드바 & 콘텐츠 페이드인 시작 (한글)");

                var fadeInTasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
                if (ToolSidebarButtons != null)
                    fadeInTasks.Add(WaitObservable(UIAnimationsRx.Fade(ToolSidebarButtons, 0, 1, AnimationConfig.Fade_Duration)));

                if (shouldShowCharacterInfo && CharacterInfoContent != null && CharacterInfoContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(BuildListEntranceAnimation(CharacterInfoContent)));

                if (shouldShowCharacterScale && CharacterScaleContent != null && CharacterScaleContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(BuildListEntranceAnimation(CharacterScaleContent)));

                if (shouldShowYokaiStats && YokaiStatsContent != null && YokaiStatsContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(BuildListEntranceAnimation(YokaiStatsContent)));
                if (shouldShowEncounterEditor && EncounterEditorContent != null && EncounterEditorContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(BuildListEntranceAnimation(EncounterEditorContent)));

                if (fadeInTasks.Count > 0)
                    await System.Threading.Tasks.Task.WhenAll(fadeInTasks);
                ThrowIfForwardTransitionInterrupted(token, transitionVersion);

                // [FIX] 페이드인 완료 후 Opacity를 1로 명시적으로 고정 (애니메이션 종료 후 사라짐 방지)
                if (shouldShowCharacterInfo && CharacterInfoContent != null) CharacterInfoContent.Opacity = 1;
                if (shouldShowCharacterScale && CharacterScaleContent != null) CharacterScaleContent.Opacity = 1;
                if (shouldShowYokaiStats && YokaiStatsContent != null) YokaiStatsContent.Opacity = 1;

                // 페이드인 완료 후 초기화 실행 (콘텐츠 채우기)
                if (shouldShowCharacterInfo)
                    _ = InitializeCharacterInfoContentAsync();
                else if (shouldShowCharacterScale)
                    _ = InitializeCharacterScaleContentAsync();
                else if (shouldShowYokaiStats)
                    _ = InitializeYokaiStatsContentAsync();
                else if (shouldShowEncounterEditor)
                    _ = InitializeEncounterEditorContentAsync();

                // [REMOVED] Transition_ToolRevealDelay는 이제 배경 확장 완료 후이므로 불필요
                // await System.Threading.Tasks.Task.Delay(AnimationConfig.Transition_ToolRevealDelay);

                // --- STEP 4: OPEN TOOL WINDOW ---
                if (_activeTransitionButton?.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vmTool)
                {
                    // Integrated Tools are handled above (Fade-In + Init)
                    // Legacy Tools are handled below (OpenToolWindow)
                    if (vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.CharacterInfo &&
                        vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.CharacterScale &&
                        vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.YokaiStats &&
                        vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.EncounterEditor)
                    {
                        if (HasConnectedTool(vmTool))
                            OpenToolWindow(vmTool);
                        else
                            SetToolEmptyToolbar(true);
                    }
                }

                await DelayForForwardTransitionAsync(AnimationConfig.Transition_ToolFinalDelay, token, transitionVersion);

                // Restore ZIndexes and Visibility
                System.Windows.Controls.Panel.SetZIndex(TxtMainHeader, 0);
                TransitionProxy.Visibility = Visibility.Collapsed;

                if (!hasTool)
                    SetToolEmptyToolbar(true);

                System.Diagnostics.Debug.WriteLine("[ModWindow] PlaySelectionAnimation 완료 (한글)");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetTransitionOriginHidden(false);
                _activeTransitionOriginElement = null;
                System.Diagnostics.Debug.WriteLine($"[ModWindow] PlaySelectionAnimation 오류: {ex.Message}");
            }
        }

        private static System.Threading.Tasks.Task WaitObservable(IObservable<System.Reactive.Unit> observable)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var subscription = observable.Subscribe(
                _ => { },
                ex => tcs.TrySetException(ex),
                () => tcs.TrySetResult(true)
            );

            tcs.Task.ContinueWith(_ => subscription.Dispose());
            return tcs.Task;
        }

        private IObservable<System.Reactive.Unit> BuildListEntranceAnimation(FrameworkElement element)
        {
            if (!AnimationConfig.ListEntrance_Enable)
                return UIAnimationsRx.Fade(element, 0, 1, AnimationConfig.Fade_Duration);

            return UIAnimationsRx.DropInBounce(
                element,
                durationMs: AnimationConfig.ListEntrance_DurationMs,
                fromOffsetY: AnimationConfig.ListEntrance_OffsetY,
                fromScale: AnimationConfig.ListEntrance_FromScale,
                toScale: AnimationConfig.ListEntrance_ToScale,
                fromOpacity: AnimationConfig.ListEntrance_FromOpacity,
                toOpacity: AnimationConfig.ListEntrance_ToOpacity,
                bounceAmplitude: AnimationConfig.ListEntrance_BounceAmplitude
            );
        }



        private void OpenToolWindow(ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
        {
            try
            {
                // New Integrated Tool Logic
                if (vm.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.CharacterInfo)
                {
                    _ = ShowCharacterInfoContentAsync();
                    return;
                }
                else if (vm.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.CharacterScale)
                {
                    _ = ShowCharacterScaleContentAsync();
                    return;
                }
                else if (vm.MToolType == ICN_T2.UI.WPF.ViewModels.ToolType.YokaiStats)
                {
                    _ = ShowYokaiStatsContentAsync();
                    return;
                }

                // Legacy Dialog Logic
                // For now, only index 1 (Character Info) is connected.
                if (vm.IconIndex != 1)
                    return;

                // Allow null CurrentGame for Design Testing
                System.Diagnostics.Debug.WriteLine("[Tool] Opening CharabaseWindow Dialog...");

                using (var window = new ICN_T2.UI.CharabaseWindow(CurrentGame!))
                {
                    var result = window.ShowDialog();
                    System.Diagnostics.Debug.WriteLine($"[Tool] Dialog Closed. Result: {result}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"도구 창을 여는 중 오류 발생: {ex.Message}");
            }
        }

        // === Stepped Layout System ===
        // [EXISTING] StepProgress (Horizontal Expansion)
        public static readonly DependencyProperty StepProgressProperty =
            DependencyProperty.Register("StepProgress", typeof(double), typeof(ModernModWindow),
                new PropertyMetadata(0.0, OnStepProgressChanged));

        public double StepProgress
        {
            get => (double)GetValue(StepProgressProperty);
            set => SetValue(StepProgressProperty, value);
        }

        // [NEW] RiserProgress (Vertical Rise)
        public static readonly DependencyProperty RiserProgressProperty =
            DependencyProperty.Register("RiserProgress", typeof(double), typeof(ModernModWindow),
                new PropertyMetadata(0.0, OnStepProgressChanged));

        public double RiserProgress
        {
            get => (double)GetValue(RiserProgressProperty);
            set => SetValue(RiserProgressProperty, value);
        }


        private static void OnStepProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernModWindow window)
            {
                window.UpdateSteppedPath();
                window.UpdateBookPositionFromProgress();

                // ViewModel과 동기화
                if (window.ViewModel != null)
                {
                    window.ViewModel.StepProgress = (double)e.NewValue;
                }
            }
        }

        /// <summary>
        /// 배경의 currentSidebarX에 비례하여 책/속지 위치를 업데이트합니다.
        /// StepProgress가 변할 때마다 호출되어 배경 확장과 책이 함께 움직입니다.
        /// </summary>
        private void UpdateBookPositionFromProgress()
        {
            if (BookCover == null || ModdingMenuContent == null) return;
            if (BookCover.Visibility != Visibility.Visible) return;
            if (_isBookMarginAnimationRunning) return;

            double bookLeft = CalculateBookLeftForProgress(StepProgress);
            SetBookLayoutMargins(bookLeft, clearAnimations: false);
        }

        private static bool IsSameWindowSize(System.Windows.Size a, System.Windows.Size b)
        {
            return Math.Abs(a.Width - b.Width) < 0.5 &&
                   Math.Abs(a.Height - b.Height) < 0.5;
        }

        private System.Windows.Size GetCurrentWindowSize()
        {
            return new System.Windows.Size(ActualWidth, ActualHeight);
        }

        private void ResetToolLayoutSession(string reason)
        {
            _isToolLayoutLocked = false;
            _isToolLayoutFinalized = false;
            _lastToolLayoutWindowSize = System.Windows.Size.Empty;
            System.Diagnostics.Debug.WriteLine($"[ModWindow] Tool layout reset: {reason} (한글)");
        }

        private void BeginToolLayoutSession(string reason)
        {
            _isToolLayoutLocked = false;
            _isToolLayoutFinalized = false;
            _lastToolLayoutWindowSize = System.Windows.Size.Empty;
            System.Diagnostics.Debug.WriteLine($"[ModWindow] Tool layout session begin: {reason} (한글)");
        }

        private void FinalizeToolLayoutOnce(string reason)
        {
            if (_isToolLayoutFinalized) return;

            _isToolLayoutFinalized = true;
            _isToolLayoutLocked = true;
            _lastToolLayoutWindowSize = GetCurrentWindowSize();
            System.Diagnostics.Debug.WriteLine($"[ModWindow] Tool layout finalized: {reason} (한글)");
        }

        private bool CanRecalculateToolLayout(bool force)
        {
            if (force) return true;
            if (_isToolLayoutLocked) return false;

            var currentSize = GetCurrentWindowSize();
            if (!_lastToolLayoutWindowSize.IsEmpty && IsSameWindowSize(currentSize, _lastToolLayoutWindowSize))
            {
                return false;
            }

            return true;
        }

        private void RequestToolHostLayoutUpdate(string reason, bool force = false)
        {
            if (!CanRecalculateToolLayout(force))
            {
                return;
            }

            AdjustCharacterInfoPosition();
            _lastToolLayoutWindowSize = GetCurrentWindowSize();
            System.Diagnostics.Debug.WriteLine($"[ModWindow] Tool layout update applied: {reason}, force={force} (한글)");
        }

        /// <summary>
        /// 표시 준비만 수행 (초기화/페이드인 제외)
        /// </summary>
        private void PrepareCharacterInfoContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterInfoContentForReveal 시작 (한글)");

            HideAllToolContents();
            BeginToolLayoutSession("PrepareCharacterInfoContentForReveal");

            // Apply final host layout before first visible frame to avoid pop/shrink.
            RequestToolHostLayoutUpdate("CharacterInfo pre-visible", force: true);

            // 초기 상태 설정
            UIAnimationsRx.ClearAnimation(CharacterInfoContent, UIElement.OpacityProperty);
            CharacterInfoContent.Opacity = 0;
            CharacterInfoContent.Visibility = Visibility.Visible;
            RequestToolHostLayoutUpdate("CharacterInfo became visible", force: true);

            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterInfoContentForReveal 완료 (한글)");
        }

        /// <summary>
        /// 데이터 초기화만 수행 (배경 확장 완료 후 실행)
        /// </summary>
        private async System.Threading.Tasks.Task InitializeCharacterInfoContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterInfoContent 시작 (한글)");
            var perfTotal = System.Diagnostics.Stopwatch.StartNew();

            // 초기화 실행 (백그라운드)
            if (CharacterInfoContent is ICN_T2.UI.WPF.Views.CharacterInfoV3 view && CurrentGame != null)
            {
                var initTimer = System.Diagnostics.Stopwatch.StartNew();
                await Dispatcher.InvokeAsync(() => view.Initialize(CurrentGame), DispatcherPriority.Background);
                System.Diagnostics.Debug.WriteLine($"[Perf] CharacterInfo Initialize: {initTimer.ElapsedMilliseconds}ms");
            }

            // 렌더링 안정화
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            // 도구 패널 셰이더 연결(모든 도구뷰 공통 로직)
            var shaderAttachTimer = System.Diagnostics.Stopwatch.StartNew();
            _toolPanelAttachRetryCount = 0;
            AttachToolPanelRefractionEffects();
            AttachToolInteractiveRefractionEffects();
            RequestToolHostLayoutUpdate("CharacterInfo init completed", force: true);
            FinalizeToolLayoutOnce("CharacterInfo init completed");
            System.Diagnostics.Debug.WriteLine($"[Perf] CharacterInfo shader attach: {shaderAttachTimer.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"[Perf] InitializeCharacterInfoContentAsync total: {perfTotal.ElapsedMilliseconds}ms");

            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterInfoContent 완료 (한글)");
        }

        private async System.Threading.Tasks.Task ShowCharacterInfoContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterInfoContent 시작 - Rx 기반 전환됨 (한글)");
            #region agent log
            try
            {
                var log = new
                {
                    sessionId = "debug-session",
                    runId = "run1",
                    hypothesisId = "H7",
                    location = "ModernModWindow.xaml.cs:ShowCharacterInfoContent:entry",
                    message = "ShowCharacterInfoContent entry",
                    data = new { hasCurrentGame = CurrentGame != null },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion

            // 표시 준비 + 초기화 실행
            PrepareCharacterInfoContentForReveal();
            await InitializeCharacterInfoContentAsync();
            RequestToolHostLayoutUpdate("ShowCharacterInfoContent post-init", force: true);
            FinalizeToolLayoutOnce("ShowCharacterInfoContent");

            System.Diagnostics.Debug.WriteLine($"[ModWindow] CharacterInfoContent 페이드인 시작: Opacity={CharacterInfoContent.Opacity} (한글)");

            // 경량 바운스 진입 (검색 내부 항목은 제외되고 컨테이너만 적용됨)
            await WaitObservable(BuildListEntranceAnimation(CharacterInfoContent));

            ViewModel.HeaderText = "캐릭터 기본정보";
            TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);

            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterInfoContent 완료 - 페이드인 적용 (300ms) (한글)");
        }

        // === Character Scale Content Methods ===

        private void PrepareCharacterScaleContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterScaleContentForReveal 시작 (한글)");
            HideAllToolContents();
            BeginToolLayoutSession("PrepareCharacterScaleContentForReveal");
            RequestToolHostLayoutUpdate("CharacterScale pre-visible", force: true);
            UIAnimationsRx.ClearAnimation(CharacterScaleContent, UIElement.OpacityProperty);
            CharacterScaleContent.Opacity = 0;
            CharacterScaleContent.Visibility = Visibility.Visible;
            RequestToolHostLayoutUpdate("CharacterScale became visible", force: true);
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterScaleContentForReveal 완료 (한글)");
        }

        private async System.Threading.Tasks.Task InitializeCharacterScaleContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterScaleContent 시작 (한글)");
            var perfTotal = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (CharacterScaleContent is ICN_T2.UI.WPF.Views.CharacterScaleView view && CurrentGame != null)
                {
                    view.Initialize(CurrentGame);
                    if (view.DataContext is not ICN_T2.UI.WPF.ViewModels.CharacterScaleViewModel)
                    {
                        System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleViewModel 생성 및 할당 (한글)");
                        var vmTimer = System.Diagnostics.Stopwatch.StartNew();
                        view.DataContext = new ICN_T2.UI.WPF.ViewModels.CharacterScaleViewModel(CurrentGame);
                        System.Diagnostics.Debug.WriteLine($"[Perf] CharacterScale VM init: {vmTimer.ElapsedMilliseconds}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] InitializeCharacterScaleContent 오류: {ex.Message}");
            }

            await System.Threading.Tasks.Task.CompletedTask;

            // 도구 패널 셰이더 연결(모든 도구뷰 공통 로직)
            var shaderAttachTimer = System.Diagnostics.Stopwatch.StartNew();
            _toolPanelAttachRetryCount = 0;
            AttachToolPanelRefractionEffects();
            AttachToolInteractiveRefractionEffects();
            RequestToolHostLayoutUpdate("CharacterScale init completed", force: true);
            FinalizeToolLayoutOnce("CharacterScale init completed");
            System.Diagnostics.Debug.WriteLine($"[Perf] CharacterScale shader attach: {shaderAttachTimer.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"[Perf] InitializeCharacterScaleContentAsync total: {perfTotal.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterScaleContent 완료 (한글)");
        }

        private async System.Threading.Tasks.Task ShowCharacterScaleContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterScaleContent 시작 (한글)");

            PrepareCharacterScaleContentForReveal();
            await InitializeCharacterScaleContentAsync();
            RequestToolHostLayoutUpdate("ShowCharacterScaleContent post-init", force: true);
            FinalizeToolLayoutOnce("ShowCharacterScaleContent");

            await WaitObservable(BuildListEntranceAnimation(CharacterScaleContent));

            CharacterScaleContent.Opacity = 1;
            CharacterScaleContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterScaleContent 완료 (한글)");
        }

        // === Yokai Stats Content Methods ===

        private void PrepareYokaiStatsContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareYokaiStatsContentForReveal 시작 (한글)");
            BeginToolLayoutSession("PrepareYokaiStatsContentForReveal");
            RequestToolHostLayoutUpdate("YokaiStats pre-visible", force: true);
            UIAnimationsRx.ClearAnimation(YokaiStatsContent, UIElement.OpacityProperty);
            YokaiStatsContent.Opacity = 0;
            YokaiStatsContent.Visibility = Visibility.Visible;
            RequestToolHostLayoutUpdate("YokaiStats became visible", force: true);
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareYokaiStatsContentForReveal 완료 (한글)");
        }

        private async System.Threading.Tasks.Task InitializeYokaiStatsContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeYokaiStatsContent 시작 (한글)");
            var perfTotal = System.Diagnostics.Stopwatch.StartNew();

            if (YokaiStatsContent is ICN_T2.UI.WPF.Views.YokaiStatsView view && CurrentGame != null)
            {
                if (view.DataContext is not ICN_T2.UI.WPF.ViewModels.YokaiStatsViewModel)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] YokaiStatsViewModel 생성 및 할당 (한글)");
                    var vmTimer = System.Diagnostics.Stopwatch.StartNew();
                    view.Initialize(CurrentGame);
                    view.DataContext = new ICN_T2.UI.WPF.ViewModels.YokaiStatsViewModel(CurrentGame);
                    System.Diagnostics.Debug.WriteLine($"[Perf] YokaiStats VM init: {vmTimer.ElapsedMilliseconds}ms");
                }
            }

            await System.Threading.Tasks.Task.CompletedTask;

            // 도구 패널 셰이더 연결(모든 도구뷰 공통 로직)
            var shaderAttachTimer = System.Diagnostics.Stopwatch.StartNew();
            _toolPanelAttachRetryCount = 0;
            AttachToolPanelRefractionEffects();
            AttachToolInteractiveRefractionEffects();
            RequestToolHostLayoutUpdate("YokaiStats init completed", force: true);
            FinalizeToolLayoutOnce("YokaiStats init completed");
            System.Diagnostics.Debug.WriteLine($"[Perf] YokaiStats shader attach: {shaderAttachTimer.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"[Perf] InitializeYokaiStatsContentAsync total: {perfTotal.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeYokaiStatsContent 완료 (한글)");
        }

        private async System.Threading.Tasks.Task ShowYokaiStatsContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowYokaiStatsContent 시작 (한글)");

            PrepareYokaiStatsContentForReveal();
            await InitializeYokaiStatsContentAsync();
            RequestToolHostLayoutUpdate("ShowYokaiStatsContent post-init", force: true);
            FinalizeToolLayoutOnce("ShowYokaiStatsContent");

            await WaitObservable(BuildListEntranceAnimation(YokaiStatsContent));

            YokaiStatsContent.Opacity = 1;
            YokaiStatsContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowYokaiStatsContent 완료 (한글)");
        }

        private void PrepareEncounterEditorContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareEncounterEditorContentForReveal 시작");
            BeginToolLayoutSession("PrepareEncounterEditorContentForReveal");

            UIAnimationsRx.ClearAnimation(EncounterEditorContent, UIElement.OpacityProperty);
            EncounterEditorContent.Opacity = 0;
            EncounterEditorContent.Visibility = Visibility.Visible;

            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareEncounterEditorContentForReveal 완료");
        }

        private async System.Threading.Tasks.Task InitializeEncounterEditorContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeEncounterEditorContent 시작");

            if (EncounterEditorContent.DataContext == null && CurrentGame != null)
            {
                // Create ViewModel if needed
                EncounterEditorContent.DataContext = new ICN_T2.UI.WPF.ViewModels.EncounterViewModel(CurrentGame);
            }
            else if (CurrentGame == null)
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] ERROR: CurrentGame is null. Cannot initialize EncounterViewModel.");
            }

            // Simulate heavy init if needed or just yield
            await System.Threading.Tasks.Task.Delay(1);

            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeEncounterEditorContent 완료");
        }

        private async System.Threading.Tasks.Task ShowEncounterEditorContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowEncounterEditorContent 시작");

            PrepareEncounterEditorContentForReveal();
            await InitializeEncounterEditorContentAsync();
            RequestToolHostLayoutUpdate("ShowEncounterEditorContent post-init", force: true);
            FinalizeToolLayoutOnce("ShowEncounterEditorContent");

            await WaitObservable(BuildListEntranceAnimation(EncounterEditorContent));

            EncounterEditorContent.Opacity = 1;
            EncounterEditorContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowEncounterEditorContent 완료");
        }

        // ----------------------------------------------------------------------------------
        // [NEW] Staggered Drop-In Bounce for Modding Menu Buttons
        // 모딩 메뉴 진입 시 버튼들이 위→아래, 왼→오 순서로 가볍게 등장
        // ----------------------------------------------------------------------------------
        private void AnimateModdingToolsEntrance()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateModdingToolsEntrance 시작 (한글)");

                if (ModdingMenuContent == null || ModdingMenuContent.Items == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateModdingToolsEntrance 스킵: ModdingMenuContent가 null (한글)");
                    return;
                }

                if (!AnimationConfig.ListEntrance_Enable)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateModdingToolsEntrance 스킵: ListEntrance 비활성 (한글)");
                    return;
                }

                AttachModdingMedalRefractionEffects();

                int itemCount = ModdingMenuContent.Items.Count;
                System.Diagnostics.Debug.WriteLine($"[ModWindow] 모딩 메뉴 버튼 수: {itemCount} (한글)");

                var orderedButtons = new List<(Button Button, double Y, double X)>();
                bool requiresDeferredPass = false;

                for (int i = 0; i < itemCount; i++)
                {
                    // ItemContainerGenerator로 각 버튼 컨테이너 가져오기
                    var container = ModdingMenuContent.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container == null)
                    {
                        requiresDeferredPass = true;
                        continue;
                    }

                    // Button 찾기
                    var button = FindVisualChild<Button>(container);
                    if (button == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModWindow] 버튼 {i}를 찾을 수 없음 (한글)");
                        continue;
                    }

                    double x = 0;
                    double y = 0;
                    try
                    {
                        var transform = button.TransformToVisual(ModdingMenuContent);
                        var position = transform.Transform(new Point(0, 0));
                        x = position.X;
                        y = position.Y;
                    }
                    catch
                    {
                        // 좌표 추출 실패 시 인덱스 순서 fallback
                        x = i;
                        y = i;
                    }

                    orderedButtons.Add((button, y, x));
                }

                if (requiresDeferredPass)
                {
                    // 컨테이너 생성이 늦은 경우 다음 레이아웃 사이클에서 다시 시도
                    Dispatcher.InvokeAsync(AnimateModdingToolsEntrance, DispatcherPriority.Loaded);
                    return;
                }

                if (orderedButtons.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateModdingToolsEntrance 스킵: 대상 버튼 없음 (한글)");
                    return;
                }

                // 요청 순서: 위→아래 우선, 같은 줄에서는 왼→오
                orderedButtons.Sort((a, b) =>
                {
                    int yCompare = a.Y.CompareTo(b.Y);
                    return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
                });

                var orderedElements = new List<FrameworkElement>(orderedButtons.Count);
                foreach (var entry in orderedButtons)
                    orderedElements.Add(entry.Button);

                // Pre-hide all targets so nothing flashes before its stagger slot begins.
                foreach (var element in orderedElements)
                {
                    UIAnimationsRx.ClearAnimation(element, UIElement.OpacityProperty);
                    element.Visibility = Visibility.Visible;
                    element.Opacity = AnimationConfig.ListEntrance_FromOpacity;
                }

                // Requirement: total entrance window must be 0.4s from first start to last completion.
                double totalWindowMs = AnimationConfig.ModdingToolsEntrance_TotalWindowMs;
                double itemDurationMs = Math.Max(1.0, Math.Min(AnimationConfig.ModdingToolsEntrance_ItemDurationMs, totalWindowMs));
                double staggerDelayMs = 0.0;
                if (orderedElements.Count > 1)
                {
                    staggerDelayMs = Math.Max(0.0, (totalWindowMs - itemDurationMs) / (orderedElements.Count - 1));
                }

                UIAnimationsRx.StaggeredDropIn(
                    orderedElements,
                    durationMs: itemDurationMs,
                    staggerDelayMs: staggerDelayMs,
                    fromOffsetY: AnimationConfig.ListEntrance_OffsetY,
                    fromScale: AnimationConfig.ListEntrance_FromScale,
                    toScale: AnimationConfig.ListEntrance_ToScale,
                    fromOpacity: AnimationConfig.ListEntrance_FromOpacity,
                    toOpacity: AnimationConfig.ListEntrance_ToOpacity,
                    bounceAmplitude: AnimationConfig.ListEntrance_BounceAmplitude
                ).Subscribe(
                    _ => { },
                    ex => System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateModdingToolsEntrance 오류: {ex.Message} (한글)")
                );

                System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateModdingToolsEntrance 완료 (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateModdingToolsEntrance 오류: {ex.Message} (한글)");
            }
        }

        // ----------------------------------------------------------------------------------
        // [HELPER] VisualTree에서 특정 타입의 자식 요소 찾기
        // ----------------------------------------------------------------------------------
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private static T? FindVisualParentByName<T>(DependencyObject start, string name) where T : FrameworkElement
        {
            DependencyObject? current = start;
            while (current != null)
            {
                if (current is T fe && fe.Name == name)
                {
                    return fe;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject start) where T : DependencyObject
        {
            DependencyObject? current = start;
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private FrameworkElement ResolveTransitionOriginElement(System.Windows.Controls.Button btn)
        {
            return FindVisualParentByName<FrameworkElement>(btn, "MedalGrid") ?? btn;
        }

        private void SetTransitionOriginHidden(bool hidden)
        {
            if (_activeTransitionOriginElement == null) return;

            _activeTransitionOriginElement.BeginAnimation(UIElement.OpacityProperty, null);
            _activeTransitionOriginElement.Opacity = hidden ? 0 : 1;
            _activeTransitionOriginElement.IsHitTestVisible = !hidden;
        }

        private void AnimateSteppedLayout(bool toStepped)
        {
            double target = toStepped ? 1.0 : 0.0;

            // [FIX] 현재 유효 값을 From으로 캡처한 뒤 기존 애니메이션 레이어를 제거
            // BeginAnimation(prop, null)은 애니메이션 레이어를 제거하여
            // 이후 새 애니메이션이 올바른 시작값(From)에서 출발하도록 보장
            double currentValue = StepProgress; // 애니메이션 레이어 포함 유효 값
            this.BeginAnimation(StepProgressProperty, null); // 기존 애니메이션 레이어 제거
            StepProgress = currentValue; // 기본 값을 유효 값으로 복원

            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateSteppedLayout 호출: toStepped={toStepped}, target={target}, from={currentValue:F2} (한글)");

            var anim = new DoubleAnimation(currentValue, target, TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(StepProgressProperty, anim);

            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateSteppedLayout 애니메이션 시작됨 (한글)");
        }

        // [NEW] 2단계 확장 시스템을 위한 특정 값으로 애니메이션하는 헬퍼 메서드
        private void AnimateSteppedLayoutTo(double targetValue)
        {
            // [FIX] 현재 유효 값을 From으로 캡처한 뒤 기존 애니메이션 레이어를 제거
            double currentValue = StepProgress;
            this.BeginAnimation(StepProgressProperty, null);
            StepProgress = currentValue;

            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateSteppedLayoutTo 호출: {currentValue:F2} → {targetValue:F2} (한글)");

            // 현재 값과 목표 값이 같으면 애니메이션 불필요
            if (Math.Abs(currentValue - targetValue) < 0.01)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] 이미 목표값에 도달, 애니메이션 스킵 (한글)");
                return;
            }

            var anim = new DoubleAnimation(currentValue, targetValue, TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(StepProgressProperty, anim);

            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateSteppedLayoutTo 애니메이션 시작 ({currentValue:F2}→{targetValue:F2}) (한글)");
        }

        // [NEW] Helper to Animate Riser
        private void AnimateRiser(bool toRise)
        {
            double target = toRise ? 1.0 : 0.0;

            // [FIX] AnimateSteppedLayout과 동일한 패턴: 기존 애니메이션 레이어 제거 후 From 명시
            double currentValue = RiserProgress;
            this.BeginAnimation(RiserProgressProperty, null);
            RiserProgress = currentValue;

            var anim = new DoubleAnimation(currentValue, target, TimeSpan.FromMilliseconds(AnimationConfig.Transition_RiserDuration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(RiserProgressProperty, anim);
        }

        // [NEW] Helper to Animate ToolCompact Layout
        // 도구 화면 진입 시:
        //   - MainContentPanel(외곽 배경)의 위/오른쪽/아래 마진을 왼쪽(20px)과 동일하게 축소
        //   - MainContentRootGrid(안쪽 그리드)의 전체 마진도 축소
        // 모딩 메뉴 복귀 시:
        //   - MainContentPanel 마진을 모딩 메뉴 상태(20,50,50,50)로 복원
        //   - MainContentRootGrid 마진을 기본(40px)으로 복원
        private void AnimateToolCompactLayout(bool enable)
        {
            /* Disabled by request:
             * Keep only vertical top-rise expansion (StepProgress / Background_TopRiseHeight),
             * and remove main-content compact margin expansion logic.
             * Keep this method as a no-op for history and potential re-enable later.
             */
            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateToolCompactLayout skipped (disabled): enable={enable}");
        }


        private void UpdateSteppedPath()
        {
            EnsureFixedGlassLayersActive();
            if (SteppedBackgroundBorder == null || MainContentPanel == null || TxtMainHeader == null)
            {
                if (AnimationConfig.EnableVerboseLayoutLogs)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] UpdateSteppedPath 스킵: 필수 요소가 null (한글)");
                }
                return;
            }
            if (AnimationConfig.EnableVerboseLayoutFileLog)
            {
                try
                {
                    var log = new
                    {
                        runId = "run1",
                        hypothesisId = "H3",
                        location = "ModernModWindow.xaml.cs:UpdateSteppedPath:entry",
                        message = "UpdateSteppedPath sizes",
                        data = new
                        {
                            stepProgress = StepProgress,
                            riserProgress = RiserProgress,
                            width = SteppedBackgroundBorder.ActualWidth,
                            height = SteppedBackgroundBorder.ActualHeight
                        },
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
                }
                catch
                {
                }
            }

            // [FIX] 실제 그려지는 컨테이너(SteppedBackgroundBorder)의 크기를 기준으로 지오메트리 계산
            // 이전: MainContentPanel.ActualWidth/Height 사용 → 코너 아크가 컨테이너 밖으로 나가 클리핑됨
            // 수정: SteppedBackgroundBorder의 실제 렌더 영역 크기 사용
            // [NEW] 유리창 내부 크기 미세 조절 (Glass_Margin 적용)
            double width = SteppedBackgroundBorder.ActualWidth - AnimationConfig.Glass_MarginRight;
            double height = SteppedBackgroundBorder.ActualHeight - AnimationConfig.Glass_MarginBottom;

            if (width <= 0 || height <= 0)
            {
                if (AnimationConfig.EnableVerboseLayoutLogs)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModWindow] UpdateSteppedPath 스킵: width={width}, height={height} (한글)");
                }
                return;
            }

            double progress = StepProgress;
            if (AnimationConfig.EnableVerboseLayoutLogs &&
                (DateTime.UtcNow - _lastSteppedPathLogAtUtc).TotalMilliseconds >= 250)
            {
                _lastSteppedPathLogAtUtc = DateTime.UtcNow;
                System.Diagnostics.Debug.WriteLine($"[ModWindow] UpdateSteppedPath 실행: progress={progress:F2}, width={width:F0}, height={height:F0} (한글)");
            }

            double radius = AnimationConfig.Background_CornerRadius;

            // [Riser Logic]
            double constantRiser = _riserMaxHeight * RiserProgress;

            // [Dynamic Expansion Logic - 2단계 시스템]
            // StepProgress 0.0~0.5 = 모딩 메뉴 (왼쪽 확장만, 위쪽 상승 없음)
            // StepProgress 0.5~1.0 = 도구 메뉴 (위쪽 추가 확장)

            // 왼쪽 확장: progress 0~0.5 범위에서 전체 이동 완료
            // progress=0 → sidebarStartX(240), progress=0.5 → targetSidebarX(90), progress>0.5 → 90 유지
            double sidebarGap = AnimationConfig.Background_SidebarGap; // 사이드바와 배경 사이 간격 (10px)
            double targetSidebarX = AnimationConfig.Sidebar_ModdingMenu_Width + sidebarGap; // 80 + 10 = 90
            double sidebarProgress = Math.Min(progress * 2.0, 1.0); // 0~0.5 → 0~1, 0.5이상 → 1 (클램프)
            double currentSidebarX = _sidebarStartX - ((_sidebarStartX - targetSidebarX) * sidebarProgress);

            double headerHeight = Math.Max(AnimationConfig.Header_MinHeight, TxtMainHeader.ActualHeight);
            double normalTopY = headerHeight + AnimationConfig.Header_ContentSpacing + AnimationConfig.Glass_MarginTop;

            TxtMainHeader.UpdateLayout();

            double stepX = AnimationConfig.Background_StepXPosition;

            // [FIX] 위쪽 상승: 0.5 이하에서는 상승 없음, 0.5~1.0에서만 상승
            // 모딩 메뉴(0.5)에서는 평평, 도구 메뉴(1.0)에서만 계단식 확장
            // [구현 완료] 도구 메뉴 확장 로직: 윗쪽만 확장 (RightContentArea 너비 확장 없음)
            // - 모딩 메뉴(StepProgress=0.5): 왼쪽으로 확장, 위쪽 평평
            // - 도구 메뉴(StepProgress=1.0): 윗쪽으로만 추가 확장 (Background_TopRiseHeight)
            double riseProgress = Math.Max(0.0, (progress - 0.5) * 2.0); // 0.5→0.0, 1.0→1.0
            double stepTopY = normalTopY - (AnimationConfig.Background_TopRiseHeight * riseProgress) - constantRiser;

            // [NEW] 왼쪽 마진 적용 (Glass_MarginLeft)
            // currentSidebarX 계산 후 추가로 밀어줌
            currentSidebarX += AnimationConfig.Glass_MarginLeft;

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                // Start Bottom-Left (At Dynamic Sidebar Offset)
                ctx.BeginFigure(new System.Windows.Point(currentSidebarX, height - radius), true, true);

                // Bottom-Left Corner
                ctx.ArcTo(new System.Windows.Point(currentSidebarX + radius, height), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                // Bottom Edge
                ctx.LineTo(new System.Windows.Point(width - radius, height), true, false);

                // Bottom-Right
                ctx.ArcTo(new System.Windows.Point(width, height - radius), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                // Right Edge
                ctx.LineTo(new System.Windows.Point(width, stepTopY + radius), true, false);

                // ** CRITICAL FIX: Handling Flat State vs Stepped State **
                // If riserHeight is near zero (Modding Menu Base View), draw a SIMPLE Top-Right corner.
                // Do NOT try to draw the step, otherwise the overlapped arcs create a visual "split/seam".
                bool isFlat = (Math.Abs(stepTopY - normalTopY) < 1.0);

                if (isFlat)
                {
                    // [FLAT MODE] Simple Rounded Top-Right -> Top-Left
                    // Top-Right Corner
                    ctx.ArcTo(new System.Windows.Point(width - radius, normalTopY), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                    // Top Edge (Straight to Top-Left)
                    ctx.LineTo(new System.Windows.Point(currentSidebarX + radius, normalTopY), true, false);
                }
                else
                {
                    // [STEPPED MODE] Complex Polygon
                    // Top-Right Corner (High)
                    ctx.ArcTo(new System.Windows.Point(width - radius, stepTopY), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                    // [FIX] Removed legacy (progress <= 0.001) check which caused "Diamond Shape".
                    // Now we ALWAYS draw the step down if not flat.

                    // High Side Top Edge
                    ctx.LineTo(new System.Windows.Point(stepX + radius, stepTopY), true, false);

                    // Step Down Corner (Outer)
                    ctx.ArcTo(new System.Windows.Point(stepX, stepTopY + radius), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                    // Drop down
                    ctx.LineTo(new System.Windows.Point(stepX, normalTopY - radius), true, false);

                    // Step Down Corner (Inner) - Turns left
                    ctx.ArcTo(new System.Windows.Point(stepX - radius, normalTopY), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Top Left Side (Back to dynamic-left)
                    ctx.LineTo(new System.Windows.Point(currentSidebarX + radius, normalTopY), true, false);
                }

                // Top-Left Corner (currentSidebarX, normalTopY)
                ctx.ArcTo(new System.Windows.Point(currentSidebarX, normalTopY + radius), new System.Windows.Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);

                // Back to Start
                ctx.LineTo(new System.Windows.Point(currentSidebarX, height - radius), true, false);
            }
            geometry.Freeze();
            SteppedBackgroundPath.Data = geometry;

            // [FIX] Apply clipping to force glass layers to follow the stepped geometry
            // This ensures the glass effect respects the dynamic "stepped" shape (riser)
            if (MainContentRefractionLayer != null) MainContentRefractionLayer.Clip = geometry;
            if (MainContentTintLayer != null) MainContentTintLayer.Clip = geometry;
            if (MainContentDarkBlurOverlay != null) MainContentDarkBlurOverlay.Clip = geometry;
            if (MainContentInsetEdgeLayer != null) MainContentInsetEdgeLayer.Clip = geometry;

            // 패널 자체 클리핑은 조심해야 함 (그림자 잘릴 수 있음) -> 내부 컨텐츠만 클리핑
            // MainContentRootGrid는 클리핑하지 않음 (팝업 등 고려)

        }

        private void ModernModWindow_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UpdateSteppedPath();

            bool anyToolVisible = CharacterInfoContent.Visibility == Visibility.Visible ||
                                  CharacterScaleContent.Visibility == Visibility.Visible ||
                                  YokaiStatsContent.Visibility == Visibility.Visible ||
                                  EncounterEditorContent.Visibility == Visibility.Visible;
            if (!anyToolVisible) return;
            if (_navStack.Count == 0 || _navStack.Peek().State != NavState.ToolWindow) return;

            var currentSize = GetCurrentWindowSize();
            if (!_lastToolLayoutWindowSize.IsEmpty && IsSameWindowSize(currentSize, _lastToolLayoutWindowSize))
            {
                return;
            }

            BeginToolLayoutSession("Window size changed");
            RequestToolHostLayoutUpdate("Window size changed", force: true);
            FinalizeToolLayoutOnce("Window size changed");
        }

        private void AdjustCharacterInfoPosition()
        {
            var parent = CharacterInfoContent.Parent as FrameworkElement
                ?? CharacterScaleContent.Parent as FrameworkElement
                ?? YokaiStatsContent.Parent as FrameworkElement
                ?? EncounterEditorContent.Parent as FrameworkElement;
            if (parent == null || parent.ActualWidth <= 0 || parent.ActualHeight <= 0) return;

            double headerHeight = Math.Max(AnimationConfig.Header_MinHeight, TxtMainHeader.ActualHeight);
            double normalTopY = headerHeight + AnimationConfig.Header_ContentSpacing + AnimationConfig.Glass_MarginTop;
            double riseProgress = Math.Max(0.0, (StepProgress - 0.5) * 2.0);
            double expandedTopY = normalTopY - (AnimationConfig.Background_TopRiseHeight * riseProgress) - (_riserMaxHeight * RiserProgress);
            double baseTop = expandedTopY + AnimationConfig.ToolHost_TopPadding;
            double contentTop = baseTop - AnimationConfig.ToolHost_MoveUpPx;

            // Character Info
            if (CharacterInfoContent.Parent is Canvas)
                Canvas.SetTop(CharacterInfoContent, contentTop);
            else
                CharacterInfoContent.Margin = new Thickness(
                    AnimationConfig.ToolHost_LeftPadding,
                    contentTop,
                    AnimationConfig.ToolHost_RightPadding,
                    AnimationConfig.ToolHost_BottomPadding);
            CharacterInfoContent.ClearValue(FrameworkElement.WidthProperty);
            CharacterInfoContent.ClearValue(FrameworkElement.HeightProperty);

            // Character Scale
            if (CharacterScaleContent.Parent is Canvas)
                Canvas.SetTop(CharacterScaleContent, contentTop);
            else
                CharacterScaleContent.Margin = new Thickness(
                    AnimationConfig.ToolHost_LeftPadding,
                    contentTop,
                    AnimationConfig.ToolHost_RightPadding,
                    AnimationConfig.ToolHost_BottomPadding);
            CharacterScaleContent.ClearValue(FrameworkElement.WidthProperty);
            CharacterScaleContent.ClearValue(FrameworkElement.HeightProperty);

            // Yokai Stats
            if (YokaiStatsContent.Parent is Canvas)
                Canvas.SetTop(YokaiStatsContent, contentTop);
            else
                YokaiStatsContent.Margin = new Thickness(
                    AnimationConfig.ToolHost_LeftPadding,
                    contentTop,
                    AnimationConfig.ToolHost_RightPadding,
                    AnimationConfig.ToolHost_BottomPadding);
            YokaiStatsContent.ClearValue(FrameworkElement.WidthProperty);
            YokaiStatsContent.ClearValue(FrameworkElement.HeightProperty);

            // Encounter Editor
            if (EncounterEditorContent.Parent is Canvas)
                Canvas.SetTop(EncounterEditorContent, contentTop);
            else
                EncounterEditorContent.Margin = new Thickness(
                    AnimationConfig.ToolHost_LeftPadding,
                    contentTop,
                    AnimationConfig.ToolHost_RightPadding,
                    AnimationConfig.ToolHost_BottomPadding);
            EncounterEditorContent.ClearValue(FrameworkElement.WidthProperty);
            EncounterEditorContent.ClearValue(FrameworkElement.HeightProperty);
        }

        private void LeftSidebarBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSidebarClip();
        }

        private void UpdateSidebarClip()
        {
            if (LeftSidebarBorder == null) return;

            double width = LeftSidebarBorder.ActualWidth;
            double height = LeftSidebarBorder.ActualHeight;
            if (width <= 0.0 || height <= 0.0) return;

            double corner = LeftSidebarBorder.CornerRadius.TopLeft;
            double radiusX = Math.Max(0.0, Math.Min(corner, width * 0.5));
            double radiusY = Math.Max(0.0, Math.Min(corner, height * 0.5));

            var clip = new RectangleGeometry(new Rect(0, 0, width, height), radiusX, radiusY);
            clip.Freeze();
            LeftSidebarBorder.Clip = clip;
        }


        private void HideAllToolContents()
        {
            ResetToolLayoutSession("HideAllToolContents");

            CharacterInfoContent.Visibility = Visibility.Collapsed;
            CharacterInfoContent.Opacity = 0;

            CharacterScaleContent.Visibility = Visibility.Collapsed;
            CharacterScaleContent.Opacity = 0;

            YokaiStatsContent.Visibility = Visibility.Collapsed;
            YokaiStatsContent.Opacity = 0;

            EncounterEditorContent.Visibility = Visibility.Collapsed;
            EncounterEditorContent.Opacity = 0;

            // Hide other future tools here
        }

        private static bool HasConnectedTool(ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
        {
            // Connected tools:
            // Index 1: Character Info
            // Index 2: Character Scale
            // Index 3: Yokai Stats
            // Index 4: Encounter Editor
            return vm.IconIndex == 1 ||
                   vm.IconIndex == 2 ||
                   vm.IconIndex == 3 ||
                   vm.IconIndex == 4;
        }

        private void SetToolEmptyToolbar(bool showOnlyBack, bool fadeIn = true)
        {
            ModdingMenuButtons.BeginAnimation(UIElement.OpacityProperty, null);
            ModdingMenuButtons.Visibility = Visibility.Visible;
            ModdingMenuButtons.Opacity = 1;
            ModdingMenuButtons.IsHitTestVisible = true;

            if (showOnlyBack)
            {
                if (BtnBackOnly != null)
                {
                    BtnBackOnly.Visibility = Visibility.Visible;
                    BtnBackOnly.Opacity = 1;
                    BtnBackOnly.IsHitTestVisible = true;
                }
                if (ToolSidebarButtons != null)
                {
                    ToolSidebarButtons.Visibility = Visibility.Visible;
                    if (!fadeIn)
                    {
                        UIAnimationsRx.ClearAnimation(ToolSidebarButtons, UIElement.OpacityProperty);
                        ToolSidebarButtons.Opacity = 0; // 페이드인 준비
                    }
                }

                if (NavProject != null) NavProject.Visibility = Visibility.Collapsed;
                if (NavTool != null) NavTool.Visibility = Visibility.Collapsed;
                if (NavOption != null) NavOption.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (ToolSidebarButtons != null) ToolSidebarButtons.Visibility = Visibility.Collapsed; // Hide Sidebar Tools

                if (NavProject != null) NavProject.Visibility = Visibility.Visible;
                if (NavTool != null) NavTool.Visibility = Visibility.Visible;
                if (NavOption != null) NavOption.Visibility = Visibility.Visible;
            }
        }


        private async void RecoverFromSelection()
        {
            // Cancel any previous RecoverFromSelection still running
            _recoverCts?.Cancel();
            _recoverCts?.Dispose();
            var cts = new System.Threading.CancellationTokenSource();
            _recoverCts = cts;
            var token = cts.Token;

            // [NEW] Disable all user interaction during back animation
            this.IsHitTestVisible = false;

            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] RecoverFromSelection 시작 - Rx 기반 전환됨 (한글)");

                if (_activeTransitionButton == null) return;

                // --- SETUP PROXY ---
                TransitionProxy.BeginAnimation(UIElement.OpacityProperty, null);

                var grp = TransitionProxy.RenderTransform as TransformGroup;
                if (grp == null || grp.Children.Count < 2)
                {
                    grp = new TransformGroup();
                    grp.Children.Add(new ScaleTransform(1, 1));
                    grp.Children.Add(new TranslateTransform(0, 0));
                    TransitionProxy.RenderTransform = grp;
                }

                var scaleT = grp.Children[0] as ScaleTransform;
                var transT = grp.Children[1] as TranslateTransform;

                scaleT?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scaleT?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                transT?.BeginAnimation(TranslateTransform.XProperty, null);
                transT?.BeginAnimation(TranslateTransform.YProperty, null);

                // Reload Content from Active Button
                if (_activeTransitionButton.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
                {
                    ProxyBag.Source = new BitmapImage(new Uri(vm.BagIconPath, UriKind.Absolute));
                    ProxyIcon.Source = new BitmapImage(new Uri(vm.IconBPath, UriKind.Absolute));
                    ProxyText.Text = vm.Title;

                    double proxySize = Math.Max(1.0, Math.Min(_activeTransitionButton.ActualWidth, _activeTransitionButton.ActualHeight));
                    ProxyIconContainer.Width = proxySize;
                    ProxyIconContainer.Height = proxySize;
                }

                TransitionProxy.Visibility = Visibility.Visible;
                TransitionProxy.Opacity = 1;
                System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxyBelowHeader);

                // --- SETUP BOOK (Closed State initially) ---
                ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuTranslate.X = 0;
                ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuSlideTranslate.X = -AnimationConfig.Book_SlideOffset;
                _isBookMarginAnimationRunning = false;
                SetBookLayoutMargins(CalculateBookLeftForProgress(StepProgress), clearAnimations: true);

                ModdingMenuContent.BeginAnimation(UIElement.OpacityProperty, null);
                ModdingMenuContent.Opacity = 1;
                ModdingMenuContent.Visibility = Visibility.Visible;

                SetToolEmptyToolbar(false);

                ModdingMenuButtons.BeginAnimation(UIElement.OpacityProperty, null);
                ModdingMenuButtons.Opacity = 1;
                ModdingMenuButtons.Visibility = Visibility.Visible;
                ModdingMenuButtons.IsHitTestVisible = true;

                BookCover.BeginAnimation(UIElement.OpacityProperty, null);
                BookCover.Visibility = Visibility.Visible;
                BookCover.Opacity = 0;

                CoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                CoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                CoverSkew.BeginAnimation(SkewTransform.AngleYProperty, null);
                CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                CoverScale.ScaleX = 1.0;
                CoverScale.ScaleY = 1.0;
                CoverSkew.AngleY = 0;
                CoverTranslate.X = 0;

                TxtMainHeader.BeginAnimation(UIElement.OpacityProperty, null);
                TxtMainHeader.Opacity = 0;
                ViewModel.HeaderText = "모딩메뉴";
                TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);

                // [FIX] 모딩 메뉴로 복귀 시 헤더 위치 원래대로 복원
                TxtMainHeader.Margin = GetHeaderDefaultMargin();

                token.ThrowIfCancellationRequested();

                // --- STEP 1: BOOK FADE IN (Fast) - Rx 기반 ---
                await Observable.Merge(
                    UIAnimationsRx.Fade(BookCover, 0, 1, AnimationConfig.Header_FadeOutDuration),
                    UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Fade_Duration)
                ).DefaultIfEmpty();

                token.ThrowIfCancellationRequested();

                // --- STEP 2: BOOK OPEN + FLY BACK + CONTENT SLIDE ---
                var duration = TimeSpan.FromMilliseconds(AnimationConfig.Book_OpenDuration);
                var ease = new SineEase { EasingMode = EasingMode.EaseIn };
                var slideAnim = new DoubleAnimation(-AnimationConfig.Book_SlideOffset, 0, duration) { EasingFunction = ease };

                var bookOpenTask = UIAnimationsRx.AnimateBook(BookCover, true, AnimationConfig.Book_OpenDuration);

                ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

                // Calculate current header position
                var rootGrid = VisualTreeHelper.GetParent(TransitionProxy) as UIElement;
                if (rootGrid == null) return;
                var headerTransform = TxtMainHeader.TransformToVisual(rootGrid);
                var headerPos = headerTransform.Transform(new System.Windows.Point(0, 0));

                double targetX = headerPos.X - TransitionProxy.Margin.Left + _medalHeaderXOffset;
                double targetY = headerPos.Y - TransitionProxy.Margin.Top;

                // Fly back animation
                var flyDuration = TimeSpan.FromMilliseconds(AnimationConfig.Medal_FlyDuration);
                var flyEase = new SineEase { EasingMode = EasingMode.EaseOut };

                var animFlyX = new DoubleAnimation(targetX, 0, flyDuration) { EasingFunction = flyEase };
                var animFlyY = new DoubleAnimation(targetY, AnimationConfig.Medal_PopYOffset, flyDuration) { EasingFunction = flyEase };
                var animScaleUpX = new DoubleAnimation(1.0, AnimationConfig.Medal_PopScale, flyDuration) { EasingFunction = flyEase };
                var animScaleUpY = new DoubleAnimation(1.0, AnimationConfig.Medal_PopScale, flyDuration) { EasingFunction = flyEase };

                transT?.BeginAnimation(TranslateTransform.XProperty, animFlyX);
                transT?.BeginAnimation(TranslateTransform.YProperty, animFlyY);
                scaleT?.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleUpX);
                scaleT?.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleUpY);

                // 책 열기를 기다림
                await bookOpenTask;
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_FlyDuration, token);

                token.ThrowIfCancellationRequested();

                // --- STEP 3: LAND ---
                var landDuration = TimeSpan.FromMilliseconds(AnimationConfig.Medal_LandDuration);
                var landEase = new CubicEase { EasingMode = EasingMode.EaseIn };

                var animLandY = new DoubleAnimation(AnimationConfig.Medal_PopYOffset, 0, landDuration) { EasingFunction = landEase };
                var animScaleDownX = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, landDuration) { EasingFunction = landEase };
                var animScaleDownY = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, landDuration) { EasingFunction = landEase };

                transT?.BeginAnimation(TranslateTransform.YProperty, animLandY);
                scaleT?.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleDownX);
                scaleT?.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleDownY);

                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_LandDuration, token);

                // Cleanup
                TransitionProxy.Visibility = Visibility.Collapsed;
                SetTransitionOriginHidden(false);
                _activeTransitionOriginElement = null;
                _isSelectionFinished = false;

                System.Diagnostics.Debug.WriteLine("[ModWindow] RecoverFromSelection 완료 (한글)");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] RecoverFromSelection 취소됨 - 즉시 안정 상태로 복원 (한글)");
                FinishRecoverImmediately();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] RecoverFromSelection 오류: {ex.Message}");
                FinishRecoverImmediately();
            }
            finally
            {
                // [NEW] Restore user interaction after animation completes
                this.IsHitTestVisible = true;
            }
        }

        /// <summary>
        /// RecoverFromSelection이 취소되었을 때, UI를 모딩 메뉴 안정 상태로 즉시 복원합니다.
        /// </summary>
        private void FinishRecoverImmediately()
        {
            // RecoverFromSelection 고유: 프록시 정지
            TransitionProxy.BeginAnimation(UIElement.OpacityProperty, null);
            TransitionProxy.Visibility = Visibility.Collapsed;
            SetTransitionOriginHidden(false);
            _activeTransitionOriginElement = null;
            _isSelectionFinished = false;

            // 진행 중인 모든 애니메이션을 정지 (값 설정은 하지 않음 - ResetSelectionAnimationToModdingMenuState가 처리)
            BookCover.BeginAnimation(UIElement.OpacityProperty, null);
            CoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            CoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            CoverSkew.BeginAnimation(SkewTransform.AngleYProperty, null);
            CoverTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ModdingMenuContent.BeginAnimation(UIElement.OpacityProperty, null);
            ModdingMenuButtons.BeginAnimation(UIElement.OpacityProperty, null);
            TxtMainHeader.BeginAnimation(UIElement.OpacityProperty, null);

            System.Diagnostics.Debug.WriteLine("[ModWindow] FinishRecoverImmediately - animations stopped");
        }


        // ReactiveUI ViewModel
        public ModernModWindowViewModel ViewModel { get; }

        public ModernModWindow()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] 생성자 시작 - ReactiveUI ViewModel 구조 적용 (한글)");

            InitializeComponent();

            // ViewModel 초기화 및 DataContext 설정 (ExecuteTool 콜백 전달)
            ViewModel = new ModernModWindowViewModel(ExecuteTool);
            DataContext = ViewModel;

            // ViewModel의 ModdingTools를 ItemsSource로 연결
            ModdingMenuContent.ItemsSource = ViewModel.ModdingTools;
            SaveOverlayItemsControl.ItemsSource = _saveSelectionItems;
            SetBookLayoutMargins(CalculateBookLeftForProgress(0.0), clearAnimations: false);
            TxtMainHeader.Margin = GetHeaderDefaultMargin();
            ModdingMenuContent.AddHandler(Button.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(ModdingMenuButton_MouseEnter), true);
            ModdingMenuContent.AddHandler(Button.MouseLeaveEvent, new System.Windows.Input.MouseEventHandler(ModdingMenuButton_MouseLeave), true);

            // 기존 로컬 컬렉션을 ViewModel 컬렉션으로 교체
            ModdingTools = ViewModel.ModdingTools;

            InitializeProjectMenu();
            InitializeModdingMenu();
            // InitializeModdingTools는 ViewModel에서 처리하므로 제거

            _navStack.Push(new NavItem { State = NavState.ProjectList });
            ShowProjectSectionImmediate();
            _suppressSidebarNavCheckedEvents = false;

            Loaded += OnWindowLoaded;
            SizeChanged += OnWindowSizeChanged;

            System.Diagnostics.Debug.WriteLine("[ModWindow] 생성자 완료 - ReactiveUI ViewModel 연결됨 (한글)");
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] OnWindowLoaded - 레이아웃 변수 적용 시작 (한글)");
            #region agent log
            try
            {
                var log = new
                {
                    runId = "run1",
                    hypothesisId = "H1",
                    location = "ModernModWindow.xaml.cs:OnWindowLoaded:entry",
                    message = "OnWindowLoaded apply layout",
                    data = new
                    {
                        mainMarginTop = AnimationConfig.MainPanel_ProjectMenu_MarginTop,
                        rightMarginRight = AnimationConfig.RightContent_MarginRight,
                        rightMarginBottom = AnimationConfig.RightContent_MarginBottom,
                        rootGridMargin = AnimationConfig.MainContentRootGrid_Margin
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // MainContentPanel 크기 적용 (AnimationConfig)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            MainContentPanel.Margin = new Thickness(
                AnimationConfig.MainPanel_ProjectMenu_MarginLeft,
                AnimationConfig.MainPanel_ProjectMenu_MarginTop,
                AnimationConfig.MainPanel_ProjectMenu_MarginRight,
                AnimationConfig.MainPanel_ProjectMenu_MarginBottom);
            MainContentPanel.CornerRadius = new CornerRadius(AnimationConfig.MainPanel_CornerRadius);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] MainContentPanel 적용: Margins (L,T,R,B)=({AnimationConfig.MainPanel_ProjectMenu_MarginLeft},{AnimationConfig.MainPanel_ProjectMenu_MarginTop},{AnimationConfig.MainPanel_ProjectMenu_MarginRight},{AnimationConfig.MainPanel_ProjectMenu_MarginBottom}), CornerRadius={AnimationConfig.MainPanel_CornerRadius} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // MainContentRootGrid 크기 적용 (AnimationConfig)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            MainContentRootGrid.Margin = new Thickness(AnimationConfig.MainContentRootGrid_Margin);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] MainContentRootGrid 적용: Margin={AnimationConfig.MainContentRootGrid_Margin} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // RightContentArea 크기 적용 (AnimationConfig)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            RightContentArea.Margin = new Thickness(0, 0, AnimationConfig.RightContent_MarginRight, AnimationConfig.RightContent_MarginBottom);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] RightContentArea 적용: MarginRight={AnimationConfig.RightContent_MarginRight}, MarginBottom={AnimationConfig.RightContent_MarginBottom} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // ProjectListView 내부 여백 적용 (AnimationConfig)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            ProjectListView.Margin = new Thickness(
                AnimationConfig.ProjectListView_Margin,
                AnimationConfig.ProjectListView_Margin,
                AnimationConfig.ProjectListView_Margin,
                AnimationConfig.ProjectListView_MarginBottom);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] ProjectListView 적용: Margin L/T/R={AnimationConfig.ProjectListView_Margin}, B={AnimationConfig.ProjectListView_MarginBottom} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Sidebar & Spacer 크기 적용 (AnimationConfig)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            LeftSidebarBorder.Width = AnimationConfig.Sidebar_ProjectMenu_Width;
            SidebarSpacerCol.Width = new GridLength(AnimationConfig.RightContent_SpacerWidth);
            LeftSidebarBorder.SizeChanged -= LeftSidebarBorder_SizeChanged;
            LeftSidebarBorder.SizeChanged += LeftSidebarBorder_SizeChanged;
            UpdateSidebarClip();
            System.Diagnostics.Debug.WriteLine($"[ModWindow] Sidebar/Spacer 적용: SidebarWidth={AnimationConfig.Sidebar_ProjectMenu_Width}, SpacerWidth={AnimationConfig.RightContent_SpacerWidth} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // StepProgress 초기화
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            StepProgress = 0;
            UpdateSteppedPath();

            if (GlobalTitleBar != null)
            {
                GlobalTitleBar.BeginAnimation(UIElement.OpacityProperty, null);
                GlobalTitleBar.Visibility = Visibility.Collapsed;
                GlobalTitleBar.Opacity = 0;
                GlobalTitleBar.IsHitTestVisible = false;
            }
            if (GlobalTitleBarTranslate != null)
            {
                GlobalTitleBarTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                GlobalTitleBarTranslate.Y = AnimationConfig.TitleBar_HiddenOffsetY;
            }

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // [Phase 4] Mica Backdrop 초기화 (Windows 11+)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // [FIX] 투명 창(AllowsTransparency=True)과 DWM Mica는 호환되지 않아 흰색 배경이 강제됨.
            // 둥근 모서리를 위해 Mica를 비활성화합니다.
            // InitializeMicaBackdrop();

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // [Phase 5] Glass Refraction Shader 초기화
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            InitializeGlassRefractionShader();
            _ = Dispatcher.InvokeAsync(() =>
            {
                AttachBookRefractionEffect();
                AttachModdingMedalRefractionEffects();
            }, DispatcherPriority.Loaded);

            System.Diagnostics.Debug.WriteLine("[ModWindow] OnWindowLoaded 완료 (한글)");
        }

        /// <summary>
        /// [Phase 4] Mica Backdrop 초기화
        /// Windows 11 이상에서 시스템 수준의 Acrylic/Mica 효과 적용
        /// - Fallback: Windows 10 이하에서는 기존 WPF 스타일 유지
        /// </summary>
        private void InitializeMicaBackdrop()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] Mica Backdrop 초기화 시작 (한글)");

                // Windows 11+ 에서만 Mica 적용
                bool micaApplied = MicaBackdropHelper.ApplyMicaBackdrop(this, useDarkMode: false);

                if (micaApplied)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] ✅ Mica Backdrop 적용 성공 (Windows 11+) (한글)");

                    // Mica가 적용되면 Window 배경을 투명하게 설정하여 효과가 보이도록 함
                    // 기존 배경 색상 제거 (XAML의 Background="Transparent" 유지)
                    this.Background = System.Windows.Media.Brushes.Transparent;

                    // [선택] MainContentPanel 배경을 약간 투명하게 하여 Mica가 비치도록 함
                    // MainContentPanel.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] ⚠️ Mica 미적용 (Windows 10 이하 또는 실패) - 기존 스타일 유지 (한글)");
                    // Fallback: 기존 WPF 스타일 유지 (이미 적용된 Acrylic 색상 사용)
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] Mica Backdrop 초기화 오류: {ex.Message} (한글)");
                // 오류 시 기존 스타일로 계속 진행
            }
        }

        /// <summary>
        /// [Phase 5] Glass Refraction Shader 초기화
        /// iOS 26 제어센터 스타일 유리 굴절 효과를 버튼 단위로 적용
        /// - HLSL Pixel Shader 3.0 기반
        /// - 마우스 위치 기반 동적 왜곡
        /// - 시간 기반 애니메이션
        /// </summary>
        private void InitializeGlassRefractionShader()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GlassShader] 초기화 시작 (한글)");

                // === Main Content: Liquid Glass (투명, 가벼운 배경) ===
                _fixedBackdropRefractionEffect = new GlassRefractionEffect
                {
                    RefractionStrength = AnimationConfig.MainContent_GlassRefractionStrength,
                    NoiseScale = AnimationConfig.MainContent_GlassNoiseScale,
                    MouseX = 0.5,
                    MouseY = 0.5,
                    AnimationTime = 0.0,
                    SpecularStrength = AnimationConfig.MainContent_GlassSpecular,
                    InnerShadowSize = AnimationConfig.MainContent_GlassInnerShadow,
                    Density = AnimationConfig.MainContent_GlassDensity,
                    MouseRadius = AnimationConfig.MainContent_GlassMouseRadius,
                    MouseFalloffPower = AnimationConfig.MainContent_GlassMouseFalloffPower,
                    MouseOffsetStrength = AnimationConfig.MainContent_GlassMouseOffsetStrength,
                    EdgeHighlightStrength = AnimationConfig.MainContent_GlassEdgeHighlightStrength,
                    NormalMap = GetGlassNormalMapBrush()
                };

                // === Sidebar: Book profile (요청: 사이드바도 책과 같은 안정적인 질감) ===
                _sidebarRefractionEffect = new GlassRefractionEffect
                {
                    RefractionStrength = AnimationConfig.Sidebar_GlassRefractionStrength,
                    NoiseScale = AnimationConfig.Sidebar_GlassNoiseScale,
                    MouseX = 0.5,
                    MouseY = 0.5,
                    AnimationTime = 0.0,
                    SpecularStrength = AnimationConfig.Sidebar_GlassSpecular,
                    InnerShadowSize = AnimationConfig.Sidebar_GlassInnerShadow,
                    Density = AnimationConfig.Sidebar_GlassDensity,
                    MouseRadius = AnimationConfig.Sidebar_GlassMouseRadius,
                    MouseFalloffPower = AnimationConfig.Sidebar_GlassMouseFalloffPower,
                    MouseOffsetStrength = AnimationConfig.Sidebar_GlassMouseOffsetStrength,
                    EdgeHighlightStrength = AnimationConfig.Sidebar_GlassEdgeHighlightStrength,
                    NormalMap = GetGlassNormalMapBrush()
                };

                // Book page glass (behind medal buttons): weaker than global backdrop.
                _bookRefractionEffect = new GlassRefractionEffect
                {
                    RefractionStrength = AnimationConfig.Book_GlassRefractionStrength,
                    NoiseScale = AnimationConfig.Book_GlassNoiseScale,
                    MouseX = 0.5,
                    MouseY = 0.5,
                    AnimationTime = 0.0,
                    SpecularStrength = AnimationConfig.Book_GlassSpecular,
                    InnerShadowSize = AnimationConfig.Book_GlassInnerShadow,
                    Density = AnimationConfig.Book_GlassDensity,
                    MouseRadius = AnimationConfig.Book_GlassMouseRadius,
                    MouseFalloffPower = AnimationConfig.Book_GlassMouseFalloffPower,
                    MouseOffsetStrength = AnimationConfig.Book_GlassMouseOffsetStrength,
                    EdgeHighlightStrength = AnimationConfig.Book_GlassEdgeHighlightStrength,
                    NormalMap = GetGlassNormalMapBrush()
                };

                // Main content tint/overlay layers stay transparent and effect-free.
                _tintLayerRefractionEffect = null;
                _blurOverlayRefractionEffect = null;

                // Legacy global panel shader remains disabled.
                _glassRefractionEffect = null;
                if (CharacterInfoContent != null) CharacterInfoContent.Effect = null;

                // Attach fixed refraction to main content glass layer
                if (MainContentRefractionLayer != null)
                {
                    MainContentRefractionLayer.Effect = _fixedBackdropRefractionEffect;
                }

                // Attach Liquid Glass to sidebar with higher density profile.
                if (LeftSidebarBorder != null)
                {
                    LeftSidebarBorder.Effect = _sidebarRefractionEffect;
                }

                // Tint layer remains transparent (refraction-only glass behavior).
                if (MainContentTintLayer != null)
                {
                    var tintBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AnimationConfig.MainContent_GlassTint));
                    tintBrush.Freeze();
                    MainContentTintLayer.Background = tintBrush;
                    MainContentTintLayer.Effect = null;
                }

                // Overlay layer remains transparent (no extra blur tint).
                if (MainContentDarkBlurOverlay != null)
                {
                    var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AnimationConfig.MainContent_GlassOverlayTint));
                    brush.Freeze();
                    MainContentDarkBlurOverlay.Background = brush;
                    MainContentDarkBlurOverlay.Effect = AnimationConfig.MainContent_GlassBlurRadius > 0
                        ? new BlurEffect { Radius = AnimationConfig.MainContent_GlassBlurRadius, RenderingBias = RenderingBias.Performance }
                        : null;
                }

                // Attach glass to tagged tool panels (all tool views).
                AttachToolPanelRefractionEffects();
                AttachToolInteractiveRefractionEffects();
                AttachBookRefractionEffect();
                AttachModdingMedalRefractionEffects();
                EnsureFixedGlassLayersActive();

                // Window MouseMove 이벤트 연결 (마우스 추적)
                this.MouseMove += Window_MouseMove_ShaderUpdate;

                // CompositionTarget.Rendering 이벤트 연결 (VSync 동기화)
                System.Windows.Media.CompositionTarget.Rendering += UpdateShaderAnimation;

                System.Diagnostics.Debug.WriteLine("[GlassShader] ✅ 초기화 완료 - CompositionTarget.Rendering 애니메이션 시작 (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] 초기화 오류: {ex.Message} (한글)");
                // Fallback: Shader 없이 계속 진행
                _glassRefractionEffect = null;
                _fixedBackdropRefractionEffect = null;
                _sidebarRefractionEffect = null;
                _bookRefractionEffect = null;
            }
        }

        /// <summary>
        /// [Phase 5] 마우스 위치 기반 Shader 업데이트
        /// Window 좌표를 0.0~1.0 정규화하여 Shader에 전달
        /// </summary>
        private void IconRefractionButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (_buttonRefractionEffects.TryGetValue(btn, out var existing) &&
                ReferenceEquals(btn.Effect, existing))
            {
                btn.Effect = null;
            }

            _buttonRefractionEffects.Remove(btn);
        }

        private void IconRefractionButton_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!_buttonRefractionEffects.TryGetValue(btn, out var effect)) return;

            if (ReferenceEquals(btn.Effect, effect))
            {
                btn.Effect = null;
            }

            _buttonRefractionEffects.Remove(btn);
        }

        private static T? FindVisualChildByName<T>(DependencyObject obj, string name) where T : FrameworkElement
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T named && string.Equals(named.Name, name, StringComparison.Ordinal))
                {
                    return named;
                }

                var result = FindVisualChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private FrameworkElement? ResolveBookGlassBackplate()
        {
            if (ModdingMenuContent == null) return null;
            if (_bookGlassBackplate != null && _bookGlassBackplate.IsLoaded)
            {
                return _bookGlassBackplate;
            }

            ModdingMenuContent.ApplyTemplate();
            _bookGlassBackplate =
                ModdingMenuContent.Template?.FindName(AnimationConfig.Book_GlassTag, ModdingMenuContent) as FrameworkElement
                ?? FindVisualChildByName<FrameworkElement>(ModdingMenuContent, AnimationConfig.Book_GlassTag);

            return _bookGlassBackplate;
        }

        private void AttachBookRefractionEffect()
        {
            if (ModdingMenuContent == null || _bookRefractionEffect == null) return;

            var bookLayer = ResolveBookGlassBackplate();
            if (bookLayer == null) return;

            if (!ReferenceEquals(bookLayer.Effect, _bookRefractionEffect))
            {
                bookLayer.Effect = _bookRefractionEffect;
            }
        }

        private void DetachBookRefractionEffect()
        {
            if (_bookGlassBackplate != null)
            {
                _bookGlassBackplate.Effect = null;
            }

            _bookGlassBackplate = null;
        }

        private void AttachModdingMedalRefractionEffects()
        {
            try
            {
                if (ModdingMenuContent == null || ModdingMenuContent.Items == null) return;

                int itemCount = ModdingMenuContent.Items.Count;
                if (itemCount <= 0) return;

                // Cleanup stale visuals first (e.g. template refresh).
                var stale = new List<FrameworkElement>();
                foreach (var pair in _moddingMedalRefractionEffects)
                {
                    if (!pair.Key.IsVisible || VisualTreeHelper.GetParent(pair.Key) == null)
                    {
                        pair.Key.SizeChanged -= MedalBackplate_SizeChanged;
                        if (ReferenceEquals(pair.Key.Effect, pair.Value))
                        {
                            pair.Key.Effect = null;
                        }
                        stale.Add(pair.Key);
                    }
                }

                foreach (var key in stale)
                {
                    _moddingMedalRefractionEffects.Remove(key);
                }

                for (int i = 0; i < itemCount; i++)
                {
                    var container = ModdingMenuContent.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container == null) continue;

                    var backplate = FindVisualChildByName<Ellipse>(container, "MedalBackplate");
                    FrameworkElement target = backplate ?? (FrameworkElement?)FindVisualChild<Button>(container);
                    if (target == null) continue;

                    if (_moddingMedalRefractionEffects.ContainsKey(target))
                    {
                        // Keep backdrop sampling in sync with animated/resized book page.
                        if (backplate != null)
                        {
                            backplate.Fill = GetMedalBackdropBrush(backplate);
                        }
                        target.SizeChanged -= MedalBackplate_SizeChanged;
                        target.SizeChanged += MedalBackplate_SizeChanged;
                        UpdateMedalBackplateClip(target);
                        continue;
                    }

                    var effect = new GlassRefractionEffect
                    {
                        RefractionStrength = AnimationConfig.ModdingMedal_GlassRefractionStrength,
                        NoiseScale = AnimationConfig.ModdingMedal_GlassNoiseScale,
                        MouseX = 0.5,
                        MouseY = 0.5,
                        AnimationTime = _shaderTime,
                        SpecularStrength = AnimationConfig.ModdingMedal_GlassSpecular,
                        InnerShadowSize = AnimationConfig.ModdingMedal_GlassInnerShadow,
                        Density = AnimationConfig.ModdingMedal_GlassDensity,
                        MouseRadius = AnimationConfig.ModdingMedal_GlassMouseRadius,
                        MouseFalloffPower = AnimationConfig.ModdingMedal_GlassMouseFalloffPower,
                        MouseOffsetStrength = AnimationConfig.ModdingMedal_GlassMouseOffsetStrength,
                        EdgeHighlightStrength = AnimationConfig.ModdingMedal_GlassEdgeHighlightStrength,
                        NormalMap = GetGlassNormalMapBrush()
                    };

                    if (backplate != null)
                    {
                        backplate.Fill = GetMedalBackdropBrush(backplate);
                    }
                    target.SizeChanged -= MedalBackplate_SizeChanged;
                    target.SizeChanged += MedalBackplate_SizeChanged;
                    UpdateMedalBackplateClip(target);
                    target.Effect = effect;
                    _moddingMedalRefractionEffects[target] = effect;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] modding medal attach error: {ex.Message}");
            }
        }

        private void DetachModdingMedalRefractionEffects()
        {
            foreach (var pair in _moddingMedalRefractionEffects)
            {
                pair.Key.SizeChanged -= MedalBackplate_SizeChanged;
                if (ReferenceEquals(pair.Key.Effect, pair.Value))
                {
                    pair.Key.Effect = null;
                }
            }

            _moddingMedalRefractionEffects.Clear();
        }

        private void MedalBackplate_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                UpdateMedalBackplateClip(element);
            }
        }

        private static void UpdateMedalBackplateClip(FrameworkElement element)
        {
            double width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
            double height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;
            if (width <= 0 || height <= 0) return;

            var radiusX = Math.Max(0.0, (width * 0.5) - 0.5);
            var radiusY = Math.Max(0.0, (height * 0.5) - 0.5);
            element.Clip = new EllipseGeometry(new Point(width * 0.5, height * 0.5), radiusX, radiusY);
        }

        private System.Windows.Media.Brush GetMedalBackdropBrush(FrameworkElement medalElement)
        {
            var bookLayer = ResolveBookGlassBackplate();
            if (bookLayer == null || medalElement.ActualWidth <= 0 || medalElement.ActualHeight <= 0)
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            if (bookLayer.ActualWidth <= 0 || bookLayer.ActualHeight <= 0)
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            try
            {
                var transform = medalElement.TransformToVisual(bookLayer);
                Point topLeft = transform.Transform(new Point(0, 0));

                return new VisualBrush
                {
                    Visual = bookLayer,
                    Stretch = Stretch.None,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(topLeft.X, topLeft.Y, medalElement.ActualWidth, medalElement.ActualHeight),
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewport = new Rect(0, 0, 1, 1),
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
            }
            catch
            {
                return System.Windows.Media.Brushes.Transparent;
            }
        }

        private void AttachToolPanelRefractionEffects()
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var roots = new FrameworkElement?[]
                {
                    CharacterInfoContent,
                    CharacterScaleContent,
                    YokaiStatsContent,
                    EncounterEditorContent
                };

                // User-observed regression: tool panel visuals look correct before shader attach,
                // then appear shrunken/clipped after late attach.
                // Stabilization policy: do not mutate tool backdrop borders at runtime.
                void ResetBackdrop(FrameworkElement panel)
                {
                    if (panel.Tag is not string tag ||
                        !string.Equals(tag, AnimationConfig.ToolPanel_BackdropTag, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (panel is not Border)
                    {
                        return;
                    }

                    if (panel.Effect is BlurEffect || panel.Effect is GlassRefractionEffect)
                    {
                        panel.Effect = null;
                    }
                }

                foreach (var root in roots)
                {
                    if (root == null) continue;

                    ResetBackdrop(root);

                    foreach (var panel in FindVisualChildren<FrameworkElement>(root))
                    {
                        ResetBackdrop(panel);
                    }
                }

                if (_toolPanelRefractionEffects.Count > 0)
                {
                    foreach (var pair in _toolPanelRefractionEffects)
                    {
                        if (ReferenceEquals(pair.Key.Effect, pair.Value))
                        {
                            pair.Key.Effect = null;
                        }
                    }
                    _toolPanelRefractionEffects.Clear();
                }

                _toolPanelAttachRetryPending = false;
                _toolPanelAttachRetryCount = 0;
                System.Diagnostics.Debug.WriteLine($"[Perf] AttachToolPanelRefractionEffects: {timer.ElapsedMilliseconds}ms, runtime tool backdrop disabled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] tool panel attach error: {ex.Message}");
            }
        }

        private ImageBrush GetGlassNormalMapBrush()
        {
            if (_glassNormalMapBrush != null)
            {
                return _glassNormalMapBrush;
            }

            const int size = 128;
            var bmp = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new int[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double u = (double)x / size;
                    double v = (double)y / size;

                    double nx =
                        Math.Sin((u * Math.PI * 2.0 * 3.0) + (v * Math.PI * 2.0 * 0.7)) * 0.45 +
                        Math.Sin((u * Math.PI * 2.0 * 1.3) - (v * Math.PI * 2.0 * 2.1)) * 0.25;
                    double ny =
                        Math.Cos((u * Math.PI * 2.0 * 2.2) - (v * Math.PI * 2.0 * 0.9)) * 0.45 +
                        Math.Sin((u * Math.PI * 2.0 * 0.8) + (v * Math.PI * 2.0 * 2.8)) * 0.25;

                    nx = Math.Max(-1.0, Math.Min(1.0, nx));
                    ny = Math.Max(-1.0, Math.Min(1.0, ny));

                    byte r = (byte)(127.5 + nx * 127.5);
                    byte g = (byte)(127.5 + ny * 127.5);
                    byte b = 255;
                    byte a = 255;

                    pixels[y * size + x] = b | (g << 8) | (r << 16) | (a << 24);
                }
            }

            bmp.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

            _glassNormalMapBrush = new ImageBrush(bmp)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 96, 96),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = new Rect(0, 0, 1, 1)
            };

            _glassNormalMapBrush.Freeze();
            return _glassNormalMapBrush;
        }

        private System.Windows.Media.Brush GetToolPanelBackdropBrush(FrameworkElement panel)
        {
            if (BackgroundContainer == null || panel.ActualWidth <= 0 || panel.ActualHeight <= 0)
            {
                return GetToolPanelBackdropBrushFallback();
            }

            try
            {
                var transform = panel.TransformToVisual(BackgroundContainer);
                Point topLeft = transform.Transform(new Point(0, 0));
                double bgWidth = BackgroundContainer.ActualWidth;
                double bgHeight = BackgroundContainer.ActualHeight;

                // If tool panel extends outside backdrop bounds (e.g. moved upward), absolute-viewbox
                // sampling produces clipped/blank edges after effects load. Use stable fallback brush.
                if (topLeft.X < 0 ||
                    topLeft.Y < 0 ||
                    (topLeft.X + panel.ActualWidth) > bgWidth ||
                    (topLeft.Y + panel.ActualHeight) > bgHeight)
                {
                    return GetToolPanelBackdropBrushFallback();
                }

                return new VisualBrush
                {
                    Visual = BackgroundContainer,
                    Stretch = Stretch.None,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(topLeft.X, topLeft.Y, panel.ActualWidth, panel.ActualHeight),
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewport = new Rect(0, 0, 1, 1),
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
            }
            catch
            {
                return GetToolPanelBackdropBrushFallback();
            }
        }

        private System.Windows.Media.Brush GetToolPanelBackdropBrushFallback()
        {
            if (_toolPanelBackdropBrush != null)
            {
                return _toolPanelBackdropBrush;
            }

            if (BackgroundContainer == null)
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            _toolPanelBackdropBrush = new VisualBrush
            {
                Visual = BackgroundContainer,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            return _toolPanelBackdropBrush;
        }

        private bool ShouldRetryToolPanelAttach()
        {
            // Retry only in tool-flow context, not at startup/project list state.
            if (ToolSidebarButtons?.Visibility == Visibility.Visible) return true;
            if (CharacterInfoContent?.Visibility == Visibility.Visible) return true;
            if (CharacterScaleContent?.Visibility == Visibility.Visible) return true;
            if (YokaiStatsContent?.Visibility == Visibility.Visible) return true;

            return _navStack.Count > 0 && _navStack.Peek().State == NavState.ToolWindow;
        }

        private void AttachToolInteractiveRefractionEffects()
        {
            try
            {
                if (!AnimationConfig.ToolInteractive_EnableRefraction)
                {
                    int clearedCount = 0;
                    foreach (var pair in _toolInteractiveRefractionEffects)
                    {
                        if (ReferenceEquals(pair.Key.Effect, pair.Value))
                        {
                            pair.Key.Effect = null;
                        }
                        clearedCount++;
                    }

                    _toolInteractiveRefractionEffects.Clear();

                    // Hard clear any lingering refraction on tool view tree.
                    var cleanupRoots = new FrameworkElement?[] { CharacterInfoContent, CharacterScaleContent, YokaiStatsContent };
                    foreach (var root in cleanupRoots)
                    {
                        if (root == null) continue;
                        foreach (var element in FindVisualChildren<FrameworkElement>(root))
                        {
                            if (element.Effect is GlassRefractionEffect)
                            {
                                element.Effect = null;
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[Perf] AttachToolInteractiveRefractionEffects: disabled by config, cleared={clearedCount}");
                    return;
                }

                var timer = System.Diagnostics.Stopwatch.StartNew();
                int attachedCount = 0;
                var activeTargets = new HashSet<FrameworkElement>();
                var roots = new FrameworkElement?[]
                {
                    CharacterInfoContent,
                    CharacterScaleContent,
                    YokaiStatsContent,
                    EncounterEditorContent
                };

                void TryAttach(FrameworkElement target)
                {
                    if (target.Tag is not string tag ||
                        !string.Equals(tag, AnimationConfig.ToolInteractive_GlassTag, StringComparison.Ordinal))
                    {
                        return;
                    }

                    activeTargets.Add(target);

                    if (!_toolInteractiveRefractionEffects.TryGetValue(target, out var effect))
                    {
                        effect = new GlassRefractionEffect
                        {
                            RefractionStrength = AnimationConfig.ToolInteractive_GlassRefractionStrength,
                            NoiseScale = AnimationConfig.ToolInteractive_GlassNoiseScale,
                            SpecularStrength = 0.06,
                            InnerShadowSize = 0.010,
                            Density = 0.24,
                            // Keep distortion as a static glass look.
                            // Hover-dependent mouse warping causes perceived blur shifts.
                            MouseRadius = 0.0,
                            MouseFalloffPower = 2.20,
                            MouseOffsetStrength = 0.0,
                            EdgeHighlightStrength = 0.05,
                            NormalMap = GetGlassNormalMapBrush()
                        };
                        _toolInteractiveRefractionEffects[target] = effect;
                    }

                    if (!ReferenceEquals(target.Effect, effect))
                    {
                        target.Effect = effect;
                    }

                    if (target is Border border)
                    {
                        if (border.Background == null || border.Background == System.Windows.Media.Brushes.Transparent)
                        {
                            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
                        }

                        if (border.BorderThickness.Left <= 0 && border.BorderThickness.Top <= 0 &&
                            border.BorderThickness.Right <= 0 && border.BorderThickness.Bottom <= 0)
                        {
                            border.BorderThickness = new Thickness(1.0);
                        }

                        if (border.BorderBrush == null || border.BorderBrush == System.Windows.Media.Brushes.Transparent)
                        {
                            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                        }
                    }

                    attachedCount++;
                }

                foreach (var root in roots)
                {
                    if (root == null) continue;

                    TryAttach(root);
                    foreach (var target in FindVisualChildren<FrameworkElement>(root))
                    {
                        TryAttach(target);
                    }
                }

                var staleTargets = new List<FrameworkElement>();
                foreach (var pair in _toolInteractiveRefractionEffects)
                {
                    if (activeTargets.Contains(pair.Key))
                    {
                        continue;
                    }

                    if (ReferenceEquals(pair.Key.Effect, pair.Value))
                    {
                        pair.Key.Effect = null;
                    }

                    staleTargets.Add(pair.Key);
                }

                foreach (var stale in staleTargets)
                {
                    _toolInteractiveRefractionEffects.Remove(stale);
                }

                System.Diagnostics.Debug.WriteLine($"[Perf] AttachToolInteractiveRefractionEffects: {timer.ElapsedMilliseconds}ms, attached={attachedCount}, active={_toolInteractiveRefractionEffects.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] tool interactive attach error: {ex.Message}");
            }
        }

        private void EnsureFixedGlassLayersActive()
        {
            bool anyToolVisible = (CharacterInfoContent?.Visibility == Visibility.Visible) ||
                                  (CharacterScaleContent?.Visibility == Visibility.Visible) ||
                                  (YokaiStatsContent?.Visibility == Visibility.Visible) ||
                                  (EncounterEditorContent?.Visibility == Visibility.Visible);

            if (MainContentRefractionLayer != null)
            {
                MainContentRefractionLayer.Visibility = Visibility.Visible;
                MainContentRefractionLayer.Opacity = 1.0;
                if (_fixedBackdropRefractionEffect != null &&
                    !ReferenceEquals(MainContentRefractionLayer.Effect, _fixedBackdropRefractionEffect))
                {
                    MainContentRefractionLayer.Effect = _fixedBackdropRefractionEffect;
                }
            }

            if (MainContentTintLayer != null)
            {
                MainContentTintLayer.Visibility = Visibility.Visible;
                MainContentTintLayer.Opacity = 1.0;

                var tint = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AnimationConfig.MainContent_GlassTint);
                if (MainContentTintLayer.Background is not SolidColorBrush tintBrush || tintBrush.Color != tint)
                {
                    var brush = new SolidColorBrush(tint);
                    brush.Freeze();
                    MainContentTintLayer.Background = brush;
                }
                MainContentTintLayer.Effect = null;
            }

            if (MainContentDarkBlurOverlay != null)
            {
                MainContentDarkBlurOverlay.Visibility = Visibility.Visible;
                MainContentDarkBlurOverlay.Opacity = 1.0;

                var tint = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AnimationConfig.MainContent_GlassOverlayTint);
                if (MainContentDarkBlurOverlay.Background is not SolidColorBrush sb || sb.Color != tint)
                {
                    var brush = new SolidColorBrush(tint);
                    brush.Freeze();
                    MainContentDarkBlurOverlay.Background = brush;
                }
                MainContentDarkBlurOverlay.Effect = (!anyToolVisible && AnimationConfig.MainContent_GlassBlurRadius > 0)
                    ? new BlurEffect { Radius = AnimationConfig.MainContent_GlassBlurRadius, RenderingBias = RenderingBias.Performance }
                    : null;
            }
        }

        private void Window_MouseMove_ShaderUpdate(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_buttonRefractionEffects.Count == 0 &&
                _moddingMedalRefractionEffects.Count == 0 &&
                (!AnimationConfig.ToolInteractive_EnableRefraction || _toolInteractiveRefractionEffects.Count == 0) &&
                _fixedBackdropRefractionEffect == null &&
                _sidebarRefractionEffect == null &&
                _bookRefractionEffect == null) return;

            try
            {
                // Virtualized list items can appear after first attach.
                if (AnimationConfig.ToolInteractive_EnableRefraction)
                {
                    AttachToolInteractiveRefractionEffects();
                }

                if (ModdingMenuContent?.Visibility == Visibility.Visible &&
                    _moddingMedalRefractionEffects.Count == 0)
                {
                    AttachModdingMedalRefractionEffects();
                }

                foreach (var pair in _buttonRefractionEffects)
                {
                    var btn = pair.Key;
                    var effect = pair.Value;
                    if (!btn.IsVisible || btn.ActualWidth <= 0.0 || btn.ActualHeight <= 0.0) continue;

                    var localPos = e.GetPosition(btn);
                    double localX = Math.Max(0.0, Math.Min(1.0, localPos.X / btn.ActualWidth));
                    double localY = Math.Max(0.0, Math.Min(1.0, localPos.Y / btn.ActualHeight));
                    effect.MouseX = localX;
                    effect.MouseY = localY;
                }

                // Medal profile: allow range outside [0,1] so falloff can stay localized near the button.
                foreach (var pair in _moddingMedalRefractionEffects)
                {
                    var medal = pair.Key;
                    var effect = pair.Value;
                    if (!medal.IsVisible || medal.ActualWidth <= 0.0 || medal.ActualHeight <= 0.0) continue;

                    var localPos = e.GetPosition(medal);
                    double localX = localPos.X / medal.ActualWidth;
                    double localY = localPos.Y / medal.ActualHeight;
                    effect.MouseX = localX;
                    effect.MouseY = localY;
                }

                if (AnimationConfig.ToolInteractive_EnableRefraction)
                {
                    foreach (var pair in _toolInteractiveRefractionEffects)
                    {
                        var target = pair.Key;
                        var effect = pair.Value;
                        if (!target.IsVisible || target.ActualWidth <= 0.0 || target.ActualHeight <= 0.0) continue;

                        var localPos = e.GetPosition(target);
                        double localX = Math.Max(0.0, Math.Min(1.0, localPos.X / target.ActualWidth));
                        double localY = Math.Max(0.0, Math.Min(1.0, localPos.Y / target.ActualHeight));
                        effect.MouseX = localX;
                        effect.MouseY = localY;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] MouseMove 오류: {ex.Message} (한글)");
            }

            // === Liquid Glass: Fixed backdrop & sidebar mouse tracking ===
            try
            {
                if (_fixedBackdropRefractionEffect != null && MainContentRefractionLayer != null &&
                    MainContentRefractionLayer.IsVisible && MainContentRefractionLayer.ActualWidth > 0)
                {
                    var pos = e.GetPosition(MainContentRefractionLayer);
                    _fixedBackdropRefractionEffect.MouseX = Math.Max(0.0, Math.Min(1.0, pos.X / MainContentRefractionLayer.ActualWidth));
                    _fixedBackdropRefractionEffect.MouseY = Math.Max(0.0, Math.Min(1.0, pos.Y / MainContentRefractionLayer.ActualHeight));
                }

                if (_sidebarRefractionEffect != null && LeftSidebarBorder != null &&
                    LeftSidebarBorder.IsVisible && LeftSidebarBorder.ActualWidth > 0)
                {
                    var pos = e.GetPosition(LeftSidebarBorder);
                    _sidebarRefractionEffect.MouseX = Math.Max(0.0, Math.Min(1.0, pos.X / LeftSidebarBorder.ActualWidth));
                    _sidebarRefractionEffect.MouseY = Math.Max(0.0, Math.Min(1.0, pos.Y / LeftSidebarBorder.ActualHeight));
                }

                if (_bookRefractionEffect != null)
                {
                    var bookLayer = ResolveBookGlassBackplate();
                    if (bookLayer != null && bookLayer.IsVisible && bookLayer.ActualWidth > 0)
                    {
                        var pos = e.GetPosition(bookLayer);
                        _bookRefractionEffect.MouseX = Math.Max(0.0, Math.Min(1.0, pos.X / bookLayer.ActualWidth));
                        _bookRefractionEffect.MouseY = Math.Max(0.0, Math.Min(1.0, pos.Y / bookLayer.ActualHeight));
                    }
                }
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// [Phase 5] Shader 애니메이션 업데이트 (CompositionTarget.Rendering)
        /// VSync와 동기화된 부드러운 애니메이션
        /// </summary>
        private void UpdateShaderAnimation(object? sender, EventArgs e)
        {
            if (_buttonRefractionEffects.Count == 0 &&
                _moddingMedalRefractionEffects.Count == 0 &&
                _toolInteractiveRefractionEffects.Count == 0 &&
                _fixedBackdropRefractionEffect == null &&
                _bookRefractionEffect == null &&
                _sidebarRefractionEffect == null &&
                _tintLayerRefractionEffect == null &&
                _glassRefractionEffect == null) return;

            try
            {
                // RenderingEventArgs를 통해 정확한 타이밍 제어 가능
                if (e is RenderingEventArgs args)
                {
                    // === Time 증가 ===
                    // 60FPS 기준 약 0.01 증가 (16.6ms)
                    // RenderingTime은 계속 증가하므로 이를 이용해 smooth loop 생성

                    // 4초 주기 루프 (4000ms)
                    double totalSeconds = args.RenderingTime.TotalSeconds;
                    double loopDuration = 4.0;

                    _shaderTime = (totalSeconds % loopDuration) / loopDuration; // 0.0 ~ 1.0

                    // Shader에 전달
                    foreach (var effect in _buttonRefractionEffects.Values)
                    {
                        effect.AnimationTime = _shaderTime;
                    }

                    foreach (var effect in _toolInteractiveRefractionEffects.Values)
                    {
                        effect.AnimationTime = _shaderTime;
                    }

                    foreach (var effect in _moddingMedalRefractionEffects.Values)
                    {
                        effect.AnimationTime = _shaderTime;
                    }

                    if (_fixedBackdropRefractionEffect != null)
                    {
                        _fixedBackdropRefractionEffect.AnimationTime = _shaderTime;
                    }

                    if (_sidebarRefractionEffect != null)
                    {
                        _sidebarRefractionEffect.AnimationTime = _shaderTime;
                    }

                    if (_bookRefractionEffect != null)
                    {
                        _bookRefractionEffect.AnimationTime = _shaderTime;
                    }

                    // 틴트 레이어 애니메이션 업데이트
                    if (_tintLayerRefractionEffect != null)
                    {
                        _tintLayerRefractionEffect.AnimationTime = _shaderTime;
                    }

                    if (_blurOverlayRefractionEffect != null)
                    {
                        _blurOverlayRefractionEffect.AnimationTime = _shaderTime;
                    }

                    // Legacy global effect path kept for compatibility.
                    if (_glassRefractionEffect != null)
                    {
                        _glassRefractionEffect.AnimationTime = _shaderTime;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] Animation 오류: {ex.Message} (한글)");
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsLoaded) return; // why: Loaded 이전 SizeChanged 방지
            ModernModWindow_SizeChanged(sender, e);
            AttachBookRefractionEffect();
            AttachModdingMedalRefractionEffects();
        }



        // InitializeModdingTools는 이제 ViewModel에서 처리합니다.
        // 이 메서드는 호환성을 위해 남겨두지만 내용은 비어있습니다.

        private void ExecuteTool(int index, object? parameter)
        {
            System.Diagnostics.Debug.WriteLine($"[ModWindow] ExecuteTool 호출됨: index={index}, parameter={parameter?.GetType().Name} (한글)");

            if (CurrentGame == null && index != 11)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] CurrentGame이 null이므로 UI 데모 모드로 진행 (한글)");
                // Bypass for UI demo
            }

            // Full Save is a menu action, not a tool screen.
            if (index == 10)
            {
                if (parameter is System.Windows.Controls.Button saveButton)
                {
                    PlaySaveButtonInPlacePulse(saveButton);
                }

                ExecuteIntegratedSaveAction();
                return;
            }

            // 인덱스 0번 (캐릭터 정보) 등 버튼 기반 도구 실행
            if (parameter is System.Windows.Controls.Button btn)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] 버튼 파라미터 감지, NavigateTo 호출 (한글)");
                NavigateTo(NavState.ToolWindow, btn);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] 버튼 파라미터가 아님, Fallback switch 처리 (한글)");
                // Fallback switch
                switch (index)
                {
                    case 11: // Settings
                        System.Windows.MessageBox.Show("설정 창 오픈");
                        break;
                    default:
                        System.Windows.MessageBox.Show($"{ModdingTools[index].EngTitle} - 준비 중입니다.");
                        break;
                }
            }
        }

        private void ExecuteIntegratedSaveAction()
        {
            CollectPendingSaveItems();
            SaveOverlaySummaryText.Text = _saveSelectionItems.Count == 0
                ? "변경된 항목이 없습니다."
                : $"변경된 항목 {_saveSelectionItems.Count}개";
            SaveOverlayStatusText.Text = _saveSelectionItems.Count == 0
                ? "저장할 변경사항이 없습니다."
                : "저장할 항목을 선택한 뒤 저장 또는 선택 저장을 실행하세요.";
            ShowSaveSelectionOverlay(true);
        }

        private void CollectPendingSaveItems()
        {
            _saveSelectionItems.Clear();
            _saveParticipantByToolId.Clear();

            AddPendingChangesFromContext(EncounterEditorContent?.DataContext, "encounter", "Encounter");
            AddPendingChangesFromContext(CharacterScaleContent?.DataContext, "char_scale", "Character Scale");
            AddPendingChangesFromContext(YokaiStatsContent?.DataContext, "yokai_stats", "Yokai Stats");
        }

        private void AddPendingChangesFromContext(object? dataContext, string fallbackToolId, string fallbackDisplayName)
        {
            if (dataContext == null)
            {
                return;
            }

            if (dataContext is ISelectiveToolSaveParticipant selective)
            {
                IReadOnlyList<ToolPendingChange> pending = selective.GetPendingChanges();
                if (pending.Count == 0)
                {
                    return;
                }

                string toolId = string.IsNullOrWhiteSpace(selective.ToolId) ? fallbackToolId : selective.ToolId;
                string displayName = string.IsNullOrWhiteSpace(selective.ToolDisplayName) ? fallbackDisplayName : selective.ToolDisplayName;
                _saveParticipantByToolId[toolId] = selective;

                foreach (ToolPendingChange change in pending)
                {
                    _saveSelectionItems.Add(new SaveSelectionItemViewModel
                    {
                        ToolId = toolId,
                        ToolDisplayName = displayName,
                        ChangeId = change.ChangeId,
                        DisplayName = change.DisplayName,
                        Description = change.Description,
                        IsChecked = true
                    });
                }

                return;
            }

            if (dataContext is IToolSaveParticipant legacy && legacy.HasPendingChanges)
            {
                string toolId = $"legacy:{fallbackToolId}";
                _saveParticipantByToolId[toolId] = legacy;
                _saveSelectionItems.Add(new SaveSelectionItemViewModel
                {
                    ToolId = toolId,
                    ToolDisplayName = fallbackDisplayName,
                    ChangeId = "__legacy_all__",
                    DisplayName = $"{fallbackDisplayName} 전체 변경사항",
                    Description = "이 도구는 부분 저장을 지원하지 않습니다.",
                    IsChecked = true
                });
            }
        }

        private void ShowSaveSelectionOverlay(bool visible)
        {
            SaveSelectionOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            SaveSelectionOverlay.IsHitTestVisible = visible;
            SaveOverlaySaveSelectedButton.IsEnabled = visible && _saveSelectionItems.Count > 0;
            SaveOverlaySaveAllButton.IsEnabled = visible && _saveSelectionItems.Count > 0;
        }

        private void SaveOverlayCloseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSaveSelectionOverlay(false);
        }

        private void SaveOverlaySaveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedPendingItems();
        }

        private void SaveOverlaySaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            SaveAllPendingItems();
        }

        private void SaveSelectedPendingItems(bool isFullSave = false)
        {
            var selected = _saveSelectionItems.Where(x => x.IsChecked).ToList();
            if (selected.Count == 0)
            {
                SaveOverlayStatusText.Text = "저장할 항목을 선택하세요.";
                return;
            }

            var grouped = selected.GroupBy(x => x.ToolId);
            var saved = new List<(string ToolId, string ChangeId)>();
            var failed = new Dictionary<(string ToolId, string ChangeId), string>();

            foreach (var group in grouped)
            {
                if (!_saveParticipantByToolId.TryGetValue(group.Key, out object? participant))
                {
                    continue;
                }

                if (participant is ISelectiveToolSaveParticipant selective)
                {
                    ToolSaveBatchResult result = selective.SavePendingChanges(group.Select(x => x.ChangeId).ToArray());
                    foreach (string savedId in result.SavedChangeIds)
                    {
                        saved.Add((group.Key, savedId));
                    }

                    foreach (var pair in result.FailedChangeReasons)
                    {
                        failed[(group.Key, pair.Key)] = pair.Value;
                    }
                }
                else if (participant is IToolSaveParticipant legacy)
                {
                    try
                    {
                        bool ok = legacy.SavePendingChanges();
                        if (ok)
                        {
                            saved.Add((group.Key, "__legacy_all__"));
                        }
                        else
                        {
                            failed[(group.Key, "__legacy_all__")] = "저장할 변경사항이 없거나 저장되지 않았습니다.";
                        }
                    }
                    catch (Exception ex)
                    {
                        failed[(group.Key, "__legacy_all__")] = ex.Message;
                    }
                }
            }

            ApplySaveResultAndRefreshOverlay(saved, failed, isFullSave);
        }

        private void SaveAllPendingItems()
        {
            foreach (SaveSelectionItemViewModel item in _saveSelectionItems)
            {
                item.IsChecked = true;
            }

            SaveSelectedPendingItems(isFullSave: true);
            bool keepOverlayOpen = _saveSelectionItems.Count > 0;
            TrySaveFullArchive(keepOverlayOpen);
        }

        private void ApplySaveResultAndRefreshOverlay(
            IReadOnlyCollection<(string ToolId, string ChangeId)> saved,
            IReadOnlyDictionary<(string ToolId, string ChangeId), string> failed,
            bool isFullSave = false)
        {
            int savedCount = 0;
            int failedCount = 0;

            foreach (var item in _saveSelectionItems)
            {
                item.ErrorMessage = null;
            }

            foreach (var key in failed.Keys)
            {
                SaveSelectionItemViewModel? target = _saveSelectionItems.FirstOrDefault(x =>
                    string.Equals(x.ToolId, key.ToolId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ChangeId, key.ChangeId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    target.ErrorMessage = failed[key];
                    target.IsChecked = false;
                    failedCount++;
                }
            }

            for (int i = _saveSelectionItems.Count - 1; i >= 0; i--)
            {
                SaveSelectionItemViewModel item = _saveSelectionItems[i];
                bool isSaved = saved.Any(x =>
                    string.Equals(x.ToolId, item.ToolId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ChangeId, item.ChangeId, StringComparison.OrdinalIgnoreCase));
                if (isSaved)
                {
                    _saveSelectionItems.RemoveAt(i);
                    savedCount++;
                }
            }

            if (_saveSelectionItems.Count == 0)
            {
                ShowSaveSelectionOverlay(false);
                SaveOverlaySummaryText.Text = "변경된 항목이 없습니다.";
                SaveOverlayStatusText.Text = savedCount > 0
                    ? $"저장 완료: {savedCount}개"
                    : "변경된 항목이 없습니다.";
                return;
            }

            SaveOverlaySummaryText.Text = $"남은 변경 항목 {_saveSelectionItems.Count}개";
            SaveOverlayStatusText.Text = failedCount > 0
                ? $"저장 성공 {savedCount}개 / 실패 {failedCount}개 / 남은 항목 {_saveSelectionItems.Count}개"
                : $"저장 성공 {savedCount}개 / 남은 항목 {_saveSelectionItems.Count}개";
            SaveOverlaySaveSelectedButton.IsEnabled = _saveSelectionItems.Count > 0;
            SaveOverlaySaveAllButton.IsEnabled = _saveSelectionItems.Count > 0;
        }

        private string ResolveFullSaveExportPath(YW2 yw2)
        {
            string baseDir = yw2.CurrentProject?.ExportsPath;
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            }

            Directory.CreateDirectory(baseDir);
            return System.IO.Path.Combine(baseDir, "yw2_a.fa");
        }

        private bool TrySaveFullArchive(bool keepOverlayOpen = false)
        {
            if (CurrentGame is not YW2 yw2)
            {
                SaveOverlayStatusText.Text = "전체 아카이브 저장은 YW2에서만 지원합니다.";
                return false;
            }

            try
            {
                string exportPath = ResolveFullSaveExportPath(yw2);
                yw2.SaveFullArchive(exportPath);

                SaveOverlaySummaryText.Text = "저장 완료";
                SaveOverlayStatusText.Text = $"저장 완료: {exportPath}";
                if (!keepOverlayOpen)
                {
                    ShowSaveSelectionOverlay(false);
                }
                return true;
            }
            catch (Exception ex)
            {
                SaveOverlayStatusText.Text = $"전체 아카이브 저장 실패: {ex.Message}";
                return false;
            }
        }

        private void PlaySaveButtonInPlacePulse(System.Windows.Controls.Button sourceButton)
        {
            try
            {
                if (TransitionProxy == null || ProxyIconContainer == null)
                {
                    return;
                }

                var root = VisualTreeHelper.GetParent(TransitionProxy) as UIElement;
                if (root == null)
                {
                    return;
                }

                if (sourceButton.DataContext is ModdingToolViewModel toolVm)
                {
                    ProxyBag.Source = new BitmapImage(new Uri(toolVm.BagIconPath, UriKind.Absolute));
                    ProxyIcon.Source = new BitmapImage(new Uri(toolVm.IconBPath, UriKind.Absolute));
                    ProxyText.Text = toolVm.Title;
                }

                double proxySize = Math.Max(1.0, Math.Min(sourceButton.ActualWidth, sourceButton.ActualHeight));
                ProxyIconContainer.Width = proxySize;
                ProxyIconContainer.Height = proxySize;

                var transform = sourceButton.TransformToVisual(root);
                var topLeft = transform.Transform(new Point(
                    (sourceButton.ActualWidth - proxySize) * 0.5,
                    (sourceButton.ActualHeight - proxySize) * 0.5));

                TransitionProxy.Margin = new Thickness(topLeft.X, topLeft.Y, 0, 0);
                TransitionProxy.Visibility = Visibility.Visible;
                TransitionProxy.Opacity = 1;
                System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxy);

                var scale = new ScaleTransform(0.92, 0.92);
                var translate = new TranslateTransform(0, 0);
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(translate);
                TransitionProxy.RenderTransform = group;

                var duration = TimeSpan.FromMilliseconds(290);
                var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
                var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

                var scaleX = new DoubleAnimationUsingKeyFrames();
                scaleX.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleX.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))) { KeySpline = new KeySpline(0.25, 0.1, 0.25, 1.0) });
                scaleX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration)) { KeySpline = new KeySpline(0.4, 0, 1, 1) });

                var scaleY = scaleX.Clone();
                var fade = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easeIn };

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                TransitionProxy.BeginAnimation(UIElement.OpacityProperty, fade);

                var hideTimer = new DispatcherTimer { Interval = duration };
                hideTimer.Tick += (_, __) =>
                {
                    hideTimer.Stop();
                    TransitionProxy.BeginAnimation(UIElement.OpacityProperty, null);
                    TransitionProxy.Opacity = 0;
                    TransitionProxy.Visibility = Visibility.Collapsed;
                    System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxyBelowHeader);
                };
                hideTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] Save pulse animation error: {ex.Message}");
            }
        }


        private void InitializeModdingMenu()
        {
            // ModdingTools는 InitializeModdingTools()에서 초기화됨
            // 이 메서드는 필요 시 추가 설정용
        }

        private void InitializeProjectMenu()
        {
            ProjectManager.EnsureProjectsRoot();
            RefreshProjectList();
        }

        private void RefreshProjectList()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] RefreshProjectList - ViewModel 사용 (한글)");

                // ViewModel의 Command를 실행하여 프로젝트 목록 갱신
                ViewModel.RefreshProjectListCommand.Execute().Subscribe();

                // ItemsSource를 ViewModel의 Projects 컬렉션으로 바인딩
                ItemsProjectList.ItemsSource = null;
                ItemsProjectList.ItemsSource = ViewModel.Projects;
                #region agent log
                try
                {
                    var log = new
                    {
                        sessionId = "debug-session",
                        runId = "run1",
                        hypothesisId = "H6",
                        location = "ModernModWindow.xaml.cs:RefreshProjectList:bound",
                        message = "Project list bound",
                        data = new { projectCount = ViewModel.Projects.Count },
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
                }
                catch
                {
                }
                #endregion

                // 프로젝트 유무에 따라 목록/빈 상태 전환
                if (ViewModel.Projects.Count > 0)
                {
                    ProjectListScroll.Visibility = Visibility.Visible;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ProjectListScroll.Visibility = Visibility.Collapsed;
                    EmptyStatePanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"프로젝트 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Project View Handlers

        private void BtnShowCreateForm_Click(object sender, RoutedEventArgs e)
        {
            ProjectListView.Visibility = Visibility.Collapsed;
            CreateProjectForm.Visibility = Visibility.Visible;

            // Clear inputs
            TxtProjName.Clear();
            TxtGamePath.Clear();
            TxtProjDesc.Clear();
        }

        private void BtnCancelCreate_Click(object sender, RoutedEventArgs e)
        {
            CreateProjectForm.Visibility = Visibility.Collapsed;
            ProjectListView.Visibility = Visibility.Visible;
        }

        private void BtnBrowseGame_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "베이스 게임 데이터가 있는 폴더를 선택해주세요.";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtGamePath.Text = dialog.SelectedPath;
                }
            }
        }

        private string? ResolveVanillaSamplePath()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ICN_T2", "sample"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample")
            };

            foreach (var dir in candidates)
            {
                if (!Directory.Exists(dir)) continue;

                string a = System.IO.Path.Combine(dir, "yw2_a.fa");
                string lg = System.IO.Path.Combine(dir, "yw2_lg_ko.fa");
                if (File.Exists(a) && File.Exists(lg))
                {
                    return dir;
                }
            }

            return null;
        }

        private void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = TxtProjName.Text.Trim();
                string desc = TxtProjDesc.Text.Trim();
                string finalGamePath = "";

                if (string.IsNullOrEmpty(name))
                {
                    System.Windows.MessageBox.Show("프로젝트 이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (RbVanilla.IsChecked == true)
                {
                    string? samplePath = ResolveVanillaSamplePath();
                    if (samplePath == null)
                    {
                        System.Windows.MessageBox.Show("바닐라 샘플 파일(yw2_a.fa, yw2_lg_ko.fa)을 찾을 수 없습니다. 경로: ICN_T2/sample", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    finalGamePath = samplePath;
                }
                else
                {
                    // 모딩됨 선택 시 사용자가 입력한 경로 사용
                    finalGamePath = TxtGamePath.Text.Trim();
                    if (string.IsNullOrEmpty(finalGamePath))
                    {
                        System.Windows.MessageBox.Show("베이스 게임 경로를 지정해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                ProjectManager.CreateProject(name, finalGamePath, desc);

                System.Windows.MessageBox.Show("프로젝트가 생성되었습니다!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

                BtnCancelCreate_Click(this, new RoutedEventArgs());
                RefreshProjectList();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"프로젝트 생성 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            string? projectPath = btn?.Tag?.ToString();

            if (string.IsNullOrEmpty(projectPath)) return;

            try
            {
                System.Diagnostics.Trace.WriteLine($"[ModWindow] 프로젝트 열기 시작: {projectPath}");

                // 프로젝트 로드
                var project = ProjectManager.LoadProject(projectPath);
                if (project == null)
                {
                    System.Windows.MessageBox.Show("프로젝트를 불러올 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 게임 인스턴스 생성 (YW2)
                string gamePath = project.BaseGamePath;
                if (!System.IO.Directory.Exists(gamePath))
                {
                    System.Windows.MessageBox.Show($"게임 데이터 폴더를 찾을 수 없습니다:\n{gamePath}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CurrentGame = new YW2(project);
                System.Diagnostics.Trace.WriteLine($"[ModWindow] 게임 인스턴스 생성 완료: {CurrentGame.GetType().Name}");

                // 모딩 메뉴로 이동
                NavigateTo(NavState.ModdingMenu, projectPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ModWindow] 프로젝트 열기 오류: {ex.Message}");
                System.Windows.MessageBox.Show($"프로젝트를 여는 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBackToMainMenu_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }



        private void BtnDeleteProject_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            string? projectPath = btn?.Tag?.ToString();

            if (string.IsNullOrEmpty(projectPath)) return;

            var result = System.Windows.MessageBox.Show("정말로 이 프로젝트를 삭제하시겠습니까?\n모든 데이터가 영구적으로 사라집니다.", "삭제 확인",
                                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ProjectManager.DeleteProject(projectPath);
                    RefreshProjectList();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"삭제 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NavProject_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSidebarNavCheckedEvents) return;
            if (!IsSenderSidebarNavInteractive(sender)) return;
            ShowProjectSectionImmediate();
        }

        private void NavTool_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSidebarNavCheckedEvents) return;
            if (!IsSenderSidebarNavInteractive(sender)) return;
            ShowModdingSectionImmediate();
        }

        private void NavOption_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSidebarNavCheckedEvents) return;
            if (!IsSenderSidebarNavInteractive(sender)) return;
            ShowOptionSectionImmediate();
        }

        private static bool IsSenderSidebarNavInteractive(object sender)
        {
            if (sender is not FrameworkElement fe)
            {
                return true;
            }

            return fe.IsLoaded && fe.IsVisible && fe.IsHitTestVisible;
        }

        private void ResetNavigationStackForImmediateSection(NavState? secondaryState = null)
        {
            _navStack.Clear();
            _navStack.Push(new NavItem { State = NavState.ProjectList, MethodName = "ImmediateReset" });

            if (secondaryState.HasValue && secondaryState.Value != NavState.ProjectList)
            {
                _navStack.Push(new NavItem { State = secondaryState.Value, MethodName = "ImmediateReset" });
            }
        }

        private void ShowProjectSectionImmediate()
        {
            if (NavProject != null && NavProject.IsChecked != true)
            {
                NavProject.IsChecked = true;
            }

            if (ProjectMenuContent == null ||
                ProjectListView == null ||
                CreateProjectForm == null ||
                ModdingMenuContent == null ||
                BookCover == null ||
                ToolPlaceholderContent == null ||
                OptionPlaceholderContent == null ||
                ModdingDescriptionPanel == null ||
                ModdingMenuButtons == null ||
                ProjectMenuButtons == null ||
                TxtMainHeader == null)
            {
                return;
            }

            ResetNavigationStackForImmediateSection();

            ProjectMenuContent.Visibility = Visibility.Visible;
            ProjectListView.Visibility = Visibility.Visible;
            CreateProjectForm.Visibility = Visibility.Collapsed;

            ModdingMenuContent.Visibility = Visibility.Collapsed;
            BookCover.Visibility = Visibility.Collapsed;
            ToolPlaceholderContent.Visibility = Visibility.Collapsed;
            OptionPlaceholderContent.Visibility = Visibility.Collapsed;
            ModdingDescriptionPanel.Visibility = Visibility.Collapsed;

            ModdingMenuButtons.Visibility = Visibility.Collapsed;
            ModdingMenuButtons.Opacity = 0;
            ModdingMenuButtons.IsHitTestVisible = false;
            ProjectMenuButtons.Visibility = Visibility.Visible;
            ProjectMenuButtons.Opacity = 1;
            ProjectMenuButtons.IsHitTestVisible = true;

            var vm = DataContext as ModernModWindowViewModel;
            if (vm != null)
            {
                vm.HeaderText = "메인메뉴";
                TxtMainHeader.Text = NormalizeHeaderText(vm.HeaderText);
            }
        }

        private void ShowModdingSectionImmediate()
        {
            if (NavTool != null && NavTool.IsChecked != true)
            {
                NavTool.IsChecked = true;
            }

            if (ProjectMenuContent == null ||
                ToolPlaceholderContent == null ||
                OptionPlaceholderContent == null ||
                ModdingMenuContent == null ||
                BookCover == null ||
                ModdingMenuButtons == null ||
                ProjectMenuButtons == null ||
                TxtMainHeader == null)
            {
                return;
            }

            ResetNavigationStackForImmediateSection(NavState.ModdingMenu);

            ProjectMenuContent.Visibility = Visibility.Collapsed;
            ToolPlaceholderContent.Visibility = Visibility.Collapsed;
            OptionPlaceholderContent.Visibility = Visibility.Collapsed;

            ModdingMenuContent.Visibility = Visibility.Visible;
            ModdingMenuContent.Opacity = 1;
            BookCover.Visibility = Visibility.Visible;
            BookCover.Opacity = 1;
            ModdingMenuButtons.Visibility = Visibility.Visible;
            ModdingMenuButtons.Opacity = 1;
            ModdingMenuButtons.IsHitTestVisible = true;
            ProjectMenuButtons.Visibility = Visibility.Collapsed;
            ProjectMenuButtons.Opacity = 0;
            ProjectMenuButtons.IsHitTestVisible = false;

            var vm = DataContext as ModernModWindowViewModel;
            if (vm != null)
            {
                vm.HeaderText = "모딩메뉴";
                TxtMainHeader.Text = NormalizeHeaderText(vm.HeaderText);
            }
        }

        private void ShowOptionSectionImmediate()
        {
            if (NavOption != null && NavOption.IsChecked != true)
            {
                NavOption.IsChecked = true;
            }

            if (ProjectMenuContent == null ||
                ProjectListView == null ||
                CreateProjectForm == null ||
                ModdingMenuContent == null ||
                BookCover == null ||
                ToolPlaceholderContent == null ||
                OptionPlaceholderContent == null ||
                ModdingDescriptionPanel == null ||
                ProjectMenuButtons == null ||
                ModdingMenuButtons == null ||
                TxtMainHeader == null)
            {
                return;
            }

            ProjectMenuContent.Visibility = Visibility.Visible;
            ProjectListView.Visibility = Visibility.Collapsed;
            CreateProjectForm.Visibility = Visibility.Collapsed;
            ModdingMenuContent.Visibility = Visibility.Collapsed;
            BookCover.Visibility = Visibility.Collapsed;
            ToolPlaceholderContent.Visibility = Visibility.Collapsed;

            OptionPlaceholderContent.Visibility = Visibility.Visible;
            ModdingDescriptionPanel.Visibility = Visibility.Collapsed;
            ProjectMenuButtons.Visibility = Visibility.Visible;
            ProjectMenuButtons.Opacity = 1;
            ProjectMenuButtons.IsHitTestVisible = true;
            ModdingMenuButtons.Visibility = Visibility.Collapsed;
            ModdingMenuButtons.Opacity = 0;
            ModdingMenuButtons.IsHitTestVisible = false;

            var vm = DataContext as ModernModWindowViewModel;
            if (vm != null)
            {
                vm.HeaderText = "설정";
                TxtMainHeader.Text = NormalizeHeaderText(vm.HeaderText);
            }
        }

        private void ModdingMenuButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is ModdingToolViewModel vm)
            {
                ModdingDescTitle.Text = vm.Title;
                ModdingDescBody.Text = vm.Description ?? "";
                ModdingDescriptionPanel.Visibility = Visibility.Visible;
            }
        }

        private void NavMainTool_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSidebarNavCheckedEvents) return;
            if (!IsSenderSidebarNavInteractive(sender)) return;
            ShowToolSectionImmediate();
        }

        private void ShowToolSectionImmediate()
        {
            // Ensure this radio button is visually checked
            // (Only needed if called programmatically, but good practice)
            // We don't have a direct reference to the sidebar 'Tools' radio button name in XAML currently,
            // so we rely on the sender or binding, but for now just updating UI state is enough.

            if (ProjectMenuContent == null ||
                ProjectListView == null ||
                CreateProjectForm == null ||
                ModdingMenuContent == null ||
                BookCover == null ||
                ToolPlaceholderContent == null ||
                OptionPlaceholderContent == null ||
                ModdingDescriptionPanel == null ||
                ProjectMenuButtons == null ||
                ModdingMenuButtons == null ||
                TxtMainHeader == null)
            {
                return;
            }

            // Hide other sections
            ProjectMenuContent.Visibility = Visibility.Visible;
            ProjectListView.Visibility = Visibility.Collapsed;
            CreateProjectForm.Visibility = Visibility.Collapsed;
            ModdingMenuContent.Visibility = Visibility.Collapsed;
            BookCover.Visibility = Visibility.Collapsed;
            OptionPlaceholderContent.Visibility = Visibility.Collapsed;
            ModdingDescriptionPanel.Visibility = Visibility.Collapsed;

            // Show Tool Placeholder
            ToolPlaceholderContent.Visibility = Visibility.Visible;

            // Ensure Main Menu Sidebar is visible (since we are in Main Menu > Tools)
            ProjectMenuButtons.Visibility = Visibility.Visible;
            ProjectMenuButtons.Opacity = 1;
            ProjectMenuButtons.IsHitTestVisible = true;

            // Hide Modding Sidebar
            ModdingMenuButtons.Visibility = Visibility.Collapsed;
            ModdingMenuButtons.Opacity = 0;
            ModdingMenuButtons.IsHitTestVisible = false;

            var vm = DataContext as ModernModWindowViewModel;
            if (vm != null)
            {
                vm.HeaderText = "도구";
                TxtMainHeader.Text = NormalizeHeaderText(vm.HeaderText);
            }
        }

        private void ModdingMenuButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ModdingDescriptionPanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                // 인터랙티브 컨트롤(ListBox, TextBox, Button 등) 위에서는 DragMove 하지 않음
                // → 리스트 선택, 텍스트 입력 등이 정상 작동하도록
                var source = e.OriginalSource as System.Windows.DependencyObject;
                while (source != null && source != this)
                {
                    if (source is System.Windows.Controls.ListBox ||
                        source is System.Windows.Controls.TextBox ||
                        source is System.Windows.Controls.Button ||
                        source is System.Windows.Controls.Primitives.ScrollBar)
                        return;

                    // [FIX] Handle non-Visual elements (like Run)
                    if (source is System.Windows.FrameworkContentElement fce)
                    {
                        source = fce.Parent;
                    }
                    else
                    {
                        // Ensure it's a Visual or Visual3D before calling GetParent
                        if (source is System.Windows.Media.Visual || source is System.Windows.Media.Media3D.Visual3D)
                        {
                            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                        }
                        else
                        {
                            // If it's neither, stop walking up to avoid crash
                            break;
                        }
                    }
                }

                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException)
                {
                    // 마우스 캡처 실패 시 무시
                }
            }
        }

        private async void TitleOverlay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 0. Disable interaction
            TitleOverlay.IsHitTestVisible = false;

            // --- "Bouncy Ball Squeeze" Hybrid Transition ---
            // Start: "Squeeze" (Tension) -> "Snap" (Release)
            // End: "Restful" Fade Out (Dreamy feeling)

            // === Timeline (Total ~3.0s) ===
            // Phase 1: Squeeze (0s -> 0.5s)
            //          ScaleX -> 0.85, ScaleY -> 1.15. "Pressing the ball".
            // Phase 2: Snap (0.5s -> 0.9s)
            //          Scale -> 2.5 (Explosion). "Releasing the ball".
            // Phase 3: Flash In (Starts at 0.7s)
            // Phase 4: Swap (0.9s)
            // Phase 5: Awakening (0.9s -> 2.9s) - Slow fade out.

            // 1. Phase 1: The Squeeze (Tension) - 0.3s
            var squeezeX = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.85, System.TimeSpan.FromSeconds(0.2));
            squeezeX.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var squeezeY = new System.Windows.Media.Animation.DoubleAnimation(1.0, 1.15, System.TimeSpan.FromSeconds(0.2));
            squeezeY.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, squeezeX);
            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, squeezeY);

            // Wait for Squeeze
            await System.Threading.Tasks.Task.Delay(300);

            // 2. Phase 2: The Snap (Release/Pop) - 0.4s
            var snapX = new System.Windows.Media.Animation.DoubleAnimation(0.85, 2.5, System.TimeSpan.FromSeconds(0.3));
            snapX.EasingFunction = new System.Windows.Media.Animation.QuinticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };
            var snapY = new System.Windows.Media.Animation.DoubleAnimation(1.15, 2.5, System.TimeSpan.FromSeconds(0.3));
            snapY.EasingFunction = new System.Windows.Media.Animation.QuinticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, snapX);
            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, snapY);

            // 3. Flash In (Starts at 80% of Snap -> 320ms)
            await System.Threading.Tasks.Task.Delay(260);

            var flashIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, System.TimeSpan.FromSeconds(0.1));
            flashIn.EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            FlashOverlay.BeginAnimation(System.Windows.UIElement.OpacityProperty, flashIn);

            // Wait for Flash to complete (Total 420ms from Snap start)
            // We waited 320ms, need 100ms more
            await System.Threading.Tasks.Task.Delay(100);

            // --- BEHIND THE SCENES SWAP ---
            TitleOverlay.Visibility = Visibility.Collapsed;
            MainContentPanel.Visibility = Visibility.Visible;
            MainContentPanel.Opacity = 0;

            // Reset Transforms
            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            SqueezeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            SqueezeScale.ScaleX = 1.0;
            SqueezeScale.ScaleY = 1.0;

            // Switch Background
            try
            {
                var newBgBrush = new System.Windows.Media.ImageBrush();
                newBgBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("pack://application:,,,/ICN_T2;component/Resources/MenuBG/pz_bg_e208_01.png"));
                newBgBrush.Stretch = System.Windows.Media.Stretch.UniformToFill;
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(newBgBrush, System.Windows.Media.BitmapScalingMode.HighQuality);
                BackgroundContainer.Background = newBgBrush;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed: " + ex.Message);
            }

            // 4. Phase 3: Awakening (Background Reveal)
            // Fade out white to show background first
            var wakeUp = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, System.TimeSpan.FromSeconds(2.0));
            wakeUp.EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
            FlashOverlay.BeginAnimation(System.Windows.UIElement.OpacityProperty, wakeUp);

            // Wait 2.0s for background to be fully visible
            await System.Threading.Tasks.Task.Delay(2000);

            // 5. Dashboard Reveal (Slide Right + Fade In)
            // "Show background... then 2s later fade in from left"
            ContentSlide.Y = 0;
            ContentSlide.X = -50; // Start from Left

            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(-50, 0, System.TimeSpan.FromSeconds(1.2));
            slideIn.EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            ContentSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, System.TimeSpan.FromSeconds(1.2));
            fadeIn.EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            MainContentPanel.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeIn);

            // [FIX] Show Header on Initial Load
            // Ensure header text is set and visible (ViewModel 사용)
            ViewModel.HeaderText = "메인메뉴";
            TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);
            TxtMainHeader.Opacity = 0;
            // Reset header translate transform
            var headerTranslate = TxtMainHeader.RenderTransform as TranslateTransform;
            if (headerTranslate != null)
            {
                headerTranslate.X = -120; // Start from left
                headerTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-120, 0, TimeSpan.FromSeconds(1.2))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            // Fade in header along with main content (Rx 기반)
            await UIAnimationsRx.Fade(TxtMainHeader, 0, 1, 1200);
            await AnimateGlobalTitleBarAsync(true);

            System.Diagnostics.Debug.WriteLine("[ModWindow] TitleOverlay_Click 완료 (한글)");
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Shader Animation Cleanup
            if (LeftSidebarBorder != null)
            {
                LeftSidebarBorder.SizeChanged -= LeftSidebarBorder_SizeChanged;
            }
            this.MouseMove -= Window_MouseMove_ShaderUpdate;
            System.Windows.Media.CompositionTarget.Rendering -= UpdateShaderAnimation;
            foreach (var pair in _buttonRefractionEffects)
            {
                if (ReferenceEquals(pair.Key.Effect, pair.Value))
                {
                    pair.Key.Effect = null;
                }
            }
            _buttonRefractionEffects.Clear();
            DetachModdingMedalRefractionEffects();
            DetachBookRefractionEffect();

            foreach (var pair in _toolPanelRefractionEffects)
            {
                if (ReferenceEquals(pair.Key.Effect, pair.Value))
                {
                    pair.Key.Effect = null;
                }
            }
            _toolPanelRefractionEffects.Clear();

            foreach (var pair in _toolInteractiveRefractionEffects)
            {
                if (ReferenceEquals(pair.Key.Effect, pair.Value))
                {
                    pair.Key.Effect = null;
                }
            }
            _toolInteractiveRefractionEffects.Clear();

            if (MainContentRefractionLayer != null &&
                _fixedBackdropRefractionEffect != null &&
                ReferenceEquals(MainContentRefractionLayer.Effect, _fixedBackdropRefractionEffect))
            {
                MainContentRefractionLayer.Effect = null;
            }
            _fixedBackdropRefractionEffect = null;
            _sidebarRefractionEffect = null;
            _bookRefractionEffect = null;
            _toolPanelBackdropBrush = null;
            _glassNormalMapBrush = null;


            System.Diagnostics.Debug.WriteLine("[ModWindow] Closed - Resources Released");
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
