using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QdratNew.ViewModels.Dashboard;

namespace QdratNew.Services.AdminDashboard
{
    public class AdminLiveStudentTracker : IAdminLiveStudentTracker
    {
        private readonly ConcurrentDictionary<int, LiveStudentSnapshot> _students =
            new ConcurrentDictionary<int, LiveStudentSnapshot>();

        private readonly ConcurrentDictionary<int, string> _lastMovementKey =
            new ConcurrentDictionary<int, string>();

        private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MovingWindow = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CleanupWindow = TimeSpan.FromMinutes(30);

        public void Track(int studentId, string userId, string path, string pageTitle)
        {
            if (studentId <= 0)
            {
                return;
            }

            DateTime now = DateTime.Now;

            string safeUserId = userId ?? string.Empty;
            string safePath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            string safePageTitle = string.IsNullOrWhiteSpace(pageTitle) ? safePath : pageTitle.Trim();

            _students.AddOrUpdate(
                studentId,
                key =>
                {
                    _lastMovementKey[studentId] = safePath;

                    return new LiveStudentSnapshot
                    {
                        StudentId = studentId,
                        UserId = safeUserId,
                        CurrentPath = safePath,
                        CurrentPageTitle = safePageTitle,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        PageHitCount = 1,
                        IsLiveNow = true,
                        IsMoving = false
                    };
                },
                (key, existing) =>
                {
                    bool changedPage = !string.Equals(existing.CurrentPath, safePath, StringComparison.OrdinalIgnoreCase);

                    if (changedPage)
                    {
                        existing.PageHitCount++;
                        _lastMovementKey[studentId] = safePath;
                    }

                    existing.UserId = safeUserId;
                    existing.CurrentPath = safePath;
                    existing.CurrentPageTitle = safePageTitle;
                    existing.LastSeenAt = now;
                    existing.IsLiveNow = true;
                    existing.IsMoving = changedPage;

                    return existing;
                });

            CleanupOldSnapshots(now);
        }

        public void MarkOffline(int studentId)
        {
            if (studentId <= 0)
            {
                return;
            }

            /*
             * لا نحذف الطالب فورًا.
             * السبب: beforeunload/sendBeacon قد يعمل عند Refresh أو انتقال صفحة.
             * نترك LiveWindow يحدد أنه خرج تلقائيًا إذا لم يصل Ping جديد خلال 5 دقائق.
             */
            if (_students.TryGetValue(studentId, out LiveStudentSnapshot? existing))
            {
                existing.LastSeenAt = DateTime.Now.Subtract(LiveWindow).Subtract(TimeSpan.FromSeconds(5));
                existing.IsLiveNow = false;
                existing.IsMoving = false;
            }
        }

        public List<LiveStudentSnapshot> GetSnapshots()
        {
            DateTime now = DateTime.Now;

            CleanupOldSnapshots(now);

            return _students.Values
                .Select(item =>
                {
                    bool isLiveNow = now.Subtract(item.LastSeenAt) <= LiveWindow;
                    bool isMoving = isLiveNow && now.Subtract(item.LastSeenAt) <= MovingWindow && item.IsMoving;

                    return new LiveStudentSnapshot
                    {
                        StudentId = item.StudentId,
                        UserId = item.UserId,
                        CurrentPath = item.CurrentPath,
                        CurrentPageTitle = item.CurrentPageTitle,
                        FirstSeenAt = item.FirstSeenAt,
                        LastSeenAt = item.LastSeenAt,
                        PageHitCount = item.PageHitCount,
                        IsLiveNow = isLiveNow,
                        IsMoving = isMoving
                    };
                })
                .OrderByDescending(item => item.LastSeenAt)
                .ToList();
        }

        public LiveStudentSnapshot? GetSnapshotByStudentId(int studentId)
        {
            if (studentId <= 0)
            {
                return null;
            }

            DateTime now = DateTime.Now;

            CleanupOldSnapshots(now);

            if (!_students.TryGetValue(studentId, out LiveStudentSnapshot? snapshot))
            {
                return null;
            }

            bool isLiveNow = now.Subtract(snapshot.LastSeenAt) <= LiveWindow;
            bool isMoving = isLiveNow && now.Subtract(snapshot.LastSeenAt) <= MovingWindow && snapshot.IsMoving;

            return new LiveStudentSnapshot
            {
                StudentId = snapshot.StudentId,
                UserId = snapshot.UserId,
                CurrentPath = snapshot.CurrentPath,
                CurrentPageTitle = snapshot.CurrentPageTitle,
                FirstSeenAt = snapshot.FirstSeenAt,
                LastSeenAt = snapshot.LastSeenAt,
                PageHitCount = snapshot.PageHitCount,
                IsLiveNow = isLiveNow,
                IsMoving = isMoving
            };
        }

        private void CleanupOldSnapshots(DateTime now)
        {
            foreach (KeyValuePair<int, LiveStudentSnapshot> item in _students)
            {
                if (now.Subtract(item.Value.LastSeenAt) > CleanupWindow)
                {
                    _students.TryRemove(item.Key, out _);
                    _lastMovementKey.TryRemove(item.Key, out _);
                }
            }
        }
    }
}