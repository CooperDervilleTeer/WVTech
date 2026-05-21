using MealPlanner.Models;
using MealPlanner.Models.DTO;
using MealPlanner.Services;
using System.Net;
using Moq;
using Moq.Protected;

namespace MealPlanner.Tests;

public class EdamamServiceTests
{
    private EdamamService SetupMocks(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
        => SetupMocksWithHandler(jsonResponse, statusCode).service;

    private (EdamamService service, Mock<HttpMessageHandler> handler) SetupMocksWithHandler(
        string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var messageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonResponse)
            })
            .Verifiable();

        var client = new HttpClient(messageHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com/api/")
        };

        return (new EdamamService(client, "testid", "testkey"), messageHandler);
    }

    [Test]
    public async Task SearchExternalRecipesByName_Returns1Recipe_IfResponseOK()
    {
        // Arrange
        string json = 
            """
            {
                "from": 1,
                "to": 1,
                "count": 1,
                "_links": {},
                "hits": [
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test123",
                            "label": "Chocolate Chip Oatmeal Cookies"
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    }
                ]
            }
            """;

        EdamamService service = SetupMocks(json);

        // Act
        var result = await service.SearchExternalRecipesByName("oatmeal");

        // Assert
        Assert.That(result, Is.Not.Empty);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.First().Name, Is.EqualTo("Chocolate Chip Oatmeal Cookies"));
            Assert.That(result.First().ExternalUri, Is.EqualTo("http://TestUri.com/Test123"));
        }
    }

    [Test]
    public async Task SearchExternalRecipesByName_ReturnsManyRecipes_IfResponseOK()
    {
        // Arrange
        string json = 
            """
            {
                "from": 1,
                "to": 4,
                "count": 4,
                "_links": {},
                "hits": [
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test1",
                            "label": "Spaghetti1"
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    },
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test12",
                            "label": "Spaghetti2"
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    },
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test123",
                            "label": "Spaghetti3"
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    },
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test1234",
                            "label": "Spaghetti4"
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    }
                ]
            }
            """;

        EdamamService service = SetupMocks(json);

        // Act
        var result = await service.SearchExternalRecipesByName("spaghetti");

        // Assert
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task SearchExternalRecipesByName_ReturnsNoRecipes_IfResponseOK()
    {
        // Arrange
        string json = 
            """
            {
                "from": 0,
                "to": 0,
                "count": 0,
                "_links": {},
                "hits": []
            }
            """;

        EdamamService service = SetupMocks(json);

        // Act
        var result = await service.SearchExternalRecipesByName("no recipes");

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SearchExternalRecipesByName_ThrowsError_IfResponseNotOK()
    {
        // Arrange
        EdamamService service = SetupMocks("", HttpStatusCode.Forbidden);
        // Act
        var exception = Assert.CatchAsync(
            async () => await service.SearchExternalRecipesByName("oatmeal"));
        
        // Assert
        Assert.That(exception, Is.TypeOf<Exception>());
    }

    [Test]
    public async Task GetExternalRecipeByURI_ReturnsRecipe_IfResponseOK()
    {
        // Arrange
        string json = 
            """
            {
                "from": 1,
                "to": 1,
                "count": 1,
                "_links": {},
                "hits": [
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test123",
                            "label": "Chocolate Chip Oatmeal Cookies",
                            "ingredients": [
                                {
                                    "text": "1/2 cup Test",
                                    "quantity": 0.5,
                                    "measure": "cup",
                                    "food": "test",
                                    "weight": 1.1,
                                    "foodCategory": "test cat",
                                    "foodId": "food_1234",
                                    "image": "https://www.test.com/img/1"
                                },
                                {
                                    "text": "3 teaspoon Test2",
                                    "quantity": 3,
                                    "measure": "teaspoon",
                                    "food": "test2",
                                    "weight": 1.1,
                                    "foodCategory": "test2 cat",
                                    "foodId": "food_1234",
                                    "image": "https://www.test.com/img/2"
                                },
                                {
                                    "text": "5 1/2 teaspoon Test3",
                                    "quantity": 5.5,
                                    "measure": "teaspoon",
                                    "food": "test2",
                                    "weight": 1.1,
                                    "foodCategory": "test2 cat",
                                    "foodId": "food_1234",
                                    "image": "https://www.test.com/img/2"
                                }
                            ],
                            "totalNutrients": {
                                "ENERC_KCAL": {
                                    "label": "Energy",
                                    "quantity": 1,
                                    "unit": "kcal"
                                },
                                "FAT": {
                                    "label": "Fat",
                                    "quantity": 2,
                                    "unit": "g"
                                },
                                "CHOCDF": {
                                    "label": "Carbs",
                                    "quantity": 3,
                                    "unit": "g"
                                },
                                "CHOCDF.net": {
                                    "label": "Carbohydrates (net)",
                                    "quantity": 4,
                                    "unit": "g"
                                },
                                "PROCNT": {
                                    "label": "Protein",
                                    "quantity": 5,
                                    "unit": "g"
                                }
                            }
                        },
                        "_links": {
                            "self": {
                                "href": "https://api.test.com/api/test123",
                                "title": "Self"
                            }
                        }
                    }
                ]
            }
            """;
        EdamamService service = SetupMocks(json);
        // Act
        var result = await service.GetExternalRecipeByURI("http://TestUri.com/Test123");

        // Assert
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Name, Is.EqualTo("Chocolate Chip Oatmeal Cookies"));
            Assert.That(result.ExternalUri, Is.EqualTo("http://TestUri.com/Test123"));
            Assert.That(result.Calories, Is.EqualTo(1));
            Assert.That(result.Fat, Is.EqualTo(2));
            Assert.That(result.Carbs, Is.EqualTo(3));
            Assert.That(result.Protein, Is.EqualTo(5));
            Assert.That(result.Ingredients.Count, Is.EqualTo(3));
            var firstIngredient = result.Ingredients.First();
            Assert.That(firstIngredient.Amount, Is.EqualTo(0.5).Within(0.005));
            Assert.That(firstIngredient.Measurement.Name, Is.EqualTo("cup"));
            Assert.That(firstIngredient.IngredientBase.Name, Is.EqualTo("test"));
        }
        
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task GetExternalRecipeByURI_ThrowsError_IfResponseNotOk(HttpStatusCode statusCode)
    {
        // Arrange
        EdamamService service = SetupMocks("", statusCode);

        // Act
        var exception = Assert.CatchAsync(
            async () => await service.GetExternalRecipeByURI("http://TestUri.com/Test123"));

        // Assert
        Assert.That(exception, Is.TypeOf<Exception>());
    }

    [Test]
    public async Task GetExternalRecipesByURIs_ReturnsEmptyList_WhenGivenEmptyList()
    {
        // Arrange — strict mock: any HTTP call would throw, proving we skip the request
        var messageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var client = new HttpClient(messageHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com/api/")
        };
        EdamamService service = new EdamamService(client, "testid", "testkey");

        // Act
        var result = await service.GetExternalRecipesByURIs([]);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetExternalRecipesByURIs_ReturnsManyRecipes_WhenResponseOK()
    {
        // Arrange
        string json =
            """
            {
                "from": 1,
                "to": 2,
                "count": 2,
                "_links": {},
                "hits": [
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test1",
                            "label": "Spaghetti Bolognese",
                            "ingredients": [],
                            "totalNutrients": {
                                "ENERC_KCAL": { "label": "Energy", "quantity": 500, "unit": "kcal" },
                                "FAT":        { "label": "Fat",    "quantity": 10,  "unit": "g" },
                                "CHOCDF":     { "label": "Carbs",  "quantity": 60,  "unit": "g" },
                                "PROCNT":     { "label": "Protein","quantity": 25,  "unit": "g" }
                            }
                        },
                        "_links": { "self": { "href": "https://api.test.com/api/test1", "title": "Self" } }
                    },
                    {
                        "recipe": {
                            "uri": "http://TestUri.com/Test2",
                            "label": "Chicken Salad",
                            "ingredients": [],
                            "totalNutrients": {
                                "ENERC_KCAL": { "label": "Energy", "quantity": 300, "unit": "kcal" },
                                "FAT":        { "label": "Fat",    "quantity": 8,   "unit": "g" },
                                "CHOCDF":     { "label": "Carbs",  "quantity": 15,  "unit": "g" },
                                "PROCNT":     { "label": "Protein","quantity": 35,  "unit": "g" }
                            }
                        },
                        "_links": { "self": { "href": "https://api.test.com/api/test2", "title": "Self" } }
                    }
                ]
            }
            """;

        EdamamService service = SetupMocks(json);

        // Act
        var result = await service.GetExternalRecipesByURIs(
            ["http://TestUri.com/Test1", "http://TestUri.com/Test2"]);

        // Assert
        Assert.That(result, Is.Not.Empty);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.First().Name, Is.EqualTo("Spaghetti Bolognese"));
            Assert.That(result.First().ExternalUri, Is.EqualTo("http://TestUri.com/Test1"));
            Assert.That(result.First().Calories, Is.EqualTo(500));
            Assert.That(result.Last().Name, Is.EqualTo("Chicken Salad"));
        }
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task GetExternalRecipesByURIs_ThrowsError_IfResponseNotOk(HttpStatusCode statusCode)
    {
        // Arrange
        EdamamService service = SetupMocks("", statusCode);

        // Act
        var exception = Assert.CatchAsync(
            async () => await service.GetExternalRecipesByURIs(["http://TestUri.com/Test1"]));

        // Assert
        Assert.That(exception, Is.TypeOf<Exception>());
    }

    [Test]
    public async Task GetExternalRecipesByURIs_MakesTwoCalls_When21URIsGiven()
    {
        // Arrange — mock returns 1 recipe per call; 21 URIs → 2 batches (20 + 1)
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/A",
                        "label": "Recipe A",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 100, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 1,   "unit": "g" },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 1,   "unit": "g" },
                            "PROCNT":     { "label": "Protein","quantity": 1,   "unit": "g" }
                        }
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/a", "title": "Self" } }
                }]
            }
            """;

        var (service, handler) = SetupMocksWithHandler(json);
        var uris = Enumerable.Range(1, 21).Select(i => $"http://TestUri.com/{i}");

        // Act
        var result = await service.GetExternalRecipesByURIs(uris);

        // Assert — 2 HTTP calls made, results from both batches combined
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        Assert.That(result.Count(), Is.EqualTo(2));
    }

    // ── SearchByContextAsync ─────────────────────────────────────────────────

    private (EdamamService service, List<HttpRequestMessage> requests) SetupMocksCapturingRequests(
        string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var requests = new List<HttpRequestMessage>();
        var messageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => requests.Add(req))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonResponse)
            });
        var client = new HttpClient(messageHandler.Object) { BaseAddress = new Uri("https://api.test.com/api/") };
        return (new EdamamService(client, "testid", "testkey"), requests);
    }

    private const string EmptyHitsJson = """{ "from": 1, "to": 0, "count": 0, "_links": {}, "hits": [] }""";

    [Test]
    public async Task SearchByContextAsync_NoCriteria_ReturnsEmptyAndMakesNoCall()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(null, null, null, null, null, null, null, null, null, []);

        var result = await service.SearchByContextAsync(query);

        Assert.That(result, Is.Empty);
        Assert.That(requests, Is.Empty, "Should short-circuit when no criteria are present");
    }

    [Test]
    public async Task SearchByContextAsync_BuildsUrlWithCaloriesRange()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(null, 100, 500, null, null, null, null, null, null, []);

        await service.SearchByContextAsync(query);

        Assert.That(requests, Has.Count.EqualTo(1));
        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("calories=100-500"));
    }

    [Test]
    public async Task SearchByContextAsync_BuildsUrlWithMacroNutrientRanges()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(
            null,
            null, null,
            10, 50,   // protein
            20, 60,   // carbs
            5, 15,    // fat
            []);

        await service.SearchByContextAsync(query);

        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("nutrients%5BPROCNT%5D=10-50"));
        Assert.That(uri, Does.Contain("nutrients%5BCHOCDF%5D=20-60"));
        Assert.That(uri, Does.Contain("nutrients%5BFAT%5D=5-15"));
    }

    [Test]
    public async Task SearchByContextAsync_BuildsUrlWithHealthFilters()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(
            null, null, null, null, null, null, null, null, null,
            ["vegan", "gluten-free"]);

        await service.SearchByContextAsync(query);

        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("health=vegan"));
        Assert.That(uri, Does.Contain("health=gluten-free"));
    }

    [Test]
    public async Task SearchByContextAsync_IncludesFreeTextQuery()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery("Italian Breakfast", null, null, null, null, null, null, null, null, []);

        await service.SearchByContextAsync(query);

        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("q=Italian"));
    }

    [Test]
    public async Task SearchByContextAsync_BuildsUrlWithFacets()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(null, null, null, null, null, null, null, null, null, [])
        {
            Diets = ["high-protein"],
            CuisineTypes = ["Italian"],
            MealTypes = ["Breakfast"],
            DishTypes = ["Salad"],
        };

        await service.SearchByContextAsync(query);

        Assert.That(requests, Has.Count.EqualTo(1));
        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("diet=high-protein"));
        Assert.That(uri, Does.Contain("cuisineType=Italian"));
        Assert.That(uri, Does.Contain("mealType=Breakfast"));
        Assert.That(uri, Does.Contain("dishType=Salad"));
    }

    [Test]
    public async Task SearchByContextAsync_OmitsParamsForUnsetBounds()
    {
        var (service, requests) = SetupMocksCapturingRequests(EmptyHitsJson);
        var query = new ExternalSearchQuery(null, 100, 500, null, null, null, null, null, null, []);

        await service.SearchByContextAsync(query);

        var uri = requests[0].RequestUri!.ToString();
        Assert.That(uri, Does.Contain("calories="));
        Assert.That(uri, Does.Not.Contain("nutrients"));
        Assert.That(uri, Does.Not.Contain("health="));
    }

    [Test]
    public async Task SearchByContextAsync_ParsesRecipeMacrosFromResponse()
    {
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/X",
                        "label": "Test Recipe",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 420, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 12,  "unit": "g" },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 45,  "unit": "g" },
                            "PROCNT":     { "label": "Protein","quantity": 30,  "unit": "g" }
                        }
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/x", "title": "Self" } }
                }]
            }
            """;
        var (service, _) = SetupMocksCapturingRequests(json);
        var query = new ExternalSearchQuery(null, 1, 1000, null, null, null, null, null, null, []);

        var result = (await service.SearchByContextAsync(query)).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Test Recipe"));
        Assert.That(result[0].ExternalUri, Is.EqualTo("http://TestUri.com/X"));
        Assert.That(result[0].Calories, Is.EqualTo(420));
        Assert.That(result[0].Protein, Is.EqualTo(30));
        Assert.That(result[0].Carbs, Is.EqualTo(45));
        Assert.That(result[0].Fat, Is.EqualTo(12));
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.BadRequest)]
    public void SearchByContextAsync_ThrowsOnNonSuccessStatus(HttpStatusCode statusCode)
    {
        var (service, _) = SetupMocksCapturingRequests("", statusCode);
        var query = new ExternalSearchQuery(null, 1, 500, null, null, null, null, null, null, []);

        Assert.CatchAsync(async () => await service.SearchByContextAsync(query));
    }

    [Test]
    public async Task SearchByContextAsync_CapturesEdamamCategorizationOnReturnedRecipe()
    {
        // Edamam's response carries dishType / cuisineType / mealType /
        // dietLabels / healthLabels — the inverse classifier needs them in
        // order to attach matching local Tags to external recipes.
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/X",
                        "label": "Vegan Italian Salad",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 200, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 5,   "unit": "g"    },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 25,  "unit": "g"    },
                            "PROCNT":     { "label": "Protein","quantity": 8,   "unit": "g"    }
                        },
                        "dietLabels": ["high-protein"],
                        "healthLabels": ["vegan", "gluten-free"],
                        "cuisineType": ["Italian"],
                        "mealType": ["lunch/dinner"],
                        "dishType": ["Salad"]
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/x", "title": "Self" } }
                }]
            }
            """;
        var (service, _) = SetupMocksCapturingRequests(json);
        var query = new ExternalSearchQuery(null, 1, 500, null, null, null, null, null, null, []);

        var result = (await service.SearchByContextAsync(query)).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ExternalCategorization, Is.SupersetOf(new[]
        {
            "high-protein", "vegan", "gluten-free", "Italian", "Salad"
        }));
    }

    [Test]
    public async Task SearchByContextAsync_MissingCategorization_ProducesEmptyList()
    {
        // An Edamam response that omits the categorization arrays must still
        // produce a Recipe with an empty (not-null) ExternalCategorization, so
        // downstream code can iterate it safely.
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/Y",
                        "label": "Plain Recipe",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 200, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 5,   "unit": "g"    },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 25,  "unit": "g"    },
                            "PROCNT":     { "label": "Protein","quantity": 8,   "unit": "g"    }
                        }
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/y", "title": "Self" } }
                }]
            }
            """;
        var (service, _) = SetupMocksCapturingRequests(json);
        var query = new ExternalSearchQuery(null, 1, 500, null, null, null, null, null, null, []);

        var result = (await service.SearchByContextAsync(query)).ToList();

        Assert.That(result[0].ExternalCategorization, Is.Not.Null);
        Assert.That(result[0].ExternalCategorization, Is.Empty);
    }

    [Test]
    public async Task GetExternalRecipeByURI_CapturesEdamamCategorizationOnReturnedRecipe()
    {
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/Z",
                        "label": "Curry",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 600, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 15,  "unit": "g"    },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 60,  "unit": "g"    },
                            "PROCNT":     { "label": "Protein","quantity": 20,  "unit": "g"    }
                        },
                        "cuisineType": ["Indian"],
                        "mealType": ["Dinner"]
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/z", "title": "Self" } }
                }]
            }
            """;
        EdamamService service = SetupMocks(json);

        var result = await service.GetExternalRecipeByURI("http://TestUri.com/Z");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ExternalCategorization, Is.SupersetOf(new[] { "Indian", "Dinner" }));
    }

    [Test]
    public async Task GetExternalRecipesByURIs_CapturesEdamamCategorizationOnEachRecipe()
    {
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/W",
                        "label": "Pancakes",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 400, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 12,  "unit": "g"    },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 50,  "unit": "g"    },
                            "PROCNT":     { "label": "Protein","quantity": 10,  "unit": "g"    }
                        },
                        "dishType": ["Pancake"],
                        "mealType": ["Breakfast"]
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/w", "title": "Self" } }
                }]
            }
            """;
        EdamamService service = SetupMocks(json);

        var result = (await service.GetExternalRecipesByURIs(["http://TestUri.com/W"])).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ExternalCategorization, Is.SupersetOf(new[] { "Pancake", "Breakfast" }));
    }

    [Test]
    public async Task GetExternalRecipesByURIs_MakesOneCall_When20URIsGiven()
    {
        // Arrange — exactly 20 URIs fits in one batch
        string json =
            """
            {
                "from": 1, "to": 1, "count": 1, "_links": {},
                "hits": [{
                    "recipe": {
                        "uri": "http://TestUri.com/A",
                        "label": "Recipe A",
                        "ingredients": [],
                        "totalNutrients": {
                            "ENERC_KCAL": { "label": "Energy", "quantity": 100, "unit": "kcal" },
                            "FAT":        { "label": "Fat",    "quantity": 1,   "unit": "g" },
                            "CHOCDF":     { "label": "Carbs",  "quantity": 1,   "unit": "g" },
                            "PROCNT":     { "label": "Protein","quantity": 1,   "unit": "g" }
                        }
                    },
                    "_links": { "self": { "href": "https://api.test.com/api/a", "title": "Self" } }
                }]
            }
            """;

        var (service, handler) = SetupMocksWithHandler(json);
        var uris = Enumerable.Range(1, 20).Select(i => $"http://TestUri.com/{i}");

        // Act
        var result = await service.GetExternalRecipesByURIs(uris);

        // Assert — exactly 1 HTTP call
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(1),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        Assert.That(result.Count(), Is.EqualTo(1));
    }
}