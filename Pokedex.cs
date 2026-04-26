using System;
using System.Collections.Generic;
using System.Text;

namespace Pokedex1.Models;

public class Pokemon
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string ImageUrl { get; set; }
    public string Description { get; set; }
    public Color TypeColor { get; set; }
    public double HP { get; set; }
    public double Attack { get; set; }
    public double Defense { get; set; }
}
