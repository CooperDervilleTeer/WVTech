Feature: WVT-177 Fraction and decimal ingredient amounts with expanded measurement units

  Background:
    Given there is a user named 'Alex'
    And 'Alex' is logged into Onebite

  Scenario: Measurement dropdown on create recipe page contains all required units
    Given 'Alex' is on the create recipe page
    When 'Alex' adds an ingredient row
    Then the ingredient measurement dropdown contains 'Teaspoon'
    And the ingredient measurement dropdown contains 'Tablespoon'
    And the ingredient measurement dropdown contains 'Fluid Ounce'
    And the ingredient measurement dropdown contains 'Cup'
    And the ingredient measurement dropdown contains 'Pint'
    And the ingredient measurement dropdown contains 'Quart'
    And the ingredient measurement dropdown contains 'Gallon'
    And the ingredient measurement dropdown contains 'Milliliter'
    And the ingredient measurement dropdown contains 'Liter'
    And the ingredient measurement dropdown contains 'Ounce'
    And the ingredient measurement dropdown contains 'Pound'
    And the ingredient measurement dropdown contains 'Gram'

  Scenario: User creates a recipe with a fraction ingredient amount
    Given 'Alex' is on the create recipe page
    And 'Alex' fills in the recipe name as 'FractionAmountTest'
    And 'Alex' fills in the recipe directions as 'Mix'
    And 'Alex' fills in the recipe calories as '100'
    And 'Alex' fills in the recipe protein as '5'
    And 'Alex' fills in the recipe fat as '5'
    And 'Alex' fills in the recipe carbs as '10'
    When 'Alex' adds an ingredient with name 'butter' amount '1/2' and measurement 'Cup'
    And 'Alex' submits the recipe form
    Then 'Alex' is redirected away from the create recipe page
    And the recipe 'FractionAmountTest' stores 'butter' with amount '0.5'

  Scenario: User creates a recipe with a mixed number ingredient amount
    Given 'Alex' is on the create recipe page
    And 'Alex' fills in the recipe name as 'MixedNumberTest'
    And 'Alex' fills in the recipe directions as 'Mix'
    And 'Alex' fills in the recipe calories as '100'
    And 'Alex' fills in the recipe protein as '5'
    And 'Alex' fills in the recipe fat as '5'
    And 'Alex' fills in the recipe carbs as '10'
    When 'Alex' adds an ingredient with name 'flour' amount '1 1/2' and measurement 'Cup'
    And 'Alex' submits the recipe form
    Then 'Alex' is redirected away from the create recipe page
    And the recipe 'MixedNumberTest' stores 'flour' with amount '1.5'

  Scenario: User creates a recipe with a decimal ingredient amount
    Given 'Alex' is on the create recipe page
    And 'Alex' fills in the recipe name as 'DecimalAmountTest'
    And 'Alex' fills in the recipe directions as 'Mix'
    And 'Alex' fills in the recipe calories as '100'
    And 'Alex' fills in the recipe protein as '5'
    And 'Alex' fills in the recipe fat as '5'
    And 'Alex' fills in the recipe carbs as '10'
    When 'Alex' adds an ingredient with name 'oil' amount '0.75' and measurement 'Cup'
    And 'Alex' submits the recipe form
    Then 'Alex' is redirected away from the create recipe page
    And the recipe 'DecimalAmountTest' stores 'oil' with amount '0.75'

  Scenario: Invalid ingredient amount is flagged and recipe is not saved
    Given 'Alex' is on the create recipe page
    And 'Alex' fills in the recipe name as 'InvalidAmountTest'
    And 'Alex' fills in the recipe directions as 'Mix'
    And 'Alex' fills in the recipe calories as '100'
    And 'Alex' fills in the recipe protein as '5'
    And 'Alex' fills in the recipe fat as '5'
    And 'Alex' fills in the recipe carbs as '10'
    When 'Alex' adds an ingredient with name 'salt' amount 'abc' and measurement 'Teaspoon'
    And 'Alex' submits the recipe form
    Then an invalid amount error is displayed

  Scenario: Shopping list measurement dropdown contains all required units
    Given 'Alex' navigates to the shopping list
    Then the shopping list measurement dropdown contains 'Teaspoon'
    And the shopping list measurement dropdown contains 'Tablespoon'
    And the shopping list measurement dropdown contains 'Cup'
    And the shopping list measurement dropdown contains 'Pound'
    And the shopping list measurement dropdown contains 'Gram'

  Scenario: Fractional amount stored as decimal displays as fraction on the shopping list
    Given 'Alex' has 'milk' with amount '0.5' and measurement 'Cup' on the shopping list
    When 'Alex' navigates to the shopping list
    Then 'milk' displays with amount '1/2' on the shopping list

  Scenario: User can update the measurement of an existing shopping list item
    Given 'Alex' has 'wvt177sugar' with amount '1' and measurement 'Count' on the shopping list
    When 'Alex' navigates to the shopping list
    And 'Alex' updates the measurement of 'wvt177sugar' to 'Cup'
    Then the measurement of 'wvt177sugar' on the shopping list shows 'Cup'

  Scenario: User can set a custom measurement word on a shopping list item
    Given 'Alex' has 'wvt177salt' with amount '1' and measurement 'Count' on the shopping list
    When 'Alex' navigates to the shopping list
    And 'Alex' updates the measurement of 'wvt177salt' to 'a pinch'
    Then the measurement of 'wvt177salt' on the shopping list shows 'a pinch'
