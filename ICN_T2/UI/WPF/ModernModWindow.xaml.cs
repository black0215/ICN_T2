using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICN_T2.Logic.Project;
using ICN_T2.UI.WPF.ViewModels;
using System.Collections.ObjectModel;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.UI.WPF.Animations;
using ICN_T2.UI.WPF.Services;

namespace ICN_T2.UI.WPF
{
    public partial class ModernModWindow : Window
    {
        private List<Project> _projects = new List<Project>();
        public ObservableCollection<ModdingToolViewModel> ModdingTools { get; set; } = new ObservableCollection<ModdingToolViewModel>();
        public IGame? CurrentGame { get; private set; }

        // Rx 기반 애니메이션 서비스
        private readonly AnimationService _animationService = new AnimationService();

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

        private Stack<NavItem> _navStack = new Stack<NavItem>();

        public void NavigateTo(NavState target, object? context = null, [System.Runtime.CompilerServices.CallerMemberName] string? methodName = null)
        {
            _navStack.Push(new NavItem { State = target, Context = context, MethodName = methodName });
            UpdateUI(target, context);
        }

        public void GoBack()
        {
            if (_navStack.Count <= 1) return;

            var current = _navStack.Pop();
            var previous = _navStack.Peek();

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
                    AnimateToolCompactLayout(false);
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
                    AnimateToolCompactLayout(false);
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

            // --- STEP 1: SETUP PROXY ---
            _activeTransitionButton = btn;

            // Setup Images
            ProxyBag.Source = new BitmapImage(new Uri(vm.BagIconPath, UriKind.Absolute));
            ProxyIcon.Source = new BitmapImage(new Uri(vm.IconBPath, UriKind.Absolute));

            var proxyTxt = ProxyIconContainer.FindName("ProxyText") as System.Windows.Controls.TextBlock;
            if (proxyTxt != null) proxyTxt.Text = vm.Title;

            ProxyIconContainer.Width = btn.ActualWidth;
            ProxyIconContainer.Height = btn.ActualHeight;

            // Get Positions relative to Root
            var rootGrid = VisualTreeHelper.GetParent(TransitionProxy) as UIElement;
            if (rootGrid == null) return;

            var btnTransform = btn.TransformToVisual(rootGrid);
            var startPoint = btnTransform.Transform(new System.Windows.Point(0, 0));

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

            // 프록시 아이콘/텍스트 세팅 후 원래 버튼 숨김
            btn.Visibility = Visibility.Hidden;

            // Manual Trigger instead of Property Setter
            IsSelectionFinished = true;
            await PlaySelectionAnimation();
        }

        private async System.Threading.Tasks.Task TransitionToModdingMenu()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionToModdingMenu 시작 - Rx 기반 전환됨 (한글)");
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

            // [FIX] 애니메이션 초기화: 이전 애니메이션 제거 후 원래 위치로 명시적 설정
            BookCover.BeginAnimation(FrameworkElement.MarginProperty, null);
            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, null);

            var bookBaseMargin = new Thickness(AnimationConfig.Book_BaseMarginLeft, AnimationConfig.Book_BaseMarginTop, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);
            BookCover.Margin = bookBaseMargin;
            ModdingMenuContent.Margin = new Thickness(
                AnimationConfig.Book_BaseMarginLeft + AnimationConfig.Book_Open2OffsetX,
                AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY,
                AnimationConfig.Book_BaseMarginRight,
                AnimationConfig.Book_BaseMarginBottom
            );

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

            // Transition Header (Rx 기반, ViewModel 사용)
            ViewModel.HeaderText = "모딩메뉴";
            TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);
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

            var bgSlide = new DoubleAnimationUsingKeyFrames();
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(_bgShakeOffset, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(_bgSlideFirstKeyTimeSeconds))));
            bgSlide.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(_bgSlideSecondKeyTimeSeconds))));
            ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, bgSlide);

            // 책 열기 완료 대기
            await bookOpenTask;

            // Phase 2: 시선 여유 후 → 배경 확장 + 사이드바 축소 + 패널 마진 변경을 동시 시작
            // 책이 이미 펼쳐진 상태에서, 모든 레이아웃 변화가 함께 시작됨
            await System.Threading.Tasks.Task.Delay(AnimationConfig.Book_ExtraDelay);

            System.Diagnostics.Debug.WriteLine("[ModWindow] Phase 2: 배경 확장 + 사이드바 축소 + 책 이동 동시 시작 (한글)");

            // 모든 애니메이션을 동일 콜 스택에서 BeginAnimation → 같은 프레임에 시작
            var duration = TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // 1. 배경 확장 (StepProgress 0→0.5)
            AnimateSteppedLayoutTo(AnimationConfig.Background_StepProgress_ModdingMenu);

            // 2. 사이드바 축소 (직접 BeginAnimation — Observable 지연 없음)
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            var sideAnim = new DoubleAnimation(LeftSidebarBorder.ActualWidth, AnimationConfig.Sidebar_ModdingMenu_Width, duration) { EasingFunction = easing };
            LeftSidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, sideAnim);

            // 3. 패널 마진 변경 (직접 BeginAnimation — Observable 지연 없음)
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            var currentMargin = MainContentPanel.Margin;
            var targetMargin = new Thickness(AnimationConfig.MainPanel_ModdingMenu_MarginLeft, currentMargin.Top, currentMargin.Right, currentMargin.Bottom);
            var marginAnim = new ThicknessAnimation(currentMargin, targetMargin, duration) { EasingFunction = easing };
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);

            // [NEW] 4. 책 이동 애니메이션 (배경보다 빠르게 도착)
            // [FIX] 목표 위치를 명시적으로 지정 (AnimationConfig에서 정의)
            double targetBookLeft = AnimationConfig.Book_ModdingMenu_MarginLeft;

            System.Diagnostics.Debug.WriteLine($"[ModWindow] 책 이동: {AnimationConfig.Book_BaseMarginLeft} → {targetBookLeft} (명시적 지정) (한글)");

            // 시작 위치는 원래 위치 (확장 전)
            var currentBookMargin = new Thickness(AnimationConfig.Book_BaseMarginLeft, AnimationConfig.Book_BaseMarginTop, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);
            var currentContentMargin = new Thickness(AnimationConfig.Book_BaseMarginLeft + AnimationConfig.Book_Open2OffsetX, AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);

            var bookDuration = TimeSpan.FromMilliseconds(AnimationConfig.Book_MoveDuration);
            var bookEasing = new CubicEase { EasingMode = EasingMode.EaseOut };

            BookCover.BeginAnimation(FrameworkElement.MarginProperty, null);
            var bookMarginAnim = new ThicknessAnimation(
                currentBookMargin,  // From: 원래 위치
                new Thickness(targetBookLeft, AnimationConfig.Book_BaseMarginTop, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom),  // To: 목표 위치
                bookDuration)
            { EasingFunction = bookEasing };
            BookCover.BeginAnimation(FrameworkElement.MarginProperty, bookMarginAnim);

            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, null);
            var contentMarginAnim = new ThicknessAnimation(
                currentContentMargin,  // From: 원래 위치
                new Thickness(targetBookLeft + AnimationConfig.Book_Open2OffsetX, AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom),  // To: 목표 위치
                bookDuration)
            { EasingFunction = bookEasing };
            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, contentMarginAnim);

            // 완료 대기 (레이아웃 확장 시간만큼)
            await System.Threading.Tasks.Task.Delay((int)AnimationConfig.Transition_LayoutDuration);

            // [FIX] 애니메이션 완료 후 최종 위치로 명시적 설정 (재진입 시 올바른 초기화를 위해)
            BookCover.BeginAnimation(FrameworkElement.MarginProperty, null);
            BookCover.Margin = new Thickness(targetBookLeft, AnimationConfig.Book_BaseMarginTop, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);

            ModdingMenuContent.BeginAnimation(FrameworkElement.MarginProperty, null);
            ModdingMenuContent.Margin = new Thickness(targetBookLeft + AnimationConfig.Book_Open2OffsetX, AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);

            await UIAnimationsRx.Fade(GlobalTitleBar, 1, 0, AnimationConfig.Fade_Duration);
            GlobalTitleBar.IsHitTestVisible = false;

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
            GlobalTitleBar.Opacity = 0;
            ProjectMenuButtons.Opacity = 0;
            ProjectListView.Opacity = 0;

            // [FIX] 책 페이드아웃 먼저 완료 (배경 경계면 문제 해결)
            await Observable.Merge(
                UIAnimationsRx.Fade(ModdingMenuContent, 1, 0, AnimationConfig.Fade_Duration),
                UIAnimationsRx.Fade(BookCover, 1, 0, AnimationConfig.Book_CloseFadeOutDuration),
                // 오른쪽으로 이동하며 사라짐
                UIAnimationsRx.SlideX(ModdingMenuContent, 0, AnimationConfig.Book_SlideOffset * 3, AnimationConfig.Fade_Duration),
                UIAnimationsRx.SlideX(BookCover, 0, AnimationConfig.Book_SlideOffset * 3, AnimationConfig.Book_CloseFadeOutDuration)
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

            // 패널 마진 복원 (직접 BeginAnimation)
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            var revCurrentMargin = MainContentPanel.Margin;
            var revTargetMargin = new Thickness(AnimationConfig.MainPanel_ProjectMenu_MarginAll);
            var revMarginAnim = new ThicknessAnimation(revCurrentMargin, revTargetMargin, revDuration) { EasingFunction = revEasing };
            MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, revMarginAnim);

            // 메인 메뉴 요소 등장 (배경 축소와 병렬)
            await Observable.Merge(
                // 메인 메뉴 요소 등장 (딜레이 후)
                Observable.FromAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(AnimationConfig.Fade_MainMenuAppearDelay);
                    return true;
                }).SelectMany(_ => UIAnimationsRx.Fade(GlobalTitleBar, 0, 1, AnimationConfig.Fade_Duration)),
                Observable.FromAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(AnimationConfig.Fade_MainMenuAppearDelay);
                    return true;
                }).SelectMany(_ => UIAnimationsRx.Fade(ProjectMenuButtons, 0, 1, AnimationConfig.Fade_Duration)),
                Observable.FromAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(AnimationConfig.Fade_MainMenuAppearDelay);
                    return true;
                }).SelectMany(_ => UIAnimationsRx.Fade(ProjectListView, 0, 1, AnimationConfig.Fade_Duration))
            ).DefaultIfEmpty();

            // Cleanup after parallel animations
            GlobalTitleBar.IsHitTestVisible = true;
            BookCover.Visibility = Visibility.Collapsed;
            ModdingMenuContent.Visibility = Visibility.Collapsed;
            ModdingMenuButtons.Visibility = Visibility.Collapsed;

            System.Diagnostics.Debug.WriteLine("[ModWindow] TransitionBackToProjectList 완료 (한글)");
        }


        // 애니메이션 상태 저장용
        private System.Windows.Controls.Button? _activeTransitionButton;
        private Thickness _activeTransitionStartMargin;
        private double _activeTransitionWidth;
        private double _activeTransitionHeight;
        private bool _isSelectionFinished;

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
        private double _sidebarStartX = 240.0;           // 프로젝트 메뉴: 사이드바 너비 (보간 시작점)
        private double _sidebarTargetX = 105.0;          // 모딩/도구 메뉴: 사이드바 너비 (보간 끝점)

        // 배경 외관 동적 계산
        private double _riserMaxHeight = 80.0;           // 도구창 최대 상승 높이 (현재 미사용)
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

        private static string NormalizeHeaderText(string? text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }

        private async System.Threading.Tasks.Task PlaySelectionAnimation()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] PlaySelectionAnimation 시작 - Rx 기반 전환됨 (한글)");

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
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_PopDuration);

                // [UPDATE] 유저 요청: 배경 확장 애니메이션 시작 시간을 0.4초±0.05초(350~450ms)로 조정
                // 메달 팝업이 300ms이므로, 추가 대기 시간 AnimationConfig 사용
                // 현재 시점: 300ms(팝업) + 100ms → 목표: 400ms 전후
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Transition_MedalPopDelay);

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
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_FlyDuration + AnimationConfig.Medal_FlyExtraDelay);

                // --- STEP 3: TRANSITION TO TOOL ---

                // Update Header Text (Rx 기반, ViewModel 사용)
                if (_activeTransitionButton != null && _activeTransitionButton.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
                {
                    string cleanTitle = vm.Title.Replace("\r", "").Replace("\n", " ");
                    await UIAnimationsRx.Fade(TxtMainHeader, 1, 0, AnimationConfig.Header_FadeOutDuration);
                    ViewModel.HeaderText = cleanTitle;
                    TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);

                    // [NEW] 도구 메뉴 진입 시 헤더를 20px 아래로 이동
                    TxtMainHeader.Margin = new Thickness(10, 20, 0, 30);

                    await UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Fade_Duration);
                }

                // [NEW] 유저 요청: 헤더 표시 후 0.1초(100ms) 대기
                System.Diagnostics.Debug.WriteLine("[ModWindow] 헤더 표시 완료, 0.1초 대기 후 배경 확장 시작 (한글)");
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Tool_HeaderBeforeBackgroundDelay);

                // [UPDATE] 2단계 확장 시스템: 도구 진입 시 0.5 → 1.0 (위쪽 추가 확장)
                // 이미 모딩 메뉴에서 0.5까지 확장되어 있으므로, 여기서는 0.5 → 1.0만 애니메이션
                System.Diagnostics.Debug.WriteLine("[ModWindow] 도구 진입 2단계 확장 시작 (0.5→1.0, 위쪽 추가) (한글)");
                AnimateSteppedLayoutTo(1.0);
                AnimateToolCompactLayout(true);

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
                }

                // 배경 확장 완료 대기
                System.Diagnostics.Debug.WriteLine("[ModWindow] 배경 확장 완료 대기 (한글)");
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Transition_LayoutDuration);

                // 확장 완료 직후 즉시 페이드인만 재생 (초기화는 페이드인 완료 후 실행해 애니메이션 취소 방지)
                System.Diagnostics.Debug.WriteLine("[ModWindow] 사이드바 & 콘텐츠 페이드인 시작 (한글)");

                var fadeInTasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
                if (ToolSidebarButtons != null)
                    fadeInTasks.Add(WaitObservable(UIAnimationsRx.Fade(ToolSidebarButtons, 0, 1, AnimationConfig.Fade_Duration)));

                if (shouldShowCharacterInfo && CharacterInfoContent != null && CharacterInfoContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(UIAnimationsRx.Fade(CharacterInfoContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration)));

                if (shouldShowCharacterScale && CharacterScaleContent != null && CharacterScaleContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(UIAnimationsRx.Fade(CharacterScaleContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration)));

                if (shouldShowYokaiStats && YokaiStatsContent != null && YokaiStatsContent.Visibility == Visibility.Visible)
                    fadeInTasks.Add(WaitObservable(UIAnimationsRx.Fade(YokaiStatsContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration)));

                if (fadeInTasks.Count > 0)
                    await System.Threading.Tasks.Task.WhenAll(fadeInTasks);

                // 페이드인 완료 후 초기화 실행 (콘텐츠 채우기)
                if (shouldShowCharacterInfo)
                    _ = InitializeCharacterInfoContentAsync();
                else if (shouldShowCharacterScale)
                    _ = InitializeCharacterScaleContentAsync();
                else if (shouldShowYokaiStats)
                    _ = InitializeYokaiStatsContentAsync();

                // [REMOVED] Transition_ToolRevealDelay는 이제 배경 확장 완료 후이므로 불필요
                // await System.Threading.Tasks.Task.Delay(AnimationConfig.Transition_ToolRevealDelay);

                // --- STEP 4: OPEN TOOL WINDOW ---
                if (_activeTransitionButton?.DataContext is ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vmTool)
                {
                    // Integrated Tools are handled above (Fade-In + Init)
                    // Legacy Tools are handled below (OpenToolWindow)
                    if (vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.CharacterInfo &&
                        vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.CharacterScale &&
                        vmTool.MToolType != ICN_T2.UI.WPF.ViewModels.ToolType.YokaiStats)
                    {
                        if (HasConnectedTool(vmTool))
                            OpenToolWindow(vmTool);
                        else
                            SetToolEmptyToolbar(true);
                    }
                }

                await System.Threading.Tasks.Task.Delay(AnimationConfig.Transition_ToolFinalDelay);

                // Restore ZIndexes and Visibility
                System.Windows.Controls.Panel.SetZIndex(TxtMainHeader, 0);
                TransitionProxy.Visibility = Visibility.Collapsed;

                if (!hasTool)
                    SetToolEmptyToolbar(true);

                System.Diagnostics.Debug.WriteLine("[ModWindow] PlaySelectionAnimation 완료 (한글)");
            }
            catch (Exception ex)
            {
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

            // [FIX] 책이 독립 애니메이션 중이면 자동 업데이트 스킵
            if (BookCover.GetAnimationBaseValue(FrameworkElement.MarginProperty) != DependencyProperty.UnsetValue)
                return;

            double progress = StepProgress;
            double sidebarProgress = Math.Min(progress * 2.0, 1.0);
            double targetSidebarX = AnimationConfig.Sidebar_ModdingMenu_Width + AnimationConfig.Background_SidebarGap;

            // 확장된 영역 크기 (배경이 왼쪽으로 이동한 양)
            double expandedWidth = (_sidebarStartX - targetSidebarX) * sidebarProgress;

            // 책 왼쪽 = 원래 위치 - 배경 확장에 비례한 이동량
            // Phase 2에서 패널 마진 변화 + 배경 확장 + 책 이동이 모두 동시 시작되므로 자연스러움
            double bookLeft = AnimationConfig.Book_BaseMarginLeft - expandedWidth;

            BookCover.Margin = new Thickness(bookLeft, AnimationConfig.Book_BaseMarginTop, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);
            ModdingMenuContent.Margin = new Thickness(bookLeft + AnimationConfig.Book_Open2OffsetX, AnimationConfig.Book_BaseMarginTop + AnimationConfig.Book_Open2OffsetY, AnimationConfig.Book_BaseMarginRight, AnimationConfig.Book_BaseMarginBottom);
        }

        /// <summary>
        /// 표시 준비만 수행 (초기화/페이드인 제외)
        /// </summary>
        private void PrepareCharacterInfoContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterInfoContentForReveal 시작 (한글)");

            HideAllToolContents();

            // 초기 상태 설정
            UIAnimationsRx.ClearAnimation(CharacterInfoContent, UIElement.OpacityProperty);
            CharacterInfoContent.Opacity = 0;
            CharacterInfoContent.Visibility = Visibility.Visible;

            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterInfoContentForReveal 완료 (한글)");
        }

        /// <summary>
        /// 데이터 초기화만 수행 (배경 확장 완료 후 실행)
        /// </summary>
        private async System.Threading.Tasks.Task InitializeCharacterInfoContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterInfoContent 시작 (한글)");

            // 초기화 실행 (백그라운드)
            if (CharacterInfoContent is ICN_T2.UI.WPF.Views.CharacterInfoV3 view && CurrentGame != null)
            {
                await Dispatcher.InvokeAsync(() => view.Initialize(CurrentGame), DispatcherPriority.Background);
            }

            // 렌더링 안정화
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

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

            System.Diagnostics.Debug.WriteLine($"[ModWindow] CharacterInfoContent 페이드인 시작: Opacity={CharacterInfoContent.Opacity} (한글)");

            // 페이드인 애니메이션
            await UIAnimationsRx.Fade(CharacterInfoContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration);

            ViewModel.HeaderText = "캐릭터 기본정보";
            TxtMainHeader.Text = NormalizeHeaderText(ViewModel.HeaderText);

            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterInfoContent 완료 - 페이드인 적용 (300ms) (한글)");
        }

        // === Character Scale Content Methods ===

        private void PrepareCharacterScaleContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterScaleContentForReveal 시작 (한글)");
            UIAnimationsRx.ClearAnimation(CharacterScaleContent, UIElement.OpacityProperty);
            CharacterScaleContent.Opacity = 0;
            CharacterScaleContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareCharacterScaleContentForReveal 완료 (한글)");
        }

        private async System.Threading.Tasks.Task InitializeCharacterScaleContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterScaleContent 시작 (한글)");

            if (CharacterScaleContent is ICN_T2.UI.WPF.Views.CharacterScaleView view && CurrentGame != null)
            {
                if (view.DataContext is not ICN_T2.UI.WPF.ViewModels.CharacterScaleViewModel)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleViewModel 생성 및 할당 (한글)");
                    view.Initialize(CurrentGame);
                    view.DataContext = new ICN_T2.UI.WPF.ViewModels.CharacterScaleViewModel(CurrentGame);
                }
            }

            await System.Threading.Tasks.Task.CompletedTask;
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeCharacterScaleContent 완료 (한글)");
        }

        private async System.Threading.Tasks.Task ShowCharacterScaleContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterScaleContent 시작 (한글)");

            PrepareCharacterScaleContentForReveal();
            await InitializeCharacterScaleContentAsync();

            await UIAnimationsRx.Fade(CharacterScaleContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration);

            CharacterScaleContent.Opacity = 1;
            CharacterScaleContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterScaleContent 완료 (한글)");
        }

        // === Yokai Stats Content Methods ===

        private void PrepareYokaiStatsContentForReveal()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareYokaiStatsContentForReveal 시작 (한글)");
            UIAnimationsRx.ClearAnimation(YokaiStatsContent, UIElement.OpacityProperty);
            YokaiStatsContent.Opacity = 0;
            YokaiStatsContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] PrepareYokaiStatsContentForReveal 완료 (한글)");
        }

        private async System.Threading.Tasks.Task InitializeYokaiStatsContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeYokaiStatsContent 시작 (한글)");

            if (YokaiStatsContent is ICN_T2.UI.WPF.Views.YokaiStatsView view && CurrentGame != null)
            {
                if (view.DataContext is not ICN_T2.UI.WPF.ViewModels.YokaiStatsViewModel)
                {
                    System.Diagnostics.Debug.WriteLine("[ModWindow] YokaiStatsViewModel 생성 및 할당 (한글)");
                    view.Initialize(CurrentGame);
                    view.DataContext = new ICN_T2.UI.WPF.ViewModels.YokaiStatsViewModel(CurrentGame);
                }
            }

            await System.Threading.Tasks.Task.CompletedTask;
            System.Diagnostics.Debug.WriteLine("[ModWindow] InitializeYokaiStatsContent 완료 (한글)");
        }

        private async System.Threading.Tasks.Task ShowYokaiStatsContentAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowYokaiStatsContent 시작 (한글)");

            PrepareYokaiStatsContentForReveal();
            await InitializeYokaiStatsContentAsync();

            await UIAnimationsRx.Fade(YokaiStatsContent, 0, 1, AnimationConfig.Tool_ContentFadeDuration);

            YokaiStatsContent.Opacity = 1;
            YokaiStatsContent.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[ModWindow] ShowYokaiStatsContent 완료 (한글)");
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
            System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateToolCompactLayout 시작: enable={enable} (한글)");
            #region agent log
            try
            {
                var log = new
                {
                    runId = "run1",
                    hypothesisId = "H4",
                    location = "ModernModWindow.xaml.cs:AnimateToolCompactLayout:entry",
                    message = "AnimateToolCompactLayout entry",
                    data = new
                    {
                        enable,
                        compactMargin = AnimationConfig.MainPanel_ToolMenu_CompactMargin,
                        rootGridCompact = AnimationConfig.MainContentRootGrid_ToolMenu_CompactMargin
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                System.IO.File.AppendAllText(@"c:\Users\home\Desktop\ICN_T2\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(log) + Environment.NewLine);
            }
            catch
            {
            }
            #endregion

            if (MainContentPanel == null || MainContentRootGrid == null)
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] AnimateToolCompactLayout 실패: 필수 요소가 null (한글)");
                return;
            }

            try
            {
                // 기존 애니메이션 클리어 (경쟁 상태 방지)
                MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
                MainContentRootGrid.BeginAnimation(FrameworkElement.MarginProperty, null);

                // 현재 마진 값 가져오기
                var currentPanelMargin = MainContentPanel.Margin;
                var currentGridMargin = MainContentRootGrid.Margin;

                // 목표 마진 값 결정
                Thickness targetPanelMargin;
                Thickness targetGridMargin;

                if (enable)
                {
                    // 도구 진입: MainContentPanel 전체를 왼쪽 마진과 동일하게
                    double m = AnimationConfig.MainPanel_ToolMenu_CompactMargin;
                    targetPanelMargin = new Thickness(m, m, m, m);

                    // MainContentRootGrid도 compact하게
                    targetGridMargin = new Thickness(AnimationConfig.MainContentRootGrid_ToolMenu_CompactMargin);

                    System.Diagnostics.Debug.WriteLine($"[ModWindow] ToolCompact 활성화: Panel 전체={m}px, Grid 전체={AnimationConfig.MainContentRootGrid_ToolMenu_CompactMargin}px (한글)");
                }
                else
                {
                    // 모딩 메뉴 복귀: Panel은 왼쪽만 축소, 나머지는 원래대로 복원
                    targetPanelMargin = new Thickness(
                        AnimationConfig.MainPanel_ModdingMenu_MarginLeft,
                        AnimationConfig.MainPanel_ModdingMenu_MarginTop,
                        AnimationConfig.MainPanel_ModdingMenu_MarginRight,
                        AnimationConfig.MainPanel_ModdingMenu_MarginBottom);

                    // Grid는 기본 마진으로 복원
                    targetGridMargin = new Thickness(AnimationConfig.MainContentRootGrid_Margin);

                    System.Diagnostics.Debug.WriteLine($"[ModWindow] ToolCompact 비활성화: Panel=({AnimationConfig.MainPanel_ModdingMenu_MarginLeft},{AnimationConfig.MainPanel_ModdingMenu_MarginTop},{AnimationConfig.MainPanel_ModdingMenu_MarginRight},{AnimationConfig.MainPanel_ModdingMenu_MarginBottom}), Grid 전체={AnimationConfig.MainContentRootGrid_Margin}px (한글)");
                }

                // ThicknessAnimation 생성
                var duration = TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var panelAnim = new ThicknessAnimation(currentPanelMargin, targetPanelMargin, duration)
                {
                    EasingFunction = easing
                };
                var gridAnim = new ThicknessAnimation(currentGridMargin, targetGridMargin, duration)
                {
                    EasingFunction = easing
                };

                // 애니메이션 시작
                MainContentPanel.BeginAnimation(FrameworkElement.MarginProperty, panelAnim);
                MainContentRootGrid.BeginAnimation(FrameworkElement.MarginProperty, gridAnim);

                System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateToolCompactLayout 완료: enable={enable} (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] AnimateToolCompactLayout 오류: {ex.Message} (한글)");
            }
        }


        private void UpdateSteppedPath()
        {
            if (SteppedBackgroundBorder == null || MainContentPanel == null || TxtMainHeader == null)
            {
                System.Diagnostics.Debug.WriteLine("[ModWindow] UpdateSteppedPath 스킵: 필수 요소가 null (한글)");
                return;
            }
            #region agent log
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
            #endregion

            // [FIX] 실제 그려지는 컨테이너(SteppedBackgroundBorder)의 크기를 기준으로 지오메트리 계산
            // 이전: MainContentPanel.ActualWidth/Height 사용 → 코너 아크가 컨테이너 밖으로 나가 클리핑됨
            // 수정: SteppedBackgroundBorder의 실제 렌더 영역 크기 사용
            double width = SteppedBackgroundBorder.ActualWidth;
            double height = SteppedBackgroundBorder.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] UpdateSteppedPath 스킵: width={width}, height={height} (한글)");
                return;
            }

            double progress = StepProgress;
            System.Diagnostics.Debug.WriteLine($"[ModWindow] UpdateSteppedPath 실행: progress={progress:F2}, width={width:F0}, height={height:F0} (한글)");

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
            double normalTopY = headerHeight + AnimationConfig.Header_ContentSpacing;

            TxtMainHeader.UpdateLayout();

            double stepX = AnimationConfig.Background_StepXPosition;

            // [FIX] 위쪽 상승: 0.5 이하에서는 상승 없음, 0.5~1.0에서만 상승
            // 모딩 메뉴(0.5)에서는 평평, 도구 메뉴(1.0)에서만 계단식 확장
            double riseProgress = Math.Max(0.0, (progress - 0.5) * 2.0); // 0.5→0.0, 1.0→1.0
            double stepTopY = normalTopY - (AnimationConfig.Background_TopRiseHeight * riseProgress) - constantRiser;

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
        }

        private void ModernModWindow_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UpdateSteppedPath();

            if (CharacterInfoContent.Visibility == Visibility.Visible &&
                _navStack.Peek().State == NavState.ToolWindow)
            {
                // ShowCharacterInfoContent()의 위치 재계산 로직 재사용
                AdjustCharacterInfoPosition();
            }
        }

        private void AdjustCharacterInfoPosition()
        {
            var headerTransform = TxtMainHeader.TransformToVisual(this);
            var headerBottom = headerTransform.Transform(new System.Windows.Point(0, 0)).Y + TxtMainHeader.ActualHeight;

            // [NEW] ToolCompact 모드일 때 헤더/콘텐츠 간격 축소
            bool isToolCompact = _navStack.Count > 0 && _navStack.Peek().State == NavState.ToolWindow;
            double headerSpacing = isToolCompact ? AnimationConfig.Tool_HeaderContentSpacing : AnimationConfig.CharacterInfo_HeaderSpacingNormal;
            double contentTop = headerBottom + headerSpacing;

            // Character Info
            if (CharacterInfoContent.Parent is Canvas)
                Canvas.SetTop(CharacterInfoContent, contentTop);
            else
                CharacterInfoContent.Margin = new Thickness(0, contentTop, 0, AnimationConfig.CharacterInfo_MarginBottom);

            CharacterInfoContent.Width = MainContentPanel.ActualWidth;
            CharacterInfoContent.Height = this.ActualHeight - contentTop - AnimationConfig.CharacterInfo_MarginBottom;

            // Character Scale
            if (CharacterScaleContent.Parent is Canvas)
                Canvas.SetTop(CharacterScaleContent, contentTop);
            else
                CharacterScaleContent.Margin = new Thickness(0, contentTop, 0, AnimationConfig.CharacterInfo_MarginBottom);

            CharacterScaleContent.Width = MainContentPanel.ActualWidth;
            CharacterScaleContent.Height = this.ActualHeight - contentTop - AnimationConfig.CharacterInfo_MarginBottom;

            // Yokai Stats
            if (YokaiStatsContent.Parent is Canvas)
                Canvas.SetTop(YokaiStatsContent, contentTop);
            else
                YokaiStatsContent.Margin = new Thickness(0, contentTop, 0, AnimationConfig.CharacterInfo_MarginBottom);

            YokaiStatsContent.Width = MainContentPanel.ActualWidth;
            YokaiStatsContent.Height = this.ActualHeight - contentTop - AnimationConfig.CharacterInfo_MarginBottom;
        }


        private void HideAllToolContents()
        {
            CharacterInfoContent.Visibility = Visibility.Collapsed;
            CharacterInfoContent.Opacity = 0;
            
            CharacterScaleContent.Visibility = Visibility.Collapsed;
            CharacterScaleContent.Opacity = 0;

            YokaiStatsContent.Visibility = Visibility.Collapsed;
            YokaiStatsContent.Opacity = 0;
            
            // Hide other future tools here
        }

        private static bool HasConnectedTool(ICN_T2.UI.WPF.ViewModels.ModdingToolViewModel vm)
        {
            // Connected tools:
            // Index 1: Character Info
            // Index 2: Character Scale
            return vm.IconIndex == 1 || vm.IconIndex == 2;
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

                    ProxyIconContainer.Width = _activeTransitionButton.ActualWidth;
                    ProxyIconContainer.Height = _activeTransitionButton.ActualHeight;
                }

                TransitionProxy.Visibility = Visibility.Visible;
                TransitionProxy.Opacity = 1;
                System.Windows.Controls.Panel.SetZIndex(TransitionProxy, AnimationConfig.ZIndex_MedalProxyBelowHeader);

                // --- SETUP BOOK (Closed State initially) ---
                // [NEW] ToolCompact Layout 비활성화: 모딩 메뉴로 복귀하므로 일반 레이아웃으로 복원
                AnimateToolCompactLayout(false);

                ModMenuTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuTranslate.X = 0;
                ModMenuSlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ModMenuSlideTranslate.X = -AnimationConfig.Book_SlideOffset; // 속지를 왼쪽으로 시작 (닫힌 상태)

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
                TxtMainHeader.Margin = new Thickness(10, 0, 0, 30);

                // --- STEP 1: BOOK FADE IN (Fast) - Rx 기반 ---
                await Observable.Merge(
                    UIAnimationsRx.Fade(BookCover, 0, 1, AnimationConfig.Header_FadeOutDuration),
                    UIAnimationsRx.Fade(TxtMainHeader, 0, 1, AnimationConfig.Fade_Duration)
                ).DefaultIfEmpty();

                // --- STEP 2: BOOK OPEN + FLY BACK + CONTENT SLIDE ---
                // 책 열기와 속지 슬라이드를 동시에 시작
                // ModMenuSlideTranslate를 직접 애니메이션
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
                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_FlyDuration);

                // --- STEP 3: LAND ---
                var landDuration = TimeSpan.FromMilliseconds(AnimationConfig.Medal_LandDuration);
                var landEase = new CubicEase { EasingMode = EasingMode.EaseIn };

                var animLandY = new DoubleAnimation(AnimationConfig.Medal_PopYOffset, 0, landDuration) { EasingFunction = landEase };
                var animScaleDownX = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, landDuration) { EasingFunction = landEase };
                var animScaleDownY = new DoubleAnimation(AnimationConfig.Medal_PopScale, 1.0, landDuration) { EasingFunction = landEase };

                transT?.BeginAnimation(TranslateTransform.YProperty, animLandY);
                scaleT?.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleDownX);
                scaleT?.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleDownY);

                await System.Threading.Tasks.Task.Delay(AnimationConfig.Medal_LandDuration);

                // Cleanup
                TransitionProxy.Visibility = Visibility.Collapsed;
                _activeTransitionButton.Visibility = Visibility.Visible;
                _isSelectionFinished = false;

                System.Diagnostics.Debug.WriteLine("[ModWindow] RecoverFromSelection 완료 (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModWindow] RecoverFromSelection 오류: {ex.Message}");
            }
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

            // 기존 로컬 컬렉션을 ViewModel 컬렉션으로 교체
            ModdingTools = ViewModel.ModdingTools;

            InitializeProjectMenu();
            InitializeModdingMenu();
            // InitializeModdingTools는 ViewModel에서 처리하므로 제거

            _navStack.Push(new NavItem { State = NavState.ProjectList });

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
                        mainMargin = AnimationConfig.MainPanel_ProjectMenu_MarginAll,
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
            MainContentPanel.Margin = new Thickness(AnimationConfig.MainPanel_ProjectMenu_MarginAll);
            MainContentPanel.CornerRadius = new CornerRadius(AnimationConfig.MainPanel_CornerRadius);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] MainContentPanel 적용: Margin={AnimationConfig.MainPanel_ProjectMenu_MarginAll}, CornerRadius={AnimationConfig.MainPanel_CornerRadius} (한글)");

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
            ProjectListView.Margin = new Thickness(AnimationConfig.ProjectListView_Margin);
            System.Diagnostics.Debug.WriteLine($"[ModWindow] ProjectListView 적용: Margin={AnimationConfig.ProjectListView_Margin} (한글)");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // StepProgress 초기화
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            StepProgress = 0;
            UpdateSteppedPath();

            System.Diagnostics.Debug.WriteLine("[ModWindow] OnWindowLoaded 완료 (한글)");
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsLoaded) return; // why: Loaded 이전 SizeChanged 방지
            UpdateSteppedPath();
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
                    case 10: // Full Save
                        System.Windows.MessageBox.Show("전체 저장 기능 (구현 예정)");
                        break;
                    case 11: // Settings
                        System.Windows.MessageBox.Show("설정 창 오픈");
                        break;
                    default:
                        System.Windows.MessageBox.Show($"{ModdingTools[index].EngTitle} - 준비 중입니다.");
                        break;
                }
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
                    // 바닐라 선택 시 Samples 폴더 사용
                    finalGamePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");
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

            System.Diagnostics.Debug.WriteLine("[ModWindow] TitleOverlay_Click 완료 (한글)");
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
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
