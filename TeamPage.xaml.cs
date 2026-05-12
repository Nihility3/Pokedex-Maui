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
        var allTypes = new List<string> { "Fire", "Water", "Grass", "Electric", "Ice", "Ground" }; // Add more

        foreach (var type in allTypes)
        {
            // Count how many team members are weak to this specific type
            int weaknessCount = CurrentTeam.Count(p => p.Weaknesses.Contains(type));

            var badge = new Frame
            {
                BackgroundColor = GetTypeColor(type),
                Content = new Label { Text = $"{type}: {weaknessCount}", TextColor = Colors.White }
            };  
            DefenseGrid.Children.Add(badge);
        }
    }

    private Color GetTypeColor(string type) => type switch
    {
        "Fire" => Colors.Red,
        "Water" => Colors.Blue,
        "Grass" => Colors.Green,
        _ => Colors.Gray
    };
    // Inside your TeamPage.xaml.cs
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