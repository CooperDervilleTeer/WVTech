using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Models;

[Table("Measurement")]
[Index(nameof(Name), IsUnique = true)]
public class Measurement
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; }

    [Required]
    [MaxLength(64)]
    public string Abbreviation { get; set; }

    public int SortOrder { get; set; }

    public override int GetHashCode()
    {
        return this.Name?.GetHashCode() ?? 0;
    }
    
    public override bool Equals(object? obj)
    {
        if (obj == null || obj is not Measurement)
        {
            return false;
        }
        if (this.Name != ((Measurement) obj).Name)
        {
            return false;
        }

        return true;
    }
}