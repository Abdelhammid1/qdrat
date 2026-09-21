namespace QdratNew.ViewModels.IntegrityGuard
{
    public class BlockedViewModel
    {
        public int? AttemptType { get; set; }
        public int? AttemptEntityId { get; set; }
        public bool CanSelfResolve { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
