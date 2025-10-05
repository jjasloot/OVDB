using System.Threading.Tasks;

namespace OV_DB.Services
{
    public interface IAchievementIconService
    {
        Task<string> GenerateAchievementIconAsync(string achievementKey, string level);
        string GetIconUrl(string achievementKey, string level);
    }
}
