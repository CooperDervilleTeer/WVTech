Feature: WVT-20
  Background:
    Given there is a user named 'Alice'
    And 'Alice' is logged into Onebite

  Scenario: Shopping list includes ingredients from upcoming meals
    Given 'Alice' has an upcoming meal with ingredients
    When 'Alice' views her shopping list
    Then the shopping list contains the ingredients from her upcoming meal

  Scenario: Adding a meal adds its ingredients to the shopping list
    Given 'Alice' is on the create meal page
    When 'Alice' creates a meal with a recipe that has ingredients
    Then the ingredients from that recipe appear on her shopping list

  Scenario: Removing a meal removes its ingredients from the shopping list
    Given 'Alice' has an upcoming meal with ingredients
    When 'Alice' deletes that meal
    Then the ingredients from that meal are no longer on her shopping list

  Scenario: Duplicate ingredients across multiple meals are not repeated
    Given 'Alice' has two upcoming meals that share an ingredient
    When 'Alice' views her shopping list
    Then that shared ingredient appears only once on the shopping list

  Scenario: User can manually add items alongside auto-populated ingredients
    Given 'Alice' has an upcoming meal with ingredients
    And 'Alice' has manually added an item to her shopping list
    When 'Alice' views her shopping list
    Then both the auto-populated ingredients and the manually added item are present

  Scenario: Manually added items are not removed when a meal is deleted
    Given 'Alice' has an upcoming meal with ingredients
    And 'Alice' has manually added an item to her shopping list
    When 'Alice' deletes that meal
    Then the manually added item is still on her shopping list

  Scenario: Shopping list database is updated when a meal is added
    Given 'Alice' is on the create meal page
    When 'Alice' creates a meal with a recipe that has ingredients
    Then the shopping list items are saved to the database

  Scenario: Shopping list database is updated when a meal is removed
    Given 'Alice' has an upcoming meal with ingredients
    When 'Alice' deletes that meal
    Then the associated shopping list items are removed from the database

  Scenario: Plural and singular forms of the same ingredient are combined
    Given 'Alice' has an upcoming meal with an ingredient named 'Potato'
    And 'Alice' has manually added 'potatoes' to her shopping list
    When 'Alice' views her shopping list
    Then 'Potato' appears only once on the shopping list

  Scenario: User can edit the quantity of a shopping list item
    Given 'Alice' has manually added an item to her shopping list
    When 'Alice' views her shopping list
    And 'Alice' updates the quantity of 'ManualShoppingItem' to 5
    Then the shopping list shows quantity 5 for 'ManualShoppingItem'

  Scenario: Shopping list updates when date range changes
    Given 'Alice' has a meal with ingredient 'wvt20rangea' scheduled on today
    And 'Alice' has a meal with ingredient 'wvt20rangeb' scheduled 7 days from now
    When 'Alice' views the shopping list for today's date
    Then the shopping list contains 'wvt20rangea'
    And the shopping list does not contain 'wvt20rangeb'
    When 'Alice' views the shopping list for 7 days from now
    Then the shopping list contains 'wvt20rangeb'
    And the shopping list does not contain 'wvt20rangea'
    When 'Alice' views the shopping list for today's date
    Then the shopping list contains 'wvt20rangea'
    And the shopping list does not contain 'wvt20rangeb'

  Scenario: Increment button increases a decimal quantity by 1 and keeps decimal notation
    Given 'Alice' has 'wvt20decimal' with amount '1.5' and measurement 'Count' on the shopping list
    When 'Alice' navigates to the shopping list
    And 'Alice' sets the quantity display of 'wvt20decimal' to '1.5'
    And 'Alice' clicks increment on 'wvt20decimal'
    Then 'wvt20decimal' displays with amount '2.5' on the shopping list

  Scenario: Increment button increases a fraction quantity by one step and keeps fraction notation
    Given 'Alice' has 'wvt20fraction' with amount '0.25' and measurement 'Count' on the shopping list
    When 'Alice' navigates to the shopping list
    And 'Alice' sets the quantity display of 'wvt20fraction' to '1/4'
    And 'Alice' clicks increment on 'wvt20fraction'
    Then 'wvt20fraction' displays with amount '2/4' on the shopping list
