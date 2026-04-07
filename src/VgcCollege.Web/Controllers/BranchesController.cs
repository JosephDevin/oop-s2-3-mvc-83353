using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class BranchesController : Controller
{
    private readonly ApplicationDbContext _db;

    public BranchesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var branches = await _db.Branches
            .Include(b => b.Courses)
            .ToListAsync();
        return View(branches);
    }

    public async Task<IActionResult> Details(int id)
    {
        var branch = await _db.Branches
            .Include(b => b.Courses)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound();
        return View(branch);
    }

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        if (!ModelState.IsValid) return View(branch);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Branch created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();
        return View(branch);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch branch)
    {
        if (id != branch.Id) return BadRequest();
        if (!ModelState.IsValid) return View(branch);
        _db.Branches.Update(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Branch updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var branch = await _db.Branches
            .Include(b => b.Courses)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound();
        return View(branch);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch != null)
        {
            _db.Branches.Remove(branch);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Branch deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
