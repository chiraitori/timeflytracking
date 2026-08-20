using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TimeFly.Core.Models;

namespace TimeFly.Data;

public sealed class TimeFlyDatabase
{
    private static readonly IReadOnlyDictionary<string, string> DefaultSettings = new Dictionary<string, string>
    {
        ["idle_timeout_min"] = "3", ["theme"] = "dark", ["minimize_to_tray"] = "false",
        ["auto_start_tracking"] = "true",
        ["tracked_apps"] = "[\"krita.exe\",\"CLIPStudioPaint.exe\",\"Photoshop.exe\",\"Aseprite.exe\",\"blender.exe\",\"sai2.exe\"]"
    };

    public string DatabasePath { get; }

    public TimeFlyDatabase(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".timefly", "timefly.db");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Initialize();
    }

    public long AddSession(string appName, string canvasName, DateTime startTime, DateTime endTime, long durationSeconds, long idleSeconds = 0, string tags = "", string notes = "")
    {
        if (durationSeconds <= 0) return -1;
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sessions (app_name, canvas_name, start_time, end_time, duration_sec, idle_sec, date, tags, notes)
            VALUES ($app, $canvas, $start, $end, $duration, $idle, $date, $tags, $notes);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$app", appName); command.Parameters.AddWithValue("$canvas", canvasName);
        command.Parameters.AddWithValue("$start", startTime.ToString("O")); command.Parameters.AddWithValue("$end", endTime.ToString("O"));
        command.Parameters.AddWithValue("$duration", durationSeconds); command.Parameters.AddWithValue("$idle", idleSeconds);
        command.Parameters.AddWithValue("$date", startTime.ToString("yyyy-MM-dd")); command.Parameters.AddWithValue("$tags", tags);
        command.Parameters.AddWithValue("$notes", notes);
        var sessionId = (long)(command.ExecuteScalar() ?? -1L);

        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO projects (canvas_name, app_name, total_duration_sec, first_worked, last_worked, session_count, tags)
            VALUES ($canvas, $app, $duration, $start, $end, 1, $tags)
            ON CONFLICT(canvas_name) DO UPDATE SET
                app_name = excluded.app_name,
                total_duration_sec = projects.total_duration_sec + excluded.total_duration_sec,
                last_worked = excluded.last_worked,
                session_count = projects.session_count + 1,
                tags = CASE WHEN excluded.tags = '' THEN projects.tags ELSE excluded.tags END;
            """;
        command.Parameters.AddWithValue("$canvas", canvasName); command.Parameters.AddWithValue("$app", appName);
        command.Parameters.AddWithValue("$duration", durationSeconds); command.Parameters.AddWithValue("$start", startTime.ToString("O"));
        command.Parameters.AddWithValue("$end", endTime.ToString("O")); command.Parameters.AddWithValue("$tags", tags);
        _ = command.ExecuteNonQuery();
        UpdateStreak(connection, transaction, startTime.Date);
        transaction.Commit();
        return sessionId;
    }

    public bool DeleteSession(long sessionId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT canvas_name, duration_sec FROM sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", sessionId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return false;
        var canvas = reader.GetString(0); var duration = reader.GetInt64(1); reader.Close();
        command.CommandText = "DELETE FROM sessions WHERE id = $id;"; _ = command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE projects SET total_duration_sec = MAX(0, total_duration_sec - $duration), session_count = MAX(0, session_count - 1) WHERE canvas_name = $canvas;
            DELETE FROM projects WHERE canvas_name = $canvas AND session_count = 0;
            """;
        command.Parameters.AddWithValue("$duration", duration); command.Parameters.AddWithValue("$canvas", canvas);
        _ = command.ExecuteNonQuery(); transaction.Commit(); return true;
    }

    public bool DeleteProject(string canvasName)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM sessions WHERE canvas_name = $canvas;
            DELETE FROM projects WHERE canvas_name = $canvas;
            """;
        command.Parameters.AddWithValue("$canvas", canvasName);
        var count = command.ExecuteNonQuery();
        transaction.Commit();
        return count > 0;
    }

    public void CleanAndConsolidateDatabase()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var updates = new List<(string OldCanvas, string AppName, string NewCanvas)>();
        using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.Transaction = transaction;
            selectCmd.CommandText = "SELECT DISTINCT canvas_name, app_name FROM sessions;";
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                var oldCanvas = reader.GetString(0);
                var app = reader.GetString(1);
                var (_, newCanvas) = TimeFly.Core.Services.WindowTitleParser.Parse(app, oldCanvas);
                if (!string.Equals(oldCanvas, newCanvas, StringComparison.Ordinal))
                {
                    updates.Add((oldCanvas, app, newCanvas));
                }
            }
        }

        foreach (var (oldCanvas, _, newCanvas) in updates)
        {
            using var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = "UPDATE sessions SET canvas_name = $newCanvas WHERE canvas_name = $oldCanvas;";
            updateCmd.Parameters.AddWithValue("$newCanvas", newCanvas);
            updateCmd.Parameters.AddWithValue("$oldCanvas", oldCanvas);
            _ = updateCmd.ExecuteNonQuery();
        }

        using (var cleanZeroCmd = connection.CreateCommand())
        {
            cleanZeroCmd.Transaction = transaction;
            cleanZeroCmd.CommandText = "DELETE FROM sessions WHERE duration_sec <= 0;";
            _ = cleanZeroCmd.ExecuteNonQuery();
        }

        using (var rebuildCmd = connection.CreateCommand())
        {
            rebuildCmd.Transaction = transaction;
            rebuildCmd.CommandText = """
                DELETE FROM projects;
                INSERT INTO projects (canvas_name, app_name, total_duration_sec, first_worked, last_worked, session_count, tags, color_tag)
                SELECT 
                    canvas_name,
                    app_name,
                    SUM(duration_sec),
                    MIN(start_time),
                    MAX(end_time),
                    COUNT(*),
                    COALESCE(MAX(tags), ''),
                    '#6366f1'
                FROM sessions
                WHERE duration_sec > 0
                GROUP BY canvas_name, app_name;
                """;
            _ = rebuildCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpdateSessionNotes(long sessionId, string notes, string tags)
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET notes = $notes, tags = $tags WHERE id = $id;";
        command.Parameters.AddWithValue("$notes", notes); command.Parameters.AddWithValue("$tags", tags); command.Parameters.AddWithValue("$id", sessionId);
        _ = command.ExecuteNonQuery();
    }

    public IReadOnlyList<SessionRecord> GetSessions(int limit = 100, int offset = 0, string search = "", DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand(); var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) { where.Add("(canvas_name LIKE $search OR app_name LIKE $search OR tags LIKE $search OR notes LIKE $search)"); command.Parameters.AddWithValue("$search", $"%{search.Trim()}%"); }
        if (fromDate is not null) { where.Add("date >= $from"); command.Parameters.AddWithValue("$from", fromDate.Value.ToString("yyyy-MM-dd")); }
        if (toDate is not null) { where.Add("date <= $to"); command.Parameters.AddWithValue("$to", toDate.Value.ToString("yyyy-MM-dd")); }
        command.CommandText = $"""
            SELECT id, app_name, canvas_name, start_time, end_time, duration_sec, idle_sec, date, tags, notes FROM sessions
            {(where.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", where))}
            ORDER BY id DESC LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000)); command.Parameters.AddWithValue("$offset", Math.Max(0, offset));
        using var reader = command.ExecuteReader(); var result = new List<SessionRecord>();
        while (reader.Read()) result.Add(ReadSession(reader));
        return result;
    }

    public DashboardStats GetDashboardStats()
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(CASE WHEN date = $today THEN duration_sec ELSE 0 END), 0), COALESCE(SUM(duration_sec), 0),
            COALESCE(SUM(CASE WHEN date = $today THEN 1 ELSE 0 END), 0), COUNT(DISTINCT CASE WHEN duration_sec > 0 THEN date END), COUNT(DISTINCT canvas_name)
            FROM sessions;
            """;
        command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd")); using var reader = command.ExecuteReader(); _ = reader.Read();
        return new DashboardStats(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    public IReadOnlyList<RecentSession> GetRecentSessions(int limit = 8) => GetSessions(limit).Select(x => new RecentSession(x.Id, x.AppName, x.CanvasName, x.StartTime, x.DurationSeconds)).ToList();
    public long GetTodaySeconds() => GetSecondsForDate(DateTime.Today);

    public long GetTodayCanvasSeconds(string canvasName)
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(duration_sec), 0) FROM sessions WHERE date = $date AND canvas_name = $canvas;";
        command.Parameters.AddWithValue("$date", DateTime.Today.ToString("yyyy-MM-dd")); command.Parameters.AddWithValue("$canvas", canvasName);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<DailyStat> GetDailyStats(int days = 7)
    {
        days = Math.Clamp(days, 1, 366); var start = DateTime.Today.AddDays(-(days - 1));
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT date, COALESCE(SUM(duration_sec), 0), COALESCE(SUM(idle_sec), 0), COUNT(*) FROM sessions WHERE date >= $start GROUP BY date;";
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd")); using var reader = command.ExecuteReader(); var found = new Dictionary<string, DailyStat>();
        while (reader.Read()) { var item = new DailyStat(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt32(3)); found[item.Date] = item; }
        return Enumerable.Range(0, days).Select(i => start.AddDays(i).ToString("yyyy-MM-dd")).Select(date => found.GetValueOrDefault(date) ?? new DailyStat(date, 0, 0, 0)).ToList();
    }

    public IReadOnlyList<ProjectRecord> GetProjects(int limit = 100, string search = "")
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, canvas_name, app_name, total_duration_sec, first_worked, last_worked, session_count, tags, color_tag FROM projects
            WHERE total_duration_sec > 0 AND ($search = '' OR canvas_name LIKE $like OR app_name LIKE $like OR tags LIKE $like)
            ORDER BY total_duration_sec DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$search", search.Trim()); command.Parameters.AddWithValue("$like", $"%{search.Trim()}%"); command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        using var reader = command.ExecuteReader(); var result = new List<ProjectRecord>();
        while (reader.Read()) result.Add(new ProjectRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetString(7), reader.GetString(8)));
        return result;
    }

    public IReadOnlyList<long> GetHourlyActivity()
    {
        var hours = new long[24];
        foreach (var session in GetSessions(5000)) if (DateTime.TryParse(session.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)) hours[start.Hour] += session.DurationSeconds;
        return hours;
    }

    public AllTimeStats GetAllTimeStats()
    {
        var dashboard = GetDashboardStats(); using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sessions; SELECT daily_goal_minutes, streak_count FROM goals LIMIT 1;"; using var reader = command.ExecuteReader();
        _ = reader.Read(); var count = reader.GetInt32(0); _ = reader.NextResult(); var goal = 120; var streak = 0;
        if (reader.Read()) { goal = reader.GetInt32(0); streak = reader.GetInt32(1); }
        return new AllTimeStats(dashboard.AllTimeSeconds, count, dashboard.ActiveDays, dashboard.ArtworkCount, goal, streak);
    }

    public int GetDailyGoalMinutes() { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "SELECT daily_goal_minutes FROM goals LIMIT 1;"; return Convert.ToInt32(command.ExecuteScalar() ?? 120, CultureInfo.InvariantCulture); }
    public void SetDailyGoalMinutes(int minutes) { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE goals SET daily_goal_minutes = $minutes;"; command.Parameters.AddWithValue("$minutes", Math.Clamp(minutes, 15, 720)); _ = command.ExecuteNonQuery(); }

    public string GetSetting(string key, string defaultValue = "") { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM settings WHERE key = $key;"; command.Parameters.AddWithValue("$key", key); return command.ExecuteScalar()?.ToString() ?? defaultValue; }
    public bool GetBooleanSetting(string key, bool defaultValue = false) => bool.TryParse(GetSetting(key, defaultValue.ToString()), out var value) ? value : defaultValue;
    public void SetSetting(string key, string value) { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;"; command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value); _ = command.ExecuteNonQuery(); }

    public bool ExportToCsv(string filePath)
    {
        var sessions = GetSessions(5000).OrderBy(x => x.Id).ToList(); if (sessions.Count == 0) return false;
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)); writer.WriteLine("id,app_name,canvas_name,start_time,end_time,duration_sec,idle_sec,date,tags,notes");
        foreach (var x in sessions) writer.WriteLine(string.Join(',', new object[] { x.Id, x.AppName, x.CanvasName, x.StartTime, x.EndTime, x.DurationSeconds, x.IdleSeconds, x.Date, x.Tags, x.Notes }.Select(Csv)));
        return true;
    }

    private long GetSecondsForDate(DateTime date) { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "SELECT COALESCE(SUM(duration_sec), 0) FROM sessions WHERE date = $date;"; command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd")); return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture); }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString()); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;"; _ = command.ExecuteNonQuery(); return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS sessions (id INTEGER PRIMARY KEY AUTOINCREMENT, app_name TEXT NOT NULL, canvas_name TEXT NOT NULL, start_time TEXT NOT NULL, end_time TEXT NOT NULL, duration_sec INTEGER NOT NULL, idle_sec INTEGER DEFAULT 0, date TEXT NOT NULL, tags TEXT DEFAULT '', notes TEXT DEFAULT '');
            CREATE TABLE IF NOT EXISTS projects (id INTEGER PRIMARY KEY AUTOINCREMENT, canvas_name TEXT UNIQUE NOT NULL, app_name TEXT NOT NULL, total_duration_sec INTEGER DEFAULT 0, first_worked TEXT NOT NULL, last_worked TEXT NOT NULL, session_count INTEGER DEFAULT 1, tags TEXT DEFAULT '', color_tag TEXT DEFAULT '#6366f1');
            CREATE TABLE IF NOT EXISTS goals (id INTEGER PRIMARY KEY AUTOINCREMENT, daily_goal_minutes INTEGER DEFAULT 120, streak_count INTEGER DEFAULT 0, last_active_date TEXT DEFAULT '');
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT INTO goals (daily_goal_minutes, streak_count, last_active_date) SELECT 120, 0, '' WHERE NOT EXISTS (SELECT 1 FROM goals);
            DELETE FROM sessions WHERE canvas_name LIKE '%TimeFly%' OR canvas_name LIKE '%Drawing Tracker%';
            DELETE FROM projects WHERE canvas_name LIKE '%TimeFly%' OR canvas_name LIKE '%Drawing Tracker%';
            """;
        _ = command.ExecuteNonQuery();
        foreach (var setting in DefaultSettings)
        {
            using var insert = connection.CreateCommand(); insert.CommandText = "INSERT OR IGNORE INTO settings (key, value) VALUES ($key, $value);";
            insert.Parameters.AddWithValue("$key", setting.Key); insert.Parameters.AddWithValue("$value", setting.Value); _ = insert.ExecuteNonQuery();
        }

        CleanAndConsolidateDatabase();
    }

    private static SessionRecord ReadSession(SqliteDataReader reader) => new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetString(7), reader.GetString(8), reader.GetString(9));

    private static void UpdateStreak(SqliteConnection connection, SqliteTransaction transaction, DateTime sessionDate)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT id, streak_count, last_active_date FROM goals LIMIT 1;"; using var reader = command.ExecuteReader(); if (!reader.Read()) return;
        var id = reader.GetInt64(0); var streak = reader.GetInt32(1); var lastValue = reader.GetString(2); reader.Close();
        if (!DateTime.TryParse(lastValue, out var lastDate)) streak = 1; else if (lastDate.Date == sessionDate.Date) { } else if ((sessionDate.Date - lastDate.Date).Days == 1) streak++; else if (sessionDate.Date > lastDate.Date) streak = 1;
        command.CommandText = "UPDATE goals SET streak_count = $streak, last_active_date = $date WHERE id = $id;"; command.Parameters.AddWithValue("$streak", streak); command.Parameters.AddWithValue("$date", sessionDate.ToString("yyyy-MM-dd")); command.Parameters.AddWithValue("$id", id); _ = command.ExecuteNonQuery();
    }

    private static string Csv(object? value) { var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text; }
}
