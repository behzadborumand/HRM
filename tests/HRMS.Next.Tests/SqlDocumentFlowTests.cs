using System.Security.Cryptography;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlDocumentFlowTests
{
    [Fact]
    public async Task PrivateUploadDownloadAndDeleteRespectCompanyAndWriteGrant()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog,
                "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Documents SQL tests require the disposable trial database.");
        await using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Document test " + Guid.NewGuid().ToString("N") };
        organization.Companies.Add(company);
        await organization.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "Test" };
        organization.Departments.Add(department);
        await organization.SaveChangesAsync();
        var employee = new Employee
        {
            CompanyId = company.Id, DepartmentId = department.Id,
            EmployeeNumber = "DOC-" + Guid.NewGuid().ToString("N")[..12],
            FirstName = "Test", LastName = "Employee", HireDate = new DateOnly(2020, 1, 1)
        };
        personnel.Employees.Add(employee);
        await personnel.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(701, "document-admin", company.Id, true, true, true, true);
            var reader = actor with { IsAdmin = false, CanWriteEmployees = false };
            var outsider = actor with { CompanyId = company.Id + 1 };
            var service = new DocumentService(documents, new PersonnelDirectory(personnel), TimeProvider.System);
            var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x0A };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.UploadAsync(reader, employee.Id, "Contract", "HR", null, "contract.pdf", new MemoryStream(payload), CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.UploadAsync(outsider, employee.Id, "Contract", null, null, "contract.pdf", new MemoryStream(payload), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.UploadAsync(actor, employee.Id, "Contract", null, null, "payload.exe", new MemoryStream(payload), CancellationToken.None));
            var id = await service.UploadAsync(actor, employee.Id, "Contract", "HR",
                new DateOnly(2030, 1, 1), "contract.pdf", new MemoryStream(payload), CancellationToken.None);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(outsider, employee.Id, 1, 20, CancellationToken.None));
            var listed = Assert.Single((await service.ListAsync(reader, employee.Id, 1, 20, CancellationToken.None)).Rows);
            Assert.Equal(id, listed.Id);
            Assert.Equal("Contract", listed.Title);
            var secondId = await service.UploadAsync(actor, employee.Id, "Certificate", null, null, "certificate.pdf", new MemoryStream(payload), CancellationToken.None);
            var firstPage = await service.ListAsync(reader, employee.Id, 1, 1, CancellationToken.None);
            Assert.Equal(2, firstPage.Total);
            Assert.Equal(secondId, Assert.Single(firstPage.Rows).Id);
            Assert.Equal(id, Assert.Single((await service.ListAsync(reader, employee.Id, 2, 1, CancellationToken.None)).Rows).Id);
            var file = await service.DownloadAsync(reader, id, CancellationToken.None);
            Assert.NotNull(file);
            Assert.Equal(payload, file.Bytes);
            Assert.Equal(SHA256.HashData(payload), file.Sha256);
            Assert.Equal("contract.pdf", file.FileName);
            Assert.Null(await service.DownloadAsync(outsider, id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(reader, id, employee.Id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(outsider, id, employee.Id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(actor, id, employee.Id + 1, CancellationToken.None));
            await service.DeleteAsync(actor, secondId, employee.Id, CancellationToken.None);
            await service.DeleteAsync(actor, id, employee.Id, CancellationToken.None);
            Assert.Null(await service.DownloadAsync(actor, id, CancellationToken.None));
            Assert.Empty((await service.ListAsync(actor, employee.Id, 1, 20, CancellationToken.None)).Rows);
            Assert.Equal(2, await documents.Audits.CountAsync(x => x.DocumentId == id && x.CompanyId == company.Id));
        }
        finally
        {
            documents.Documents.RemoveRange(await documents.Documents.Where(x => x.CompanyId == company.Id).ToListAsync());
            documents.Audits.RemoveRange(await documents.Audits.Where(x => x.CompanyId == company.Id).ToListAsync());
            await documents.SaveChangesAsync();
            personnel.Employees.Remove(employee);
            await personnel.SaveChangesAsync();
            organization.Departments.Remove(department);
            organization.Companies.Remove(company);
            await organization.SaveChangesAsync();
        }
    }
}
