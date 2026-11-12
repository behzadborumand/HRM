using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Pages.Employees;
[Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PersonalModel(AccessService access,EmployeeService employees,EmployeePersonalService personal):PageModel
{
    [BindProperty,ValidateNever] public PersonalContactForm Contact {get;set;}=new();
    [BindProperty,ValidateNever] public PersonalIdentityForm Identity {get;set;}=new();
    public Employee? Employee {get;private set;}
    public bool CanEditIdentity {get;private set;}
    public bool CanEditContact {get;private set;}
    public async Task<IActionResult> OnGetAsync(int id,CancellationToken ct)
    {
        var actor=await access.GetActiveAsync(User,ct);
        if(actor is null||!EmployeePersonalService.CanRead(actor,id))return Forbid();
        if(!await LoadAsync(actor,id,ct))return NotFound();
        return Page();
    }
    public async Task<IActionResult> OnPostContactAsync(int id,CancellationToken ct)
    {
        var actor=await access.GetActiveAsync(User,ct);
        if(actor is null||!EmployeePersonalService.CanRead(actor,id))return Forbid();
        var submitted=Contact;
        if(!await LoadAsync(actor,id,ct))return NotFound();
        Contact=submitted;
        if(!CanEditContact)return Forbid();
        TryValidateModel(Contact,nameof(Contact));
        if(!ModelState.IsValid){ModelState.AddModelError("","اطلاعات تماس معتبر نیست؛ طول فیلدها را بررسی کنید.");return Page();}
        try
        {
            await personal.SaveContactAsync(actor,id,Contact.ToInput(),Version(Contact.Version),ct);
            TempData["PersonalMessage"]="اطلاعات تماس ذخیره شد.";
            return RedirectToPage("Details",null,new{id},"tab-personal");
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception error)when(error is ArgumentException or FormatException or DbUpdateConcurrencyException){Error(error);}
        return Page();
    }
    public async Task<IActionResult> OnPostIdentityAsync(int id,CancellationToken ct)
    {
        var actor=await access.GetActiveAsync(User,ct);
        if(actor is not {IsActive:true,IsAdmin:true})return Forbid();
        var submitted=Identity;
        if(!await LoadAsync(actor,id,ct))return NotFound();
        Identity=submitted;
        TryValidateModel(Identity,nameof(Identity));
        if(!ModelState.IsValid){ModelState.AddModelError("","اطلاعات هویتی معتبر نیست؛ طول فیلدها را بررسی کنید.");return Page();}
        try
        {
            await personal.SaveIdentityAsync(actor,id,Identity.ToInput(),Version(Identity.Version),ct);
            TempData["PersonalMessage"]="اطلاعات هویتی ذخیره شد.";
            return RedirectToPage("Details",null,new{id},"tab-personal");
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception error)when(error is ArgumentException or FormatException or DbUpdateConcurrencyException){Error(error);}
        return Page();
    }
    private async Task<bool> LoadAsync(ActorScope actor,int id,CancellationToken ct)
    {
        Employee=await employees.GetAsync(actor,id,ct);
        if(Employee is null)return false;
        var data=await personal.ReadAsync(actor,id,ct);
        CanEditIdentity=actor.IsAdmin;CanEditContact=actor.IsAdmin||Employee.TerminationDate is null;
        Contact=PersonalContactForm.From(data);Identity=PersonalIdentityForm.From(data);return true;
    }
    private static byte[]? Version(string? value)=>string.IsNullOrEmpty(value)?null:Convert.FromBase64String(value);
    private void Error(Exception error)=>ModelState.AddModelError("",error switch{
        DbUpdateConcurrencyException=>"اطلاعات شخصی تغییر کرده است؛ صفحه را بازخوانی و دوباره ویرایش کنید.",
        FormatException=>"نسخهٔ اطلاعات معتبر نیست.",_=>error.Message});
}
