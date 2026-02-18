namespace ICN_T2.UI.WPF.ViewModels.Contracts
{
    public sealed class ToolPendingChange
    {
        public ToolPendingChange(string changeId, string displayName, string? description = null)
        {
            ChangeId = changeId;
            DisplayName = displayName;
            Description = description;
        }

        public string ChangeId { get; }
        public string DisplayName { get; }
        public string? Description { get; }
    }
}
