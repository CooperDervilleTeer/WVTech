Feature: selectMeal

  Background:
    Given there is a user named 'bob'
    And there is a user named 'alice'
    And 'bob' is logged into Onebite

  Scenario: 'bob' adds a previously created meal to today via the Select Meal page
    Given 'bob' has no previously created meals
    And 'bob' has a previously created meal named 'FavMeal'
    And 'bob' is on the select meal page
    Then 'bob' sees a meal named 'FavMeal' in the select meal list
    When 'bob' clicks the meal named 'FavMeal'
    Then 'bob' is redirected to the home page
    And 'bob' has a meal named 'FavMeal' scheduled for today

  Scenario: 'bob' sees an empty state when he has no meals
    Given 'bob' has no previously created meals
    And 'bob' is on the select meal page
    Then 'bob' sees the empty meal list message

  Scenario: 'bob' sees all of his distinct meals on the Select Meal page
    Given 'bob' has no previously created meals
    And 'bob' has a previously created meal named 'Breakfast'
    And 'bob' has a previously created meal named 'Lunch'
    And 'bob' has a previously created meal named 'Dinner'
    And 'bob' is on the select meal page
    Then 'bob' sees a meal named 'Breakfast' in the select meal list
    And 'bob' sees a meal named 'Lunch' in the select meal list
    And 'bob' sees a meal named 'Dinner' in the select meal list

  Scenario: 'bob' only sees his own meals, not other users' meals
    Given 'bob' has no previously created meals
    And 'alice' has no previously created meals
    And 'alice' has a previously created meal named 'AliceSecretMeal'
    And 'bob' has a previously created meal named 'BobMeal'
    And 'bob' is on the select meal page
    Then 'bob' sees a meal named 'BobMeal' in the select meal list
    And 'bob' does not see a meal named 'AliceSecretMeal' in the select meal list

  Scenario: 'bob' only sees one entry when the same meal title was scheduled on multiple days
    Given 'bob' has no previously created meals
    And 'bob' has a meal named 'RepeatedMeal' scheduled on 3 different past dates
    And 'bob' is on the select meal page
    Then 'bob' sees exactly 1 meal named 'RepeatedMeal' in the select meal list

  Scenario: 'bob' removes a meal from the select meal page and it disappears from the list
    Given 'bob' has no previously created meals
    And 'bob' has a previously created meal named 'ToBeRemoved'
    And 'bob' is on the select meal page
    When 'bob' clicks the remove button for the meal named 'ToBeRemoved'
    Then 'bob' does not see a meal named 'ToBeRemoved' in the select meal list

  Scenario: generated meals do not appear on the select meal page
    Given 'bob' has no previously created meals
    And 'bob' has a generated meal named 'GeneratedLunch'
    And 'bob' is on the select meal page
    Then 'bob' does not see a meal named 'GeneratedLunch' in the select meal list

  Scenario: manually created meals appear but generated meals do not
    Given 'bob' has no previously created meals
    And 'bob' has a generated meal named 'GeneratedDinner'
    And 'bob' has a previously created meal named 'ManualBreakfast'
    And 'bob' is on the select meal page
    Then 'bob' sees a meal named 'ManualBreakfast' in the select meal list
    And 'bob' does not see a meal named 'GeneratedDinner' in the select meal list

  Scenario: a meal removed from the schedule still appears in the select meal list
    Given 'bob' has no previously created meals
    And 'bob' has a previously created meal named 'RemovedMeal'
    And 'bob' has excluded 'RemovedMeal' from today's schedule
    And 'bob' is on the select meal page
    Then 'bob' sees a meal named 'RemovedMeal' in the select meal list
