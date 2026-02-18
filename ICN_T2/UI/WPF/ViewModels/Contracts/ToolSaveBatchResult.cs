using System.Collections.Generic;

namespace ICN_T2.UI.WPF.ViewModels.Contracts
{
    public sealed class ToolSaveBatchResult
    {
        public ToolSaveBatchResult(
            IReadOnlyList<string> savedChangeIds,
            IReadOnlyDictionary<string, string> failedChangeReasons)
        {
            SavedChangeIds = savedChangeIds;
            FailedChangeReasons = failedChangeReasons;
        }

        public IReadOnlyList<string> SavedChangeIds { get; }
        public IReadOnlyDictionary<string, string> FailedChangeReasons { get; }
    }
}
