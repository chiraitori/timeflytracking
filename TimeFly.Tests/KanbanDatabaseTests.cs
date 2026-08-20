using TimeFly.Core.Models;
using TimeFly.Data;

namespace TimeFly.Tests;

public sealed class KanbanDatabaseTests
{
    [Fact]
    public void Kanban_crud_and_column_transitions_work()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"timefly_kanban_{Guid.NewGuid():N}.db");
        try
        {
            var db = new TimeFlyDatabase(tempDb);

            // 1. Add card
            var id = db.AddKanbanCard(
                title: "Cyberpunk Character Illustration",
                description: "Full body character for portfolio",
                columnId: "ideas",
                tags: "#Personal, #Character",
                priority: "High",
                linkedCanvas: "cyberpunk_girl.kra",
                checklistJson: "[{\"Text\":\"Rough thumbnail\",\"IsDone\":true},{\"Text\":\"Lineart\",\"IsDone\":false}]");

            Assert.True(id > 0);

            // 2. Fetch cards
            var cards = db.GetKanbanCards();
            Assert.Single(cards);
            var card = cards[0];
            Assert.Equal("Cyberpunk Character Illustration", card.Title);
            Assert.Equal("ideas", card.ColumnId);
            Assert.Equal("High", card.Priority);
            Assert.Equal("cyberpunk_girl.kra", card.LinkedCanvas);

            var checklist = card.GetChecklist();
            Assert.Equal(2, checklist.Count);
            Assert.True(checklist[0].IsDone);
            Assert.False(checklist[1].IsDone);

            // 3. Move across stages (ideas -> sketch -> render -> done)
            Assert.True(db.MoveKanbanCard(id, "sketch"));
            cards = db.GetKanbanCards();
            Assert.Equal("sketch", cards[0].ColumnId);

            Assert.True(db.MoveKanbanCard(id, "render"));
            cards = db.GetKanbanCards();
            Assert.Equal("render", cards[0].ColumnId);

            // 4. Update card content
            Assert.True(db.UpdateKanbanCard(
                id,
                title: "Cyberpunk Girl - Final Polish",
                description: "Adding glowing neon VFX",
                columnId: "render",
                tags: "#Portfolio",
                priority: "Medium",
                linkedCanvas: "cyberpunk_girl.kra",
                checklistJson: "[{\"Text\":\"Rough thumbnail\",\"IsDone\":true},{\"Text\":\"Lineart\",\"IsDone\":true}]"));

            var updated = Assert.Single(db.GetKanbanCards(search: "Final Polish"));
            Assert.Equal("Cyberpunk Girl - Final Polish", updated.Title);
            Assert.Equal("#Portfolio", updated.Tags);

            // 5. Delete card
            Assert.True(db.DeleteKanbanCard(id));
            Assert.Empty(db.GetKanbanCards());
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
