Feature: nutritionBar

  Background:
    Given there is a user named 'bob'
    And 'bob' is logged into Onebite

  Scenario: 'bob' checks to see their progress
    Given 'bob' has no meals
    And 'bob' has a recipe 'TestRecipe' with 20 calories, 30 protein, 40 fat, 50 carbs
    And 'bob' has a completed meal 'testMeal' today with recipe 'TestRecipe'
    And 'bob' has nutrition targets of 40 calories, 50 protein, 60 fat, 70 carbs
    And 'bob' is on the page 'FoodEntries/NutritionSummary'
    Then Meal Bars callories are at 20/40
    Then Meal Bars protien are at 30/50
    Then Meal Bars fats are at 40/60
    Then Meal Bars carbs are at 50/70
    And 'bob' is on the home page
    Then Meal Bars callories are at 20/40
    Then Meal Bars protien are at 30/50
    Then Meal Bars fats are at 40/60
    Then Meal Bars carbs are at 50/70
