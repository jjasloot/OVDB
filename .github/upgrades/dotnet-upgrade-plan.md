# .NET 10 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade OVDB_database\OVDB_database.csproj to .NET 10
4. Upgrade OV_DB\OV_DB.csproj to .NET 10
5. Upgrade OV_DB.Tests\OV_DB.Tests.csproj to .NET 10
6. Run unit tests to validate upgrade in the projects listed below:
   - OV_DB.Tests\OV_DB.Tests.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

No projects are excluded from this upgrade.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                         | Current Version | New Version | Description                     |
|:-----------------------------------------------------|:---------------:|:-----------:|:--------------------------------|
| Microsoft.AspNetCore.Authentication.JwtBearer        | 9.0.10          | 10.0.1      | Recommended for .NET 10         |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson              | 9.0.7           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.AspNetCore.SpaServices.Extensions          | 9.0.10          | 10.0.1      | Recommended for .NET 10         |
| Microsoft.EntityFrameworkCore.Design                 | 9.0.7;9.0.8     | 10.0.1      | Recommended for .NET 10         |
| Microsoft.EntityFrameworkCore.Sqlite                 | 9.0.7           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.EntityFrameworkCore.Sqlite.NetTopologySuite| 9.0.8           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.EntityFrameworkCore.SqlServer              | 9.0.7           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.EntityFrameworkCore.Tools                  | 9.0.7           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.Extensions.Caching.Memory                  | 9.0.8           | 10.0.1      | Recommended for .NET 10         |
| Microsoft.VisualStudio.Web.CodeGeneration.Design     | 9.0.0           | 10.0.0      | Recommended for .NET 10         |
| Newtonsoft.Json                                      | 13.0.3          | 13.0.4      | Recommended for .NET 10         |
| System.Drawing.Common                                | 9.0.7           | 10.0.1      | Recommended for .NET 10         |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### OVDB_database\OVDB_database.csproj modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
- Microsoft.EntityFrameworkCore.Design should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.Sqlite should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.Sqlite.NetTopologySuite should be updated from `9.0.8` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.SqlServer should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.Tools should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)
- Newtonsoft.Json should be updated from `13.0.3` to `13.0.4` (*recommended for .NET 10*)

#### OV_DB\OV_DB.csproj modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
- Microsoft.AspNetCore.Authentication.JwtBearer should be updated from `9.0.10` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.AspNetCore.Mvc.NewtonsoftJson should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.AspNetCore.SpaServices.Extensions should be updated from `9.0.10` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.Design should be updated from `9.0.8` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.Extensions.Caching.Memory should be updated from `9.0.8` to `10.0.1` (*recommended for .NET 10*)
- Microsoft.VisualStudio.Web.CodeGeneration.Design should be updated from `9.0.0` to `10.0.0` (*recommended for .NET 10*)
- System.Drawing.Common should be updated from `9.0.7` to `10.0.1` (*recommended for .NET 10*)

#### OV_DB.Tests\OV_DB.Tests.csproj modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`
