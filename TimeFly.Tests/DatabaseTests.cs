using TimeFly.Core.Models;
using TimeFly.Data;

namespace TimeFly.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void AddSession_computes_elapsed_and_focus_ratio()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"timefly_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new TimeFlyDatabase(tempDb);
            var start = DateTime.Now.AddHours(-1);
            var end = DateTime.Now;
            
            // 45m focused, 60m elapsed, 3 focus blocks
            var id = db.AddSession("Krita", "artwork.kra", start, end, durationSeconds: 2700, idleSeconds: 300, elapsedSeconds: 3600, focusBlocks: 3);
            Assert.True(id > 0);

            var sessions = db.GetSessions();
            Assert.Single(sessions);
            var s = sessions[0];
            Assert.Equal(2700, s.DurationSeconds);
            Assert.Equal(3600, s.ElapsedSeconds);
            Assert.Equal(3, s.FocusBlocks);
            Assert.Equal(75.0, s.FocusRatio);

            var projects = db.GetProjects();
            Assert.Single(projects);
            var p = projects[0];
            Assert.Equal(2700, p.TotalDurationSeconds);
            Assert.Equal(3600, p.TotalElapsedSeconds);
            Assert.Equal(3, p.FocusBlocksCount);
            Assert.Equal(75.0, p.FocusRatio);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    [Fact]
    public void MergeCanvasIdentity_smoothly_migrates_unsaved_session()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"timefly_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new TimeFlyDatabase(tempDb);
            var start = DateTime.Now.AddMinutes(-30);
            var mid = DateTime.Now;
            
            // Draw on unsaved draft for 25 mins
            db.AddSession("Krita", "New / Unsaved Canvas", start, mid, durationSeconds: 1500, idleSeconds: 0, elapsedSeconds: 1500, focusBlocks: 2);

            // User hits Ctrl+S and saves as "character_sketch.kra"
            var merged = db.MergeCanvasIdentity("New / Unsaved Canvas", "character_sketch.kra");
            Assert.True(merged);

            var sessions = db.GetSessions();
            Assert.Single(sessions);
            Assert.Equal("character_sketch.kra", sessions[0].CanvasName);

            var projects = db.GetProjects();
            Assert.Single(projects);
            Assert.Equal("character_sketch.kra", projects[0].CanvasName);
            Assert.Equal(1500, projects[0].TotalDurationSeconds);
            Assert.False(projects[0].IsUnsaved);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    [Fact]
    public void GetProjects_filters_saved_and_unsaved()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"timefly_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new TimeFlyDatabase(tempDb);
            var now = DateTime.Now;
            
            db.AddSession("Krita", "saved_illustration.kra", now.AddMinutes(-20), now, durationSeconds: 1200);
            db.AddSession("Krita", "New / Unsaved Canvas", now.AddMinutes(-15), now, durationSeconds: 900);

            var all = db.GetProjects(filter: "all");
            Assert.Equal(2, all.Count);

            var saved = db.GetProjects(filter: "saved");
            Assert.Single(saved);
            Assert.Equal("saved_illustration.kra", saved[0].CanvasName);

            var unsaved = db.GetProjects(filter: "unsaved");
            Assert.Single(unsaved);
            Assert.Equal("New / Unsaved Canvas", unsaved[0].CanvasName);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
