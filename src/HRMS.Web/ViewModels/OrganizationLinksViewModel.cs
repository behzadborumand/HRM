using System.ComponentModel.DataAnnotations;
using HRMS.Core.Entities;

namespace HRMS.Web.ViewModels;

public class OrganizationLinksViewModel
{
    public int? LinkId { get; set; }
    [Required(ErrorMessage = "عنوان پیوند الزامی است")]
    [StringLength(100, ErrorMessage = "عنوان حداکثر ۱۰۰ نویسه است")]
    [Display(Name = "عنوان پیوند")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "نشانی پیوند الزامی است")]
    [StringLength(2000, ErrorMessage = "نشانی حداکثر ۲۰۰۰ نویسه است")]
    [Display(Name = "نشانی وب")]
    public string Url { get; set; } = string.Empty;
    [StringLength(300, ErrorMessage = "توضیح حداکثر ۳۰۰ نویسه است")]
    [Display(Name = "توضیح کوتاه")]
    public string? Description { get; set; }
    [Range(0, 9999, ErrorMessage = "ترتیب باید بین ۰ و ۹۹۹۹ باشد")]
    [Display(Name = "ترتیب نمایش")]
    public int SortOrder { get; set; }
    [Display(Name = "فعال")]
    public bool Active { get; set; }
    public List<OrganizationLink> Rows { get; set; } = new();
}
