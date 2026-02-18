namespace ICN_T2.UI.WPF.ViewModels.Contracts
{
    public interface IToolSaveParticipant
    {
        bool HasPendingChanges { get; }
        bool SavePendingChanges();
    }
}
