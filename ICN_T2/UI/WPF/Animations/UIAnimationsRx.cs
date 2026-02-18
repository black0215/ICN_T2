﻿using System;
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
        // 요소를 팝업 효과로 강조 (Scale 1.0 → 1.98 → 1.0)
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
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.5))));
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
                return;
            }

            if (element.RenderTransform is ScaleTransform existingScale)
            {
                var grp = new TransformGroup();
                grp.Children.Add(existingScale);
                grp.Children.Add(new TranslateTransform(0, 0));
                element.RenderTransform = grp;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                return;
            }

            if (element.RenderTransform is TranslateTransform existingTranslate)
            {
                var grp = new TransformGroup();
                grp.Children.Add(new ScaleTransform(1, 1));
                grp.Children.Add(existingTranslate);
                element.RenderTransform = grp;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                return;
            }

            if (element.RenderTransform is TransformGroup existingGroup)
            {
                bool hasScale = false;
                bool hasTranslate = false;
                foreach (var child in existingGroup.Children)
                {
                    if (child is ScaleTransform)
                        hasScale = true;
                    else if (child is TranslateTransform)
                        hasTranslate = true;
                }

                if (!hasScale)
                    existingGroup.Children.Insert(0, new ScaleTransform(1, 1));
                if (!hasTranslate)
                    existingGroup.Children.Add(new TranslateTransform(0, 0));
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

        // ----------------------------------------------------------------------------------
        // [RX CORE: SPRING SCALE]
        // 스프링 탄력 효과가 있는 스케일 애니메이션
        // ElasticEase를 사용하여 bounce 효과 구현
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> SpringScale(
            FrameworkElement element,
            double fromScale = 0.99,
            double targetScale = 1.65,
            double durationMs = 800,
            double bounce = 0.4)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringScale 시작: {element.Name}, {fromScale}→{targetScale}, bounce={bounce}");

                    // Transform 준비
                    EnsureMutableTransformGroup(element);

                    if (!TryGetScaleTransform(element, out var scaleTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringScale 실패: ScaleTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // 초기 스케일 설정
                    scaleTrans.ScaleX = fromScale;
                    scaleTrans.ScaleY = fromScale;

                    // ElasticEase 설정 (bounce 파라미터 활용)
                    var easing = new ElasticEase
                    {
                        EasingMode = EasingMode.EaseOut,
                        Oscillations = 3,
                        Springiness = bounce * 2  // bounce=0.4 → springiness=0.8
                    };

                    // Storyboard 생성
                    var sb = new Storyboard();

                    // ScaleX 애니메이션
                    var scaleAnimX = new DoubleAnimation(fromScale, targetScale, TimeSpan.FromMilliseconds(durationMs))
                    {
                        EasingFunction = easing
                    };
                    Storyboard.SetTarget(scaleAnimX, scaleTrans);
                    Storyboard.SetTargetProperty(scaleAnimX, new PropertyPath(ScaleTransform.ScaleXProperty));
                    sb.Children.Add(scaleAnimX);

                    // ScaleY 애니메이션
                    var scaleAnimY = new DoubleAnimation(fromScale, targetScale, TimeSpan.FromMilliseconds(durationMs))
                    {
                        EasingFunction = easing
                    };
                    Storyboard.SetTarget(scaleAnimY, scaleTrans);
                    Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                    sb.Children.Add(scaleAnimY);

                    sb.Completed += (sender, args) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringScale 완료: {element.Name}");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    sb.Begin();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringScale 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: SPRING FADE AND SCALE]
        // Fade + Scale을 동시에 진행하는 스프링 애니메이션
        // 도구 메뉴 버튼 진입 시 사용 (Stagger와 함께)
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> SpringFadeAndScale(
            FrameworkElement element,
            double fromOpacity = 0,
            double toOpacity = 1,
            double fromScale = 0.99,
            double toScale = 1.65,
            double durationMs = 800,
            double bounce = 0.4)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringFadeAndScale 시작: {element.Name}");

                    // Transform 준비
                    EnsureMutableTransformGroup(element);

                    if (!TryGetScaleTransform(element, out var scaleTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringFadeAndScale 실패: ScaleTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // 초기 상태 설정
                    element.Opacity = fromOpacity;
                    scaleTrans.ScaleX = fromScale;
                    scaleTrans.ScaleY = fromScale;

                    // Visibility 처리
                    if (toOpacity > 0 && element.Visibility != Visibility.Visible)
                        element.Visibility = Visibility.Visible;

                    // Storyboard 생성
                    var sb = new Storyboard();

                    // 1. Opacity 애니메이션 (부드러운 페이드)
                    var opacityAnim = new DoubleAnimation(fromOpacity, toOpacity, TimeSpan.FromMilliseconds(durationMs))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(opacityAnim, element);
                    Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
                    sb.Children.Add(opacityAnim);

                    // 2. Scale 애니메이션 (스프링 효과)
                    var easing = new ElasticEase
                    {
                        EasingMode = EasingMode.EaseOut,
                        Oscillations = 3,
                        Springiness = bounce * 2
                    };

                    var scaleAnimX = new DoubleAnimation(fromScale, toScale, TimeSpan.FromMilliseconds(durationMs))
                    {
                        EasingFunction = easing
                    };
                    Storyboard.SetTarget(scaleAnimX, scaleTrans);
                    Storyboard.SetTargetProperty(scaleAnimX, new PropertyPath(ScaleTransform.ScaleXProperty));
                    sb.Children.Add(scaleAnimX);

                    var scaleAnimY = new DoubleAnimation(fromScale, toScale, TimeSpan.FromMilliseconds(durationMs))
                    {
                        EasingFunction = easing
                    };
                    Storyboard.SetTarget(scaleAnimY, scaleTrans);
                    Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                    sb.Children.Add(scaleAnimY);

                    sb.Completed += (sender, args) =>
                    {
                        // Visibility 정리
                        if (toOpacity == 0)
                            element.Visibility = Visibility.Collapsed;

                        System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringFadeAndScale 완료: {element.Name}");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    };

                    sb.Begin();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] SpringFadeAndScale 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: DROP-IN BOUNCE]
        // 리스트/패널 진입용 경량 바운스 (위→아래 낙하 + 약한 스케일 반동)
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> DropInBounce(
            FrameworkElement element,
            double durationMs = 280,
            double fromOffsetY = -14,
            double fromScale = 0.985,
            double toScale = 1.0,
            double fromOpacity = 0,
            double toOpacity = 1,
            double bounceAmplitude = 0.22)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    EnsureMutableTransformGroup(element);

                    if (!TryGetScaleTransform(element, out var scaleTrans) ||
                        !TryGetTranslateTransform(element, out var translateTrans))
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    ClearAnimation(element, UIElement.OpacityProperty);
                    scaleTrans.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scaleTrans.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    translateTrans.BeginAnimation(TranslateTransform.YProperty, null);

                    element.Visibility = Visibility.Visible;
                    element.Opacity = fromOpacity;
                    scaleTrans.ScaleX = fromScale;
                    scaleTrans.ScaleY = fromScale;
                    translateTrans.Y = fromOffsetY;

                    var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = bounceAmplitude };
                    var opacityEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                    var duration = TimeSpan.FromMilliseconds(durationMs);

                    var animOpacity = new DoubleAnimation(fromOpacity, toOpacity, duration)
                    {
                        EasingFunction = opacityEase,
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    var animScaleX = new DoubleAnimation(fromScale, toScale, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    var animScaleY = new DoubleAnimation(fromScale, toScale, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    var animY = new DoubleAnimation(fromOffsetY, 0, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = FillBehavior.HoldEnd
                    };

                    animY.Completed += (_, __) =>
                    {
                        try
                        {
                            element.Opacity = toOpacity;
                            scaleTrans.ScaleX = toScale;
                            scaleTrans.ScaleY = toScale;
                            translateTrans.Y = 0;

                            if (toOpacity <= 0)
                                element.Visibility = Visibility.Collapsed;

                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    };

                    element.BeginAnimation(UIElement.OpacityProperty, animOpacity);
                    scaleTrans.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
                    scaleTrans.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
                    translateTrans.BeginAnimation(TranslateTransform.YProperty, animY);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: STAGGERED DROP-IN]
        // 여러 요소를 순차(스태거) 등장시키는 경량 바운스
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> StaggeredDropIn(
            System.Collections.Generic.IEnumerable<FrameworkElement> elements,
            double durationMs = 280,
            double staggerDelayMs = 36,
            double fromOffsetY = -14,
            double fromScale = 0.985,
            double toScale = 1.0,
            double fromOpacity = 0,
            double toOpacity = 1,
            double bounceAmplitude = 0.22)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    var tasks = new System.Collections.Generic.List<IObservable<Unit>>();
                    double currentDelay = 0;

                    foreach (var element in elements)
                    {
                        var delay = currentDelay;
                        tasks.Add(
                            Observable.Timer(TimeSpan.FromMilliseconds(delay), DispatcherScheduler)
                                .SelectMany(_ => DropInBounce(
                                    element,
                                    durationMs,
                                    fromOffsetY,
                                    fromScale,
                                    toScale,
                                    fromOpacity,
                                    toOpacity,
                                    bounceAmplitude
                                ))
                        );
                        currentDelay += staggerDelayMs;
                    }

                    if (tasks.Count == 0)
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    Observable.Merge(tasks).Subscribe(
                        _ => { },
                        ex => observer.OnError(ex),
                        () =>
                        {
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        }
                    );
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: LIQUID MORPH]
        // 액체처럼 부드럽게 형태를 잡으며 나타나는 애니메이션
        // 도구 메뉴 버튼 진입 시 사용
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> LiquidMorphIn(FrameworkElement element, double durationMs = 600, double delayMs = 0)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] LiquidMorphIn 시작: {element.Name}, delay={delayMs}ms");

                    // Transform 준비
                    EnsureMutableTransformGroup(element);

                    if (!TryGetScaleTransform(element, out var scaleTrans))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimRx] LiquidMorphIn 실패: ScaleTransform을 찾을 수 없음");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // 초기 상태: 투명 + 작은 크기
                    element.Opacity = 0;
                    scaleTrans.ScaleX = 0.3;
                    scaleTrans.ScaleY = 0.3;

                    // 딜레이 후 애니메이션 시작
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(delayMs)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();

                        // Storyboard로 복합 애니메이션 구성
                        var sb = new Storyboard();

                        // 1. Scale 애니메이션 (탄성 효과)
                        var scaleAnim = new DoubleAnimationUsingKeyFrames();
                        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.6)))
                        {
                            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
                        });
                        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)))
                        {
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        });

                        Storyboard.SetTarget(scaleAnim, scaleTrans);
                        Storyboard.SetTargetProperty(scaleAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
                        sb.Children.Add(scaleAnim);

                        var scaleAnimY = scaleAnim.Clone();
                        Storyboard.SetTarget(scaleAnimY, scaleTrans);
                        Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                        sb.Children.Add(scaleAnimY);

                        // 2. Opacity 애니메이션 (부드러운 페이드인)
                        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs * 0.7))
                        {
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                        };
                        Storyboard.SetTarget(opacityAnim, element);
                        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
                        sb.Children.Add(opacityAnim);

                        sb.Completed += (sender, args) =>
                        {
                            System.Diagnostics.Debug.WriteLine($"[AnimRx] LiquidMorphIn 완료: {element.Name}");
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        };

                        sb.Begin();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] LiquidMorphIn 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }

        // ----------------------------------------------------------------------------------
        // [RX CORE: STAGGERED MORPH]
        // 여러 요소에 시차를 두고 LiquidMorphIn 적용
        // ----------------------------------------------------------------------------------
        public static IObservable<Unit> StaggeredLiquidMorph(System.Collections.Generic.IEnumerable<FrameworkElement> elements, double durationMs = 600, double staggerDelayMs = 80)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    var tasks = new System.Collections.Generic.List<IObservable<Unit>>();
                    double currentDelay = 0;

                    foreach (var element in elements)
                    {
                        var delay = currentDelay;
                        tasks.Add(LiquidMorphIn(element, durationMs, delay));
                        currentDelay += staggerDelayMs;
                    }

                    if (tasks.Count == 0)
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    // 모든 애니메이션을 병렬로 시작 (각자 딜레이 포함)
                    Observable.Merge(tasks).Subscribe(
                        _ => { },
                        ex => observer.OnError(ex),
                        () =>
                        {
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimRx] StaggeredLiquidMorph 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            }).SubscribeOn(DispatcherScheduler);
        }
    }
}
