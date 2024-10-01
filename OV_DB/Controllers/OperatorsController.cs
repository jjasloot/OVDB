using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OperatorsController : ControllerBase
{
    private readonly string _storagePath;
    private readonly OVDBDatabaseContext _dbContext;
    private readonly IMapper _mapper;

    public OperatorsController(IConfiguration configuration, OVDBDatabaseContext dbContext, IMapper mapper)
    {
        _storagePath = configuration.GetValue<string>("LogoLocation");
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
        _dbContext = dbContext;
        _mapper = mapper;
    }

    [HttpPost]
    public async Task<ActionResult<Operator>> CreateOperator(OperatorUpdateDTO createOperator)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var runsTrainsInRegions = await _dbContext.Regions.Where(r => createOperator.RunsTrainsInRegionIds.Contains(r.Id)).ToListAsync();
        var restrictToRegions = await _dbContext.Regions.Where(r => createOperator.RestrictToRegionIds.Contains(r.Id)).ToListAsync();
        var newOperator = new Operator
        {
            Names = createOperator.Names,
            RunsTrainsInRegions = runsTrainsInRegions,
            RestrictToRegions = restrictToRegions
        };
        _dbContext.Operators.Add(newOperator);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOperatorById), new { id = newOperator.Id }, newOperator);
    }

    // Read by Id
    [HttpGet("{id}")]
    public async Task<ActionResult<OperatorDTO>> GetOperatorById(int id)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var operatorDb = await _dbContext.Operators.ProjectTo<OperatorDTO>(_mapper.ConfigurationProvider).FirstOrDefaultAsync(o => o.Id == id);
        if (operatorDb == null)
        {
            return NotFound();
        }
        return operatorDb;
    }

    [HttpGet("minimal")]
    public async Task<ActionResult<List<OperatorMinimalDTO>>> GetOperatorsMinimal()
    {
        var operators = await _dbContext.Operators.ToListAsync();

        var response = new List<OperatorMinimalDTO>();
        foreach (var op in operators)
        {
            foreach (var name in op.Names)
            {
                response.Add(new OperatorMinimalDTO { Id = op.Id, Name = name });
            }
        }

        return response;
    }

    [HttpGet("forRoute/{routeId}")]
    public async Task<ActionResult<List<OperatorMinimalDTO>>> GetOperatorsForRoute(long routeId)
    {

        var regions = await _dbContext.Routes.Where(r => r.RouteId == routeId).SelectMany(r => r.Regions).Select(r => r.Id).ToListAsync();

        var operators = await _dbContext.Operators
            .Where(o => o.RestrictToRegions.Any(r => regions.Contains(r.Id)) || !o.RestrictToRegions.Any())
            .ToListAsync();

        var response = new List<OperatorMinimalDTO>();
        foreach (var op in operators)
        {
            foreach (var name in op.Names)
            {
                response.Add(new OperatorMinimalDTO { Id = op.Id, Name = name });
            }
        }

        return response;
    }

    [HttpGet("{id}/logo")]
    public async Task<ActionResult<OperatorDTO>> GetOperatorLogo(int id)
    {
        var logoInfo = await _dbContext.Operators.Where(o => o.Id == id).Select(o => new { o.LogoFilePath, o.LogoContentType }).FirstOrDefaultAsync();
        if (logoInfo?.LogoFilePath == null)
        {
            return File(new byte[0], "image/png");
        }

        return File(System.IO.File.OpenRead(Path.Combine(_storagePath, logoInfo.LogoFilePath)), logoInfo.LogoContentType);
    }

    // Read all
    [HttpGet]
    public async Task<ActionResult<List<OperatorDTO>>> GetAllOperators()
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        return await _dbContext.Operators.ProjectTo<OperatorDTO>(_mapper.ConfigurationProvider).ToListAsync();
    }

    // Update
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOperator(int id, OperatorUpdateDTO updatedOperator)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var existingOperator = await _dbContext.Operators.Include(o => o.RunsTrainsInRegions).Include(o => o.RestrictToRegions).SingleOrDefaultAsync(o => o.Id == id);
        if (existingOperator == null)
        {
            return NotFound();
        }

        existingOperator.Names = updatedOperator.Names;
        var runsTrainsInRegions = await _dbContext.Regions.Where(r => updatedOperator.RunsTrainsInRegionIds.Contains(r.Id)).ToListAsync();
        existingOperator.RunsTrainsInRegions = runsTrainsInRegions;

        var restrictToRegions = await _dbContext.Regions.Where(r => updatedOperator.RestrictToRegionIds.Contains(r.Id)).ToListAsync();
        existingOperator.RestrictToRegions = restrictToRegions;

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    // Delete
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOperator(int id)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var operatorToDelete = await _dbContext.Operators.FindAsync(id);
        if (operatorToDelete == null)
        {
            return NotFound();
        }

        _dbContext.Operators.Remove(operatorToDelete);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }


    [HttpPost("{id}/uploadLogo")]
    public async Task<IActionResult> Upload(IFormFile file, int id)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest("Only image files are allowed.");

        var filePath = Path.Combine(_storagePath, id.ToString(), file.FileName);
        var relativePath = Path.Combine(id.ToString(), file.FileName);
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));


        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var operatorDb = await _dbContext.Operators.FindAsync(id);
        operatorDb.LogoFilePath = relativePath;
        operatorDb.LogoContentType = file.ContentType;
        await _dbContext.SaveChangesAsync();

        return Ok(new { FilePath = filePath });
    }

    [HttpPatch("{id}/connect")]
    public async Task<IActionResult> ConnectAllRoutesForThisOperator(int id)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var operatorDb = await _dbContext.Operators.Where(o => o.Id == id).Include(o => o.RestrictToRegions).SingleOrDefaultAsync();
        if (operatorDb == null)
        {
            return NotFound();
        }
        var operatorNames = operatorDb.Names.Select(n => n.ToLower().Trim()).ToList();
        var count = 0;
        foreach (var operatorName in operatorNames)
        {
            var routesQuery = _dbContext.Routes
                .Include(r => r.Operators)
                .Where(r => !r.Operators.Any(o => o.Id == operatorDb.Id))
                .Where(r => r.OperatingCompany.ToLower() == operatorName);

            if (operatorDb.RestrictToRegions.Count != 0)
            {
                routesQuery = routesQuery
                    .Where(r => r.Regions.Any(r => operatorDb.RestrictToRegions.Contains(r)));
            }
            var routes = await routesQuery
            .ToListAsync();

            foreach (var route in routes)
            {
                route.Operators.Add(operatorDb);
            }
            await _dbContext.SaveChangesAsync();
            count += routes.Count;
        }
        return Ok(count);
    }

    [HttpGet("openOperators/{regionId}")]
    public async Task<ActionResult<IEnumerable<string>>> GetUnclaimedOperators(int regionId)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var operators = await _dbContext.Routes
            .Where(r => r.Operators.Count == 0)
            .Where(r => r.Regions.Any(r => r.Id == regionId))
            .Where(r => !string.IsNullOrWhiteSpace(r.OperatingCompany))
            .Select(r => new { r.OperatingCompany, IsTrain = r.RouteType != null ? r.RouteType.IsTrain : false })
            .Distinct()
            .ToListAsync();

        var operatorStrings = operators.Select(o => (o.IsTrain ? "[Train] " : "") + o.OperatingCompany).ToList();

        return operatorStrings;
    }
}
