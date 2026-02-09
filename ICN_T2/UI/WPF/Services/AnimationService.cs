using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Windows;
using Point = System.Windows.Point;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ICN_T2.UI.WPF.Animations;
using ICN_T2.UI.WPF.ViewModels;

namespace ICN_T2.UI.WPF.Services
{
    /// <summary>
    /// 애니메이션 로직을 완전 분리한 서비스 클래스
    /// - 모든 애니메이션 시퀀스를 Rx 스트림으로 구성
    /// - UI 로직과 완전히 분리되어 테스트 가능
    /// - 로깅 및 에러 핸들링 중앙화
    /// </summary>
    public class AnimationService : IDisposable
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // 애니메이션 타이밍 설정 (한 곳에서 관리)
        public class AnimationConfig
        {
            public int BookOpenDurationMs { get; set; } = 250;
            public int BookCloseDurationMs { get; set; } = 250;
            public int MedalPopDurationMs { get; set; } = 300;
            public int MedalFlyDurationMs { get; set; } = 600;
            public int MedalLandDurationMs { get; set; } = 600;
            public int SteppedLayoutDurationMs { get; set; } = 600;
            public int RiserDurationMs { get; set; } = 600;
            public int FadeDurationMs { get; set; } = 250;
            public int HeaderFadeOutDurationMs { get; set; } = 150;
            public int HeaderSlideDurationMs { get; set; } = 150;
            public int BookSlideDurationMs { get; set; } = 350;
            public int BookOpenDelayMs { get; set; } = 200;
            public int BookExtraDelayMs { get; set; } = 150;
            public int MedalFlyExtraDelayMs { get; set; } = 50;

            // 거리/위치 설정
            public double BookSlideOffset { get; set; } = 50.0;
            public double MedalPopScale { get; set; } = 1.6;
            public double MedalPopYOffset { get; set; } = -88.0;
            public double MedalHeaderXOffset { get; set; } = 20.0;
            public double HeaderSlideStartX { get; set; } = -120.0;
            public double BgShakeOffset { get; set; } = -10.0;
            public double RiserMaxHeight { get; set; } = 80.0;

            // Z-Index 설정
            public int MedalProxyZIndex { get; set; } = 9999;
            public int HeaderZIndex { get; set; } = 10000;
            public int MedalProxyBelowHeaderZIndex { get; set; } = 5000;
            public int BookCoverZIndex { get; set; } = 999;
        }

        public AnimationConfig Config { get; set; } = new AnimationConfig();

        public AnimationService()
        {
            System.Diagnostics.Debug.WriteLine("[AnimService] 애니메이션 서비스 초기화됨 (한글)");
        }

        // ----------------------------------------------------------------------------------
        // [고수준 시퀀스: 모딩 메뉴로 전환]
        // ----------------------------------------------------------------------------------
        public IObservable<Unit> TransitionToModdingMenu(
            UIElement projectMenuButtons,
            UIElement projectListView,
            UIElement emptyStatePanel,
            UIElement moddingMenuContent,
            UIElement moddingMenuButtons,
            FrameworkElement bookCover,
            UIElement txtMainHeader,
            FrameworkElement leftSidebarBorder,
            FrameworkElement mainContentPanel)
        {
            System.Diagnostics.Debug.WriteLine("[AnimService] TransitionToModdingMenu 시작 (한글)");

            return Observable.Create<Unit>(observer =>
            {
                var sequence = Observable.Return(Unit.Default)
                    // 1. 프로젝트 목록 페이드 아웃
                    .SelectMany(_ => Observable.Merge(
                        UIAnimationsRx.Fade(projectMenuButtons, 1, 0, Config.FadeDurationMs),
                        UIAnimationsRx.Fade(projectListView, 1, 0, Config.FadeDurationMs)
                    ))
                    // 2. Visibility 변경
                    .Do(_ =>
                    {
                        projectMenuButtons.Visibility = Visibility.Collapsed;
                        projectListView.Visibility = Visibility.Collapsed;
                        emptyStatePanel.Visibility = Visibility.Collapsed;

                        // 책 표지 초기화
                        UIAnimationsRx.ClearAnimation(bookCover, UIElement.OpacityProperty);
                        bookCover.Opacity = 1;
                        bookCover.Visibility = Visibility.Visible;
                        System.Windows.Controls.Panel.SetZIndex(bookCover, Config.BookCoverZIndex);

                        moddingMenuContent.Opacity = 1;
                        moddingMenuContent.Visibility = Visibility.Visible;
                        moddingMenuButtons.Visibility = Visibility.Visible;
                        moddingMenuButtons.Opacity = 0;
                    })
                    // 3. 헤더 전환
                    .SelectMany(_ => UIAnimationsRx.Fade(txtMainHeader, 0, 1, Config.FadeDurationMs))
                    // 4. 딜레이 후 책 열기
                    .Delay(TimeSpan.FromMilliseconds(Config.BookOpenDelayMs))
                    .SelectMany(_ => UIAnimationsRx.AnimateBook(bookCover, true, Config.BookOpenDurationMs))
                    // 5. 모딩 메뉴 버튼 페이드 인
                    .SelectMany(_ => UIAnimationsRx.Fade(moddingMenuButtons, 0, 1, Config.FadeDurationMs))
                    // 6. 딜레이 후 배경 확장
                    .Delay(TimeSpan.FromMilliseconds(Config.BookExtraDelayMs))
                    // 7. 레이아웃 애니메이션
                    .SelectMany(_ => UIAnimationsRx.AnimateLayout(leftSidebarBorder, mainContentPanel, true, Config.SteppedLayoutDurationMs));

                var subscription = sequence.Subscribe(
                    _ =>
                    {
                        System.Diagnostics.Debug.WriteLine("[AnimService] TransitionToModdingMenu 완료 (한글)");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    },
                    ex =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimService] TransitionToModdingMenu 오류: {ex.Message}");
                        observer.OnError(ex);
                    }
                );

                return subscription;
            });
        }

        // ----------------------------------------------------------------------------------
        // [고수준 시퀀스: 프로젝트 목록으로 복귀]
        // ----------------------------------------------------------------------------------
        public IObservable<Unit> TransitionBackToProjectList(
            UIElement moddingMenuButtons,
            FrameworkElement bookCover,
            UIElement moddingMenuContent,
            UIElement txtMainHeader,
            UIElement projectMenuButtons,
            UIElement projectListView,
            FrameworkElement leftSidebarBorder,
            FrameworkElement mainContentPanel)
        {
            System.Diagnostics.Debug.WriteLine("[AnimService] TransitionBackToProjectList 시작 (한글)");

            return Observable.Create<Unit>(observer =>
            {
                var sequence = Observable.Return(Unit.Default)
                    // 1. 모딩 메뉴 페이드 아웃
                    .SelectMany(_ => UIAnimationsRx.Fade(moddingMenuButtons, 1, 0, Config.FadeDurationMs))
                    // 2. 책 닫기
                    .SelectMany(_ => UIAnimationsRx.AnimateBook(bookCover, false, Config.BookCloseDurationMs))
                    .Delay(TimeSpan.FromMilliseconds(Config.BookExtraDelayMs))
                    // 3. 레이아웃 복원
                    .SelectMany(_ => UIAnimationsRx.AnimateLayout(leftSidebarBorder, mainContentPanel, false, Config.SteppedLayoutDurationMs))
                    .Do(_ =>
                    {
                        moddingMenuContent.Visibility = Visibility.Collapsed;
                        moddingMenuButtons.Visibility = Visibility.Collapsed;
                    })
                    // 4. 책 표지 축소 및 페이드 아웃
                    .SelectMany(_ => UIAnimationsRx.Fade(bookCover, 1, 0, Config.FadeDurationMs))
                    .Do(_ =>
                    {
                        bookCover.Visibility = Visibility.Collapsed;
                        projectMenuButtons.Visibility = Visibility.Visible;
                        projectListView.Visibility = Visibility.Visible;
                    })
                    // 5. 프로젝트 목록 페이드 인
                    .SelectMany(_ => Observable.Merge(
                        UIAnimationsRx.Fade(projectMenuButtons, 0, 1, Config.FadeDurationMs),
                        UIAnimationsRx.Fade(projectListView, 0, 1, Config.FadeDurationMs),
                        UIAnimationsRx.Fade(txtMainHeader, 0, 1, Config.FadeDurationMs)
                    ));

                var subscription = sequence.Subscribe(
                    _ =>
                    {
                        System.Diagnostics.Debug.WriteLine("[AnimService] TransitionBackToProjectList 완료 (한글)");
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    },
                    ex =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimService] TransitionBackToProjectList 오류: {ex.Message}");
                        observer.OnError(ex);
                    }
                );

                return subscription;
            });
        }

        // ----------------------------------------------------------------------------------
        // [고수준 시퀀스: 도구 선택 애니메이션]
        // 메달 팝업 → 일시 정지 → 책 닫기 → 헤더로 날아감 → 도구 창 오픈
        // ----------------------------------------------------------------------------------
        public IObservable<Unit> PlaySelectionAnimation(
            FrameworkElement transitionProxy,
            FrameworkElement bookCover,
            UIElement moddingMenuContent,
            UIElement txtMainHeader,
            Point headerTargetPos)
        {
            System.Diagnostics.Debug.WriteLine("[AnimService] PlaySelectionAnimation 시작 (한글)");

            return Observable.Create<Unit>(observer =>
            {
                var sequence = Observable.Return(Unit.Default)
                    // 1. 메달 팝업 (Scale + Y 이동)
                    .SelectMany(_ => AnimateMedalPopup(transitionProxy))
                    // 2. 일시 정지 (메달이 떠있는 상태)
                    .Delay(TimeSpan.FromMilliseconds(100))
                    // 3. 책 닫기 + 배경 페이드 (병렬)
                    .SelectMany(_ => Observable.Merge(
                        UIAnimationsRx.AnimateBook(bookCover, false, Config.BookCloseDurationMs),
                        UIAnimationsRx.Fade(moddingMenuContent, 1, 0, Config.FadeDurationMs),
                        UIAnimationsRx.Fade(bookCover, 1, 0, Config.FadeDurationMs)
                    ))
                    // 4. 헤더로 날아가기
                    .SelectMany(_ => AnimateMedalFlyToHeader(transitionProxy, headerTargetPos))
                    // 5. 완료
                    .Do(_ =>
                    {
                        transitionProxy.Visibility = Visibility.Collapsed;
                        System.Diagnostics.Debug.WriteLine("[AnimService] PlaySelectionAnimation 완료 (한글)");
                    });

                var subscription = sequence.Subscribe(
                    _ =>
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    },
                    ex =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[AnimService] PlaySelectionAnimation 오류: {ex.Message}");
                        observer.OnError(ex);
                    }
                );

                return subscription;
            });
        }

        // ----------------------------------------------------------------------------------
        // [하위 시퀀스: 메달 팝업]
        // ----------------------------------------------------------------------------------
        private IObservable<Unit> AnimateMedalPopup(FrameworkElement transitionProxy)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    var grp = new TransformGroup();
                    var scaleT = new ScaleTransform(1, 1);
                    var transT = new TranslateTransform(0, 0);
                    grp.Children.Add(scaleT);
                    grp.Children.Add(transT);

                    transitionProxy.RenderTransform = grp;
                    transitionProxy.RenderTransformOrigin = new Point(0.5, 0.5);
                    transitionProxy.Visibility = Visibility.Visible;
                    transitionProxy.Opacity = 1;
                    System.Windows.Controls.Panel.SetZIndex(transitionProxy, Config.MedalProxyZIndex);

                    var duration = TimeSpan.FromMilliseconds(Config.MedalPopDurationMs);
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                    var animScaleX = new System.Windows.Media.Animation.DoubleAnimation(1.0, Config.MedalPopScale, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animScaleY = new System.Windows.Media.Animation.DoubleAnimation(1.0, Config.MedalPopScale, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animMoveY = new System.Windows.Media.Animation.DoubleAnimation(0, Config.MedalPopYOffset, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };

                    int completedCount = 0;
                    void OnCompleted(object? s, EventArgs e)
                    {
                        completedCount++;
                        if (completedCount == 3)
                        {
                            System.Diagnostics.Debug.WriteLine("[AnimService] MedalPopup 완료 (한글)");
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        }
                    }

                    animScaleX.Completed += OnCompleted;
                    animScaleY.Completed += OnCompleted;
                    animMoveY.Completed += OnCompleted;

                    scaleT.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
                    scaleT.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
                    transT.BeginAnimation(TranslateTransform.YProperty, animMoveY);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimService] MedalPopup 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
        }

        // ----------------------------------------------------------------------------------
        // [하위 시퀀스: 메달이 헤더로 날아가기]
        // ----------------------------------------------------------------------------------
        private IObservable<Unit> AnimateMedalFlyToHeader(FrameworkElement transitionProxy, Point targetPos)
        {
            return Observable.Create<Unit>(observer =>
            {
                try
                {
                    var grp = transitionProxy.RenderTransform as TransformGroup;
                    var scaleT = grp?.Children[0] as ScaleTransform;
                    var transT = grp?.Children[1] as TranslateTransform;

                    if (scaleT == null || transT == null)
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                        return Disposable.Empty;
                    }

                    var duration = TimeSpan.FromMilliseconds(Config.MedalFlyDurationMs);
                    var ease = new SineEase { EasingMode = EasingMode.EaseIn };

                    var animFlyX = new System.Windows.Media.Animation.DoubleAnimation(0, targetPos.X, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animFlyY = new System.Windows.Media.Animation.DoubleAnimation(Config.MedalPopYOffset, targetPos.Y, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animScaleDownX = new System.Windows.Media.Animation.DoubleAnimation(Config.MedalPopScale, 1.0, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animScaleDownY = new System.Windows.Media.Animation.DoubleAnimation(Config.MedalPopScale, 1.0, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animFade = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration)
                    {
                        EasingFunction = ease,
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };

                    int completedCount = 0;
                    void OnCompleted(object? s, EventArgs e)
                    {
                        completedCount++;
                        if (completedCount == 5)
                        {
                            System.Diagnostics.Debug.WriteLine("[AnimService] MedalFlyToHeader 완료 (한글)");
                            observer.OnNext(Unit.Default);
                            observer.OnCompleted();
                        }
                    }

                    animFlyX.Completed += OnCompleted;
                    animFlyY.Completed += OnCompleted;
                    animScaleDownX.Completed += OnCompleted;
                    animScaleDownY.Completed += OnCompleted;
                    animFade.Completed += OnCompleted;

                    transT.BeginAnimation(TranslateTransform.XProperty, animFlyX);
                    transT.BeginAnimation(TranslateTransform.YProperty, animFlyY);
                    scaleT.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleDownX);
                    scaleT.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleDownY);
                    transitionProxy.BeginAnimation(UIElement.OpacityProperty, animFade);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimService] MedalFlyToHeader 오류: {ex.Message}");
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
        }

        public void Dispose()
        {
            _disposables.Dispose();
            System.Diagnostics.Debug.WriteLine("[AnimService] 애니메이션 서비스 해제됨 (한글)");
        }
    }
}
