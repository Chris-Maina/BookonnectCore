namespace BookonnectCore.Models
{
    public class SyncStatus
    {
        public int RelationalBookCount { get; set; }
        public int VectorBookCount { get; set; }
        public int InSyncCount { get; set; }
        public int MissingInVectorDBCount { get; set; }
        public int OrphanedInVectorDBCount { get; set; }
        public double SyncPercentage => RelationalBookCount > 0 ? (double)InSyncCount / RelationalBookCount * 100 : 0;
        public bool IsFullySynced => RelationalBookCount > 0 && RelationalBookCount == InSyncCount && OrphanedInVectorDBCount == 0;
    }
}