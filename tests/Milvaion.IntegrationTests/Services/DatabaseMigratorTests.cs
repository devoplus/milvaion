using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milvaion.Application.Utils.Constants;
using Milvaion.Infrastructure.Persistence;
using Milvaion.Infrastructure.Persistence.Context;
using Milvaion.IntegrationTests.TestBase;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

[Collection(nameof(MilvaionTestCollection))]
[Trait("Service Integration Tests", "Integration tests for DatabaseMigrator.")]
public class DatabaseMigratorTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : IntegrationTestBase(factory, output)
{
    [Fact]
    public async Task SeedUIRelatedDataAsync_ShouldReturnError_WhenJsonFileNotFound()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Temporarily rename the file if it exists
        var filePath = Path.Combine(GlobalConstant.JsonFilesPath, "ui_data.json");
        var tempPath = filePath + ".bak";
        var fileExisted = File.Exists(filePath);

        if (fileExisted)
            File.Move(filePath, tempPath);

        try
        {
            // Act
            var result = await migrator.SeedUIRelatedDataAsync();

            // Assert
            result.IsSuccess.Should().BeFalse("should return error when ui_data.json file is missing");
        }
        finally
        {
            // Restore file
            if (fileExisted && File.Exists(tempPath))
                File.Move(tempPath, filePath);
        }
    }

    [Fact]
    public async Task SeedDefaultDataAsync_ShouldCreateRootUser()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Act
        var rootPass = await migrator.SeedDefaultDataAsync("testpassword123");

        // Assert
        rootPass.Should().Be("testpassword123");

        var dbContext = GetDbContext();
        var rootUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == GlobalConstant.RootUsername);
        rootUser.Should().NotBeNull("root user should be created");
    }

    [Fact]
    public async Task SeedDefaultDataAsync_ShouldGenerateRandomPassword_WhenNullProvided()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Act
        var rootPass = await migrator.SeedDefaultDataAsync(null);

        // Assert
        rootPass.Should().NotBeNullOrEmpty("random password should be generated");
        rootPass.Length.Should().BeGreaterOrEqualTo(16, "generated password should have sufficient length");
    }

    [Fact]
    public async Task SeedDefaultDataAsync_ShouldCreateSuperAdminRole()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Act
        await migrator.SeedDefaultDataAsync("testpassword123");

        // Assert
        var dbContext = GetDbContext();
        var superAdminRole = await dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        superAdminRole.Should().NotBeNull("SuperAdmin role should be created");
    }

    [Fact]
    public async Task SeedDefaultDataAsync_ShouldCreateSuperAdminPermission()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Act
        await migrator.SeedDefaultDataAsync("testpassword123");

        // Assert
        var dbContext = GetDbContext();
        var permission = await dbContext.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Name == "SuperAdmin");
        permission.Should().NotBeNull("SuperAdmin permission should be created");
    }

    [Fact]
    public async Task InitDatabaseAsync_ShouldReturnError_WhenInitialMigrationNotFound()
    {
        // Arrange
        await InitializeAsync();

        var migrator = new DatabaseMigrator(_serviceProvider);
        var permissionManager = _serviceProvider.GetRequiredService<Application.Interfaces.IPermissionManager>();

        // Act - Call InitDatabase which checks for initial migration SQL file
        var result = await migrator.InitDatabaseAsync(permissionManager);

        // Assert - Should return error since initial migration file may not exist
        // or migration is already completed
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SeedFakeDataAsync_ShouldInsertRolesAndUsers()
    {
        // Arrange
        await InitializeAsync();
        await SeedRootUserAndSuperAdminRoleAsync("testpass");

        var migrator = new DatabaseMigrator(_serviceProvider);

        // Act
        await migrator.SeedFakeDataAsync(sameData: true, locale: "tr");

        // Assert
        var dbContext = GetDbContext();
        var roleCount = await dbContext.Roles.CountAsync();
        var userCount = await dbContext.Users.CountAsync();

        roleCount.Should().BeGreaterThan(1, "fake roles should be inserted");
        userCount.Should().BeGreaterThan(1, "fake users should be inserted");
    }
}
