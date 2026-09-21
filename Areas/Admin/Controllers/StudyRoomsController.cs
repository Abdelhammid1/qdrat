using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudyRoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudyRoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ Index
        public IActionResult Index()
        {
            var model = _context.StudyRooms.Select(r => new StudyRoomViewModel
            {
                Id = r.Id,
                RoomName = r.RoomName,
                Capacity = r.Capacity,
                DeviceCount = r.DeviceCount,
                HasComputers = r.HasComputers,
                Location = r.Location,
                IsActive = r.IsActive
            }).ToList();

            return View(model);
        }

        // ✅ Details
        public async Task<IActionResult> Details(int id)
        {
            var room = await _context.StudyRooms.FindAsync(id);
            if (room == null) return NotFound();

            var model = new StudyRoomViewModel
            {
                Id = room.Id,
                RoomName = room.RoomName,
                Capacity = room.Capacity,
                DeviceCount = room.DeviceCount,
                HasComputers = room.HasComputers,
                Location = room.Location,
                IsActive = room.IsActive
            };

            return View(model);
        }

        // ✅ Create - GET
        public IActionResult Create()
        {
            return View(new StudyRoomViewModel());
        }

        // ✅ Create - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudyRoomViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var entity = new StudyRoom
            {
                RoomName = model.RoomName,
                Capacity = model.Capacity,
                DeviceCount = model.DeviceCount ?? 0,
                HasComputers = model.HasComputers,
                Location = model.Location,
                IsActive = model.IsActive
            };

            _context.StudyRooms.Add(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ Edit - GET
        public async Task<IActionResult> Edit(int id)
        {
            var room = await _context.StudyRooms.FindAsync(id);
            if (room == null) return NotFound();

            var model = new StudyRoomViewModel
            {
                Id = room.Id,
                RoomName = room.RoomName,
                Capacity = room.Capacity,
                DeviceCount = room.DeviceCount,
                HasComputers = room.HasComputers,
                Location = room.Location,
                IsActive = room.IsActive
            };

            return View(model);
        }

        // ✅ Edit - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StudyRoomViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var entity = await _context.StudyRooms.FindAsync(model.Id);
            if (entity == null) return NotFound();

            entity.RoomName = model.RoomName;
            entity.Capacity = model.Capacity;
            entity.DeviceCount = model.DeviceCount ?? 0;
            entity.HasComputers = model.HasComputers;
            entity.Location = model.Location;
            entity.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ Delete - GET
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.StudyRooms.FindAsync(id);
            if (room == null) return NotFound();

            var model = new StudyRoomViewModel
            {
                Id = room.Id,
                RoomName = room.RoomName,
                Capacity = room.Capacity,
                DeviceCount = room.DeviceCount,
                HasComputers = room.HasComputers,
                Location = room.Location,
                IsActive = room.IsActive
            };

            return View(model);
        }

        // ✅ Delete - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.StudyRooms.FindAsync(id);
            if (room == null) return NotFound();

            _context.StudyRooms.Remove(room);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
