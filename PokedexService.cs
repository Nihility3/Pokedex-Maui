using Pokedex1.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pokedex1.Services;

public static class PokemonService
{
    public static List<Pokemon> GetPokemon() => new()
    {
        new Pokemon {
            Id = 1, Name = "Bulbasaur", Type = "Grass",
            ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/1.png",
            Description = "A strange seed was planted on its back at birth.",
            TypeColor = Colors.Green, HP = 0.45, Attack = 0.49, Defense = 0.49
        },
        new Pokemon {
            Id = 4, Name = "Charmander", Type = "Fire",
            ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/4.png",
            Description = "The flame on its tail indicates its life force.",
            TypeColor = Colors.Orange, HP = 0.39, Attack = 0.52, Defense = 0.43
        },
        new Pokemon {
            Id = 7, Name = "Squirtle", Type = "Water",
            ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/7.png",
            Description = "Shoots water at prey while retreating into its shell.",
            TypeColor = Colors.Blue, HP = 0.44, Attack = 0.48, Defense = 0.65
        }
    };
}
