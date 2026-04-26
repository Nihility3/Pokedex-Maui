using Pokedex1.Models;

namespace Pokedex1;

public partial class DetailPage : ContentPage
{
    public DetailPage(Pokemon pokemon)
    {
        InitializeComponent();
        BindingContext = pokemon; // This maps the UI to the selected Pokemon's data
    }
}
