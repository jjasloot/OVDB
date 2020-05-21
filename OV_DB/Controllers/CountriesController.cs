using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CountriesController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public CountriesController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        // GET: api/Countries
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.Countries.Where(c => c.UserId == userIdClaim).OrderBy(c => c.OrderNr).ThenBy(c => c.Name).ToListAsync();
        }

        // GET: api/Countries/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Country>> GetCountry(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var country = await _context.Countries.Where(c => c.UserId == userIdClaim).SingleOrDefaultAsync(c => c.CountryId == id);

            if (country == null)
            {
                return NotFound();
            }

            return country;
        }

        [HttpPost]
        public async Task<ActionResult> AddCountry([FromBody] Country country)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            country.UserId = userIdClaim;
            _context.Countries.Add(country);
            await _context.SaveChangesAsync();
            return Ok(country.CountryId);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateCountry([FromBody] Country country)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var dbCountry = await _context.Countries.Where(c => c.UserId == userIdClaim).SingleOrDefaultAsync(c => c.CountryId == country.CountryId);
            if (dbCountry == null)
            {
                return NotFound();
            }
            dbCountry.Name = country.Name;
            dbCountry.NameNL = country.NameNL;
            await _context.SaveChangesAsync();
            return Ok(country.CountryId);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCountry(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var country = await _context.Countries.SingleOrDefaultAsync(c => c.CountryId == id);
            if (country == null)
            {
                return NotFound();
            }
            if (country.UserId != userIdClaim)
            {
                return Forbid();
            }
            var routesCountries = await _context.RoutesCountries.Where(rc => rc.CountryId == id).ToListAsync();

            _context.Countries.Remove(country);
            _context.RoutesCountries.RemoveRange(routesCountries);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
