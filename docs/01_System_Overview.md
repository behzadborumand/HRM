# سیستم منابع انسانی HRMS_BHR

## معرفی
این سیستم بر اساس ساختار و ماژول‌های موجود در نمونه BambooHR طراحی شده است.

## ماژول‌های اصلی
- مدیریت کارکنان (Employees)
- اطلاعات شخصی (Personal Information)
- اطلاعات شغلی و سازمانی (Job/Employment)
- مرخصی و تایم آف (Time Off)
- گزارش‌ها (Reports)
- مدارک و فایل‌ها (Documents)
- آموزش‌ها (Training)
- مدیریت دسترسی (Users/Roles)
- درخواست‌های تغییر (Change Requests/Workflows)

## اتصال به دیتابیس
Connection String:
Server=localhost;Database=HRMS_BHR;Integrated Security=True;TrustServerCertificate=True

## معماری پیشنهادی
- Backend: ASP.NET Core Web API / Clean Architecture
- Frontend: ASP.NET Core MVC یا Angular
- Database: SQL Server
