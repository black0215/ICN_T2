using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ICN_T2.Logic.Project;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.UI.WPF.Services;
using System.Linq;
using System.IO;
using ICN_T2.Logic.Level5.Binary;

namespace ICN_T2.UI.WPF.ViewModels
{
    /// <summary>
    /// ModernModWindow를 위한 ReactiveUI ViewModel
    /// - ReactiveObject 상속으로 속성 변경 알림 자동화
    /// - ReactiveCommand로 명령 처리
    /// - 애니메이션은 AnimationService에 위임
    /// </summary>
    public class ModernModWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly AnimationService _animationService;
        private readonly Action<int, object?>? _executeToolCallback;

        // ----------------------------------------------------------------------------------
        // [상태 관리]
        // ----------------------------------------------------------------------------------

        public enum NavState
        {
            ProjectList = 0,
            ModdingMenu = 1,
            ToolWindow = 2,
            DetailView = 3
        }

        private NavState _currentNavState;
        public NavState CurrentNavState
        {
            get => _currentNavState;
            set => this.RaiseAndSetIfChanged(ref _currentNavState, value);
        }

        private IGame? _currentGame;
        public IGame? CurrentGame
        {
            get => _currentGame;
            set => this.RaiseAndSetIfChanged(ref _currentGame, value);
        }

        private double _stepProgress;
        public double StepProgress
        {
            get => _stepProgress;
            set => this.RaiseAndSetIfChanged(ref _stepProgress, value);
        }

        private double _riserProgress;
        public double RiserProgress
        {
            get => _riserProgress;
            set => this.RaiseAndSetIfChanged(ref _riserProgress, value);
        }

        private string _headerText = "메인메뉴";
        public string HeaderText
        {
            get => _headerText;
            set => this.RaiseAndSetIfChanged(ref _headerText, NormalizeHeaderText(value));
        }

        private bool _isTransitioning;
        public bool IsTransitioning
        {
            get => _isTransitioning;
            set => this.RaiseAndSetIfChanged(ref _isTransitioning, value);
        }

        // ----------------------------------------------------------------------------------
        // [컬렉션]
        // ----------------------------------------------------------------------------------

        public ObservableCollection<Project> Projects { get; } = new ObservableCollection<Project>();
        public ObservableCollection<ModdingToolViewModel> ModdingTools { get; } = new ObservableCollection<ModdingToolViewModel>();
        public ObservableCollection<GlobalToolViewModel> GlobalTools { get; } = new ObservableCollection<GlobalToolViewModel>();

        // ----------------------------------------------------------------------------------
        // [Commands]
        // ----------------------------------------------------------------------------------

        public ReactiveCommand<Unit, Unit> NavigateToModdingMenuCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateBackToProjectListCommand { get; }
        public ReactiveCommand<ModdingToolViewModel, Unit> OpenToolCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshProjectListCommand { get; }
        public ReactiveCommand<Project, Unit> OpenProjectCommand { get; }
        public ReactiveCommand<Project, Unit> DeleteProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> ExtractAydCommand { get; }

        // ----------------------------------------------------------------------------------
        // [Constructor]
        // ----------------------------------------------------------------------------------

        public ModernModWindowViewModel(Action<int, object?>? executeToolCallback = null)
        {
            _animationService = new AnimationService();
            _executeToolCallback = executeToolCallback;

            System.Diagnostics.Debug.WriteLine("[ViewModel] ModernModWindowViewModel 초기화 시작 (한글)");

            // 초기 상태
            CurrentNavState = NavState.ProjectList;
            StepProgress = 0;
            RiserProgress = 0;

            // Commands 초기화
            var canNavigate = this.WhenAnyValue(x => x.IsTransitioning).Select(x => !x);

            NavigateToModdingMenuCommand = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    System.Diagnostics.Debug.WriteLine("[ViewModel] NavigateToModdingMenu Command 실행 (한글)");
                    IsTransitioning = true;
                    CurrentNavState = NavState.ModdingMenu;
                    HeaderText = "모딩메뉴";
                    
                    // 실제 애니메이션은 View에서 AnimationService를 통해 실행
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    IsTransitioning = false;
                },
                canNavigate);

            NavigateBackToProjectListCommand = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    System.Diagnostics.Debug.WriteLine("[ViewModel] NavigateBackToProjectList Command 실행 (한글)");
                    IsTransitioning = true;
                    CurrentNavState = NavState.ProjectList;
                    HeaderText = "메인메뉴";
                    StepProgress = 0;
                    RiserProgress = 0;
                    
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    IsTransitioning = false;
                },
                canNavigate);

            OpenToolCommand = ReactiveCommand.CreateFromTask<ModdingToolViewModel>(
                async (tool) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewModel] OpenTool Command 실행: {tool.Title} (한글)");
                    IsTransitioning = true;
                    CurrentNavState = NavState.ToolWindow;
                    HeaderText = tool.Title;
                    
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    IsTransitioning = false;
                },
                canNavigate);

            RefreshProjectListCommand = ReactiveCommand.Create(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ViewModel] RefreshProjectList Command 실행 (한글)");
                RefreshProjectList();
            });

            OpenProjectCommand = ReactiveCommand.CreateFromTask<Project>(
                async (project) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewModel] OpenProject Command 실행: {project.Name} (한글)");
                    IsTransitioning = true;
                    
                    // 프로젝트 로드 로직 (추후 구현)
                    await LoadProject(project);
                    
                    // 모딩 메뉴로 전환
                    await NavigateToModdingMenuCommand.Execute();
                },
                canNavigate);

            DeleteProjectCommand = ReactiveCommand.CreateFromTask<Project>(
                async (project) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewModel] DeleteProject Command 실행: {project.Name} (한글)");
                    
                    // 삭제 확인 및 실행 (View에서 처리)
                    await System.Threading.Tasks.Task.CompletedTask;
                });

            ExtractAydCommand = ReactiveCommand.Create(ExtractAyd);

            // 초기 프로젝트 목록 로드
            RefreshProjectList();
            InitializeModdingTools();
            InitializeGlobalTools();

            System.Diagnostics.Debug.WriteLine("[ViewModel] ModernModWindowViewModel 초기화 완료 (한글)");
        }

        // ----------------------------------------------------------------------------------
        // [Private Methods]
        // ----------------------------------------------------------------------------------

        private void RefreshProjectList()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ViewModel] 프로젝트 목록 새로고침 시작 (한글)");
                
                ProjectManager.EnsureProjectsRoot();
                var projects = ProjectManager.GetAvailableProjects();

                Projects.Clear();
                foreach (var project in projects)
                {
                    Projects.Add(project);
                }

                System.Diagnostics.Debug.WriteLine($"[ViewModel] 프로젝트 {Projects.Count}개 로드됨 (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] 프로젝트 목록 로드 오류: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadProject(Project project)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] 프로젝트 로드 시작: {project.Name} (한글)");
                
                // 실제 게임 파일 로드 로직 (추후 구현)
                await System.Threading.Tasks.Task.Delay(100);
                
                System.Diagnostics.Debug.WriteLine($"[ViewModel] 프로젝트 로드 완료: {project.Name} (한글)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] 프로젝트 로드 오류: {ex.Message}");
            }
        }

        private void InitializeModdingTools()
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] 모딩 도구 목록 초기화 (한글)");

            var features = new[]
            {
                ("캐릭터\n기본정보", "Character Info", "Edit basic info like Name, Money, Time played."),
                ("캐릭터\n비율", "Model Scale", "Adjust the scale/size of the player character."),
                ("요괴\n능력치", "Yo-kai Stats", "Modify IVs, EVs, and base stats for Yo-kai."),
                ("인카운터", "Encounter Editor", "Change wild Yo-kai spawns in maps."),
                ("상점", "Shop Editor", "Edit items sold in various shops."),
                ("아이템", "Item Editor", "Modify item properties."),
                ("퀘스트", "Quest Editor", "Edit quest requirements and rewards."),
                ("대화", "Text Editor", "Modify game dialogues."),
                ("배틀", "Battle Config", "Edit battle parameters."),
                ("맵", "Map Editor", "View and edit map entities."),
                ("전체 저장", "Full Save", "Export the complete yw2_a.fa archive to /Export folder."),
                ("설정", "Settings", "Tool configuration.")
            };

            double scale = 1.1116;
            double gridStartX = (65.2 * scale) + 2;
            double gridStartY = (8 * scale) + 2;
            double cellW = 90;
            double cellH = 90;
            double gapX = -1.3;
            double gapY = -7.3;

            for (int i = 0; i < features.Length && i < 12; i++)
            {
                var feat = features[i];
                int iconIndex = i + 1;

                int col = i % 4;
                int row = i / 4;
                double x = gridStartX + col * (cellW + gapX);
                double y = gridStartY + row * (cellH + gapY);

                int capturedIndex = i;
                var vm = new ModdingToolViewModel(feat.Item1, feat.Item2, feat.Item3, iconIndex, (p) => _executeToolCallback?.Invoke(capturedIndex, p))
                {
                    X = x,
                    Y = y,
                    Width = cellW,
                    Height = cellH
                };

                if (i == 0)
                    vm.MToolType = ToolType.CharacterInfo;
                else if (i == 1)
                    vm.MToolType = ToolType.CharacterScale;
                else if (i == 2)
                    vm.MToolType = ToolType.YokaiStats;
                else if (i == 3)
                    vm.MToolType = ToolType.EncounterEditor;

                ModdingTools.Add(vm);
            }

            System.Diagnostics.Debug.WriteLine($"[ViewModel] 모딩 도구 {ModdingTools.Count}개 초기화 완료 (한글)");
        }

        private void InitializeGlobalTools()
        {
            GlobalTools.Clear();
            GlobalTools.Add(new GlobalToolViewModel(
                title: "AYD Extractor",
                iconPath: "pack://application:,,,/ICN_T2;component/Resources/UI%20icon/Tool.png",
                command: ExtractAydCommand));
        }

        private void ExtractAyd()
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "AYD 파일 선택",
                    Filter = "AYD Files (*.ayd)|*.ayd"
                };

                if (openDialog.ShowDialog() != true)
                {
                    return;
                }

                using var outputDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "추출 파일을 저장할 폴더를 선택하세요."
                };

                if (outputDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK ||
                    string.IsNullOrWhiteSpace(outputDialog.SelectedPath))
                {
                    return;
                }

                var loader = new AydLoader();
                var data = loader.Load(File.ReadAllBytes(openDialog.FileName));

                string outputRoot = Path.GetFullPath(outputDialog.SelectedPath);
                string outputRootPrefix = outputRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? outputRoot
                    : outputRoot + Path.DirectorySeparatorChar;

                int extractedCount = 0;
                for (int i = 0; i < data.Files.Count; i++)
                {
                    var file = data.Files[i];

                    string relativePath = string.IsNullOrWhiteSpace(file.FileName)
                        ? $"file_{i + 1:D4}.bin"
                        : file.FileName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

                    string outputPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
                    if (!outputPath.StartsWith(outputRootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string safeExtension = Path.GetExtension(relativePath);
                        if (string.IsNullOrWhiteSpace(safeExtension))
                        {
                            safeExtension = ".bin";
                        }

                        outputPath = Path.Combine(outputRoot, $"file_{i + 1:D4}{safeExtension}");
                    }

                    string? outputDirectory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    File.WriteAllBytes(outputPath, file.Data);
                    extractedCount++;
                }

                System.Windows.MessageBox.Show(
                    $"AYD 추출이 완료되었습니다.\n파일 수: {extractedCount}\n출력 경로: {outputRoot}",
                    "AYD Extractor",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"AYD 추출 중 오류가 발생했습니다.\n{ex.Message}",
                    "AYD Extractor",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static string NormalizeHeaderText(string? text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }

        // ----------------------------------------------------------------------------------
        // [IDisposable]
        // ----------------------------------------------------------------------------------

        public void Dispose()
        {
            _disposables.Dispose();
            _animationService.Dispose();
            System.Diagnostics.Debug.WriteLine("[ViewModel] ModernModWindowViewModel 해제됨 (한글)");
        }
    }
}
