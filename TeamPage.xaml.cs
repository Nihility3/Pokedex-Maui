using Microsoft.Maui.Graphics; 
using Pokedex1.Models;
using System.Collections.ObjectModel;
using System.Linq; 
using Pokedex1.Services;

namespace Pokedex1;

public partial class TeamPage : ContentPage
{
    public ObservableCollection<Pokemon> CurrentTeam { get; set; } = new();

    public TeamPage(List<Pokemon> team)
    {
        InitializeComponent();
        foreach (var p in team) CurrentTeam.Add(p);
        BindingContext = this;
        CalculateMetrics();
    }

    private void CalculateMetrics()
    {
        DefenseGrid.Children.Clear();
        StrengthGrid.Children.Clear();
        var allTypes = TypeChart.Types;

        foreach (var type in allTypes)
        {
            int weaknessCount = CurrentTeam.Count(p => TypeChart.GetDefenseMultiplier(type, p.Types.Count > 0 ? p.Types : new List<string> { p.Type }) > 1);
            if (weaknessCount > 0)
                DefenseGrid.Children.Add(CreateBadge($"{type}: {weaknessCount}", GetTypeColor(type)));

            int strongCount = CurrentTeam.Count(p => (p.Types.Count > 0 ? p.Types : new List<string> { p.Type }).Any(teamType => TypeChart.GetAttackMultiplier(teamType, type) > 1));
            if (strongCount > 0)
                StrengthGrid.Children.Add(CreateBadge($"{type}: {strongCount}", GetTypeColor(type)));
        }
    }

    private Color GetTypeColor(string type) => type switch
    {
        "Normal" => Colors.Gray,
        "Fire" => Colors.Red,
        "Water" => Colors.Blue,
        "Grass" => Colors.Green,
        "Electric" => Colors.Gold,
        "Ice" => Colors.LightBlue,
        "Fighting" => Colors.DarkRed,
        "Poison" => Colors.Purple,
        "Ground" => Colors.SaddleBrown,
        "Flying" => Colors.SkyBlue,
        "Psychic" => Colors.DeepPink,
        "Bug" => Colors.OliveDrab,
        "Rock" => Colors.Sienna,
        "Ghost" => Colors.Indigo,
        "Dragon" => Colors.MediumPurple,
        "Dark" => Colors.Black,
        "Steel" => Colors.SlateGray,
        "Fairy" => Colors.HotPink,
        _ => Colors.Gray
    };

    private static Border CreateBadge(string text, Color color) => new()
    {
        BackgroundColor = color,
        Padding = 8,
        Margin = 4,
        Content = new Label { Text = text, TextColor = Colors.White }
    };

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        var button = (Button)sender;
        var pokemonToRemove = (Pokemon)button.CommandParameter;

        if (pokemonToRemove != null)
        {
            // Remove from the UI collection
            CurrentTeam.Remove(pokemonToRemove);

            // Also remove from your global static list to keep them synced
            PokemonService.MyTeam.Remove(pokemonToRemove);

            // Refresh the defense metrics since the team changed
            CalculateMetrics();
        }
    }
}
