USE HRMS_BHR;
GO

-- Indexes
CREATE INDEX IX_Employees_Email ON dbo.Employees(Email);
CREATE INDEX IX_EmployeeJobs_EmployeeId_IsCurrent ON dbo.EmployeeJobs(EmployeeId, IsCurrent);
CREATE INDEX IX_TimeOffRequests_EmployeeId_Status ON dbo.TimeOffRequests(EmployeeId, Status);
CREATE INDEX IX_TimeOffBalances_EmployeeId_Year ON dbo.TimeOffBalances(EmployeeId, Year);

-- View: vw_EmployeeDetails
IF OBJECT_ID('dbo.vw_EmployeeDetails','V') IS NOT NULL DROP VIEW dbo.vw_EmployeeDetails;
GO
CREATE VIEW dbo.vw_EmployeeDetails
AS
SELECT 
  e.EmployeeId,
  e.EmployeeNumber,
  e.FirstName,
  e.LastName,
  e.PreferredName,
  e.Email,
  e.Phone,
  e.HireDate,
  e.EmploymentStatus,
  e.CompanyId,
  c.CompanyName,
  jt.TitleName AS JobTitle,
  d.DepartmentName AS Department,
  m.FirstName + ' ' + m.LastName AS ManagerName,
  ej.EmploymentType,
  ej.PayType,
  ej.PayRate
FROM dbo.Employees e
LEFT JOIN dbo.Companies c ON e.CompanyId = c.CompanyId
LEFT JOIN dbo.EmployeeJobs ej ON ej.EmployeeId = e.EmployeeId AND ej.IsCurrent = 1
LEFT JOIN dbo.JobTitles jt ON ej.JobTitleId = jt.JobTitleId
LEFT JOIN dbo.Departments d ON ej.DepartmentId = d.DepartmentId
LEFT JOIN dbo.Employees m ON ej.ManagerId = m.EmployeeId;
GO

-- View: vw_TimeOffSummary
IF OBJECT_ID('dbo.vw_TimeOffSummary','V') IS NOT NULL DROP VIEW dbo.vw_TimeOffSummary;
GO
CREATE VIEW dbo.vw_TimeOffSummary
AS
SELECT 
  e.EmployeeId,
  e.FirstName + ' ' + e.LastName AS EmployeeName,
  p.PolicyName,
  b.Year,
  b.AccruedHours,
  b.UsedHours,
  (b.AccruedHours - b.UsedHours) AS AvailableHours,
  b.LastUpdated
FROM dbo.TimeOffBalances b
JOIN dbo.Employees e ON b.EmployeeId = e.EmployeeId
JOIN dbo.TimeOffPolicies p ON b.PolicyId = p.PolicyId;
GO
