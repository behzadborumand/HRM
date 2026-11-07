using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Security;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Web.Controllers;

[Authorize]
public class OrganizationLinksController(IRepository<OrganizationLink> links) : Controller
{
    [Authorize(Policy = Policies.OrgRead)]
    public async Task<IActionResult> Index(int? edit = null)
    {
        ViewData["Title"] = "پیوندهای سازمانی";
        var model = new OrganizationLinksViewModel { Active = true, Rows = await RowsAsync() };
        if (edit.HasValue)
        {
            var row = model.Rows.FirstOrDefault(l => l.LinkId == edit);
            if (row is null) return NotFound();
            model.LinkId = row.LinkId;
            model.Title = row.Title;
            model.Url = row.Url;
            model.Description = row.Description;
            model.SortOrder = row.SortOrder;
            model.Active = row.Active;
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrgWrite)]
    public async Task<IActionResult> Save(OrganizationLinksViewModel model)
    {
        ViewData["Title"] = "پیوندهای سازمانی";
        if (!OrganizationLinkRules.IsAllowedUrl(model.Url))
            ModelState.AddModelError(nameof(model.Url), "نشانی کامل با http:// یا https:// و بدون نام کاربری و رمز وارد کنید.");
        if (!ModelState.IsValid)
        {
            model.Rows = await RowsAsync();
            return View("Index", model);
        }
        var row = model.LinkId.HasValue ? await links.FindAsync(model.LinkId.Value) : new OrganizationLink();
        if (row is null) return NotFound();
        row.Title = model.Title.Trim();
        row.Url = model.Url.Trim();
        row.Description = model.Description?.Trim();
        row.SortOrder = model.SortOrder;
        row.Active = model.Active;
        if (model.LinkId.HasValue) links.Update(row);
        else await links.AddAsync(row);
        await links.SaveChangesAsync();
        TempData["Message"] = "پیوند سازمانی ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrgWrite)]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await links.FindAsync(id);
        if (row is null) return NotFound();
        links.Remove(row);
        await links.SaveChangesAsync();
        TempData["Message"] = "پیوند حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<OrganizationLink>> RowsAsync() =>
        (await links.ListAsync()).OrderBy(l => l.SortOrder).ThenBy(l => l.Title).ThenBy(l => l.LinkId).ToList();
}
