USE HRMS_BHR;
GO

-- افزودن ستون‌های محاسبه مانده به جدول سیاست‌های مرخصی (روی دیتابیس‌های ایجادشده قبل از فاز ۷)
IF COL_LENGTH('dbo.TimeOffPolicies', 'Category') IS NULL
    ALTER TABLE dbo.TimeOffPolicies ADD Category NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.TimeOffPolicies', 'AnnualHours') IS NULL
    ALTER TABLE dbo.TimeOffPolicies ADD AnnualHours DECIMAL(10,2) NOT NULL
        CONSTRAINT DF_TimeOffPolicies_AnnualHours DEFAULT 0;
GO

-- یک رکورد مانده برای هر کارمند/سیاست/سال
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TimeOffBalances_Employee_Policy_Year'
               AND object_id = OBJECT_ID('dbo.TimeOffBalances'))
    CREATE UNIQUE INDEX UX_TimeOffBalances_Employee_Policy_Year
        ON dbo.TimeOffBalances(EmployeeId, PolicyId, Year);
GO

-- تسریع گردش کار تایید درخواست‌ها
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TimeOffRequests_Status_StartDate'
               AND object_id = OBJECT_ID('dbo.TimeOffRequests'))
    CREATE INDEX IX_TimeOffRequests_Status_StartDate
        ON dbo.TimeOffRequests(Status, StartDate);
GO
