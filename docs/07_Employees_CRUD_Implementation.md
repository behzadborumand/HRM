# پیاده‌سازی CRUD کارکنان

## وضعیت: تکمیل شده و تست شده روی اپ در حال اجرا

### معماری
```
Views/Employees/*.cshtml  →  EmployeesController  →  EmployeeService  →  IEmployeeRepository / IRepository<T>  →  HrmsDbContext  →  SQL Server (HRMS_BHR)
```

- `HRMS.Core/Repositories` — تعریف رابط‌ها (بدون وابستگی به EF)
- `HRMS.Data/Repositories` — پیاده‌سازی با EF Core ۸؛ `EmployeeRepository` با زیرپروژه‌های همبسته (correlated subquery) برای نام واحد، عنوان شغلی و مدیر مستقیم
- `HRMS.Web/Services/EmployeeService` — قواعد نگارش: یکتایی کد پرسنلی و ایمیل، upsert شغل و اطلاعات فردی، بستن بازه شغلی در خروج کارمند، گارد حذف
- `HRMS.Web/ViewModels/EmployeeViewModels` — فرم مشترک افزودن/ویرایش با اعتبارسنجی فارسی و لیست‌های انتخابی (واحدها، عناوین، مدیران، شرکت‌ها)

### اکشن‌های کنترلر
| مسیر | متد | دسترسی لازم | توضیح |
|---|---|---|---|
| `/Employees` | GET | `employees.read` | لیست با جست‌وجو، فیلتر واحد/وضعیت و صفحه‌بندی |
| `/Employees/Details/{id}` | GET | مالک پرونده یا `employees.read` | شناسنامه، مانده مرخصی، تاریخچه درخواست‌ها |
| `/Employees/Create` | GET/POST | `employees.write` | فرم افزودن (نمای مشترک `Form.cshtml`) |
| `/Employees/Edit/{id}` | GET/POST | `employees.write` | همان فرم در حالت ویرایش |
| `/Employees/Terminate` | POST | `employees.write` | ثبت تاریخ خروج و بستن شغل جاری |
| `/Employees/BulkEdit?ids=…` | GET | `employees.write` | پیش‌نمایش وضعیت فعلی انتخاب‌شده‌ها و فرم تغییر گروهی |
| `/Employees/BulkApply` | POST | `employees.write` | اعمال تغییرات روی انتخاب‌شده‌ها با ثبت سابقه شغلی |
| `/Employees/Delete/{id}` | POST | `employees.delete` | حذف با گارد سوابق مرخصی، حساب کاربری، مدیر مستقیم بودنِ دیگران و مدارک |

### نکات پیاده‌سازی که در تست‌ها تثبیت شد
- **فرهنگ پارسینگ:** `CultureInfo.DefaultThreadCurrentCulture` روی Invariant قفل شده تا فرهنگ سیستم‌عامل میزبان (fa-IR) تاریخ `yyyy-MM-dd` فرم را جلالی نخواند (۲۰۲۶ → ۲۶۴۷). ورود جلالی به جای فرهنگ، از مسیر صریح `JalaliDateModelBinder` انجام می‌شود.
- **تاریخ جلالی در فرم:** فیلدهای تاریخ `type="text"` با `dir="ltr"` هستند؛ `JalaliDateModelBinderProvider` (ثبت شده در `Program.cs`) برای هر `DateTime`/`DateTime?` جای مودربینر پیش‌فرض می‌نشیند و `PersianDate.TryParse` را صدا می‌زند: ارقام فارسی، عربی و ASCII، جداکننده `/ - .` یا فاصله، و سال ۱۲۰۰..۱۶۰۰ به‌عنوان جلالی (بقیه میلادی ISO). مقدار نامعتبر پیام فارسی می‌گیرد و خالی بودن فقط برای فیلد اجباری خطا تولید می‌کند.
- **دام `asp-for` روی DateTime:** تگ‌هلپر را به `type="datetime-local"` تبدیل و یک `__Invariant` پنهان اضافه می‌کند؛ مقدار جلالی در چنین فیلدی توسط مرورگر پذیرفته نمی‌شود و خالی ارسال می‌گردد. فیلدهای جلالی با `id`/`name` صریح و `type="text"` نوشته می‌شوند.
- **EF Core و `Update`:** موجودی که تازه `Add` شده را نباید `Update` کرد (خطای کلید موقت)؛ سرویس با پرچم `isNew` این مسیر را جدا می‌کند.
- **ترتیب حذف:** کلیدهای خارجی به‌صورت property اسکالر مپ شده‌اند، بنابراین EF خودش ترتیب DELETE را نمی‌شناسد؛ رکوردهای وابسته (شغل، اطلاعات فردی، مانده مرخصی) در یک `SaveChanges` جدا و پیش از حذف خود کارمند ثبت می‌شوند.
- **LINQ قابل ترجمه:** هیچ متد C# سفارشی داخل expression tree فراخوانی نمی‌شود؛ نام کارمند و مدیر با join در query syntax یا زیرپروژه همبسته ساخته می‌شود.
- **Anti-forgery:** صفحه‌ها بیش از یک `__RequestVerificationToken` دارند (فرم خروج در منو)؛ تست HTTP باید فقط اولین توکن را بردارد.
- **تست HTTP با متن فارسی:** اگر بدنه فرم از طریق argv به curl.exe در Git Bash داده شود، MSYS آن را به کدصفحه ANSI تبدیل و ارقام جلالی را به `?` تبدیل می‌کند (خطای سمت ابزار، نه اپ). راه درست: مقدار را در فایل UTF-8 بنویسید و `--data-urlencode "Field@C:/tmp/file.txt"` استفاده کنید. مرورگر این مشکل را ندارد.
- **آزمون خودکار:** `tests/HRMS.Tests` با xUnit (۳۶ آزمون) قواعد مرخصی، `PasswordHasher` و `PersianDate.TryParse` را می‌پوشاند؛ پروژه به `src/HRMS.sln` اضافه شده و `dotnet test src/HRMS.sln` سبز است.

### داده نمونه
Seed در استارتاپ (`DbInitializer`) به‌صورت idempotent اجرا می‌شود؛ اگر جدولی خالی باشد ۱۲ کارمند و مقادیر مرجع ساخته می‌شود. کد پرسنلی بعدی با الگوی `EMP-0001` از روی رکوردهای موجود تولید می‌گردد.
