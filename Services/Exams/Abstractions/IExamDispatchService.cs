using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Abstractions
{
    public interface IExamDispatchService
    {
        // =========================
        // SEND TO BATCHES
        // =========================
        Task SendToBatchesAsync(
            int draftId,
            int instructorId,
            List<int> batchIds,
            string examTitle,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes,
            string examMode);

        // =========================
        // SEND TO STUDENTS
        // =========================
        Task SendToStudentsAsync(
            int draftId,
            int instructorId,
            List<int> studentIds,
            string examTitle,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes,
            string examMode);

        // =========================
        // PLACEMENT TEST
        // =========================
        Task SendPlacementTestAsync(
            int draftId,
            int instructorId,
            List<int> studentIds,
            string examTitle,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes,
            string examMode);

        // =========================
        // PERFORMANCE INDICATOR
        // =========================
        Task SendPerformanceIndicatorAsync(
            int draftId,
            int instructorId,
            List<int> batchIds,
            List<int> studentIds);
    }







}