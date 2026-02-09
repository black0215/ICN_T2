using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;
using Point = System.Windows.Point;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ICN_T2.UI.WPF.Animations
{
    /// <summary>
    /// Rx 기반 WPF 애니메이션 헬퍼 클래스
    /// - 애니메이션 경쟁 상태(race condition) 방지
    /// - 스트림 기반으로 애니메이션 조합 가능
    /// - 로깅 및 에러 핸들링 내장
    /// - 모든 Observable은 UI Dispatcher에서 Subscribe됨 (크로스 스레드 안전)
    /// </summary>
    public static class UIAnimationsRx
    {
        // Attached Property: 애니메이션 토큰 (경쟁 상태 방지용)
        private static readonly DependencyProperty AnimationTokenProperty =
            DependencyProperty.RegisterAttached("AnimationToken", typeof(int), typeof(UIAnimationsRx), new PropertyMetadata(0));

        private static int GetAnimationToken(UIElement element) => (int)element.GetValue(AnimationTokenProperty);
        private static void SetAnimationToken(UIElement element, int value) => element.SetValue(AnimationTokenProperty, value);

        /// <summary>
        /// UI Dispatcher 스케줄러를 가져옵니다.
        /// Observable.Create 내부의 WPF 접근이 항상 UI 스레드에서 실행되도록 보장합니다.
        /// </summary>
        private static DispatcherScheduler DispatcherScheduler =>
            new DispatcherScheduler(System.Windows.Application.Current.Dispatcher);

        // ----------------------------------------------------------------------------------
        // [RX CORE: FADE]
        // Opacity 애니메이션을 Observable로 반환. 경쟁 상태에 안전함.
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> Fade(UIElement element, double from, double to, double durationMs = 300)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    var elName = (element as FrameworkElement)?.Name ?? element.GetType().Name;
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] Fade 시작 ({elName}): {from} → {to}, {durationMs}ms");

                    // 1. 토큰 증가 (이 Fade가 최신임을 표시)
                    int currentToken = GetAnimationToken(element) + 1;
                    SetAnimationToken(element, currentToken);

                    // 2. 기존 애니메이션 클리어
                    element.BeginAnimation(UIElement.OpacityProperty, null);

                    // 3. Visibility 사전 처리
                    if (to > 0 && element.Visibility != Visibility.Visible)
                        element.Visibility = Visibility.Visible;

                    // 4. 애니메이션 생성 및 시작
                    var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs));
                    anim.Completed += (s, e) =>
                    {
                        try
                        {
                            // 5. 토큰 검증: 최신 애니메이션인지 확인
                            if (GetAnimationToken(element) == currentToken)
                            {
                                if (to == 0)
                                    element.Visibility = Visibility.Collapsed;

                                System.Diagnostics.Debug.WriteLine($"[AnimRx] Fade 완료 ({elName}), Token={currentToken}");
                                observer.OnNext(Unit.Default);
                                observer.OnCompleted();
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[AnimRx] Fade 무시됨 ({elName}), Token 불일치: 현재={GetAnimationToken(element)}, 예상={currentToken}");
                                observer.OnCompleted(); // 에러 아님, 단순히 중단됨
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AnimRx] Fade 완료 핸들러 오류: {ex.Message}");
                            observer.OnError(ex);
                        }
                    };

                    element.BeginAnimation(UIElement.OpacityProperty, anim);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] Fade 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: POP]
        // 요소를 팝업 효과로 강조 (Scale 1.0 → 1.2 → 1.0)
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> PopElement(FrameworkElement element, double durationMs = 200)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] PopElement 시작: {element.Name}");

                    // Transform 준비
                    EnsureMutableTransformGroup(element);

                    if (!TryGetScaleTransform(element, out var scaleTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] PopElement 실패: ScaleTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // Storyboard 생성
                    var sb = new Storyboard();
                    var anim = new DoubleAnimationUsingKeyFrames();
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.5))));
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs))));

                    Storyboard.SetTarget(anim, scaleTrans);
                    Storyboard.SetTargetProperty(anim, new PropertyPath(ScaleTransform.ScaleXProperty));
                    sb.Children.Add(anim);

                    var animY = anim.Clone();
                    Storyboard.SetTarget(animY, scaleTrans);
                    Storyboard.SetTargetProperty(animY, new PropertyPath(ScaleTransform.ScaleYProperty));
                    sb.Children.Add(animY);

                    sb.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] PopElement 완료: {element.Name}");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    sb.Begin();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] PopElement 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: FLY]
        // 요소를 목표 지점으로 이동
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> FlyToPoint(FrameworkElement element, Point targetPos, double durationMs = 400)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] FlyToPoint 시작: {element.Name} → {targetPos}");

                    EnsureMutableTransformGroup(element);

                    if (!TryGetTranslateTransform(element, out var translateTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] FlyToPoint 실패: TranslateTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    var sb = new Storyboard();
                    var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

                    var moveX = new DoubleAnimation(targetPos.X, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease };
                    var moveY = new DoubleAnimation(targetPos.Y, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease };

                    Storyboard.SetTarget(moveX, translateTrans);
                    Storyboard.SetTargetProperty(moveX, new PropertyPath(TranslateTransform.XProperty));
                    sb.Children.Add(moveX);

                    Storyboard.SetTarget(moveY, translateTrans);
                    Storyboard.SetTargetProperty(moveY, new PropertyPath(TranslateTransform.YProperty));
                    sb.Children.Add(moveY);

                    sb.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] FlyToPoint 완료: {element.Name}");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    sb.Begin();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] FlyToPoint 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: SLIDE X]
        // 요소를 X축으로 슬라이드 이동
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> SlideX(FrameworkElement element, double from, double to, double durationMs = 400)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SlideX 시작: {element.Name} {from} → {to}");

                    if (!TryGetTranslateTransform(element, out var translateTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SlideX 실패: TranslateTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    translateTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    translateTrans.X = from;

                    var duration = TimeSpan.FromMilliseconds(durationMs);
                    var ease = new SineEase { EasingMode = EasingMode.EaseIn };
                    var anim = new DoubleAnimation(from, to, duration) { EasingFunction = ease };

                    anim.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SlideX 완료: {element.Name}");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    translateTrans.BeginAnimation(TranslateTransform.XProperty, anim);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SlideX 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: BOOK ANIMATION]
        // 책 열기/닫기 애니메이션
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> AnimateBook(FrameworkElement cover, bool isOpen, double durationMs = 400)
        {
            string action = isOpen ? "Open" : "Close";

            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] Book{action} 시작");

                    cover.Visibility = Visibility.Visible;
                    System.Windows.Controls.Panel.SetZIndex(cover, 100);
                    cover.RenderTransformOrigin = new Point(0.0, 0.5);

                    var transGroup = cover.RenderTransform as TransformGroup;
                    var scale = transGroup?.Children[0] as ScaleTransform;
                    var skew = transGroup?.Children[1] as SkewTransform;

                    if (scale == null || skew == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] Book{action} 실패: Transform 누락");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // 애니메이션 클리어
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    skew.BeginAnimation(SkewTransform.AngleYProperty, null);

                    double fromScaleX = isOpen ? 1.0 : 0.0;
                    double toScaleX = isOpen ? 0.0 : 1.0;
                    double fromSkewY = isOpen ? 0.0 : -20.0;
                    double toSkewY = isOpen ? -20.0 : 0.0;

                    scale.ScaleX = fromScaleX;
                    skew.AngleY = fromSkewY;

                    var duration = TimeSpan.FromMilliseconds(durationMs);
                    var ease = new SineEase { EasingMode = isOpen ? EasingMode.EaseIn : EasingMode.EaseOut };

                    var animScaleX = new DoubleAnimation(fromScaleX, toScaleX, duration) { EasingFunction = ease };
                    var animSkewY = new DoubleAnimation(fromSkewY, toSkewY, duration) { EasingFunction = ease };

                    animScaleX.Completed += (s, e) =>
                    {
                        if (isOpen)
                        {
                            cover.Visibility = Visibility.Collapsed;
                            System.Windows.Controls.Panel.SetZIndex(cover, 0);
                        }
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] Book{action} 완료");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
                    skew.BeginAnimation(SkewTransform.AngleYProperty, animSkewY);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] Book{action} 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: LAYOUT ANIMATION]
        // 레이아웃 전환 애니메이션 (사이드바 + 콘텐츠 영역)
        // [UPDATE] 유저 요청: 모딩 메뉴에서는 왼쪽 마진만 조정 (사이드바 축소에 맞춤)
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> AnimateLayout(
            FrameworkElement sidebar, 
            FrameworkElement content, 
            bool isToModding, 
            double durationMs = 600,
            double sidebarWidthModding = 80,      // 모딩 메뉴 사이드바 너비
            double sidebarWidthProject = 220,     // 프로젝트 메뉴 사이드바 너비
            double marginLeftModding = 20,        // 모딩 메뉴 왼쪽 마진
            double marginAll = 50)                // 프로젝트 메뉴 전체 마진
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] AnimateLayout 시작: isToModding={isToModding}");

                    var duration = TimeSpan.FromMilliseconds(durationMs);
                    var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                    // [FIX] 기존 애니메이션 레이어 제거 → 유효값 캡처 → From 명시
                    // 이전 애니메이션이 HoldEnd로 남아있으면 From이 잘못 잡혀 뚝 줄어드는 현상 발생
                    content.BeginAnimation(FrameworkElement.MarginProperty, null);
                    sidebar.BeginAnimation(FrameworkElement.WidthProperty, null);

                    var currentMargin = content.Margin;
                    double currentSideWidth = sidebar.ActualWidth > 0 ? sidebar.ActualWidth : (isToModding ? sidebarWidthProject : sidebarWidthModding);

                    double sideEnd = isToModding ? sidebarWidthModding : sidebarWidthProject;
                    var sideAnim = new DoubleAnimation(currentSideWidth, sideEnd, duration) { EasingFunction = easing };

                    Thickness contentEnd;
                    if (isToModding)
                    {
                        // 모딩 메뉴: 왼쪽 마진만 축소
                        contentEnd = new Thickness(marginLeftModding, currentMargin.Top, currentMargin.Right, currentMargin.Bottom);
                    }
                    else
                    {
                        // 메인 메뉴 복귀: 전체 마진 복구
                        contentEnd = new Thickness(marginAll);
                    }

                    var contentAnim = new ThicknessAnimation(currentMargin, contentEnd, duration) { EasingFunction = easing };

                    int completedCount = 0;
                    void OnAnimCompleted(object? s, EventArgs e)
                    {
                        completedCount++;
                        if (completedCount == 2)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AnimRx] AnimateLayout 완료 (isToModding={isToModding})");
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        }
                    }

                    sideAnim.Completed += OnAnimCompleted;
                    contentAnim.Completed += OnAnimCompleted;

                    sidebar.BeginAnimation(FrameworkElement.WidthProperty, sideAnim);
                    content.BeginAnimation(FrameworkElement.MarginProperty, contentAnim);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] AnimateLayout 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [HELPER FUNCTIONS]
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// RenderTransform이 변경 가능한 TransformGroup인지 확인하고, 필요시 생성
        /// </summary>
        private static void EnsureMutableTransformGroup(FrameworkElement element)
        {
            if (element.RenderTransform == null ||
                element.RenderTransform == Transform.Identity ||
                element.RenderTransform.IsFrozen)
            {
                var grp = new TransformGroup();
                grp.Children.Add(new ScaleTransform(1, 1));
                grp.Children.Add(new TranslateTransform(0, 0));
                element.RenderTransform = grp;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        /// <summary>
        /// ScaleTransform 가져오기
        /// </summary>
        private static bool TryGetScaleTransform(FrameworkElement element, out ScaleTransform? scaleTrans)
        {
            scaleTrans = null;
            if (element.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is ScaleTransform st)
                    {
                        scaleTrans = st;
                        return true;
                    }
                }
            }
            else if (element.RenderTransform is ScaleTransform st)
            {
                scaleTrans = st;
                return true;
            }
            return false;
        }

        /// <summary>
        /// TranslateTransform 가져오기
        /// </summary>
        private static bool TryGetTranslateTransform(FrameworkElement element, out TranslateTransform? translateTrans)
        {
            translateTrans = null;
            if (element.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is TranslateTransform tt)
                    {
                        translateTrans = tt;
                        return true;
                    }
                }
            }
            else if (element.RenderTransform is TranslateTransform tt)
            {
                translateTrans = tt;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 애니메이션 클리어 (경쟁 상태 방지용)
        /// </summary>
        public static void ClearAnimation(UIElement element, DependencyProperty property)
        {
            element.BeginAnimation(property, null);

            // 토큰도 리셋하여 진행 중인 Fade가 완료될 때 무시되도록 함
            if (property == UIElement.OpacityProperty)
            {
                SetAnimationToken(element, GetAnimationToken(element) + 1);
            }
        }
    }
}
