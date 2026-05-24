using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MealPlanner.Models
{
    public class MealPlannerDBContext : IdentityDbContext<User>
    {
        public MealPlannerDBContext(DbContextOptions<MealPlannerDBContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserNutritionPreference> UserNutritionPreferences { get; set; }
        public DbSet<Meal> Meals { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<DietaryRestriction> DietaryRestrictions { get; set; }
        public DbSet<UserDietaryRestriction> UserDietaryRestrictions { get; set; }

        public DbSet<Tag> Tags { get; set; }
        public DbSet<UserFoodPreference> UserFoodPreferences { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<DismissedShoppingItem> DismissedShoppingItems { get; set; }
        public DbSet<MealCompletion> MealCompletions { get; set; }
        public DbSet<MealExclusion> MealExclusions { get; set; }
        public DbSet<KrogerExport> KrogerExports { get; set; }
        public DbSet<KrogerExportItem> KrogerExportItems { get; set; }
        public DbSet<MealAutoRemovedIngredient> MealAutoRemovedIngredients { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Recipe>(b =>
            {
                b.HasData(
                    new Recipe { Id = -1, Name = "Oatmeal Cookies", Directions = "", Calories = 250 },
                    new Recipe { Id = -2, Name = "Spaghetti All'assassina", Directions = "", Calories = 400 },
                    new Recipe { Id = -3, Name = "Spaghetti and Meatballs", Directions = "", Calories = 1000 },
                    new Recipe { Id = -4, Name = "Vegan Spaghetti with Mushrooms", Directions = "", Calories = 350 },
                    new Recipe { Id = -5, Name = "Baked Spaghetti Casserole", Directions = "", Calories = 400 },
                    new Recipe { Id = -6, Name = "Mac 'n Cheese Casserole", Directions = "", Calories = 550 },
                    new Recipe { Id = -7, Name = "Homemade Mac 'n Cheese", Directions = "", Calories = 850 },
                    new Recipe { Id = -8, Name = "Mushroom Steak Salad", Directions = "", Calories = 400 },
                    new Recipe { Id = -9, Name = "Ceasar Salad", Directions = "", Calories = 300 }
                );
            });

            modelBuilder.Entity<MealCompletion>()
                .HasKey(mc => new { mc.MealId, mc.CompletionDate });

            modelBuilder.Entity<MealCompletion>()
                .HasOne(mc => mc.Meal)
                .WithMany()
                .HasForeignKey(mc => mc.MealId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MealExclusion>()
                .HasKey(me => new { me.MealId, me.ExclusionDate });

            modelBuilder.Entity<MealExclusion>()
                .HasOne(me => me.Meal)
                .WithMany()
                .HasForeignKey(me => me.MealId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDietaryRestriction>()
                .HasKey(udr => new { udr.UserId, udr.DietaryRestrictionId });

            modelBuilder.Entity<UserDietaryRestriction>()
                .HasOne(udr => udr.User)
                .WithMany()
                .HasForeignKey(udr => udr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDietaryRestriction>()
                .HasOne(udr => udr.DietaryRestriction)
                .WithMany()
                .HasForeignKey(udr => udr.DietaryRestrictionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ingredient>()
                .Navigation(i => i.IngredientBase)
                .AutoInclude();

            modelBuilder.Entity<Ingredient>()
                .Navigation(i => i.Measurement)
                .AutoInclude();

            modelBuilder.Entity<ShoppingListItem>()
                .Navigation(i => i.IngredientBase)
                .AutoInclude();

            modelBuilder.Entity<ShoppingListItem>()
                .Navigation(i => i.Measurement)
                .AutoInclude();
        }
    }
}