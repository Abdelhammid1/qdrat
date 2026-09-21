using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QdratNew.Services
{
    public class RemedialPlanBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public RemedialPlanBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var remedialPlanService = scope.ServiceProvider.GetRequiredService<RemedialPlanService>();
                    remedialPlanService.CheckForLateStudents();
                    remedialPlanService.NotifyParentsAboutStudentProgress();
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // ✅ تنفيذ المهمة كل 24 ساعة
            }
        }
    }
}
