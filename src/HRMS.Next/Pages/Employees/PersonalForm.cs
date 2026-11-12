using System.ComponentModel.DataAnnotations;
using HRMS.Next.Modules.Personnel;
namespace HRMS.Next.Pages.Employees;
public sealed class PersonalContactForm
{
    [StringLength(100)] public string? PreferredName {get;set;}
    [StringLength(50)] public string? HomePhone {get;set;}
    [StringLength(50)] public string? MobilePhone {get;set;}
    [StringLength(200)] public string? AddressLine1 {get;set;}
    [StringLength(200)] public string? AddressLine2 {get;set;}
    [StringLength(100)] public string? City {get;set;}
    [StringLength(100)] public string? State {get;set;}
    [StringLength(20)] public string? PostalCode {get;set;}
    [StringLength(100)] public string? Country {get;set;}
    [StringLength(200)] public string? EmergencyContactName {get;set;}
    [StringLength(50)] public string? EmergencyContactPhone {get;set;}
    [StringLength(100)] public string? EmergencyContactRelation {get;set;}
    public string? Version {get;set;}
    public PersonalContactInput ToInput()=>new(PreferredName,HomePhone,MobilePhone,AddressLine1,AddressLine2,City,State,PostalCode,Country,EmergencyContactName,EmergencyContactPhone,EmergencyContactRelation);
    public static PersonalContactForm From(EmployeePersonalInfo? p)=>p is null?new():new(){PreferredName=p.PreferredName,HomePhone=p.HomePhone,MobilePhone=p.MobilePhone,
        AddressLine1=p.AddressLine1,AddressLine2=p.AddressLine2,City=p.City,State=p.State,PostalCode=p.PostalCode,Country=p.Country,
        EmergencyContactName=p.EmergencyContactName,EmergencyContactPhone=p.EmergencyContactPhone,EmergencyContactRelation=p.EmergencyContactRelation,Version=Convert.ToBase64String(p.RowVersion)};
}
public sealed class PersonalIdentityForm
{
    [StringLength(50)] public string? Phone {get;set;}
    public string? DateOfBirthJalali {get;set;}
    [StringLength(20)] public string? Gender {get;set;}
    [StringLength(20)] public string? MaritalStatus {get;set;}
    public string? Version {get;set;}
    public PersonalIdentityInput ToInput()
    {
        DateOnly? birth=null;
        if(!string.IsNullOrWhiteSpace(DateOfBirthJalali))
        {
            if(!JalaliDate.TryParse(DateOfBirthJalali,out var parsed))throw new ArgumentException("تاریخ تولد شمسی معتبر نیست.");
            birth=parsed;
        }
        return new(Phone,birth,Gender,MaritalStatus);
    }
    public static PersonalIdentityForm From(EmployeePersonalInfo? p)=>p is null?new():new(){Phone=p.Phone,
        DateOfBirthJalali=p.DateOfBirth is {} date?JalaliDate.Format(date):null,Gender=p.Gender,MaritalStatus=p.MaritalStatus,Version=Convert.ToBase64String(p.RowVersion)};
}
