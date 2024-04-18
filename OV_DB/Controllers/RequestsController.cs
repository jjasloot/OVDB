using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers;
[Route("api/[controller]")]
[ApiController]
public class RequestsController(OVDBDatabaseContext dbContext, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserRequests()
    {
        var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
        if (userIdClaim < 0)
        {
            return Forbid();
        }

        var requests = await dbContext.Requests.Where(r => r.UserId == userIdClaim).OrderByDescending(r => r.Created).ToListAsync();
        var responseList = mapper.Map<List<RequestForUserDTO>>(requests);

        foreach (var request in requests.Where(r => !string.IsNullOrWhiteSpace(r.Response) && !r.ResponseRead))
        {
            request.ResponseRead = true;
        }
        await dbContext.SaveChangesAsync();


        return Ok(responseList);
    }

    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminRequests()
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var requests = await dbContext.Requests.OrderByDescending(r => r.Created).Include(r => r.User).ToListAsync();
        var responseList = mapper.Map<List<RequestForAdminDTO>>(requests);

        foreach (var request in requests.Where(r => !string.IsNullOrWhiteSpace(r.Response) && !r.ResponseRead))
        {
            request.Read = true;
        }
        await dbContext.SaveChangesAsync();


        return Ok(responseList);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] CreateRequestDTO createRequest)
    {
        var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
        if (userIdClaim < 0)
        {
            return Forbid();
        }

        var newRequest = new Request
        {
            UserId = userIdClaim,
            Message = createRequest.Message,
            Created = DateTime.Now
        };

        dbContext.Requests.Add(newRequest);
        await dbContext.SaveChangesAsync();
        return Ok();
    }


    [HttpPatch("admin/{id}/respond")]
    public async Task<IActionResult> RespondToRequest(int id, [FromBody] CreateRequestDTO response)
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var request = await dbContext.Requests.SingleOrDefaultAsync(r => r.Id == id);
        if (request == null)
        {
            return Ok();
        }

        request.Response = response.Message;
        request.Responded = DateTime.Now;
        await dbContext.SaveChangesAsync();
        return Ok();
    }


    [HttpGet("anyUnread")]
    public async Task<IActionResult> AnyUnreadRequests()
    {
        var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
        if (userIdClaim < 0)
        {
            return Forbid();
        }

        var anyUnread = await dbContext.Requests.AnyAsync(r => r.UserId == userIdClaim && !r.ResponseRead && !string.IsNullOrWhiteSpace(r.Response));
        return Ok(anyUnread);
    }

    [HttpGet("admin/anyUnread")]
    public async Task<IActionResult> AnyUnreadAdminRequests()
    {
        var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
        if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var anyUnread = await dbContext.Requests.AnyAsync(r => !r.Read);
        return Ok(anyUnread);
    }
}
