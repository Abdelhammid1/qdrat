using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetTotalLecturesForStudentAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId)
                .Select(a => a.LectureId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetAttendedLecturesCountAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId && a.IsPresent)
                .CountAsync();
        }

        public async Task<int> GetAbsentLecturesCountAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId && !a.IsPresent && a.Notes == null)
                .CountAsync();
        }

        public async Task<int> GetExcusedAbsencesCountAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId && !a.IsPresent && a.Notes != null)
                .CountAsync();
        }

        public async Task<List<AttendanceRecord>> GetAttendanceDetailsAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .Include(a => a.Lecture)
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.RecordedAt)
                .ToListAsync();
        }
    }
}
