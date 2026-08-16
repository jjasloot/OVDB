using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OV_DB.Tests
{
    /// <summary>
    /// The base requirement of station visit history is that nothing marks a station visited on the
    /// user's behalf: inference may propose, only a user may decide. That is a property of the whole
    /// codebase rather than of any one method, so it is guarded here rather than left to memory —
    /// the failure this prevents is a future import or backfill job quietly adding visits.
    /// </summary>
    public class StationVisitWriteBoundaryTests
    {
        private const string ServiceFile = "StationVisitService.cs";

        // StationMergeController legitimately moves and removes existing rows while merging two
        // stations; it never brings a visit into existence, so it is not a way in.
        private static readonly string[] AllowedElsewhere = ["StationMergeController.cs"];

        private static DirectoryInfo RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OV_DB", "Services")))
            {
                directory = directory.Parent;
            }
            return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
        }

        private static string[] SourceFiles() =>
            Directory.GetFiles(Path.Combine(RepositoryRoot().FullName, "OV_DB"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray();

        [Fact]
        public void OnlyTheVisitServiceCreatesStationVisits()
        {
            // Matches "new StationVisit {" and "new StationVisit(" but not types that merely start
            // with the same name, such as StationVisitStateDTO.
            var construction = new Regex(@"new\s+StationVisit\s*[({]");

            var offenders = SourceFiles()
                .Where(path => Path.GetFileName(path) != ServiceFile)
                .Where(path => construction.IsMatch(File.ReadAllText(path)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "A station visit may only be created through StationVisitService, so that every visit "
                + "originates in an explicit user action. Found construction in: " + string.Join(", ", offenders));
        }

        [Fact]
        public void OnlyTheVisitServiceAddsOrRemovesStationVisits()
        {
            var offenders = SourceFiles()
                .Where(path => Path.GetFileName(path) != ServiceFile)
                .Where(path => !AllowedElsewhere.Contains(Path.GetFileName(path)))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("StationVisits.Add(") || source.Contains("StationVisits.Remove(");
                })
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "Station visits are added and removed only by StationVisitService. Found direct writes in: "
                + string.Join(", ", offenders));
        }
    }
}
