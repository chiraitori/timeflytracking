using Microsoft.Data.Sqlite;
using TimeFly.Data;

namespace TimeFly.Tests;

public sealed class TimeFlyDatabaseTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"timefly-dotnet-{Guid.NewGuid():N}");

    [Fact]
    public void Reads_existing_python_schema_and_dashboard_totals()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "timefly.db");
        var database = new TimeFlyDatabase(path);
        var now = DateTime.Now;

        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sessions
                    (app_name, canvas_name, start_time, end_time, duration_sec, idle_sec, date, tags, notes)
                VALUES
                    ('Krita', 'portrait.kra', $start, $end, 3600, 0, $date, '', '');
                """;
            command.Parameters.AddWithValue("$start", now.ToString("O"));
            command.Parameters.AddWithValue("$end", now.ToString("O"));
            command.Parameters.AddWithValue("$date", now.ToString("yyyy-MM-dd"));
            _ = command.ExecuteNonQuery();
        }

        var stats = database.GetDashboardStats();

        Assert.Equal(3600, stats.TodaySeconds);
        Assert.Equal(1, stats.SessionsToday);
        Assert.Equal(1, stats.ArtworkCount);
        Assert.Single(database.GetRecentSessions());
    }

    [Fact]
    public void Full_crud_rollups_settings_and_export_stay_consistent()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "timefly.db");
        var database = new TimeFlyDatabase(path);
        var end = DateTime.Now;

        var id = database.AddSession("Krita", "integration.kra", end.AddMinutes(-45), end, 2700, 30, "#lineart", "test note");

        Assert.True(id > 0);
        Assert.Equal(2700, database.GetTodaySeconds());
        Assert.Equal("integration.kra", Assert.Single(database.GetProjects()).CanvasName);
        Assert.Equal(2700, Assert.Single(database.GetDailyStats(1)).TotalSeconds);
        database.SetSetting("idle_timeout_min", "8");
        Assert.Equal("8", database.GetSetting("idle_timeout_min"));
        database.SetDailyGoalMinutes(180);
        Assert.Equal(180, database.GetDailyGoalMinutes());

        var csv = Path.Combine(directory, "sessions.csv");
        Assert.True(database.ExportToCsv(csv));
        Assert.Contains("integration.kra", File.ReadAllText(csv));
        Assert.True(database.DeleteSession(id));
        Assert.Empty(database.GetSessions());
        Assert.Empty(database.GetProjects());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
