using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TimeFly.App.Services;
using TimeFly.Core.Models;
using TimeFly.Core.Services;

namespace TimeFly.App.Views;

public sealed partial class KanbanPage : Page
{
    private readonly AppServices services;
    private IReadOnlyList<KanbanCardRecord> allCards = [];

    public KanbanPage(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        Loaded += (s, e) => Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();
    private void NewCard_Click(object sender, RoutedEventArgs e) => ShowCardDialog();
    private void AddColumnCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string col }) ShowCardDialog(defaultColumn: col);
    }

    private void Refresh()
    {
        allCards = services.Database.GetKanbanCards(search: SearchBox.Text.Trim());

        var ideas = allCards.Where(x => x.ColumnId.Equals("ideas", StringComparison.OrdinalIgnoreCase)).ToList();
        var sketch = allCards.Where(x => x.ColumnId.Equals("sketch", StringComparison.OrdinalIgnoreCase)).ToList();
        var render = allCards.Where(x => x.ColumnId.Equals("render", StringComparison.OrdinalIgnoreCase)).ToList();
        var done = allCards.Where(x => x.ColumnId.Equals("done", StringComparison.OrdinalIgnoreCase)).ToList();

        IdeasCountText.Text = ideas.Count.ToString();
        SketchCountText.Text = sketch.Count.ToString();
        RenderCountText.Text = render.Count.ToString();
        DoneCountText.Text = done.Count.ToString();

        PopulateColumn(IdeasStack, ideas, "ideas");
        PopulateColumn(SketchStack, sketch, "sketch");
        PopulateColumn(RenderStack, render, "render");
        PopulateColumn(DoneStack, done, "done");
    }

    private static Brush GetBrush(string key, string fallbackHex = "#25242D")
    {
        if (Application.Current.Resources.TryGetValue(key, out var res) && res is Brush b) return b;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 36, 45));
    }

    private void PopulateColumn(StackPanel stack, List<KanbanCardRecord> cards, string columnId)
    {
        stack.Children.Clear();
        if (cards.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No projects in this stage",
                FontSize = 12,
                Foreground = GetBrush("TimeFlySubtleTextBrush"),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (var card in cards)
        {
            stack.Children.Add(CreateCardVisual(card, columnId));
        }
    }

    private UIElement CreateCardVisual(KanbanCardRecord card, string columnId)
    {
        var border = new Border
        {
            Background = GetBrush("TimeFlyCardBrush"),
            BorderBrush = GetBrush("TimeFlyCardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12)
        };

        var mainStack = new StackPanel { Spacing = 8 };

        // Top Row: Priority + Tags + More Menu
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tagsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        // Priority Badge
        var prioColor = card.Priority.ToLowerInvariant() switch
        {
            "high" => Windows.UI.Color.FromArgb(255, 239, 68, 68),
            "low" => Windows.UI.Color.FromArgb(255, 148, 163, 184),
            _ => Windows.UI.Color.FromArgb(255, 245, 158, 11)
        };

        var prioBorder = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(35, prioColor.R, prioColor.G, prioColor.B)),
            BorderBrush = new SolidColorBrush(prioColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 1)
        };
        prioBorder.Child = new TextBlock
        {
            Text = card.Priority,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(prioColor)
        };
        tagsStack.Children.Add(prioBorder);

        if (!string.IsNullOrWhiteSpace(card.Tags))
        {
            var tagBadge = new Border
            {
                Background = GetBrush("TimeFlyBadgeBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1)
            };
            tagBadge.Child = new TextBlock
            {
                Text = card.Tags,
                FontSize = 10,
                Foreground = GetBrush("TimeFlyBadgeTextBrush")
            };
            tagsStack.Children.Add(tagBadge);
        }

        topGrid.Children.Add(tagsStack);

        // Delete button
        var delBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 10, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) },
            Padding = new Thickness(4, 2, 4, 2),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = card.Id
        };
        delBtn.Click += async (s, e) =>
        {
            if (s is Button { Tag: long cardId })
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Project Card",
                    Content = $"Delete \"{card.Title}\"? This cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    services.Database.DeleteKanbanCard(cardId);
                    Refresh();
                }
            }
        };
        Grid.SetColumn(delBtn, 1);
        topGrid.Children.Add(delBtn);
        mainStack.Children.Add(topGrid);

        // Title
        var titleBlock = new TextBlock
        {
            Text = card.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        mainStack.Children.Add(titleBlock);

        // Description (if any)
        if (!string.IsNullOrWhiteSpace(card.Description))
        {
            mainStack.Children.Add(new TextBlock
            {
                Text = card.Description,
                FontSize = 11,
                Foreground = GetBrush("TimeFlyMutedTextBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Linked Canvas Artwork Time Tracking
        if (!string.IsNullOrWhiteSpace(card.LinkedCanvas))
        {
            var linkedStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            linkedStack.Children.Add(new FontIcon { Glyph = "\uE790", FontSize = 11, Foreground = GetBrush("TimeFlyAccentBrush") });
            linkedStack.Children.Add(new TextBlock
            {
                Text = card.LinkedCanvas,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = GetBrush("TimeFlyTimeBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            mainStack.Children.Add(linkedStack);
        }

        // Checklist Items
        var checklist = card.GetChecklist();
        if (checklist.Count > 0)
        {
            var checkStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 4) };
            for (var i = 0; i < checklist.Count; i++)
            {
                var idx = i;
                var item = checklist[i];
                var cb = new CheckBox
                {
                    Content = item.Text,
                    IsChecked = item.IsDone,
                    FontSize = 11,
                    Padding = new Thickness(4, 0, 0, 0)
                };
                cb.Click += (s, e) =>
                {
                    var updatedList = card.GetChecklist().ToList();
                    if (idx < updatedList.Count)
                    {
                        updatedList[idx] = new ChecklistItem(item.Text, cb.IsChecked == true);
                        services.Database.UpdateKanbanCard(
                            card.Id,
                            card.Title,
                            card.Description,
                            card.ColumnId,
                            card.Tags,
                            card.Priority,
                            card.LinkedCanvas,
                            JsonSerializer.Serialize(updatedList));
                    }
                };
                checkStack.Children.Add(cb);
            }
            mainStack.Children.Add(checkStack);
        }

        // Bottom Navigation Bar: [◀ Move Left] [✏ Edit] [Move Right ▶]
        var bottomGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var prevCol = GetPreviousColumn(columnId);
        if (prevCol is not null)
        {
            var prevBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE72B", FontSize = 10 },
                Padding = new Thickness(6, 4, 6, 4),
                Tag = prevCol
            };
            ToolTipService.SetToolTip(prevBtn, $"Move back to {GetColumnTitle(prevCol)}");
            prevBtn.Click += (s, e) =>
            {
                services.Database.MoveKanbanCard(card.Id, prevCol);
                Refresh();
            };
            bottomGrid.Children.Add(prevBtn);
        }

        var editBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE70F", FontSize = 10 },
                    new TextBlock { Text = "Edit", FontSize = 11 }
                }
            },
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Tag = card
        };
        editBtn.Click += (s, e) => ShowCardDialog(card);
        Grid.SetColumn(editBtn, 1);
        bottomGrid.Children.Add(editBtn);

        var nextCol = GetNextColumn(columnId);
        if (nextCol is not null)
        {
            var nextBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE72A", FontSize = 10 },
                Padding = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Tag = nextCol
            };
            ToolTipService.SetToolTip(nextBtn, $"Advance to {GetColumnTitle(nextCol)}");
            nextBtn.Click += (s, e) =>
            {
                services.Database.MoveKanbanCard(card.Id, nextCol);
                Refresh();
            };
            Grid.SetColumn(nextBtn, 2);
            bottomGrid.Children.Add(nextBtn);
        }

        mainStack.Children.Add(bottomGrid);
        border.Child = mainStack;
        return border;
    }

    private static string? GetPreviousColumn(string col) => col.ToLowerInvariant() switch
    {
        "sketch" => "ideas",
        "render" => "sketch",
        "done" => "render",
        _ => null
    };

    private static string? GetNextColumn(string col) => col.ToLowerInvariant() switch
    {
        "ideas" => "sketch",
        "sketch" => "render",
        "render" => "done",
        _ => null
    };

    private static string GetColumnTitle(string col) => col.ToLowerInvariant() switch
    {
        "ideas" => "Idea & Backlog",
        "sketch" => "Sketch & Lineart",
        "render" => "Color & Render",
        "done" => "Completed",
        _ => col
    };

    private async void ShowCardDialog(KanbanCardRecord? existing = null, string defaultColumn = "ideas")
    {
        var titleBox = new TextBox { Header = "Project Title", Text = existing?.Title ?? "", PlaceholderText = "e.g. Cyberpunk Character Concept" };
        var descBox = new TextBox { Header = "Description & Notes", Text = existing?.Description ?? "", PlaceholderText = "Concept details, canvas dimensions, client specs…", AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
        var tagsBox = new TextBox { Header = "Tags", Text = existing?.Tags ?? "#Commission", PlaceholderText = "#Commission, #Personal, #Study, #Manga" };

        var stageCombo = new ComboBox
        {
            Header = "Production Stage",
            ItemsSource = new[] { "Idea & Backlog", "Sketch & Lineart", "Color & Render", "Completed" },
            SelectedIndex = (existing?.ColumnId ?? defaultColumn).ToLowerInvariant() switch
            {
                "sketch" => 1,
                "render" => 2,
                "done" => 3,
                _ => 0
            },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var prioCombo = new ComboBox
        {
            Header = "Priority",
            ItemsSource = new[] { "High", "Medium", "Low" },
            SelectedIndex = (existing?.Priority ?? "Medium").ToLowerInvariant() switch
            {
                "high" => 0,
                "low" => 2,
                _ => 1
            },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var projects = services.Database.GetProjects(100);
        var canvasList = new List<string> { "None (Not linked)" };
        canvasList.AddRange(projects.Select(x => x.CanvasName));

        var canvasCombo = new ComboBox
        {
            Header = "Link with Tracked Artwork",
            ItemsSource = canvasList,
            SelectedIndex = existing is not null && !string.IsNullOrWhiteSpace(existing.LinkedCanvas) ? Math.Max(0, canvasList.IndexOf(existing.LinkedCanvas)) : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var existingChecklistLines = existing is not null ? string.Join(Environment.NewLine, existing.GetChecklist().Select(x => x.Text)) : "Thumbnails / Concept\nRough Sketch\nLineart\nFlat Colors\nRendering & Shading";
        var checklistBox = new TextBox
        {
            Header = "Checklist Steps (One per line)",
            Text = existingChecklistLines,
            AcceptsReturn = true,
            Height = 100,
            TextWrapping = TextWrapping.Wrap
        };

        var panel = new StackPanel { Spacing = 10, Width = 380 };
        panel.Children.Add(titleBox);
        panel.Children.Add(descBox);
        panel.Children.Add(stageCombo);
        panel.Children.Add(prioCombo);
        panel.Children.Add(tagsBox);
        panel.Children.Add(canvasCombo);
        panel.Children.Add(checklistBox);

        var dialog = new ContentDialog
        {
            Title = existing is null ? "New Art Project Card" : "Edit Project Card",
            Content = new ScrollViewer { Content = panel, MaxHeight = 500 },
            PrimaryButtonText = "Save Card",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var title = titleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var colId = stageCombo.SelectedIndex switch
            {
                1 => "sketch",
                2 => "render",
                3 => "done",
                _ => "ideas"
            };

            var prio = prioCombo.SelectedItem?.ToString() ?? "Medium";
            var linked = canvasCombo.SelectedIndex > 0 ? canvasCombo.SelectedItem?.ToString() ?? "" : "";

            var checkItems = checklistBox.Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line =>
                {
                    var wasDone = existing?.GetChecklist().FirstOrDefault(x => string.Equals(x.Text, line, StringComparison.OrdinalIgnoreCase))?.IsDone ?? false;
                    return new ChecklistItem(line, wasDone);
                })
                .ToList();

            var checkJson = JsonSerializer.Serialize(checkItems);

            if (existing is null)
            {
                services.Database.AddKanbanCard(title, descBox.Text, colId, tagsBox.Text, prio, linked, checkJson);
            }
            else
            {
                services.Database.UpdateKanbanCard(existing.Id, title, descBox.Text, colId, tagsBox.Text, prio, linked, checkJson);
            }

            Refresh();
        }
    }
}
