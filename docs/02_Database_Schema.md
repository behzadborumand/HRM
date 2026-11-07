# طراحی دیتابیس HRMS_BHR

## جداول اصلی

### 1. Companies
```sql
Companies (
  CompanyId INT IDENTITY(1,1) PRIMARY KEY,
  CompanyName NVARCHAR(200) NOT NULL,
  LogoPath NVARCHAR(500),
  CreatedDate DATETIME DEFAULT GETDATE()
)
```

### 2. Employees
```sql
Employees (
  EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
  CompanyId INT FOREIGN KEY REFERENCES Companies(CompanyId),
  EmployeeNumber NVARCHAR(50) UNIQUE,
  FirstName NVARCHAR(100) NOT NULL,
  LastName NVARCHAR(100) NOT NULL,
  PreferredName NVARCHAR(100),
  Email NVARCHAR(200),
  Phone NVARCHAR(50),
  DateOfBirth DATE,
  Gender NVARCHAR(20),
  MaritalStatus NVARCHAR(20),
  HireDate DATE NOT NULL,
  TerminationDate DATE NULL,
  EmploymentStatus NVARCHAR(50) DEFAULT 'Active',
  AvatarPath NVARCHAR(500),
  SSNEncrypted NVARCHAR(500),
  CreatedDate DATETIME DEFAULT GETDATE(),
  ModifiedDate DATETIME DEFAULT GETDATE()
)
```

### 3. EmployeePersonalInfo
```sql
EmployeePersonalInfo (
  PersonalInfoId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  AddressLine1 NVARCHAR(200),
  AddressLine2 NVARCHAR(200),
  City NVARCHAR(100),
  State NVARCHAR(100),
  PostalCode NVARCHAR(20),
  Country NVARCHAR(100),
  HomePhone NVARCHAR(50),
  MobilePhone NVARCHAR(50),
  EmergencyContactName NVARCHAR(200),
  EmergencyContactPhone NVARCHAR(50),
  EmergencyContactRelation NVARCHAR(100)
)
```

### 4. JobTitles
```sql
JobTitles (
  JobTitleId INT IDENTITY(1,1) PRIMARY KEY,
  TitleName NVARCHAR(200) NOT NULL,
  Description NVARCHAR(MAX)
)
```

### 5. Departments
```sql
Departments (
  DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
  DepartmentName NVARCHAR(200) NOT NULL,
  ParentDepartmentId INT NULL
)
```

### 6. EmployeeJobs
```sql
EmployeeJobs (
  EmployeeJobId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  JobTitleId INT FOREIGN KEY REFERENCES JobTitles(JobTitleId),
  DepartmentId INT FOREIGN KEY REFERENCES Departments(DepartmentId),
  ManagerId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  EmploymentType NVARCHAR(50),
  PayType NVARCHAR(50),
  PayRate DECIMAL(18,2),
  StartDate DATE NOT NULL,
  EndDate DATE NULL,
  IsCurrent BIT DEFAULT 1
)
```

### 7. TimeOffPolicies
```sql
TimeOffPolicies (
  PolicyId INT IDENTITY(1,1) PRIMARY KEY,
  PolicyName NVARCHAR(200) NOT NULL,
  CompanyId INT FOREIGN KEY REFERENCES Companies(CompanyId),
  Description NVARCHAR(MAX),
  Active BIT DEFAULT 1
)
```

### 8. TimeOffBalances
```sql
TimeOffBalances (
  BalanceId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  PolicyId INT FOREIGN KEY REFERENCES TimeOffPolicies(PolicyId),
  Year INT NOT NULL,
  AccruedHours DECIMAL(10,2) DEFAULT 0,
  UsedHours DECIMAL(10,2) DEFAULT 0,
  LastUpdated DATETIME DEFAULT GETDATE()
)
```

### 9. TimeOffRequests
```sql
TimeOffRequests (
  RequestId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  PolicyId INT FOREIGN KEY REFERENCES TimeOffPolicies(PolicyId),
  StartDate DATE NOT NULL,
  EndDate DATE NOT NULL,
  TotalHours DECIMAL(10,2),
  Reason NVARCHAR(MAX),
  Status NVARCHAR(50) DEFAULT 'Pending',
  RequestedBy INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  ApprovedBy INT NULL FOREIGN KEY REFERENCES Employees(EmployeeId),
  RequestedDate DATETIME DEFAULT GETDATE(),
  ApprovedDate DATETIME NULL,
  Comments NVARCHAR(MAX)
)
```

### 10. Users
```sql
Users (
  UserId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT FOREIGN KEY REFERENCES Employees(EmployeeId),
  Username NVARCHAR(100) UNIQUE NOT NULL,
  PasswordHash NVARCHAR(500) NOT NULL,
  RoleId INT NOT NULL,
  IsActive BIT DEFAULT 1,
  LastLogin DATETIME NULL,
  CreatedDate DATETIME DEFAULT GETDATE()
)
```

### 11. Roles
```sql
Roles (
  RoleId INT IDENTITY(1,1) PRIMARY KEY,
  RoleName NVARCHAR(100) NOT NULL,
  Description NVARCHAR(200),
  Permissions NVARCHAR(MAX)
)
```

### 12. EmployeeDocuments
اسکریپت `sql/05_Create_Employee_Documents.sql` (فاز ۱۱)
```sql
EmployeeDocuments (
  DocumentId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeId),
  Title NVARCHAR(200) NOT NULL,
  Category NVARCHAR(50) NULL,
  FileName NVARCHAR(260) NOT NULL,      -- نام اصلی فایل، فقط برای نمایش و دانلود
  StoredPath NVARCHAR(400) NOT NULL,    -- مسیر نسبی داخل App_Data/uploads
  ContentType NVARCHAR(120) NULL,
  FileSize BIGINT NOT NULL DEFAULT 0,
  UploadedBy INT NULL,
  UploadDate DATETIME NOT NULL DEFAULT GETDATE(),
  ExpiryDate DATETIME NULL
)
```

### 13. TrainingCourses
اسکریپت `sql/06_Create_Training.sql` (فاز ۱۵)
```sql
TrainingCourses (
  CourseId INT IDENTITY(1,1) PRIMARY KEY,
  Title NVARCHAR(200) NOT NULL,
  Category NVARCHAR(50) NULL,
  Provider NVARCHAR(200) NULL,
  Description NVARCHAR(1000) NULL,
  RenewalDays INT NULL,                 -- اگر پر باشد، پس از انجام «سررسید بعدی» محاسبه می‌شود
  MandatoryForAll BIT NOT NULL DEFAULT 0,
  Active BIT NOT NULL DEFAULT 1,        -- خارج‌سازی به‌جای حذف، تا ثبت‌های تاریخی سالم بمانند
  CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
)
```

### 14. TrainingAssignments
اسکریپت `sql/06_Create_Training.sql` (فاز ۱۵)
```sql
TrainingAssignments (
  AssignmentId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeId),
  CourseId INT NOT NULL FOREIGN KEY REFERENCES TrainingCourses(CourseId),
  AssignedDate DATETIME NOT NULL DEFAULT GETDATE(),
  DueDate DATETIME NULL,                -- بی‌تاریخ یعنی «بدون سررسید»
  CompletedDate DATETIME NULL,          -- تنها منبع وضعیت؛ ستون Status وجود ندارد
  CertificateNumber NVARCHAR(100) NULL,
  Notes NVARCHAR(500) NULL,
  AssignedBy INT NULL                   -- userid ثبت‌کننده، بدون FK تا حذف کاربر ثبت را از بین نبرد
)
CREATE INDEX IX_TrainingAssignments_EmployeeId ON TrainingAssignments(EmployeeId, DueDate);
CREATE INDEX IX_TrainingAssignments_CourseId ON TrainingAssignments(CourseId);
```

> وضعیت آموزش (`Pending` / `Overdue` / `Completed`) در هیچ جدولی ذخیره نمی‌شود؛ در `TrainingRules.ResolveStatus` از `CompletedDate` و `DueDate` محاسبه می‌گردد تا اصلاح تاریخ، خودکار وضعیت را هم درست کند.

## پیوندهای سازمانی — فاز ۱۷

اسکریپت تکرارپذیر `sql/07_Create_Organization_Links.sql` جدول پانزدهم را ایجاد می‌کند:

```sql
OrganizationLinks (
  LinkId INT IDENTITY PRIMARY KEY,
  Title NVARCHAR(100) NOT NULL,
  Url NVARCHAR(2000) NOT NULL,
  Description NVARCHAR(300) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Active BIT NOT NULL DEFAULT 1
)
```

پیوندها عمومیِ داخل سامانه‌اند و همهٔ کاربران واردشده آن‌ها را در Home می‌بینند؛ به کارمند یا شرکت خاصی متصل نیستند. فقط ردیف فعال نمایش داده می‌شود؛ ترتیب با `SortOrder` سپس عنوان و شناسه است. برنامه پیش از ذخیره و نمایش، HTTP/HTTPS بودن نشانی و نبود مشخصات ورود را کنترل می‌کند. اسکریپت باید پیش از اجرای نسخهٔ جدید اعمال شود؛ Seed دادهٔ نمایشی ایجاد نمی‌کند.
