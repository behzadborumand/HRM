USE HRMS_BHR;
GO

-- Companies
IF OBJECT_ID('dbo.Companies','U') IS NOT NULL DROP TABLE dbo.Companies;
CREATE TABLE dbo.Companies (
  CompanyId INT IDENTITY(1,1) PRIMARY KEY,
  CompanyName NVARCHAR(200) NOT NULL,
  LogoPath NVARCHAR(500),
  CreatedDate DATETIME DEFAULT GETDATE()
);

-- Employees
IF OBJECT_ID('dbo.Employees','U') IS NOT NULL DROP TABLE dbo.Employees;
CREATE TABLE dbo.Employees (
  EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
  CompanyId INT NULL REFERENCES dbo.Companies(CompanyId),
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
);

-- EmployeePersonalInfo
IF OBJECT_ID('dbo.EmployeePersonalInfo','U') IS NOT NULL DROP TABLE dbo.EmployeePersonalInfo;
CREATE TABLE dbo.EmployeePersonalInfo (
  PersonalInfoId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
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
);

-- JobTitles
IF OBJECT_ID('dbo.JobTitles','U') IS NOT NULL DROP TABLE dbo.JobTitles;
CREATE TABLE dbo.JobTitles (
  JobTitleId INT IDENTITY(1,1) PRIMARY KEY,
  TitleName NVARCHAR(200) NOT NULL,
  Description NVARCHAR(MAX)
);

-- Departments
IF OBJECT_ID('dbo.Departments','U') IS NOT NULL DROP TABLE dbo.Departments;
CREATE TABLE dbo.Departments (
  DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
  DepartmentName NVARCHAR(200) NOT NULL,
  ParentDepartmentId INT NULL
);

-- EmployeeJobs
IF OBJECT_ID('dbo.EmployeeJobs','U') IS NOT NULL DROP TABLE dbo.EmployeeJobs;
CREATE TABLE dbo.EmployeeJobs (
  EmployeeJobId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
  JobTitleId INT NULL REFERENCES dbo.JobTitles(JobTitleId),
  DepartmentId INT NULL REFERENCES dbo.Departments(DepartmentId),
  ManagerId INT NULL REFERENCES dbo.Employees(EmployeeId),
  EmploymentType NVARCHAR(50),
  PayType NVARCHAR(50),
  PayRate DECIMAL(18,2),
  StartDate DATE NOT NULL,
  EndDate DATE NULL,
  IsCurrent BIT DEFAULT 1
);

-- TimeOffPolicies
IF OBJECT_ID('dbo.TimeOffPolicies','U') IS NOT NULL DROP TABLE dbo.TimeOffPolicies;
CREATE TABLE dbo.TimeOffPolicies (
  PolicyId INT IDENTITY(1,1) PRIMARY KEY,
  PolicyName NVARCHAR(200) NOT NULL,
  CompanyId INT NULL REFERENCES dbo.Companies(CompanyId),
  Description NVARCHAR(MAX),
  Category NVARCHAR(50) NULL,
  AnnualHours DECIMAL(10,2) NOT NULL DEFAULT 0,
  Active BIT DEFAULT 1
);

-- TimeOffBalances
IF OBJECT_ID('dbo.TimeOffBalances','U') IS NOT NULL DROP TABLE dbo.TimeOffBalances;
CREATE TABLE dbo.TimeOffBalances (
  BalanceId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
  PolicyId INT NOT NULL REFERENCES dbo.TimeOffPolicies(PolicyId),
  Year INT NOT NULL,
  AccruedHours DECIMAL(10,2) DEFAULT 0,
  UsedHours DECIMAL(10,2) DEFAULT 0,
  LastUpdated DATETIME DEFAULT GETDATE()
);

-- TimeOffRequests
IF OBJECT_ID('dbo.TimeOffRequests','U') IS NOT NULL DROP TABLE dbo.TimeOffRequests;
CREATE TABLE dbo.TimeOffRequests (
  RequestId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
  PolicyId INT NOT NULL REFERENCES dbo.TimeOffPolicies(PolicyId),
  StartDate DATE NOT NULL,
  EndDate DATE NOT NULL,
  TotalHours DECIMAL(10,2),
  Reason NVARCHAR(MAX),
  Status NVARCHAR(50) DEFAULT 'Pending',
  RequestedBy INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
  ApprovedBy INT NULL REFERENCES dbo.Employees(EmployeeId),
  RequestedDate DATETIME DEFAULT GETDATE(),
  ApprovedDate DATETIME NULL,
  Comments NVARCHAR(MAX)
);

-- Roles
IF OBJECT_ID('dbo.Roles','U') IS NOT NULL DROP TABLE dbo.Roles;
CREATE TABLE dbo.Roles (
  RoleId INT IDENTITY(1,1) PRIMARY KEY,
  RoleName NVARCHAR(100) NOT NULL,
  Description NVARCHAR(200),
  Permissions NVARCHAR(MAX)
);

-- Users
IF OBJECT_ID('dbo.Users','U') IS NOT NULL DROP TABLE dbo.Users;
CREATE TABLE dbo.Users (
  UserId INT IDENTITY(1,1) PRIMARY KEY,
  EmployeeId INT NOT NULL REFERENCES dbo.Employees(EmployeeId),
  Username NVARCHAR(100) UNIQUE NOT NULL,
  PasswordHash NVARCHAR(500) NOT NULL,
  RoleId INT NOT NULL REFERENCES dbo.Roles(RoleId),
  IsActive BIT DEFAULT 1,
  LastLogin DATETIME NULL,
  CreatedDate DATETIME DEFAULT GETDATE()
);
GO
