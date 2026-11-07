namespace HRMS.Web.Helpers
{
    public static class FaLabels
    {
        public static string EmploymentStatus(string status) => status switch
        {
            "Active" => "فعال",
            "OnLeave" => "در مرخصی",
            "Terminated" => "خروج قطعی",
            "Inactive" => "غیرفعال",
            _ => status
        };

        public static string Gender(string? gender) => gender switch
        {
            "Male" => "مرد",
            "Female" => "زن",
            _ => "-"
        };

        public static string MaritalStatus(string? status) => status switch
        {
            "Single" => "مجرد",
            "Married" => "متاهل",
            _ => "-"
        };

        public static string EmploymentType(string? type) => type switch
        {
            "FullTime" => "تمام وقت",
            "PartTime" => "پاره وقت",
            "Contract" => "قراردادی",
            "Intern" => "کارآموز",
            _ => "-"
        };

        public static string PayType(string? type) => type switch
        {
            "Salary" => "حقوق ثابت",
            "Hourly" => "ساعتی",
            _ => "-"
        };

        public static string RequestStatus(string status) => status switch
        {
            "Pending" => "در انتظار تایید",
            "Approved" => "تایید شده",
            "Rejected" => "رد شده",
            "Cancelled" => "لغو شده",
            _ => status
        };

        public static string RequestStatusBadge(string status) => status switch
        {
            "Pending" => "warning",
            "Approved" => "success",
            "Rejected" => "danger",
            "Cancelled" => "secondary",
            _ => "info"
        };

        public static string EmploymentStatusBadge(string status) => status switch
        {
            "Active" => "success",
            "OnLeave" => "warning",
            "Terminated" => "danger",
            _ => "secondary"
        };

        public static string PolicyCategory(string? category) => category switch
        {
            "Vacation" => "استحقاقی",
            "Sick" => "استعلاجی",
            "Unpaid" => "بدون حقوق",
            "Special" => "ویژه",
            _ => "-"
        };

        public static string TrainingStatus(string status) => status switch
        {
            "Pending" => "در انتظار انجام",
            "Overdue" => "از موعد گذشته",
            "Completed" => "انجام شده",
            _ => status
        };

        public static string TrainingStatusBadge(string status) => status switch
        {
            "Pending" => "warning",
            "Overdue" => "danger",
            "Completed" => "success",
            _ => "secondary"
        };

        /// <summary>متن کمِ روزهای باقی‌مانده تا سررسید، با ارقام فارسی.</summary>
        public static string TrainingDueHint(int? daysRemaining) => daysRemaining switch
        {
            null => "بدون سررسید",
            < 0 => $"{PersianDate.ToPersianDigits((-daysRemaining.Value).ToString())} روز از موعد گذشته",
            0 => "امروز مهلت است",
            _ => $"{PersianDate.ToPersianDigits(daysRemaining.Value.ToString())} روز مانده"
        };
    }
}
