using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public interface IOverpassService
    {
        /// <summary>
        /// Runs an Overpass query (QL or XML) against the first available public endpoint,
        /// failing over on rate limits, server errors and timeouts.
        /// Queries that reference historic data ([date:...] / &lt;osm-script date="..."&gt;)
        /// are only sent to endpoints that keep attic data.
        /// Returns the response body, or null if all eligible endpoints failed.
        /// </summary>
        Task<string> QueryAsync(string query, CancellationToken cancellationToken = default);
    }
}
