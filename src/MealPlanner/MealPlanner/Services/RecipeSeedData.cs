using MealPlanner.Models;

namespace MealPlanner.Services;

// Recipes here use "shell" IngredientBase, Measurement, and Tag instances
// (Id = 0, Name populated). SeedService.SeedRecipesAsync resolves them against
// already-tracked entities so the unique-name constraints on IngredientBase,
// Measurement, and Tag are respected across the whole seed batch.
//
// Most recipes below represent one serving. A handful yield multiple servings
// (batch bakes, dips, cookies, etc.): for those, the ingredient amounts are for
// the full batch but the stored Calories/Protein/Carbs/Fat are per serving.
// The "// yields N servings" comment on those recipes marks them so we can add
// a Servings column to Recipe later without having to re-derive this info.
public static class RecipeSeedData
{
    private static Ingredient I(float amount, string measurement, string ingredient) => new()
    {
        Amount = amount,
        DisplayName = ingredient,
        Measurement = new Measurement { Name = measurement, Abbreviation = measurement },
        IngredientBase = new IngredientBase { Name = IngredientNameNormalizer.NormalizeKey(ingredient) },
    };

    private static List<Tag> T(params string[] names) =>
        names.Select(n => new Tag { Name = n }).ToList();

    public static List<Recipe> GetRecipes() => new()
    {
        // 1. Veggie Scramble
        new Recipe
        {
            Name = "Veggie Scramble",
            Calories = 280, Protein = 22, Carbs = 6, Fat = 18,
            Directions = "Heat olive oil in a non-stick skillet over medium heat. Add spinach and sauté until wilted, about 1 minute. Whisk the eggs with salt and pepper, pour into the pan, and gently stir until just set, about 2 minutes. Sprinkle cheese over the top, remove from heat, and let the residual warmth melt the cheese before serving.",
            Ingredients =
            {
                I(3, "Count", "Egg"),
                I(1, "cup", "Spinach"),
                I(0.25f, "cup", "Cheddar Cheese"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Breakfast", "Vegetarian", "Gluten-Free", "High Protein", "Low Calorie", "Keto", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 2. Greek Yogurt Berry Parfait
        new Recipe
        {
            Name = "Greek Yogurt Berry Parfait",
            Calories = 320, Protein = 18, Carbs = 42, Fat = 8,
            Directions = "In a tall glass, layer half of the Greek yogurt, then half of the berries, then half of the granola. Repeat with the remaining ingredients. Drizzle honey over the top and serve immediately.",
            Ingredients =
            {
                I(1, "cup", "Plain Greek Yogurt"),
                I(0.5f, "cup", "Mixed Berries"),
                I(0.33f, "cup", "Granola"),
                I(1, "tablespoon", "Honey"),
            },
            Tags = T("Breakfast", "Snack", "Vegetarian", "Quick & Easy", "High Protein", "Halal", "Kosher"),
        },

        // 3. Avocado Toast with Tomato
        new Recipe
        {
            Name = "Avocado Toast with Tomato",
            Calories = 340, Protein = 9, Carbs = 38, Fat = 18,
            Directions = "Toast the bread to golden brown. Mash the avocado with lemon juice and salt in a small bowl. Spread the avocado mixture evenly on each slice of toast. Top with thin slices of tomato and sprinkle red pepper flakes over the top.",
            Ingredients =
            {
                I(2, "Count", "Whole Grain Bread"),
                I(1, "Count", "Avocado"),
                I(0.5f, "Count", "Roma Tomato"),
                I(1, "teaspoon", "Lemon Juice"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Red Pepper Flakes"),
            },
            Tags = T("Breakfast", "Lunch", "Vegan", "Vegetarian", "Dairy-Free", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 4. Protein Oatmeal Bowl
        new Recipe
        {
            Name = "Protein Oatmeal Bowl",
            Calories = 420, Protein = 32, Carbs = 52, Fat = 10,
            Directions = "Combine oats and milk in a small pot and bring to a simmer over medium heat, stirring occasionally, until thickened, about 5 minutes. Remove from heat, let cool slightly, then stir in the whey protein. Top with sliced banana, chia seeds, and cinnamon.",
            Ingredients =
            {
                I(0.5f, "cup", "Rolled Oats"),
                I(1, "cup", "Milk"),
                I(1, "Count", "Vanilla Whey Protein"),
                I(1, "Count", "Banana"),
                I(1, "tablespoon", "Chia Seeds"),
                I(1, "teaspoon", "Cinnamon"),
            },
            Tags = T("Breakfast", "Vegetarian", "High Protein", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 5. Grilled Chicken Caesar Wrap
        new Recipe
        {
            Name = "Grilled Chicken Caesar Wrap",
            Calories = 520, Protein = 38, Carbs = 45, Fat = 22,
            Directions = "Season the chicken breast with salt and pepper, then grill over medium-high heat for 6 minutes per side or until cooked through. Let rest for 5 minutes and slice. Toss romaine with caesar dressing, parmesan, and croutons in a bowl. Spread the salad mixture on the tortilla, top with the sliced chicken, and roll tightly. Slice in half to serve.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(1, "Count", "Flour Tortilla"),
                I(2, "cup", "Romaine Lettuce"),
                I(3, "tablespoon", "Caesar Dressing"),
                I(0.25f, "cup", "Parmesan Cheese"),
                I(2, "tablespoon", "Crouton"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Lunch", "High Protein", "Quick & Easy", "Halal", "American", "BBQ"),
        },

        // 6. Quinoa Buddha Bowl
        new Recipe
        {
            Name = "Quinoa Buddha Bowl",
            Calories = 480, Protein = 18, Carbs = 62, Fat = 18,
            Directions = "Preheat the oven to 400°F. Cube the sweet potato, toss with olive oil and a pinch of salt, and roast on a sheet pan for 25 minutes. Meanwhile, massage kale with a pinch of salt until tender. Assemble the bowl with quinoa as the base, then arrange chickpeas, roasted sweet potato, massaged kale, and avocado on top. Whisk tahini with lemon juice and a splash of water, and drizzle over the bowl before serving.",
            Ingredients =
            {
                I(1, "cup", "Cooked Quinoa"),
                I(0.5f, "cup", "Chickpeas"),
                I(1, "cup", "Sweet Potato"),
                I(1, "cup", "Kale"),
                I(0.25f, "Count", "Avocado"),
                I(2, "tablespoon", "Tahini"),
                I(1, "tablespoon", "Lemon Juice"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
            },
            Tags = T("Lunch", "Dinner", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Kosher"),
        },

        // 7. Turkey and Hummus Wrap
        new Recipe
        {
            Name = "Turkey and Hummus Wrap",
            Calories = 460, Protein = 32, Carbs = 48, Fat = 16,
            Directions = "Lay the tortilla flat and spread hummus evenly across it. Layer turkey slices, spinach, thin cucumber slices, and thin strips of red bell pepper. Fold in the sides and roll tightly. Slice diagonally to serve.",
            Ingredients =
            {
                I(5, "ounce", "Sliced Turkey Breast"),
                I(1, "Count", "Whole Wheat Tortilla"),
                I(3, "tablespoon", "Hummus"),
                I(0.5f, "cup", "Baby Spinach"),
                I(0.25f, "Count", "Cucumber"),
                I(0.25f, "Count", "Red Bell Pepper"),
            },
            Tags = T("Lunch", "High Protein", "Quick & Easy", "Dairy-Free", "Halal", "Nut Allergy", "Mediterranean"),
        },

        // 8. Tomato Basil Soup
        new Recipe
        {
            Name = "Tomato Basil Soup",
            Calories = 220, Protein = 6, Carbs = 32, Fat = 8,
            Directions = "Heat olive oil in a large pot over medium heat. Add diced onion and sauté until translucent, about 5 minutes. Stir in minced garlic and cook for 30 seconds. Add crushed tomatoes and vegetable broth, and bring to a simmer. Cover and cook for 15 minutes. Remove from heat and blend with an immersion blender until smooth. Stir in torn basil leaves, salt, and pepper, and serve.",
            Ingredients =
            {
                I(28, "ounce", "Canned Crushed Tomatoes"),
                I(1, "Count", "Yellow Onion"),
                I(3, "Count", "Garlic"),
                I(0.25f, "cup", "Fresh Basil"),
                I(2, "cup", "Vegetable Broth"),
                I(2, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Lunch", "Dinner", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Low Calorie", "Halal", "Kosher", "Nut Allergy", "Italian"),
        },

        // 9. Grilled Salmon with Asparagus
        new Recipe
        {
            Name = "Grilled Salmon with Asparagus",
            Calories = 440, Protein = 40, Carbs = 10, Fat = 26,
            Directions = "Preheat a grill or grill pan to medium-high heat. Brush salmon and asparagus with olive oil and season with salt, pepper, and minced garlic. Grill salmon skin-side down for 5 minutes, flip, and cook an additional 3 minutes. Grill asparagus alongside, turning once, until tender and slightly charred, about 6 minutes total. Squeeze lemon over both before serving.",
            Ingredients =
            {
                I(6, "ounce", "Salmon Fillet"),
                I(10, "Count", "Asparagus"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "Count", "Lemon"),
                I(2, "Count", "Garlic"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Keto", "Kosher", "Nut Allergy", "BBQ"),
        },

        // 10. Vegetable Tofu Stir Fry
        new Recipe
        {
            Name = "Vegetable Tofu Stir Fry",
            Calories = 380, Protein = 24, Carbs = 36, Fat = 16,
            Directions = "Press tofu between paper towels for 10 minutes to remove excess moisture, then cube. Heat sesame oil in a large wok over high heat. Add tofu and sear until golden on all sides, about 5 minutes; remove and set aside. In the same wok, stir fry minced garlic, ginger, broccoli, bell pepper, and snap peas for 4 minutes. Return tofu to the pan, add soy sauce, toss to coat, and cook 1 more minute before serving.",
            Ingredients =
            {
                I(8, "ounce", "Firm Tofu"),
                I(1, "cup", "Broccoli Floret"),
                I(1, "Count", "Red Bell Pepper"),
                I(1, "cup", "Snap Peas"),
                I(2, "tablespoon", "Soy Sauce"),
                I(1, "tablespoon", "Sesame Oil"),
                I(2, "Count", "Garlic"),
                I(1, "tablespoon", "Fresh Ginger"),
            },
            Tags = T("Dinner", "Vegan", "Vegetarian", "Dairy-Free", "High Protein", "Halal", "Kosher", "Nut Allergy", "Chinese"),
        },

        // 11. Chicken Fajita Bowl
        new Recipe
        {
            Name = "Chicken Fajita Bowl",
            Calories = 560, Protein = 42, Carbs = 48, Fat = 20,
            Directions = "Slice the chicken, bell peppers, and onion into strips. Toss the chicken with olive oil, chili powder, cumin, and salt. Heat a large skillet over medium-high heat and cook the chicken for 6 minutes until browned and cooked through. Add the peppers and onion, and sauté until tender-crisp, about 4 more minutes. Build the bowl with brown rice on the bottom, the fajita mixture over the top, and garnish with black beans, sliced avocado, and salsa.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(1, "cup", "Cooked Brown Rice"),
                I(1, "Count", "Red Bell Pepper"),
                I(1, "Count", "Green Bell Pepper"),
                I(0.5f, "Count", "Yellow Onion"),
                I(0.25f, "cup", "Black Beans"),
                I(0.25f, "Count", "Avocado"),
                I(2, "tablespoon", "Salsa"),
                I(1, "tablespoon", "Olive Oil"),
                I(1, "teaspoon", "Chili Powder"),
                I(1, "teaspoon", "Cumin"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Dinner", "Lunch", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "Mexican", "Spicy"),
        },

        // 12. Baked Lemon Garlic Tilapia
        new Recipe
        {
            Name = "Baked Lemon Garlic Tilapia",
            Calories = 260, Protein = 34, Carbs = 4, Fat = 12,
            Directions = "Preheat the oven to 400°F. Place tilapia on a parchment-lined baking sheet. Whisk olive oil, juice from half the lemon, and minced garlic, then spoon over the fish. Season with salt and pepper. Top with thin lemon slices from the remaining half. Bake for 12 minutes, or until the fish flakes easily with a fork. Garnish with chopped parsley before serving.",
            Ingredients =
            {
                I(6, "ounce", "Tilapia Fillet"),
                I(1, "Count", "Lemon"),
                I(3, "Count", "Garlic"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Fresh Parsley"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Low Calorie", "Keto", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 13. Hearty Beef Chili
        new Recipe
        {
            Name = "Hearty Beef Chili",
            Calories = 540, Protein = 36, Carbs = 42, Fat = 22,
            Directions = "Brown ground beef in a large pot over medium-high heat, breaking it apart as it cooks, about 7 minutes. Drain excess fat. Add diced onion and minced garlic, and cook until softened, about 4 minutes. Stir in chili powder, cumin, paprika, and salt and toast for 30 seconds. Add diced tomatoes, kidney beans, and black beans. Reduce heat to low, cover partially, and simmer for 25 minutes, stirring occasionally. Serve hot.",
            Ingredients =
            {
                I(1, "pound", "Ground Beef"),
                I(1, "Count", "Yellow Onion"),
                I(3, "Count", "Garlic"),
                I(15, "ounce", "Canned Kidney Beans"),
                I(15, "ounce", "Canned Black Beans"),
                I(28, "ounce", "Canned Diced Tomatoes"),
                I(2, "tablespoon", "Chili Powder"),
                I(1, "tablespoon", "Cumin"),
                I(1, "teaspoon", "Paprika"),
                I(0.5f, "teaspoon", "Salt"),
            },
            Tags = T("Dinner", "Lunch", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "American", "Mexican", "Spicy", "Comfort Food"),
        },

        // 14. Apple with Almond Butter
        new Recipe
        {
            Name = "Apple with Almond Butter",
            Calories = 250, Protein = 7, Carbs = 28, Fat = 12,
            Directions = "Core the apple and slice into wedges. Sprinkle the apple slices with cinnamon. Serve alongside almond butter for dipping.",
            Ingredients =
            {
                I(1, "Count", "Apple"),
                I(2, "tablespoon", "Almond Butter"),
                I(1, "teaspoon", "Cinnamon"),
            },
            Tags = T("Snack", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Quick & Easy", "Halal", "Kosher"),
        },

        // 15. Dark Chocolate Avocado Mousse
        new Recipe
        {
            Name = "Dark Chocolate Avocado Mousse",
            Calories = 320, Protein = 6, Carbs = 32, Fat = 22,
            Directions = "Scoop the avocado into a food processor with cocoa powder, maple syrup, oat milk, vanilla extract, and salt. Blend until completely smooth, scraping down the sides as needed, about 2 minutes. Divide between two small bowls, cover, and chill in the refrigerator for at least 30 minutes before serving.",
            Ingredients =
            {
                I(1, "Count", "Avocado"),
                I(0.25f, "cup", "Cocoa Powder"),
                I(0.25f, "cup", "Maple Syrup"),
                I(2, "tablespoon", "Oat Milk"),
                I(1, "teaspoon", "Vanilla Extract"),
                I(0.125f, "teaspoon", "Salt"),
            },
            Tags = T("Dessert", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Nut Allergy", "Halal", "Kosher"),
        },

        // 16. Blueberry Pancakes
        new Recipe
        {
            Name = "Blueberry Pancakes",
            Calories = 420, Protein = 14, Carbs = 68, Fat = 12,
            Directions = "Whisk flour, sugar, baking powder, and salt. In a separate bowl, beat egg with milk and melted butter, then stir into dry ingredients until just combined and fold in blueberries. Pour 0.25-cup portions onto a hot greased skillet over medium heat, cook until bubbles form on top, flip, and cook one more minute. Serve with maple syrup.",
            Ingredients =
            {
                I(1, "cup", "All-Purpose Flour"),
                I(1, "cup", "Milk"),
                I(1, "Count", "Egg"),
                I(2, "tablespoon", "Sugar"),
                I(2, "teaspoon", "Baking Powder"),
                I(0.25f, "teaspoon", "Salt"),
                I(2, "tablespoon", "Butter"),
                I(0.5f, "cup", "Blueberries"),
                I(2, "tablespoon", "Maple Syrup"),
            },
            Tags = T("Breakfast", "Vegetarian", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "American", "Comfort Food"),
        },

        // 17. Western Omelet
        new Recipe
        {
            Name = "Western Omelet",
            Calories = 460, Protein = 32, Carbs = 6, Fat = 32,
            Directions = "Melt butter in a non-stick skillet over medium heat and sauté diced bell pepper and onion until softened, about 3 minutes. Add diced ham and heat 1 minute. Whisk eggs with salt and pepper, pour into skillet, and let set 1 minute, then sprinkle cheese over one half. Fold, slide onto a plate, and serve.",
            Ingredients =
            {
                I(3, "Count", "Egg"),
                I(2, "ounce", "Ham"),
                I(0.25f, "cup", "Green Bell Pepper"),
                I(0.25f, "cup", "Yellow Onion"),
                I(0.25f, "cup", "Cheddar Cheese"),
                I(1, "tablespoon", "Butter"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Breakfast", "Gluten-Free", "High Protein", "Quick & Easy", "Keto", "Nut Allergy", "American", "Comfort Food"),
        },

        // 18. Overnight Oats
        new Recipe
        {
            Name = "Overnight Oats",
            Calories = 380, Protein = 20, Carbs = 58, Fat = 8,
            Directions = "In a mason jar, combine oats, milk, honey, chia seeds, and whey protein and stir thoroughly. Cover and refrigerate at least 6 hours or overnight. Top with mixed berries before serving.",
            Ingredients =
            {
                I(0.5f, "cup", "Rolled Oats"),
                I(1, "cup", "Milk"),
                I(1, "tablespoon", "Honey"),
                I(1, "tablespoon", "Chia Seeds"),
                I(0.5f, "cup", "Mixed Berries"),
                I(0.5f, "Count", "Vanilla Whey Protein"),
            },
            Tags = T("Breakfast", "Vegetarian", "Quick & Easy", "High Protein", "Halal", "Kosher", "Nut Allergy"),
        },

        // 19. Breakfast Burrito
        new Recipe
        {
            Name = "Breakfast Burrito",
            Calories = 540, Protein = 28, Carbs = 52, Fat = 24,
            Directions = "Melt butter in a skillet over medium heat, whisk eggs with salt, and scramble until just set. Warm the tortilla in a dry skillet for 20 seconds. Pile eggs, cheese, black beans, salsa, and sliced avocado down the center, fold in the sides, and roll tightly.",
            Ingredients =
            {
                I(1, "Count", "Flour Tortilla"),
                I(3, "Count", "Egg"),
                I(0.25f, "cup", "Cheddar Cheese"),
                I(0.25f, "cup", "Black Beans"),
                I(2, "tablespoon", "Salsa"),
                I(0.25f, "Count", "Avocado"),
                I(1, "tablespoon", "Butter"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Breakfast", "High Protein", "Halal", "Nut Allergy", "Mexican", "American", "Comfort Food"),
        },

        // 20. Spinach Feta Egg Muffins
        // yields 6 servings of 2 muffins each (12 muffins total); nutrients listed are per 2-muffin serving
        new Recipe
        {
            Name = "Spinach Feta Egg Muffins",
            Calories = 180, Protein = 16, Carbs = 4, Fat = 12,
            Directions = "Preheat oven to 375°F and grease a 12-cup muffin tin with olive oil. Divide chopped spinach and feta among the cups. Whisk eggs with milk, salt, and pepper and pour over the fillings, filling each cup three-quarters full. Bake 20 minutes until set. Serves 6 at 2 muffins each.",
            Ingredients =
            {
                I(6, "Count", "Egg"),
                I(1, "cup", "Spinach"),
                I(0.5f, "cup", "Feta Cheese"),
                I(0.25f, "cup", "Milk"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
                I(1, "tablespoon", "Olive Oil"),
            },
            Tags = T("Breakfast", "Snack", "Vegetarian", "Gluten-Free", "Low Calorie", "High Protein", "Keto", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 21. Chia Seed Pudding
        new Recipe
        {
            Name = "Chia Seed Pudding",
            Calories = 260, Protein = 8, Carbs = 32, Fat = 12,
            Directions = "Whisk chia seeds, oat milk, maple syrup, and vanilla in a jar, cover, and refrigerate 4 hours or overnight, stirring once after 30 minutes to prevent clumping. Top with mixed berries before serving.",
            Ingredients =
            {
                I(0.33f, "cup", "Chia Seeds"),
                I(1, "cup", "Oat Milk"),
                I(1, "tablespoon", "Maple Syrup"),
                I(0.5f, "teaspoon", "Vanilla Extract"),
                I(0.5f, "cup", "Mixed Berries"),
            },
            Tags = T("Breakfast", "Snack", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 22. Steel Cut Oatmeal with Cinnamon Apples
        new Recipe
        {
            Name = "Steel Cut Oatmeal with Cinnamon Apples",
            Calories = 340, Protein = 8, Carbs = 62, Fat = 6,
            Directions = "Bring water to a boil, add oats and salt, and simmer uncovered 25 minutes until thick. In a skillet, cook diced apple with cinnamon and 1 tablespoon water until softened, about 5 minutes. Top oats with cinnamon apples and drizzle with maple syrup.",
            Ingredients =
            {
                I(0.5f, "cup", "Steel Cut Oats"),
                I(2, "cup", "Water"),
                I(1, "Count", "Apple"),
                I(1, "tablespoon", "Maple Syrup"),
                I(1, "teaspoon", "Cinnamon"),
                I(0.125f, "teaspoon", "Salt"),
            },
            Tags = T("Breakfast", "Vegan", "Vegetarian", "Dairy-Free", "Halal", "Kosher", "Nut Allergy"),
        },

        // 23. Classic Chicken Caesar Salad
        new Recipe
        {
            Name = "Classic Chicken Caesar Salad",
            Calories = 460, Protein = 40, Carbs = 12, Fat = 28,
            Directions = "Season chicken with salt and pepper, then sear in olive oil over medium-high heat 6 minutes per side until cooked through. Rest 5 minutes and slice. Toss chopped romaine with caesar dressing and parmesan, top with chicken, and serve.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(3, "cup", "Romaine Lettuce"),
                I(0.33f, "cup", "Parmesan Cheese"),
                I(3, "tablespoon", "Caesar Dressing"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Lunch", "Gluten-Free", "High Protein", "Keto", "Halal", "Nut Allergy", "American"),
        },

        // 24. Turkey Club Sandwich
        new Recipe
        {
            Name = "Turkey Club Sandwich",
            Calories = 580, Protein = 38, Carbs = 52, Fat = 22,
            Directions = "Toast bread and cook turkey bacon in a skillet until crisp, about 4 minutes per side. Spread mayo on one side of each slice; on the bottom layer turkey, Swiss, lettuce, and tomato. Add the middle slice mayo-up, then turkey bacon and more lettuce, and top with the final slice mayo-down. Secure with toothpicks and slice into quarters.",
            Ingredients =
            {
                I(3, "Count", "Whole Wheat Bread"),
                I(4, "ounce", "Sliced Turkey Breast"),
                I(2, "Count", "Turkey Bacon"),
                I(2, "Count", "Lettuce"),
                I(2, "Count", "Tomato"),
                I(2, "tablespoon", "Mayonnaise"),
                I(2, "Count", "Swiss Cheese"),
            },
            Tags = T("Lunch", "High Protein", "Quick & Easy", "Halal", "Nut Allergy", "American"),
        },

        // 25. Chickpea Smash Sandwich
        new Recipe
        {
            Name = "Chickpea Smash Sandwich",
            Calories = 420, Protein = 16, Carbs = 58, Fat = 14,
            Directions = "Mash chickpeas with hummus, lemon juice, salt, and pepper until mostly smooth with some texture remaining. Spread mixture on one slice of bread, top with lettuce and tomato, close with the second slice, and slice in half.",
            Ingredients =
            {
                I(2, "Count", "Whole Wheat Bread"),
                I(0.5f, "cup", "Chickpeas"),
                I(2, "tablespoon", "Hummus"),
                I(1, "tablespoon", "Lemon Juice"),
                I(2, "Count", "Lettuce"),
                I(2, "Count", "Tomato"),
                I(0.125f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Lunch", "Vegan", "Vegetarian", "Dairy-Free", "High Protein", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 26. Nicoise Salad
        new Recipe
        {
            Name = "Nicoise Salad",
            Calories = 480, Protein = 32, Carbs = 28, Fat = 26,
            Directions = "Boil eggs 9 minutes and shock in ice water; peel and halve. Boil diced potato 10 minutes until tender. Blanch green beans 2 minutes and shock in ice water. Whisk olive oil, lemon juice, dijon, and salt for dressing. Arrange greens on a plate with tuna, eggs, green beans, cherry tomatoes, olives, and potato, and drizzle with dressing.",
            Ingredients =
            {
                I(5, "ounce", "Canned Tuna"),
                I(3, "cup", "Mixed Greens"),
                I(2, "Count", "Egg"),
                I(0.5f, "cup", "Green Beans"),
                I(0.5f, "cup", "Cherry Tomatoes"),
                I(0.25f, "cup", "Kalamata Olives"),
                I(0.5f, "Count", "Red Potato"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Lemon Juice"),
                I(1, "teaspoon", "Dijon Mustard"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Lunch", "Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "Mediterranean"),
        },

        // 27. Red Lentil Soup
        new Recipe
        {
            Name = "Red Lentil Soup",
            Calories = 320, Protein = 18, Carbs = 48, Fat = 6,
            Directions = "Sauté diced onion and carrots in olive oil 5 minutes, add minced garlic, cumin, and turmeric and toast 30 seconds. Add rinsed lentils and broth, bring to a boil, then cover and simmer 20 minutes. Stir in salt and lemon juice; partially blend for a creamier texture if desired.",
            Ingredients =
            {
                I(1, "cup", "Red Lentils"),
                I(4, "cup", "Vegetable Broth"),
                I(1, "Count", "Yellow Onion"),
                I(2, "Count", "Carrot"),
                I(3, "Count", "Garlic"),
                I(1, "tablespoon", "Olive Oil"),
                I(1, "teaspoon", "Cumin"),
                I(0.5f, "teaspoon", "Turmeric"),
                I(0.5f, "teaspoon", "Salt"),
                I(1, "tablespoon", "Lemon Juice"),
            },
            Tags = T("Lunch", "Dinner", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "High Protein", "Low Calorie", "Halal", "Kosher", "Nut Allergy", "Indian", "Mediterranean"),
        },

        // 28. Thai Peanut Noodles
        new Recipe
        {
            Name = "Thai Peanut Noodles",
            Calories = 520, Protein = 18, Carbs = 68, Fat = 22,
            Directions = "Cook rice noodles per package, drain, and rinse in cold water. Whisk peanut butter, soy sauce, rice vinegar, maple syrup, and 2 tablespoons warm water until smooth. Toss noodles with sauce, shredded carrots, and cabbage, drizzle sesame oil, and garnish with chopped peanuts and cilantro.",
            Ingredients =
            {
                I(4, "ounce", "Rice Noodles"),
                I(2, "tablespoon", "Peanut Butter"),
                I(2, "tablespoon", "Soy Sauce"),
                I(1, "tablespoon", "Rice Vinegar"),
                I(1, "teaspoon", "Maple Syrup"),
                I(0.5f, "cup", "Carrot"),
                I(0.5f, "cup", "Cabbage"),
                I(1, "tablespoon", "Sesame Oil"),
                I(0.25f, "cup", "Peanuts"),
                I(2, "tablespoon", "Fresh Cilantro"),
            },
            Tags = T("Lunch", "Dinner", "Vegan", "Vegetarian", "Dairy-Free", "Quick & Easy", "Halal", "Kosher", "Thai"),
        },

        // 29. Buffalo Chicken Wrap
        new Recipe
        {
            Name = "Buffalo Chicken Wrap",
            Calories = 540, Protein = 36, Carbs = 48, Fat = 24,
            Directions = "Season chicken with salt, sear in olive oil over medium-high heat 6 minutes per side, rest, slice, and toss with buffalo sauce. Spread ranch on the tortilla, layer lettuce, carrots, buffalo chicken, and blue cheese, roll tightly, and slice diagonally.",
            Ingredients =
            {
                I(5, "ounce", "Chicken Breast"),
                I(1, "Count", "Flour Tortilla"),
                I(3, "tablespoon", "Buffalo Sauce"),
                I(2, "tablespoon", "Ranch Dressing"),
                I(1, "cup", "Lettuce"),
                I(0.25f, "cup", "Carrot"),
                I(0.25f, "cup", "Blue Cheese"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Lunch", "High Protein", "Quick & Easy", "Halal", "Nut Allergy", "American", "Spicy"),
        },

        // 30. Cobb Salad
        new Recipe
        {
            Name = "Cobb Salad",
            Calories = 580, Protein = 42, Carbs = 14, Fat = 38,
            Directions = "Cook turkey bacon in a skillet until crisp, about 4 minutes per side, and crumble. Arrange greens on a plate and top with rows of sliced chicken, turkey bacon, halved egg, diced avocado, blue cheese, and halved cherry tomatoes. Drizzle with ranch before serving.",
            Ingredients =
            {
                I(3, "cup", "Mixed Greens"),
                I(4, "ounce", "Chicken Breast"),
                I(2, "Count", "Turkey Bacon"),
                I(1, "Count", "Egg"),
                I(0.5f, "Count", "Avocado"),
                I(0.25f, "cup", "Blue Cheese"),
                I(0.33f, "cup", "Cherry Tomatoes"),
                I(3, "tablespoon", "Ranch Dressing"),
            },
            Tags = T("Lunch", "Dinner", "Gluten-Free", "High Protein", "Keto", "Halal", "Nut Allergy", "American"),
        },

        // 31. Greek Salad
        new Recipe
        {
            Name = "Greek Salad",
            Calories = 280, Protein = 9, Carbs = 16, Fat = 22,
            Directions = "Combine romaine, half-moon cucumber, halved cherry tomatoes, thinly sliced red onion, olives, and feta in a large bowl. Whisk olive oil, lemon juice, oregano, and salt and toss gently to coat.",
            Ingredients =
            {
                I(2, "cup", "Romaine Lettuce"),
                I(1, "Count", "Cucumber"),
                I(1, "cup", "Cherry Tomatoes"),
                I(0.5f, "Count", "Red Onion"),
                I(0.5f, "cup", "Kalamata Olives"),
                I(0.5f, "cup", "Feta Cheese"),
                I(3, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Lemon Juice"),
                I(1, "teaspoon", "Dried Oregano"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Lunch", "Dinner", "Vegetarian", "Gluten-Free", "Low Calorie", "Halal", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 32. Teriyaki Chicken Bowl
        new Recipe
        {
            Name = "Teriyaki Chicken Bowl",
            Calories = 580, Protein = 38, Carbs = 68, Fat = 14,
            Directions = "Cube chicken and toss with 2 tablespoons teriyaki. Sear in sesame oil over medium-high heat 7 minutes. Remove, then stir fry broccoli and sliced pepper 4 minutes. Return chicken with remaining teriyaki and toss. Serve over rice, topped with sesame seeds and sliced green onion.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(1, "cup", "Cooked White Rice"),
                I(1, "cup", "Broccoli Floret"),
                I(0.5f, "Count", "Red Bell Pepper"),
                I(3, "tablespoon", "Teriyaki Sauce"),
                I(1, "tablespoon", "Sesame Oil"),
                I(1, "tablespoon", "Sesame Seeds"),
                I(1, "Count", "Green Onion"),
            },
            Tags = T("Lunch", "Dinner", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "Japanese"),
        },

        // 33. Herb Roasted Chicken Thighs with Root Vegetables
        new Recipe
        {
            Name = "Herb Roasted Chicken Thighs with Root Vegetables",
            Calories = 620, Protein = 42, Carbs = 42, Fat = 30,
            Directions = "Preheat oven to 425°F. Toss 1-inch chunks of carrots, parsnips, and onion with 2 tablespoons oil, salt, and pepper on a sheet pan. Rub chicken thighs with minced garlic, chopped rosemary, thyme, salt, pepper, and remaining oil, and place skin-side up on the vegetables. Roast 40 minutes until chicken reaches 165°F.",
            Ingredients =
            {
                I(2, "Count", "Chicken Thigh"),
                I(2, "Count", "Carrot"),
                I(2, "Count", "Parsnip"),
                I(1, "Count", "Yellow Onion"),
                I(3, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Fresh Rosemary"),
                I(1, "tablespoon", "Fresh Thyme"),
                I(4, "Count", "Garlic"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "American", "Comfort Food"),
        },

        // 34. Mongolian Beef Stir Fry
        new Recipe
        {
            Name = "Mongolian Beef Stir Fry",
            Calories = 560, Protein = 38, Carbs = 52, Fat = 22,
            Directions = "Slice flank steak against the grain, toss with cornstarch, and sear in a hot wok with vegetable oil 1 minute per side; remove. Stir fry minced garlic and ginger 30 seconds, add soy sauce, brown sugar, and 0.25 cup water, and simmer until thickened. Return steak with broccoli and sliced green onions, toss 1 minute, and serve over rice.",
            Ingredients =
            {
                I(6, "ounce", "Flank Steak"),
                I(1, "cup", "Cooked White Rice"),
                I(3, "Count", "Green Onion"),
                I(3, "Count", "Garlic"),
                I(1, "tablespoon", "Fresh Ginger"),
                I(3, "tablespoon", "Soy Sauce"),
                I(2, "tablespoon", "Brown Sugar"),
                I(1, "tablespoon", "Cornstarch"),
                I(2, "tablespoon", "Vegetable Oil"),
                I(0.5f, "cup", "Broccoli Floret"),
            },
            Tags = T("Dinner", "Dairy-Free", "High Protein", "Halal", "Nut Allergy", "Chinese"),
        },

        // 35. Garlic Shrimp Scampi
        new Recipe
        {
            Name = "Garlic Shrimp Scampi",
            Calories = 540, Protein = 32, Carbs = 52, Fat = 20,
            Directions = "Cook linguine per package and reserve 0.25 cup pasta water. Heat olive oil and butter in a skillet over medium heat, add minced garlic and red pepper flakes and cook 30 seconds, then add peeled shrimp and sear 2 minutes per side. Stir in lemon juice, salt, and reserved pasta water, toss with linguine, and finish with parsley.",
            Ingredients =
            {
                I(6, "ounce", "Shrimp"),
                I(4, "ounce", "Linguine"),
                I(4, "Count", "Garlic"),
                I(2, "tablespoon", "Olive Oil"),
                I(2, "tablespoon", "Butter"),
                I(1, "tablespoon", "Lemon Juice"),
                I(0.25f, "cup", "Fresh Parsley"),
                I(0.5f, "teaspoon", "Red Pepper Flakes"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Dinner", "Dairy-Free", "High Protein", "Quick & Easy", "Halal", "Italian"),
        },

        // 36. Basil Pesto Pasta
        new Recipe
        {
            Name = "Basil Pesto Pasta",
            Calories = 580, Protein = 16, Carbs = 68, Fat = 28,
            Directions = "Cook penne per package, drain, and reserve 0.25 cup pasta water. Toss hot pasta with pesto and olive oil, loosening with pasta water as needed. Stir in halved cherry tomatoes and finish with parmesan, salt, and pepper.",
            Ingredients =
            {
                I(4, "ounce", "Penne Pasta"),
                I(0.25f, "cup", "Basil Pesto"),
                I(0.5f, "cup", "Cherry Tomatoes"),
                I(0.25f, "cup", "Parmesan Cheese"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.125f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Lunch", "Dinner", "Vegetarian", "Quick & Easy", "Halal", "Kosher", "Italian"),
        },

        // 37. Chicken Coconut Curry
        new Recipe
        {
            Name = "Chicken Coconut Curry",
            Calories = 620, Protein = 36, Carbs = 42, Fat = 32,
            Directions = "Sauté sliced onion in vegetable oil 3 minutes, add minced garlic, ginger, and curry paste and stir 30 seconds. Add cubed chicken and cook 5 minutes, then pour in coconut milk and fish sauce and simmer 8 minutes. Add sliced bell pepper for 3 minutes and wilt in spinach. Serve over jasmine rice.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(1, "cup", "Cooked Jasmine Rice"),
                I(1, "cup", "Coconut Milk"),
                I(2, "tablespoon", "Red Curry Paste"),
                I(1, "Count", "Red Bell Pepper"),
                I(1, "cup", "Spinach"),
                I(1, "Count", "Yellow Onion"),
                I(3, "Count", "Garlic"),
                I(1, "tablespoon", "Fresh Ginger"),
                I(1, "tablespoon", "Vegetable Oil"),
                I(1, "tablespoon", "Fish Sauce"),
            },
            Tags = T("Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Thai", "Spicy"),
        },

        // 38. Vegetable Lasagna
        // yields 6 servings; ingredient amounts are for the full 9x13 pan, nutrients are per serving
        new Recipe
        {
            Name = "Vegetable Lasagna",
            Calories = 580, Protein = 28, Carbs = 62, Fat = 24,
            Directions = "Preheat oven to 375°F and cook noodles per package. Sauté minced garlic and sliced zucchini in olive oil 5 minutes, then wilt in spinach. Mix ricotta with egg, salt, and pepper. In a 9x13 dish, layer marinara, noodles, ricotta, vegetables, and mozzarella; repeat twice and top with parmesan. Bake covered 30 minutes and uncovered 10 minutes; rest 10 minutes and garnish with basil.",
            Ingredients =
            {
                I(8, "Count", "Lasagna Noodle"),
                I(2, "cup", "Marinara Sauce"),
                I(2, "cup", "Ricotta Cheese"),
                I(2, "cup", "Mozzarella Cheese"),
                I(0.5f, "cup", "Parmesan Cheese"),
                I(2, "cup", "Spinach"),
                I(1, "Count", "Zucchini"),
                I(1, "Count", "Egg"),
                I(2, "Count", "Garlic"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
                I(1, "tablespoon", "Fresh Basil"),
            },
            Tags = T("Dinner", "Vegetarian", "Halal", "Kosher", "Nut Allergy", "Italian", "Comfort Food"),
        },

        // 39. Chicken Tikka Masala
        new Recipe
        {
            Name = "Chicken Tikka Masala",
            Calories = 680, Protein = 42, Carbs = 52, Fat = 30,
            Directions = "Marinate cubed chicken in yogurt with 1 tablespoon garam masala, cumin, and salt for 30 minutes. Sear chicken in vegetable oil 6 minutes and remove. Sauté diced onion 5 minutes, add minced garlic, ginger, remaining garam masala, and paprika for 1 minute, then crushed tomatoes and simmer 10 minutes. Stir in cream, return chicken, simmer 5 more minutes, and serve over rice.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(1, "cup", "Cooked Basmati Rice"),
                I(0.5f, "cup", "Plain Yogurt"),
                I(1, "cup", "Canned Crushed Tomatoes"),
                I(0.25f, "cup", "Heavy Cream"),
                I(1, "Count", "Yellow Onion"),
                I(3, "Count", "Garlic"),
                I(1, "tablespoon", "Fresh Ginger"),
                I(2, "tablespoon", "Garam Masala"),
                I(1, "teaspoon", "Paprika"),
                I(1, "teaspoon", "Cumin"),
                I(1, "tablespoon", "Vegetable Oil"),
                I(0.5f, "teaspoon", "Salt"),
            },
            Tags = T("Dinner", "Gluten-Free", "High Protein", "Halal", "Nut Allergy", "Indian", "Spicy", "Comfort Food"),
        },

        // 40. Bison Burger
        new Recipe
        {
            Name = "Bison Burger",
            Calories = 620, Protein = 38, Carbs = 42, Fat = 32,
            Directions = "Form bison into a 6-ounce patty 0.75 inch thick and season with salt and pepper. Cook in a hot cast iron skillet 4 minutes per side, topping with cheese in the final minute. Toast the bun in the skillet, spread mayo on the bottom, and layer lettuce, tomato, patty, and red onion before closing.",
            Ingredients =
            {
                I(6, "ounce", "Ground Bison"),
                I(1, "Count", "Hamburger Bun"),
                I(1, "Count", "Cheddar Cheese"),
                I(2, "Count", "Lettuce"),
                I(2, "Count", "Tomato"),
                I(2, "Count", "Red Onion"),
                I(1, "tablespoon", "Mayonnaise"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "High Protein", "Halal", "Nut Allergy", "American", "BBQ", "Comfort Food"),
        },

        // 41. Vegetarian Three Bean Chili
        new Recipe
        {
            Name = "Vegetarian Three Bean Chili",
            Calories = 420, Protein = 22, Carbs = 72, Fat = 6,
            Directions = "Sauté diced onion and bell pepper in olive oil 5 minutes, add minced garlic, chili powder, cumin, and paprika and toast 30 seconds. Stir in tomatoes, rinsed and drained beans, broth, and salt. Simmer 25 minutes, stirring occasionally.",
            Ingredients =
            {
                I(15, "ounce", "Canned Black Beans"),
                I(15, "ounce", "Canned Kidney Beans"),
                I(15, "ounce", "Canned Pinto Beans"),
                I(28, "ounce", "Canned Diced Tomatoes"),
                I(1, "Count", "Yellow Onion"),
                I(1, "Count", "Green Bell Pepper"),
                I(3, "Count", "Garlic"),
                I(2, "tablespoon", "Chili Powder"),
                I(1, "tablespoon", "Cumin"),
                I(1, "teaspoon", "Smoked Paprika"),
                I(1, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
                I(1, "cup", "Vegetable Broth"),
            },
            Tags = T("Dinner", "Lunch", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Kosher", "Nut Allergy", "American", "Mexican", "Spicy"),
        },

        // 42. Baked Cod with Roasted Vegetables
        new Recipe
        {
            Name = "Baked Cod with Roasted Vegetables",
            Calories = 340, Protein = 36, Carbs = 18, Fat = 14,
            Directions = "Preheat oven to 400°F. Toss cherry tomatoes, sliced zucchini, and wedged onion with 1 tablespoon oil, salt, and pepper on a sheet pan and roast 10 minutes. Add cod, drizzle with remaining oil, minced garlic, oregano, and juice from half a lemon, and top with slices from the other half. Bake 12 more minutes until the fish flakes.",
            Ingredients =
            {
                I(6, "ounce", "Cod Fillet"),
                I(1, "cup", "Cherry Tomatoes"),
                I(1, "Count", "Zucchini"),
                I(0.5f, "Count", "Red Onion"),
                I(2, "tablespoon", "Olive Oil"),
                I(2, "Count", "Garlic"),
                I(1, "Count", "Lemon"),
                I(1, "teaspoon", "Dried Oregano"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "Dairy-Free", "High Protein", "Low Calorie", "Keto", "Halal", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 43. Steak and Garlic Potatoes
        new Recipe
        {
            Name = "Steak and Garlic Potatoes",
            Calories = 720, Protein = 44, Carbs = 42, Fat = 42,
            Directions = "Roast halved baby potatoes tossed with olive oil, 0.25 teaspoon salt, and pepper at 425°F for 25 minutes, flipping halfway. Sear seasoned steak in a hot cast iron skillet 4 minutes per side, adding butter, smashed garlic, and rosemary in the final minute and basting. Rest 5 minutes and slice against the grain; serve with potatoes.",
            Ingredients =
            {
                I(6, "ounce", "Ribeye Steak"),
                I(1, "pound", "Baby Potato"),
                I(3, "tablespoon", "Butter"),
                I(4, "Count", "Garlic"),
                I(2, "tablespoon", "Fresh Rosemary"),
                I(2, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "High Protein", "Halal", "Nut Allergy", "American", "BBQ", "Comfort Food"),
        },

        // 44. Salmon Sushi Bowl
        new Recipe
        {
            Name = "Salmon Sushi Bowl",
            Calories = 560, Protein = 32, Carbs = 52, Fat = 22,
            Directions = "Stir rice vinegar into warm rice and let cool slightly. Cube sushi-grade salmon and toss with soy sauce and sesame oil. Place rice in a bowl and arrange salmon, sliced cucumber and avocado, and shredded carrots on top. Garnish with sesame seeds, sliced green onion, and strips of nori.",
            Ingredients =
            {
                I(5, "ounce", "Sushi-Grade Salmon"),
                I(1, "cup", "Cooked White Rice"),
                I(0.5f, "Count", "Cucumber"),
                I(0.5f, "Count", "Avocado"),
                I(0.25f, "cup", "Carrot"),
                I(2, "tablespoon", "Soy Sauce"),
                I(1, "tablespoon", "Rice Vinegar"),
                I(1, "teaspoon", "Sesame Oil"),
                I(1, "tablespoon", "Sesame Seeds"),
                I(1, "Count", "Green Onion"),
                I(1, "Count", "Nori"),
            },
            Tags = T("Dinner", "Lunch", "Gluten-Free", "Dairy-Free", "High Protein", "Kosher", "Nut Allergy", "Japanese", "Sushi"),
        },

        // 45. Pad Thai with Chicken
        new Recipe
        {
            Name = "Pad Thai with Chicken",
            Calories = 620, Protein = 32, Carbs = 72, Fat = 22,
            Directions = "Soak rice noodles in warm water 20 minutes and drain. Whisk fish sauce, brown sugar, lime juice, and tamarind. Stir fry minced garlic in vegetable oil 15 seconds, add sliced chicken and cook 3 minutes, then scramble eggs in the open space. Add drained noodles and sauce, toss 2 minutes, and stir in bean sprouts and sliced green onions. Finish with chopped peanuts.",
            Ingredients =
            {
                I(5, "ounce", "Chicken Breast"),
                I(4, "ounce", "Rice Noodles"),
                I(2, "Count", "Egg"),
                I(2, "cup", "Bean Sprouts"),
                I(3, "tablespoon", "Fish Sauce"),
                I(2, "tablespoon", "Brown Sugar"),
                I(2, "tablespoon", "Lime Juice"),
                I(1, "tablespoon", "Tamarind Paste"),
                I(0.25f, "cup", "Peanuts"),
                I(2, "Count", "Green Onion"),
                I(2, "Count", "Garlic"),
                I(2, "tablespoon", "Vegetable Oil"),
            },
            Tags = T("Dinner", "Dairy-Free", "High Protein", "Halal", "Thai", "Spicy"),
        },

        // 46. Eggplant Parmesan
        // yields 4 servings; ingredient amounts are for the full bake, nutrients are per serving
        new Recipe
        {
            Name = "Eggplant Parmesan",
            Calories = 580, Protein = 26, Carbs = 58, Fat = 28,
            Directions = "Slice eggplant into 0.5-inch rounds, salt, and drain on paper towels 15 minutes. Dredge in flour, beaten egg, then breadcrumbs mixed with parmesan. Fry in olive oil 2 minutes per side. Layer marinara, eggplant, mozzarella, and basil in a baking dish; repeat once. Bake at 400°F for 20 minutes until bubbly.",
            Ingredients =
            {
                I(1, "Count", "Eggplant"),
                I(1, "cup", "Marinara Sauce"),
                I(1.5f, "cup", "Mozzarella Cheese"),
                I(0.5f, "cup", "Parmesan Cheese"),
                I(1, "cup", "Breadcrumbs"),
                I(2, "Count", "Egg"),
                I(0.5f, "cup", "All-Purpose Flour"),
                I(2, "tablespoon", "Olive Oil"),
                I(0.5f, "teaspoon", "Salt"),
                I(0.25f, "teaspoon", "Black Pepper"),
                I(2, "tablespoon", "Fresh Basil"),
            },
            Tags = T("Dinner", "Vegetarian", "Halal", "Kosher", "Nut Allergy", "Italian", "Comfort Food"),
        },

        // 47. Hummus and Vegetable Platter
        new Recipe
        {
            Name = "Hummus and Vegetable Platter",
            Calories = 220, Protein = 8, Carbs = 28, Fat = 10,
            Directions = "Cut carrot and cucumber into sticks, slice bell pepper into strips, and arrange all vegetables around a small bowl of hummus on a platter. Serve immediately or chill up to 4 hours.",
            Ingredients =
            {
                I(0.5f, "cup", "Hummus"),
                I(1, "Count", "Carrot"),
                I(1, "Count", "Cucumber"),
                I(0.5f, "Count", "Red Bell Pepper"),
                I(0.5f, "cup", "Cherry Tomatoes"),
                I(0.5f, "cup", "Snap Peas"),
            },
            Tags = T("Snack", "Appetizer", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Low Calorie", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "Mediterranean"),
        },

        // 48. Homemade Trail Mix
        // yields several servings; ingredient amounts make one batch, nutrients are per 0.5 cup serving
        new Recipe
        {
            Name = "Homemade Trail Mix",
            Calories = 280, Protein = 8, Carbs = 24, Fat = 18,
            Directions = "Combine almonds, cashews, raisins, dried cranberries, chocolate chips, and pumpkin seeds in a jar and shake to mix. Store at room temperature up to 2 weeks. Portion 0.5 cup per serving.",
            Ingredients =
            {
                I(0.25f, "cup", "Almonds"),
                I(0.25f, "cup", "Cashews"),
                I(0.25f, "cup", "Raisins"),
                I(0.25f, "cup", "Dried Cranberries"),
                I(2, "tablespoon", "Dark Chocolate Chips"),
                I(0.25f, "cup", "Pumpkin Seeds"),
            },
            Tags = T("Snack", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Quick & Easy", "Halal", "Kosher"),
        },

        // 49. Strawberry Banana Protein Smoothie
        new Recipe
        {
            Name = "Strawberry Banana Protein Smoothie",
            Calories = 320, Protein = 28, Carbs = 48, Fat = 4,
            Directions = "Combine banana, frozen strawberries, milk, whey protein, honey, and ice in a blender and blend on high 45 seconds, scraping down once. Pour into a tall glass and serve immediately.",
            Ingredients =
            {
                I(1, "Count", "Banana"),
                I(0.5f, "cup", "Strawberries"),
                I(1, "cup", "Milk"),
                I(1, "Count", "Vanilla Whey Protein"),
                I(1, "tablespoon", "Honey"),
                I(0.5f, "cup", "Ice"),
            },
            Tags = T("Breakfast", "Snack", "Vegetarian", "Gluten-Free", "High Protein", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 50. Deviled Eggs
        // yields 4 servings of 3 halves each (12 halves total); nutrients are per 3-half serving
        new Recipe
        {
            Name = "Deviled Eggs",
            Calories = 180, Protein = 12, Carbs = 2, Fat = 14,
            Directions = "Cover eggs with cold water in a pot, bring to a boil, cover, turn off heat, and let sit 10 minutes; shock in ice water and peel. Halve, scoop yolks, and mash with mayo, dijon, vinegar, and salt. Pipe back into whites and garnish with paprika and sliced green onion.",
            Ingredients =
            {
                I(6, "Count", "Egg"),
                I(3, "tablespoon", "Mayonnaise"),
                I(1, "teaspoon", "Dijon Mustard"),
                I(1, "teaspoon", "White Vinegar"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Paprika"),
                I(1, "Count", "Green Onion"),
            },
            Tags = T("Snack", "Appetizer", "Vegetarian", "Gluten-Free", "Dairy-Free", "Low Calorie", "High Protein", "Keto", "Halal", "Kosher", "Nut Allergy", "American"),
        },

        // 51. Cheese and Crackers Plate
        new Recipe
        {
            Name = "Cheese and Crackers Plate",
            Calories = 340, Protein = 14, Carbs = 28, Fat = 20,
            Directions = "Slice cheddar and brie into bite-size pieces and arrange on a plate with crackers and grapes. Pour honey into a small ramekin for dipping with brie.",
            Ingredients =
            {
                I(2, "ounce", "Cheddar Cheese"),
                I(2, "ounce", "Brie Cheese"),
                I(10, "Count", "Wheat Cracker"),
                I(0.5f, "cup", "Grapes"),
                I(2, "tablespoon", "Honey"),
            },
            Tags = T("Snack", "Appetizer", "Vegetarian", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "American"),
        },

        // 52. Rice Cakes with Peanut Butter and Banana
        new Recipe
        {
            Name = "Rice Cakes with Peanut Butter and Banana",
            Calories = 260, Protein = 8, Carbs = 36, Fat = 10,
            Directions = "Spread peanut butter over each rice cake and layer with thin banana rounds. Drizzle with honey and dust with cinnamon.",
            Ingredients =
            {
                I(2, "Count", "Rice Cake"),
                I(2, "tablespoon", "Peanut Butter"),
                I(1, "Count", "Banana"),
                I(1, "teaspoon", "Honey"),
                I(0.5f, "teaspoon", "Cinnamon"),
            },
            Tags = T("Snack", "Breakfast", "Vegetarian", "Gluten-Free", "Dairy-Free", "Quick & Easy", "Halal", "Kosher"),
        },

        // 53. Tomato Basil Bruschetta
        new Recipe
        {
            Name = "Tomato Basil Bruschetta",
            Calories = 240, Protein = 6, Carbs = 34, Fat = 10,
            Directions = "Combine diced tomatoes, chiffonaded basil, minced garlic, 1 tablespoon oil, vinegar, and salt and marinate 10 minutes. Brush baguette slices with remaining oil and broil 2 minutes per side until golden. Spoon tomato mixture generously onto each toast.",
            Ingredients =
            {
                I(4, "Count", "Baguette"),
                I(3, "Count", "Roma Tomato"),
                I(2, "tablespoon", "Fresh Basil"),
                I(2, "Count", "Garlic"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Balsamic Vinegar"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Appetizer", "Snack", "Vegan", "Vegetarian", "Dairy-Free", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "Italian"),
        },

        // 54. Spinach Artichoke Dip
        // yields 6 servings; ingredient amounts are for the full bake, nutrients are per serving
        new Recipe
        {
            Name = "Spinach Artichoke Dip",
            Calories = 320, Protein = 12, Carbs = 14, Fat = 26,
            Directions = "Preheat oven to 375°F. Thaw spinach and squeeze dry; chop drained artichokes. Mix cream cheese, sour cream, mozzarella, parmesan, minced garlic, salt, and pepper until smooth, then fold in spinach and artichokes. Transfer to an oven-safe dish and bake 25 minutes until bubbly and golden on top.",
            Ingredients =
            {
                I(10, "ounce", "Frozen Spinach"),
                I(14, "ounce", "Canned Artichoke Hearts"),
                I(1, "cup", "Cream Cheese"),
                I(0.5f, "cup", "Sour Cream"),
                I(0.5f, "cup", "Mozzarella Cheese"),
                I(0.25f, "cup", "Parmesan Cheese"),
                I(2, "Count", "Garlic"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Appetizer", "Vegetarian", "Gluten-Free", "Halal", "Kosher", "Nut Allergy", "American", "Comfort Food"),
        },

        // 55. Stuffed Mushrooms
        // yields 4 servings of 3 mushrooms each (12 caps total); nutrients are per 3-mushroom serving
        new Recipe
        {
            Name = "Stuffed Mushrooms",
            Calories = 180, Protein = 10, Carbs = 8, Fat = 12,
            Directions = "Remove and chop mushroom stems; arrange caps on a baking sheet. Sauté chopped stems with minced garlic in 1 tablespoon oil for 3 minutes, cool slightly, and mix with cream cheese, parmesan, parsley, salt, and pepper. Spoon into caps, drizzle with remaining oil, and bake at 400°F for 20 minutes until golden.",
            Ingredients =
            {
                I(12, "Count", "Cremini Mushroom"),
                I(0.5f, "cup", "Cream Cheese"),
                I(0.25f, "cup", "Parmesan Cheese"),
                I(2, "Count", "Garlic"),
                I(2, "tablespoon", "Fresh Parsley"),
                I(2, "tablespoon", "Olive Oil"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Appetizer", "Vegetarian", "Gluten-Free", "Low Calorie", "Halal", "Kosher", "Nut Allergy", "Italian"),
        },

        // 56. Caprese Skewers
        // yields 4 servings of 3 skewers each (12 skewers total); nutrients are per 3-skewer serving
        new Recipe
        {
            Name = "Caprese Skewers",
            Calories = 180, Protein = 10, Carbs = 6, Fat = 14,
            Directions = "Thread one cherry tomato, folded basil leaf, and mozzarella pearl onto each of 12 skewers and arrange on a platter. Drizzle with olive oil and balsamic glaze and sprinkle with salt before serving.",
            Ingredients =
            {
                I(12, "Count", "Cherry Tomato"),
                I(12, "Count", "Mozzarella Pearl"),
                I(12, "Count", "Fresh Basil"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "tablespoon", "Balsamic Glaze"),
                I(0.125f, "teaspoon", "Salt"),
            },
            Tags = T("Appetizer", "Snack", "Vegetarian", "Gluten-Free", "Low Calorie", "Quick & Easy", "Halal", "Kosher", "Nut Allergy", "Italian"),
        },

        // 57. Shrimp Cocktail
        new Recipe
        {
            Name = "Shrimp Cocktail",
            Calories = 140, Protein = 22, Carbs = 10, Fat = 2,
            Directions = "Boil peeled, deveined shrimp (tail on) in salted water 2 minutes and shock in ice water. Whisk ketchup, horseradish, lemon juice, and worcestershire for cocktail sauce. Drain shrimp and arrange on a platter with lemon wedges and sauce.",
            Ingredients =
            {
                I(12, "Count", "Shrimp"),
                I(0.25f, "cup", "Ketchup"),
                I(1, "tablespoon", "Horseradish"),
                I(1, "teaspoon", "Lemon Juice"),
                I(0.25f, "teaspoon", "Worcestershire Sauce"),
                I(1, "Count", "Lemon"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Appetizer", "Gluten-Free", "Dairy-Free", "Low Calorie", "High Protein", "Quick & Easy", "Halal", "Nut Allergy", "American"),
        },

        // 58. Summer Fruit Salad
        new Recipe
        {
            Name = "Summer Fruit Salad",
            Calories = 160, Protein = 2, Carbs = 40, Fat = 0,
            Directions = "Hull and quarter strawberries, cube pineapple, slice kiwi, and segment orange. Combine all fruit in a bowl, whisk lime juice and honey, and drizzle over the fruit. Toss gently and garnish with torn mint.",
            Ingredients =
            {
                I(1, "cup", "Strawberries"),
                I(1, "cup", "Blueberries"),
                I(1, "cup", "Pineapple"),
                I(1, "Count", "Kiwi"),
                I(1, "Count", "Orange"),
                I(1, "tablespoon", "Lime Juice"),
                I(1, "tablespoon", "Honey"),
                I(2, "tablespoon", "Fresh Mint"),
            },
            Tags = T("Dessert", "Snack", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Low Calorie", "Quick & Easy", "Halal", "Kosher", "Nut Allergy"),
        },

        // 59. Classic Chocolate Chip Cookies
        // yields 24 cookies (1 cookie per serving); ingredient amounts make 24 cookies, nutrients are per cookie
        new Recipe
        {
            Name = "Classic Chocolate Chip Cookies",
            Calories = 180, Protein = 2, Carbs = 24, Fat = 10,
            Directions = "Cream softened butter with both sugars until fluffy, then beat in eggs one at a time and vanilla. Whisk flour, baking soda, and salt and stir into wet ingredients until just combined; fold in chocolate chips. Drop rounded tablespoons onto a parchment-lined sheet 2 inches apart and bake at 375°F for 10 minutes. Cool on the pan 5 minutes before transferring to a wire rack.",
            Ingredients =
            {
                I(2.25f, "cup", "All-Purpose Flour"),
                I(1, "cup", "Butter"),
                I(0.75f, "cup", "Brown Sugar"),
                I(0.5f, "cup", "Sugar"),
                I(2, "Count", "Egg"),
                I(1, "teaspoon", "Vanilla Extract"),
                I(1, "teaspoon", "Baking Soda"),
                I(0.5f, "teaspoon", "Salt"),
                I(2, "cup", "Chocolate Chips"),
            },
            Tags = T("Dessert", "Snack", "Vegetarian", "Halal", "Kosher", "Nut Allergy", "American", "Comfort Food"),
        },

        // 60. Mixed Berry Crumble
        // yields 6 servings; ingredient amounts fill an 8x8 pan, nutrients are per serving
        new Recipe
        {
            Name = "Mixed Berry Crumble",
            Calories = 340, Protein = 4, Carbs = 58, Fat = 12,
            Directions = "Preheat oven to 375°F. Toss berries with lemon juice and 2 tablespoons brown sugar and spread in a greased 8x8 dish. Combine oats, flour, remaining sugar, cinnamon, and salt, cut in cold butter until crumbly, and sprinkle over the berries. Bake 35 minutes until golden and bubbly; cool 15 minutes.",
            Ingredients =
            {
                I(4, "cup", "Mixed Berries"),
                I(0.75f, "cup", "Rolled Oats"),
                I(0.5f, "cup", "All-Purpose Flour"),
                I(0.5f, "cup", "Brown Sugar"),
                I(0.33f, "cup", "Butter"),
                I(1, "teaspoon", "Cinnamon"),
                I(1, "tablespoon", "Lemon Juice"),
                I(0.25f, "teaspoon", "Salt"),
            },
            Tags = T("Dessert", "Vegetarian", "Halal", "Kosher", "Nut Allergy", "American", "Comfort Food"),
        },

        // 61. Coconut Rice Pudding
        // yields 4 servings; ingredient amounts make the full pot, nutrients are per serving
        new Recipe
        {
            Name = "Coconut Rice Pudding",
            Calories = 320, Protein = 4, Carbs = 58, Fat = 10,
            Directions = "Combine rice, coconut milk, water, sugar, and salt in a pot, bring to a boil, then cover and simmer on low 25 minutes, stirring occasionally. Stir in vanilla and serve warm dusted with cinnamon.",
            Ingredients =
            {
                I(1, "cup", "Jasmine Rice"),
                I(2, "cup", "Coconut Milk"),
                I(1, "cup", "Water"),
                I(0.33f, "cup", "Sugar"),
                I(1, "teaspoon", "Vanilla Extract"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.5f, "teaspoon", "Cinnamon"),
            },
            Tags = T("Dessert", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Halal", "Kosher", "Comfort Food"),
        },

        // 62. Ahi Tuna Poke Bowl
        new Recipe
        {
            Name = "Ahi Tuna Poke Bowl",
            Calories = 540, Protein = 38, Carbs = 58, Fat = 16,
            Directions = "Cube ahi and marinate in soy sauce, sesame oil, rice vinegar, and sriracha 10 minutes. Layer rice in a bowl and arrange tuna, diced cucumber, sliced avocado, edamame, and shredded carrots on top. Finish with sliced green onion and sesame seeds.",
            Ingredients =
            {
                I(5, "ounce", "Sushi-Grade Ahi Tuna"),
                I(1, "cup", "Cooked White Rice"),
                I(0.5f, "Count", "Cucumber"),
                I(0.5f, "Count", "Avocado"),
                I(0.25f, "cup", "Edamame"),
                I(0.25f, "cup", "Carrot"),
                I(3, "tablespoon", "Soy Sauce"),
                I(1, "tablespoon", "Sesame Oil"),
                I(1, "tablespoon", "Rice Vinegar"),
                I(1, "teaspoon", "Sriracha"),
                I(1, "Count", "Green Onion"),
                I(1, "tablespoon", "Sesame Seeds"),
            },
            Tags = T("Dinner", "Lunch", "Dairy-Free", "High Protein", "Halal", "Kosher", "Nut Allergy", "Japanese", "Sushi", "Spicy"),
        },

        // 63. Tropical Smoothie Bowl
        new Recipe
        {
            Name = "Tropical Smoothie Bowl",
            Calories = 380, Protein = 6, Carbs = 82, Fat = 6,
            Directions = "Blend frozen mango, banana, pineapple, and oat milk until thick, adding more oat milk only if needed. Pour into a shallow bowl and arrange granola, sliced strawberries, shredded coconut, and chia seeds in rows across the top. Serve immediately.",
            Ingredients =
            {
                I(1, "cup", "Mango"),
                I(1, "Count", "Banana"),
                I(0.5f, "cup", "Pineapple"),
                I(0.75f, "cup", "Oat Milk"),
                I(0.25f, "cup", "Granola"),
                I(0.5f, "cup", "Strawberries"),
                I(1, "tablespoon", "Shredded Coconut"),
                I(1, "tablespoon", "Chia Seeds"),
            },
            Tags = T("Breakfast", "Snack", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "Halal", "Kosher"),
        },

        // 64. Loaded Sweet Potato
        new Recipe
        {
            Name = "Loaded Sweet Potato",
            Calories = 440, Protein = 18, Carbs = 84, Fat = 6,
            Directions = "Pierce sweet potato several times and bake at 400°F for 50 minutes until tender. Slit lengthwise and fluff the flesh. Warm black beans and corn with chili powder 3 minutes. Top the sweet potato with beans, corn, salsa, avocado, and cilantro, and drizzle with lime juice.",
            Ingredients =
            {
                I(1, "Count", "Sweet Potato"),
                I(0.5f, "cup", "Black Beans"),
                I(0.25f, "cup", "Corn"),
                I(0.25f, "cup", "Salsa"),
                I(0.25f, "Count", "Avocado"),
                I(2, "tablespoon", "Fresh Cilantro"),
                I(1, "tablespoon", "Lime Juice"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.5f, "teaspoon", "Chili Powder"),
            },
            Tags = T("Lunch", "Dinner", "Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free", "High Protein", "Halal", "Kosher", "Nut Allergy", "Mexican", "Spicy"),
        },

        // 65. Mediterranean Baked Chicken
        new Recipe
        {
            Name = "Mediterranean Baked Chicken",
            Calories = 520, Protein = 44, Carbs = 14, Fat = 30,
            Directions = "Preheat oven to 400°F. Whisk olive oil, juice from half a lemon, minced garlic, oregano, salt, and pepper and pour over chicken in a baking dish. Scatter halved cherry tomatoes and olives around and top with slices from the other lemon half. Bake 25 minutes, scatter feta over the top, and bake 3 more minutes.",
            Ingredients =
            {
                I(6, "ounce", "Chicken Breast"),
                I(0.5f, "cup", "Cherry Tomatoes"),
                I(0.25f, "cup", "Kalamata Olives"),
                I(0.25f, "cup", "Feta Cheese"),
                I(2, "tablespoon", "Olive Oil"),
                I(1, "Count", "Lemon"),
                I(2, "Count", "Garlic"),
                I(1, "tablespoon", "Dried Oregano"),
                I(0.25f, "teaspoon", "Salt"),
                I(0.125f, "teaspoon", "Black Pepper"),
            },
            Tags = T("Dinner", "Gluten-Free", "High Protein", "Keto", "Halal", "Nut Allergy", "Mediterranean"),
        },
    };
}
