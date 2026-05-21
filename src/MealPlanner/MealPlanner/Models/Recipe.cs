using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Models;

[Table("Recipe")]
[Index(nameof(ExternalUri), IsUnique = true)]
public class Recipe
{
    private static readonly HashSet<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    public string Directions { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];

    public int Calories { get; set; }
    public int Protein { get; set; }
    public int Carbs { get; set; }
    public int Fat { get; set; }
    public string? ExternalUri { get; set; }
    public string? ImageUrl { get; set; }
    public List<Meal> Meals { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];

    // Edamam's recipe categorization (dietLabels / healthLabels / cuisineType /
    // mealType / dishType) — present only on recipes coming back from the
    // external stream, used by EdamamTagClassifier.ResolveLocalTags to attach
    // local Tag entities so the tag-based scorers can act on them. Transient:
    // never persisted (cached external rows are URI-only per Edamam's TOS).
    [NotMapped]
    public List<string> ExternalCategorization { get; set; } = [];
    public List<User> Users { get; } = [];
    public List<UserRecipe> UserRecipes { get; } = [];

    public async Task SaveImageAsync(IFormFile? imageFile, BlobContainerClient blobContainer)
    {
        if (imageFile == null || imageFile.Length == 0) return;
        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext)) return;

        var blobName = $"{Guid.NewGuid()}{ext}";
        var blobClient = blobContainer.GetBlobClient(blobName);
        using var stream = imageFile.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = imageFile.ContentType });
        ImageUrl = blobClient.Uri.ToString();
    }

    public async Task SaveImageAsync(IFormFile? imageFile, string webRootPath)
    {
        if (imageFile == null || imageFile.Length == 0) return;
        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext)) return;

        var dir = Path.Combine(webRootPath, "images", "recipes");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid()}{ext}";
        using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await imageFile.CopyToAsync(stream);
        ImageUrl = $"/images/recipes/{fileName}";
    }

    public static async Task DeleteImageAsync(string? imageUrl, BlobContainerClient? blobContainer, string? webRootPath = null)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        if (imageUrl.Contains(".blob.core.windows.net") && blobContainer != null)
        {
            var blobName = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
            await blobContainer.GetBlobClient(blobName).DeleteIfExistsAsync();
        }
        else if (imageUrl.StartsWith("/images/recipes/") && webRootPath != null)
        {
            var filePath = Path.Combine(webRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}
