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

    public long AddSession(
        string appName,
        string canvasName,
        DateTime startTime,
        DateTime endTime,
        long durationSeconds,
        long idleSeconds = 0,
        long elapsedSeconds = 0,
        int focusBlocks = 1,
        string tags = "",
        string notes = "")
    {
        if (durationSeconds <= 0) return -1;
        if (elapsedSeconds <= 0) elapsedSeconds = Math.Max(durationSeconds + idleSeconds, (long)(endTime - startTime).TotalSeconds);
        if (focusBlocks <= 0) focusBlocks = 1;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sessions (app_name, canvas_name, start_time, end_time, duration_sec, idle_sec, elapsed_sec, focus_blocks, date, tags, notes)
            VALUES ($app, $canvas, $start, $end, $duration, $idle, $elapsed, $blocks, $date, $tags, $notes);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$app", appName); command.Parameters.AddWithValue("$canvas", canvasName);
        command.Parameters.AddWithValue("$start", startTime.ToString("O")); command.Parameters.AddWithValue("$end", endTime.ToString("O"));
        command.Parameters.AddWithValue("$duration", durationSeconds); command.Parameters.AddWithValue("$idle", idleSeconds);
        command.Parameters.AddWithValue("$elapsed", elapsedSeconds); command.Parameters.AddWithValue("$blocks", focusBlocks);
        command.Parameters.AddWithValue("$date", startTime.ToString("yyyy-MM-dd")); command.Parameters.AddWithValue("$tags", tags);
        command.Parameters.AddWithValue("$notes", notes);
        var sessionId = (long)(command.ExecuteScalar() ?? -1L);

        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO projects (canvas_name, app_name, total_duration_sec, total_elapsed_sec, first_worked, last_worked, session_count, total_focus_blocks, tags)
            VALUES ($canvas, $app, $duration, $elapsed, $start, $end, 1, $blocks, $tags)
            ON CONFLICT(canvas_name) DO UPDATE SET
                app_name = excluded.app_name,
                total_duration_sec = projects.total_duration_sec + excluded.total_duration_sec,
                total_elapsed_sec = projects.total_elapsed_sec + excluded.total_elapsed_sec,
                last_worked = excluded.last_worked,
                session_count = projects.session_count + 1,
                total_focus_blocks = projects.total_focus_blocks + excluded.total_focus_blocks,
                tags = CASE WHEN excluded.tags = '' THEN projects.tags ELSE excluded.tags END;
            """;
        command.Parameters.AddWithValue("$canvas", canvasName); command.Parameters.AddWithValue("$app", appName);
        command.Parameters.AddWithValue("$duration", durationSeconds); command.Parameters.AddWithValue("$elapsed", elapsedSeconds);
        command.Parameters.AddWithValue("$blocks", focusBlocks);
        command.Parameters.AddWithValue("$start", startTime.ToString("O"));
        command.Parameters.AddWithValue("$end", endTime.ToString("O")); command.Parameters.AddWithValue("$tags", tags);
        _ = command.ExecuteNonQuery();
        UpdateStreak(connection, transaction, startTime.Date);
        transaction.Commit();
        return sessionId;
    }

    public bool MergeCanvasIdentity(string oldCanvas, string newCanvas)
    {
        if (string.IsNullOrWhiteSpace(oldCanvas) || string.IsNullOrWhiteSpace(newCanvas) || string.Equals(oldCanvas, newCanvas, StringComparison.Ordinal))
            return false;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE sessions SET canvas_name = $newCanvas WHERE canvas_name = $oldCanvas;";
        command.Parameters.AddWithValue("$newCanvas", newCanvas);
        command.Parameters.AddWithValue("$oldCanvas", oldCanvas);
        var affected = command.ExecuteNonQuery();

        if (affected > 0)
        {
            RebuildProjectsInternal(connection, transaction);
            transaction.Commit();
            return true;
        }

        transaction.Rollback();
        return false;
    }

    public bool DeleteSession(long sessionId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT canvas_name, duration_sec, elapsed_sec, focus_blocks FROM sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", sessionId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return false;
        var canvas = reader.GetString(0); var duration = reader.GetInt64(1);
        var elapsed = reader.IsDBNull(2) ? duration : reader.GetInt64(2);
        var blocks = reader.IsDBNull(3) ? 1 : reader.GetInt32(3);
        reader.Close();

        command.CommandText = "DELETE FROM sessions WHERE id = $id;"; _ = command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE projects SET 
                total_duration_sec = MAX(0, total_duration_sec - $duration),
                total_elapsed_sec = MAX(0, total_elapsed_sec - $elapsed),
                session_count = MAX(0, session_count - 1),
                total_focus_blocks = MAX(0, total_focus_blocks - $blocks)
            WHERE canvas_name = $canvas;
            DELETE FROM projects WHERE canvas_name = $canvas AND session_count = 0;
            """;
        command.Parameters.AddWithValue("$duration", duration);
        command.Parameters.AddWithValue("$elapsed", elapsed);
        command.Parameters.AddWithValue("$blocks", blocks);
        command.Parameters.AddWithValue("$canvas", canvas);
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

        RebuildProjectsInternal(connection, transaction);
        transaction.Commit();
    }

    private static void RebuildProjectsInternal(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var rebuildCmd = connection.CreateCommand();
        rebuildCmd.Transaction = transaction;
        rebuildCmd.CommandText = """
            DELETE FROM projects;
            INSERT INTO projects (canvas_name, app_name, total_duration_sec, total_elapsed_sec, first_worked, last_worked, session_count, total_focus_blocks, tags, color_tag)
            SELECT 
                canvas_name,
                app_name,
                SUM(duration_sec),
                SUM(COALESCE(elapsed_sec, duration_sec)),
                MIN(start_time),
                MAX(end_time),
                COUNT(*),
                SUM(COALESCE(focus_blocks, 1)),
                COALESCE(MAX(tags), ''),
                '#38BDF8'
            FROM sessions
            WHERE duration_sec > 0
            GROUP BY canvas_name, app_name;
            """;
        _ = rebuildCmd.ExecuteNonQuery();
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
            SELECT id, app_name, canvas_name, start_time, end_time, duration_sec, idle_sec, COALESCE(elapsed_sec, duration_sec), COALESCE(focus_blocks, 1), date, tags, notes FROM sessions
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
            SELECT 
                COALESCE(SUM(CASE WHEN date = $today THEN duration_sec ELSE 0 END), 0),
                COALESCE(SUM(duration_sec), 0),
                COALESCE(SUM(CASE WHEN date = $today THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN date = $today THEN COALESCE(focus_blocks, 1) ELSE 0 END), 0),
                COUNT(DISTINCT CASE WHEN duration_sec > 0 THEN date END),
                COUNT(DISTINCT canvas_name),
                COALESCE(SUM(CASE WHEN date = $today THEN COALESCE(elapsed_sec, duration_sec) ELSE 0 END), 0)
            FROM sessions;
            """;
        command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        _ = reader.Read();
        var todaySec = reader.GetInt64(0);
        var allTimeSec = reader.GetInt64(1);
        var sessionsToday = reader.GetInt32(2);
        var focusBlocksToday = reader.GetInt32(3);
        var activeDays = reader.GetInt32(4);
        var artCount = reader.GetInt32(5);
        var elapsedToday = reader.GetInt64(6);
        var focusRatio = elapsedToday > 0 ? Math.Clamp((double)todaySec / elapsedToday * 100d, 0, 100) : 100d;

        return new DashboardStats(todaySec, allTimeSec, sessionsToday, focusBlocksToday, activeDays, artCount, focusRatio);
    }

    public IReadOnlyList<RecentSession> GetRecentSessions(int limit = 8) =>
        GetSessions(limit).Select(x => new RecentSession(x.Id, x.AppName, x.CanvasName, x.StartTime, x.DurationSeconds, x.ElapsedSeconds, x.FocusBlocks)).ToList();

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
        command.CommandText = "SELECT date, COALESCE(SUM(duration_sec), 0), COALESCE(SUM(idle_sec), 0), COUNT(*), COALESCE(SUM(COALESCE(focus_blocks, 1)), 0) FROM sessions WHERE date >= $start GROUP BY date;";
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd")); using var reader = command.ExecuteReader(); var found = new Dictionary<string, DailyStat>();
        while (reader.Read()) { var item = new DailyStat(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt32(3), reader.GetInt32(4)); found[item.Date] = item; }
        return Enumerable.Range(0, days).Select(i => start.AddDays(i).ToString("yyyy-MM-dd")).Select(date => found.GetValueOrDefault(date) ?? new DailyStat(date, 0, 0, 0, 0)).ToList();
    }

    public IReadOnlyList<ProjectRecord> GetProjects(int limit = 100, string search = "", string filter = "all")
    {
        using var connection = OpenConnection(); using var command = connection.CreateCommand();
        var filterClause = filter.ToLowerInvariant() switch
        {
            "saved" => "AND NOT (canvas_name LIKE 'New / Unsaved%' OR canvas_name LIKE '%Untitled%' OR canvas_name LIKE '%Not Saved%')",
            "unsaved" => "AND (canvas_name LIKE 'New / Unsaved%' OR canvas_name LIKE '%Untitled%' OR canvas_name LIKE '%Not Saved%')",
            _ => string.Empty
        };

        command.CommandText = $"""
            SELECT id, canvas_name, app_name, total_duration_sec, COALESCE(total_elapsed_sec, total_duration_sec), first_worked, last_worked, session_count, COALESCE(total_focus_blocks, session_count), tags, color_tag FROM projects
            WHERE total_duration_sec >= 10 {filterClause} AND ($search = '' OR canvas_name LIKE $like OR app_name LIKE $like OR tags LIKE $like)
            ORDER BY last_worked DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$search", search.Trim()); command.Parameters.AddWithValue("$like", $"%{search.Trim()}%"); command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        using var reader = command.ExecuteReader(); var result = new List<ProjectRecord>();
        while (reader.Read())
        {
            var canvas = reader.GetString(1);
            var isUnsaved = canvas.StartsWith("New / Unsaved", StringComparison.OrdinalIgnoreCase) || canvas.Contains("Untitled", StringComparison.OrdinalIgnoreCase) || canvas.Contains("Not Saved", StringComparison.OrdinalIgnoreCase);
            result.Add(new ProjectRecord(reader.GetInt64(0), canvas, reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), reader.GetString(10), isUnsaved));
        }
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
        command.CommandText = "SELECT COUNT(*), COALESCE(SUM(COALESCE(focus_blocks, 1)), 0) FROM sessions; SELECT daily_goal_minutes, streak_count FROM goals LIMIT 1;"; using var reader = command.ExecuteReader();
        _ = reader.Read();
        var totalSessions = reader.GetInt32(0);
        var totalBlocks = reader.GetInt32(1);
        _ = reader.NextResult();
        var goal = 120; var streak = 0;
        if (reader.Read()) { goal = reader.GetInt32(0); streak = reader.GetInt32(1); }
        return new AllTimeStats(dashboard.AllTimeSeconds, totalSessions, totalBlocks, dashboard.ActiveDays, dashboard.ArtworkCount, goal, streak);
    }

    public int GetDailyGoalMinutes() { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "SELECT daily_goal_minutes FROM goals LIMIT 1;"; return Convert.ToInt32(command.ExecuteScalar() ?? 120, CultureInfo.InvariantCulture); }
    public void SetDailyGoalMinutes(int minutes) { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE goals SET daily_goal_minutes = $minutes;"; command.Parameters.AddWithValue("$minutes", Math.Clamp(minutes, 15, 720)); _ = command.ExecuteNonQuery(); }

    public string GetSetting(string key, string defaultValue = "") { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM settings WHERE key = $key;"; command.Parameters.AddWithValue("$key", key); return command.ExecuteScalar()?.ToString() ?? defaultValue; }
    public bool GetBooleanSetting(string key, bool defaultValue = false) => bool.TryParse(GetSetting(key, defaultValue.ToString()), out var value) ? value : defaultValue;
    public void SetSetting(string key, string value) { using var connection = OpenConnection(); using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;"; command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value); _ = command.ExecuteNonQuery(); }

    public IReadOnlyList<KanbanCardRecord> GetKanbanCards(string search = "", string tag = "")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(title LIKE $search OR description LIKE $search OR tags LIKE $search OR linked_canvas LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            where.Add("tags LIKE $tag");
            command.Parameters.AddWithValue("$tag", $"%{tag.Trim()}%");
        }

        command.CommandText = $"""
            SELECT id, title, description, column_id, tags, priority, linked_canvas, checklist_json, created_at, updated_at
            FROM kanban_cards
            {(where.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", where))}
            ORDER BY id ASC;
            """;
        using var reader = command.ExecuteReader();
        var list = new List<KanbanCardRecord>();
        while (reader.Read())
        {
            list.Add(new KanbanCardRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9)));
        }
        return list;
    }

    public long AddKanbanCard(
        string title,
        string description = "",
        string columnId = "ideas",
        string tags = "",
        string priority = "Medium",
        string linkedCanvas = "",
        string checklistJson = "[]")
    {
        if (string.IsNullOrWhiteSpace(title)) return -1;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var now = DateTime.Now.ToString("O");
        command.CommandText = """
            INSERT INTO kanban_cards (title, description, column_id, tags, priority, linked_canvas, checklist_json, created_at, updated_at)
            VALUES ($title, $desc, $col, $tags, $prio, $canvas, $check, $created, $updated);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$desc", description.Trim());
        command.Parameters.AddWithValue("$col", columnId.ToLowerInvariant().Trim());
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.Parameters.AddWithValue("$prio", priority);
        command.Parameters.AddWithValue("$canvas", linkedCanvas.Trim());
        command.Parameters.AddWithValue("$check", string.IsNullOrWhiteSpace(checklistJson) ? "[]" : checklistJson);
        command.Parameters.AddWithValue("$created", now);
        command.Parameters.AddWithValue("$updated", now);
        return (long)(command.ExecuteScalar() ?? -1L);
    }

    public bool UpdateKanbanCard(
        long id,
        string title,
        string description,
        string columnId,
        string tags,
        string priority,
        string linkedCanvas,
        string checklistJson)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var now = DateTime.Now.ToString("O");
        command.CommandText = """
            UPDATE kanban_cards SET
                title = $title,
                description = $desc,
                column_id = $col,
                tags = $tags,
                priority = $prio,
                linked_canvas = $canvas,
                checklist_json = $check,
                updated_at = $updated
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$desc", description.Trim());
        command.Parameters.AddWithValue("$col", columnId.ToLowerInvariant().Trim());
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.Parameters.AddWithValue("$prio", priority);
        command.Parameters.AddWithValue("$canvas", linkedCanvas.Trim());
        command.Parameters.AddWithValue("$check", string.IsNullOrWhiteSpace(checklistJson) ? "[]" : checklistJson);
        command.Parameters.AddWithValue("$updated", now);
        return command.ExecuteNonQuery() > 0;
    }

    public bool MoveKanbanCard(long id, string newColumnId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE kanban_cards SET column_id = $col, updated_at = $updated WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$col", newColumnId.ToLowerInvariant().Trim());
        command.Parameters.AddWithValue("$updated", DateTime.Now.ToString("O"));
        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteKanbanCard(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM kanban_cards WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    public bool ExportToCsv(string filePath)
    {
        var sessions = GetSessions(5000).OrderBy(x => x.Id).ToList(); if (sessions.Count == 0) return false;
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)); writer.WriteLine("id,app_name,canvas_name,start_time,end_time,duration_sec,idle_sec,elapsed_sec,focus_blocks,date,tags,notes");
        foreach (var x in sessions) writer.WriteLine(string.Join(',', new object[] { x.Id, x.AppName, x.CanvasName, x.StartTime, x.EndTime, x.DurationSeconds, x.IdleSeconds, x.ElapsedSeconds, x.FocusBlocks, x.Date, x.Tags, x.Notes }.Select(Csv)));
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
            CREATE TABLE IF NOT EXISTS sessions (id INTEGER PRIMARY KEY AUTOINCREMENT, app_name TEXT NOT NULL, canvas_name TEXT NOT NULL, start_time TEXT NOT NULL, end_time TEXT NOT NULL, duration_sec INTEGER NOT NULL, idle_sec INTEGER DEFAULT 0, elapsed_sec INTEGER DEFAULT 0, focus_blocks INTEGER DEFAULT 1, date TEXT NOT NULL, tags TEXT DEFAULT '', notes TEXT DEFAULT '');
            CREATE TABLE IF NOT EXISTS projects (id INTEGER PRIMARY KEY AUTOINCREMENT, canvas_name TEXT UNIQUE NOT NULL, app_name TEXT NOT NULL, total_duration_sec INTEGER DEFAULT 0, total_elapsed_sec INTEGER DEFAULT 0, first_worked TEXT NOT NULL, last_worked TEXT NOT NULL, session_count INTEGER DEFAULT 1, total_focus_blocks INTEGER DEFAULT 1, tags TEXT DEFAULT '', color_tag TEXT DEFAULT '#38BDF8');
            CREATE TABLE IF NOT EXISTS kanban_cards (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, description TEXT DEFAULT '', column_id TEXT NOT NULL DEFAULT 'ideas', tags TEXT DEFAULT '', priority TEXT DEFAULT 'Medium', linked_canvas TEXT DEFAULT '', checklist_json TEXT DEFAULT '[]', created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS goals (id INTEGER PRIMARY KEY AUTOINCREMENT, daily_goal_minutes INTEGER DEFAULT 120, streak_count INTEGER DEFAULT 0, last_active_date TEXT DEFAULT '');
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT INTO goals (daily_goal_minutes, streak_count, last_active_date) SELECT 120, 0, '' WHERE NOT EXISTS (SELECT 1 FROM goals);
            DELETE FROM sessions WHERE canvas_name LIKE '%TimeFly%' OR canvas_name LIKE '%Drawing Tracker%';
            DELETE FROM projects WHERE canvas_name LIKE '%TimeFly%' OR canvas_name LIKE '%Drawing Tracker%';
            """;
        _ = command.ExecuteNonQuery();

        // Safe column migrations for existing databases
        try { using var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE sessions ADD COLUMN elapsed_sec INTEGER DEFAULT 0;"; alter.ExecuteNonQuery(); } catch { }
        try { using var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE sessions ADD COLUMN focus_blocks INTEGER DEFAULT 1;"; alter.ExecuteNonQuery(); } catch { }
        try { using var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE projects ADD COLUMN total_elapsed_sec INTEGER DEFAULT 0;"; alter.ExecuteNonQuery(); } catch { }
        try { using var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE projects ADD COLUMN total_focus_blocks INTEGER DEFAULT 1;"; alter.ExecuteNonQuery(); } catch { }
        try { using var alter = connection.CreateCommand(); alter.CommandText = "CREATE TABLE IF NOT EXISTS kanban_cards (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, description TEXT DEFAULT '', column_id TEXT NOT NULL DEFAULT 'ideas', tags TEXT DEFAULT '', priority TEXT DEFAULT 'Medium', linked_canvas TEXT DEFAULT '', checklist_json TEXT DEFAULT '[]', created_at TEXT NOT NULL, updated_at TEXT NOT NULL);"; alter.ExecuteNonQuery(); } catch { }

        foreach (var setting in DefaultSettings)
        {
            using var insert = connection.CreateCommand(); insert.CommandText = "INSERT OR IGNORE INTO settings (key, value) VALUES ($key, $value);";
            insert.Parameters.AddWithValue("$key", setting.Key); insert.Parameters.AddWithValue("$value", setting.Value); _ = insert.ExecuteNonQuery();
        }

        CleanAndConsolidateDatabase();
    }

    private static SessionRecord ReadSession(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetInt64(5),
        reader.GetInt64(6),
        reader.GetInt64(7),
        reader.GetInt32(8),
        reader.GetString(9),
        reader.GetString(10),
        reader.GetString(11));

    private static void UpdateStreak(SqliteConnection connection, SqliteTransaction transaction, DateTime sessionDate)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT id, streak_count, last_active_date FROM goals LIMIT 1;"; using var reader = command.ExecuteReader(); if (!reader.Read()) return;
        var id = reader.GetInt64(0); var streak = reader.GetInt32(1); var lastValue = reader.GetString(2); reader.Close();
        if (!DateTime.TryParse(lastValue, out var lastDate)) streak = 1; else if (lastDate.Date == sessionDate.Date) { } else if ((sessionDate.Date - lastDate.Date).Days == 1) streak++; else if (sessionDate.Date > lastDate.Date) streak = 1;
        command.CommandText = "UPDATE goals SET streak_count = $streak, last_active_date = $date WHERE id = $id;"; command.Parameters.AddWithValue("$streak", streak); command.Parameters.AddWithValue("$date", sessionDate.ToString("yyyy-MM-dd")); command.Parameters.AddWithValue("$id", id); _ = command.ExecuteNonQuery();
    }

    private static string Csv(object? value) { var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text; }
}
