using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Web.Controllers;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace HRMS.Tests;

public class OrganizationLinksTests
{
    [Fact]
    public async Task Invalid_url_does_not_create_a_link()
    {
        var store = new LinkStore();
        var controller = Create(store);
        var response = Assert.IsType<ViewResult>(await controller.Save(new() { Title = "پیوند", Url = "javascript:alert(1)" }));
        Assert.Equal("Index", response.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Invalid_model_does_not_write_or_delete_existing_links()
    {
        var store = new LinkStore();
        var controller = Create(store);
        controller.ModelState.AddModelError("Title", "required");
        Assert.IsType<ViewResult>(await controller.Save(new() { Url = "https://example.com" }));
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Create_and_update_preserve_order_and_inactive_status()
    {
        var store = new LinkStore();
        var controller = Create(store);
        Assert.IsType<RedirectToActionResult>(await controller.Save(new() { Title = "  راهنما  ", Url = "https://example.com", SortOrder = 7, Active = true }));
        var row = Assert.Single(store.Rows);
        Assert.Equal("راهنما", row.Title);
        Assert.IsType<RedirectToActionResult>(await controller.Save(new() { LinkId = row.LinkId, Title = "بایگانی", Url = "http://intranet.local", SortOrder = 2, Active = false }));
        Assert.Equal("بایگانی", row.Title);
        Assert.Equal(2, row.SortOrder);
        Assert.False(row.Active);
    }

    [Fact]
    public async Task Edit_or_delete_of_missing_link_returns_not_found()
    {
        var controller = Create(new LinkStore());
        Assert.IsType<NotFoundResult>(await controller.Index(99));
        Assert.IsType<NotFoundResult>(await controller.Save(new() { LinkId = 99, Title = "راهنما", Url = "https://example.com" }));
        Assert.IsType<NotFoundResult>(await controller.Delete(99));
    }

    [Fact]
    public async Task Delete_removes_only_requested_link()
    {
        var store = new LinkStore();
        store.Rows.AddRange(new[] {new OrganizationLink {LinkId = 1}, new OrganizationLink {LinkId = 2}});
        Assert.IsType<RedirectToActionResult>(await Create(store).Delete(1));
        Assert.Equal(2, Assert.Single(store.Rows).LinkId);
    }

    private static OrganizationLinksController Create(LinkStore store)
    {
        var controller = new OrganizationLinksController(store) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new NoTempData());
        return controller;
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class LinkStore : IRepository<OrganizationLink>
    {
        public List<OrganizationLink> Rows { get; } = new();
        public IQueryable<OrganizationLink> Query() => Rows.AsQueryable();
        public Task<OrganizationLink?> FindAsync(object? id) => Task.FromResult(Rows.FirstOrDefault(r => r.LinkId == (int?)id));
        public Task<List<OrganizationLink>> ListAsync() => Task.FromResult(Rows.ToList());
        public Task AddAsync(OrganizationLink entity) { entity.LinkId = Rows.Count + 1; Rows.Add(entity); return Task.CompletedTask; }
        public void Update(OrganizationLink entity) { }
        public void Remove(OrganizationLink entity) => Rows.Remove(entity);
        public Task<bool> AnyAsync() => Task.FromResult(Rows.Count != 0);
        public Task<int> CountAsync() => Task.FromResult(Rows.Count);
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }
}
