# ساختار پروژه HRMS_BHR

## راه‌اندازی شده
- دیتابیس SQL Server با نام HRMS_BHR ایجاد و جداول، ایندکس و نماها ساخته شده
- Solution با پروژه‌های Core، Data، Web ایجاد شده
- وبسایت ASP.NET Core MVC با Bootstrap RTL فارسی طراحی شده

## مسیرهای مهم
- Connection String در appsettings.json
- کنترلرها و ویوهای فارسی در Views/Controllers
- اسکریپت‌های SQL در sql/
- مستندات در docs/

## نحوه اجرا
1. اطمینان از در دسترس بودن SQL Server (localhost)
2. اجرای پروژه Web از طریق dotnet run یا Visual Studio
