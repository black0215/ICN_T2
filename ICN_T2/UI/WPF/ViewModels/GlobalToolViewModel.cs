using System.Windows.Input;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class GlobalToolViewModel
    {
        public string Title { get; }
        public string IconPath { get; }
        public ICommand Command { get; }

        public GlobalToolViewModel(string title, string iconPath, ICommand command)
        {
            Title = title;
            IconPath = iconPath;
            Command = command;
        }
    }
}
