Feature: Recommend meals for entire day

# WVT-144

  Background:
    Given there is a user named 'Gary'
    And 'Gary' is logged into Onebite
    And 'Gary' has a calorie target set

  Scenario: User sees the option to generate a full day plan on the Create Meal page
    When 'Gary' navigates to the Create Meal page
    Then a 'Generate Day Plan' button is visible alongside the existing generate meal button

  Scenario: User is walked through day plan configuration on the Create Meal page
    Given 'Gary' is on the Create Meal page
    When 'Gary' clicks 'Generate Day Plan'
    Then 'Gary' is asked how many meals he would like for the day
    When 'Gary' enters 3 for the number of meals
    And 'Gary' clicks 'Next — Configure Meals'
    Then 'Gary' is asked what size he would like each meal to be
    And the meal size options include 'Small', 'Average', and 'Large'
    And 'Gary' is asked what type of food he would like using tags

  Scenario: User configures size and food type for each meal in the day plan
    Given 'Gary' has specified 2 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    Then 'Gary' is asked what size he would like each meal to be
    And the meal size options include 'Small', 'Average', and 'Large'
    And 'Gary' is asked what type of food he would like using tags

  Scenario: User is shown a summary after the day plan is generated
    Given 'Gary' has completed the day plan configuration
    When the day plan is generated
    Then 'Gary' sees a summary of his meal plan for the day
    And the summary shows each recommended meal by name

  Scenario: User provides a meal title that appears in the day plan summary
    Given 'Gary' has specified 2 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    And 'Gary' enters 'Weekend Brunch' as the title for the first meal
    When the day plan is generated
    Then the summary contains a meal titled 'Weekend Brunch'
    And the second meal in the summary uses the default naming scheme

  Scenario: User enters a custom tag not shown in the suggested list
    Given 'Gary' has specified 2 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    Then 'Gary' can enter a custom tag name for the meal
    When 'Gary' types 'Vegan' as a custom tag and generates the plan
    Then 'Gary' sees a summary of his meal plan for the day

  Scenario: Day plan recommendations respect the user's dietary restrictions
    # Uses a test-specific restriction tag so the seeded recipe catalogue
    # (which carries real tags like 'Vegan') cannot crowd out 'Tofu Stir Fry'.
    Given 'Gary' has a 'DietaryFilterTest' dietary restriction
    And 'Gary' has a recipe tagged 'DietaryFilterTest' named 'Tofu Stir Fry'
    And 'Gary' has a recipe named 'Beef Stew' without any tags
    And 'Gary' has specified 1 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    When the day plan is generated
    Then the day plan summary includes a recipe named 'Tofu Stir Fry'
    And the day plan summary does not include a recipe named 'Beef Stew'

  Scenario: User regenerates a meal from the summary
    Given 'Gary' is viewing his generated day plan summary
    When 'Gary' chooses to regenerate one of the meals
    Then 'Gary' is shown the meal configuration form inline on the summary page
    When 'Gary' confirms the configuration and regenerates
    Then the updated meal appears in the summary in place of the previous recommendation
    And all other meals in the summary remain unchanged

  # WVT-176

  Scenario: Average-sized meal targets more calories than the static limit when the daily goal is high
    Given 'Gary' has a daily calorie target of 3000
    And 'Gary' has 10 upvoted recipes each with 300 calories
    And 'Gary' has specified 1 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    When the day plan is generated
    Then the first meal in the day plan summary shows more than 800 total calories

  Scenario: Large meal in a day plan receives proportionally more calories than a Small meal
    Given 'Gary' has a daily calorie target of 1600
    And 'Gary' has 10 upvoted recipes each with 200 calories
    And 'Gary' has specified 2 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    And 'Gary' sets meal 1 to 'Small' and meal 2 to 'Large'
    When the day plan is generated
    Then the second meal in the day plan shows more total calories than the first meal

  Scenario: Regenerating a meal as Large with a high daily target produces more than the static limit
    Given 'Gary' has a daily calorie target of 3000
    And 'Gary' has 10 upvoted recipes each with 300 calories
    And 'Gary' is viewing his generated day plan summary
    When 'Gary' chooses to regenerate one of the meals
    Then 'Gary' is shown the meal configuration form inline on the summary page
    When 'Gary' sets the regenerate size to 'Large' and confirms
    Then the first meal in the day plan summary shows more than 1200 total calories

  # Macro-target fit (soft — recipes are ranked by macro fit, not hard-excluded)

  Scenario: Day plan prefers the recipe that better fits the user's protein target
    Given 'Gary' has a 'MacroProteinTest' dietary restriction
    And 'Gary' has a nutrition target of 2000 calories, 20g protein, 200g carbs, and 200g fat
    And 'Gary' has a recipe named 'High Protein Dish' with 200 calories, 50g protein, 10g carbs, and 5g fat tagged 'MacroProteinTest'
    And 'Gary' has a recipe named 'Low Protein Dish' with 200 calories, 10g protein, 10g carbs, and 5g fat tagged 'MacroProteinTest'
    And 'Gary' has specified 1 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    When the day plan is generated
    Then the day plan summary includes a recipe named 'Low Protein Dish'
    And the day plan summary does not include a recipe named 'High Protein Dish'

  Scenario: Day plan prefers the recipe that better fits the user's carb target
    Given 'Gary' has a 'MacroCarbTest' dietary restriction
    And 'Gary' has a nutrition target of 2000 calories, 200g protein, 10g carbs, and 200g fat
    And 'Gary' has a recipe named 'High Carb Dish' with 200 calories, 10g protein, 50g carbs, and 5g fat tagged 'MacroCarbTest'
    And 'Gary' has a recipe named 'Low Carb Dish' with 200 calories, 10g protein, 5g carbs, and 5g fat tagged 'MacroCarbTest'
    And 'Gary' has specified 1 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    When the day plan is generated
    Then the day plan summary includes a recipe named 'Low Carb Dish'
    And the day plan summary does not include a recipe named 'High Carb Dish'

  Scenario: Day plan prefers the recipe that better fits the user's fat target
    Given 'Gary' has a 'MacroFatTest' dietary restriction
    And 'Gary' has a nutrition target of 2000 calories, 200g protein, 200g carbs, and 10g fat
    And 'Gary' has a recipe named 'High Fat Dish' with 200 calories, 10g protein, 5g carbs, and 50g fat tagged 'MacroFatTest'
    And 'Gary' has a recipe named 'Low Fat Dish' with 200 calories, 10g protein, 5g carbs, and 5g fat tagged 'MacroFatTest'
    And 'Gary' has specified 1 meals for his day plan
    When 'Gary' is presented with the configuration for each meal
    When the day plan is generated
    Then the day plan summary includes a recipe named 'Low Fat Dish'
    And the day plan summary does not include a recipe named 'High Fat Dish'
