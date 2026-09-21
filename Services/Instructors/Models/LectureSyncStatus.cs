namespace QdratNew.Services.Instructors.Models
{
    public class LectureSyncStatus
    {
        public int TotalAutoLectures { get; set; }
        public int SyncedCount { get; set; }
        public int OutOfSyncCount => TotalAutoLectures - SyncedCount;
        public bool IsFullySynced => TotalAutoLectures == 0 || OutOfSyncCount == 0;
    }
}
