# HRMS_BHR

سیستم مدیریت منابع انسانی طراحی شده بر اساس نمونه‌های موجود در پوشه Save site (BambooHR-like).

## ساختار پروژه
- docs/ - مستندات طراحی سیستم، اسکیمای دیتابیس، طراحی UI
- [دستورالعمل به‌روزرسانی پرونده بر اساس تصاویر و منابع BambooHR](docs/13_BambooHR_Profile_Update_Instructions.md)
- [اصلاحات تأییدشدهٔ صفحهٔ اصلی و نتیجهٔ بررسی](docs/14_Home_Approved_Improvements.md)
- [ادامهٔ فازهای ۱۶ تا ۱۸ و نتیجهٔ بررسی](docs/15_Home_Next_Phases.md)
- sql/ - اسکریپت‌های SQL برای ایجاد دیتابیس و جداول
- src/ - کد منبع پروژه

## اتصال به دیتابیس
```json
"ConnectionStrings": {
  "HRMSDatabase": "Server=localhost;Database=HRMS_BHR;Integrated Security=True;TrustServerCertificate=True"
}
```

## راه‌اندازی دیتابیس
با استفاده از SQL Server Management Studio یا sqlcmd اسکریپت‌های زیر را به ترتیب اجرا کنید:
1. sql/01_Create_Database.sql
2. sql/02_Create_Tables.sql
3. sql/03_Create_Indexes_Views.sql
4. sql/04_Alter_TimeOff_Accrual.sql
5. sql/05_Create_Employee_Documents.sql
6. sql/06_Create_Training.sql
7. sql/07_Create_Organization_Links.sql

### ارتقای نسخهٔ موجود

اگر اسکریپت‌های ۰۱ تا ۰۶ قبلاً اعمال شده‌اند، **پیش از اجرای نسخهٔ جدید** اسکریپت ۰۷ را اجرا کنید. صفحهٔ اصلی به جدول `OrganizationLinks` نیاز دارد؛ ایجاد جدول در شروع برنامه خودکار نیست. این اسکریپت تکرارپذیر است و دادهٔ قبلی را حذف نمی‌کند.

```powershell
sqlcmd -S localhost -E -b -i sql/07_Create_Organization_Links.sql
dotnet run --project src/HRMS.Web
```
