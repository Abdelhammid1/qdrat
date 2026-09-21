using QdratNew.Data;
using QdratNew.Services.Statistics.DTOs;
using QdratNew.Services.Statistics.Interfaces;
using System;
using System.Linq;
using System.Collections.Generic;

namespace QdratNew.Services.Statistics.Implementations
{
    public class HomeworkStatisticsService : IHomeworkStatisticsService
    {
        private readonly ApplicationDbContext _context;

        public HomeworkStatisticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public HomeworkDraftStatsDto GetPartnerDraftStats(
             int partnerId,
             int subscriptionPeriodId)
        {
            var drafts = _context.HomeworkDrafts
                .Where(d =>
                    d.PartnerId == partnerId &&
                    d.SubscriptionPeriodId == subscriptionPeriodId)
                .ToList(); // ✅ في الذاكرة

            return new HomeworkDraftStatsDto
            {
                TotalDrafts = drafts.Count,
                AverageQuestions = drafts.Any()
                    ? drafts.Average(d => d.Questions.Count)
                    : 0
            };
        }
    }
}
