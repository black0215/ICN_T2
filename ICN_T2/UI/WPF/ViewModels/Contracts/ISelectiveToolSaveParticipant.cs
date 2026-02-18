using System.Collections.Generic;

namespace ICN_T2.UI.WPF.ViewModels.Contracts
{
    public interface ISelectiveToolSaveParticipant
    {
        string ToolId { get; }
        string ToolDisplayName { get; }
        IReadOnlyList<ToolPendingChange> GetPendingChanges();
        ToolSaveBatchResult SavePendingChanges(IReadOnlyCollection<string> changeIds);
    }
}
