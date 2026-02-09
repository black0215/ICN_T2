using System.Windows.Input;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class ModdingToolViewModel
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string EngTitle { get; set; } = "";

        // 1-based index for icon loading (e.g. icon_a1.png)
        public int IconIndex { get; set; }

        public ItemCommand Command { get; set; }

        // Position & Size for Canvas
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // Custom offsets for icons (from legacy code)
        public double IconScale { get; set; } = 1.0;
        public double IconOffsetX { get; set; } = 0.0;
        public double IconOffsetY { get; set; } = 0.0;

        // Paths for Binding (max index is 11, so clamp to 11 for index 12+)
        private int SafeIconIndex => IconIndex > 11 ? 11 : IconIndex;

        private string? _bagIconPath;
        public string BagIconPath
        {
            get => _bagIconPath ?? $"pack://application:,,,/ICN_T2;component/Resources/Tribe/icon_bag{SafeIconIndex}.png";
            set => _bagIconPath = value;
        }

        private string? _iconAPath;
        public string IconAPath
        {
            get => _iconAPath ?? $"pack://application:,,,/ICN_T2;component/Resources/Tribe/icon_a{SafeIconIndex}.png";
            set => _iconAPath = value;
        }

        private string? _iconBPath;
        public string IconBPath
        {
            get => _iconBPath ?? $"pack://application:,,,/ICN_T2;component/Resources/Tribe/icon_b{SafeIconIndex}.png";
            set => _iconBPath = value;
        }

        public ToolType MToolType { get; set; } = ToolType.Default;

        public ModdingToolViewModel(string title, string engTitle, string desc, int index, Action<object?> execute)
        {
            Title = title;
            EngTitle = engTitle;
            Description = desc;
            IconIndex = index;
            Command = new ItemCommand(execute);
        }
    }

    public enum ToolType
    {
        Default,
        CharacterInfo,
        CharacterScale,
        YokaiStats
    }

    public class ItemCommand : ICommand
    {
        private readonly Action<object?> _action;
        public ItemCommand(Action<object?> action) => _action = action;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action?.Invoke(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
