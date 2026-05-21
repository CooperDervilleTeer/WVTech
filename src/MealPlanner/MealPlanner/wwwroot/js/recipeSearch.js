const LOW_RATING_COLOR = "red";
const HIGH_RATING_COLOR = "green";
const API_ROUTE = "/api/recipe/";

$(document).ready(() => {
    $("#searchText").on("input", throttle(recipeSearchHandler, 1000));
    $("#tagFilter").on("change", recipeSearchHandler);
    loadTags();

    if ($("#editMealForm").length) {
        $(document).on("click", ".mealRecipeItem", function (e) {
            if ($(e.target).closest(".delete-recipe-btn").length) return;
            const recipeId = $(this).data("id");
            if (recipeId) window.location.href = `/FoodEntries/Recipes/${recipeId}`;
        });

        $(document).on("click", ".recipeSearchRow", async function () {
            const $row = $(this);
            const recipeName = $row.find(".recipeName").text();
            const externalUri = $row.attr("externalUri");

            let recipeId = Number($row.find(".recipeId").text());
            recipeId = await resolveRecipeId(recipeId, recipeName, externalUri);
            if (!recipeId) return;

            // Store resolved ID back on the row
            $row.find(".recipeId").text(recipeId);
            $row.find(".recipeIdInput").val(recipeId);

            // Alert if already in the meal
            if ($(`#mealRecipeList [data-id="${recipeId}"]`).length > 0) {
                alert("This recipe is already in the meal");
                return;
            }

            // Toggle pending selection
            $row.toggleClass("selected");
            updateAddSelectedBtn();
        });

        $(document).on("click", "#addSelectedRecipesBtn", function () {
            const $mealForm = $("#editMealForm");
            $(".recipeSearchRow.selected").each(function () {
                const $row = $(this);
                const recipeId = Number($row.find(".recipeId").text());
                const recipeName = $row.find(".recipeName").text();
                if (!recipeId) return;
                if ($(`#mealRecipeList [data-id="${recipeId}"]`).length > 0) return;
                addRecipeToMealForm($mealForm, recipeId, recipeName);
            });
            $(".recipeSearchRow").removeClass("selected");
            updateAddSelectedBtn();
        });

        $(document).on("click", ".delete-recipe-btn", function (e) {
            e.stopPropagation();

            const $row = $(this).closest(".mealRecipeItem");

            showDeleteModal("Remove this recipe?", function () {
                $row.remove();
            });
        });
    }
});

async function resolveRecipeId(recipeId, recipeName, externalUri) {
    if (recipeId || !externalUri) return recipeId;

    const response = await fetch(
        `${API_ROUTE}external?recipeName=${encodeURIComponent(recipeName)}&externalUri=${encodeURIComponent(externalUri)}`
    );

    if (!response.ok) {
        console.warn("Failed to fetch external recipe ID");
        return null;
    }

    const fetchedId = await response.json();
    return fetchedId;
}

function addRecipeToMealForm($mealForm, recipeId, recipeName) {
    const $row = $('<div class="em-recipe-row mealRecipeItem">').attr('data-id', recipeId);
    const $name = $('<span class="em-recipe-name">').text(recipeName);
    const $actions = $('<div class="em-recipe-actions">');
    const $removeBtn = $('<button type="button" class="em-recipe-remove-btn delete-recipe-btn" title="Remove recipe">')
        .attr('data-id', recipeId)
        .html('<i class="ti ti-x"></i>');
    const $hidden = $('<input type="hidden" name="RecipeIds">').val(recipeId);

    $actions.append($removeBtn);
    $row.append($name, $actions, $hidden);
    $("#mealRecipeList").append($row);
}

function updateAddSelectedBtn() {
    const count = $(".recipeSearchRow.selected").length;
    const $btn = $("#addSelectedRecipesBtn");
    if (count > 0) {
        $btn.show().text(`Add ${count} recipe${count === 1 ? "" : "s"}`);
    } else {
        $btn.hide();
    }
}

// Throttle function from https://www.geeksforgeeks.org/javascript/javascript-throttling/
function throttle(func, delay)
{
    let last = 0;
    return function (...args)
    {
        const search = $("#searchText").val();

        if (search.length < 3) return;
        let now = Date.now();
        if (now - last >= delay)
        {
            func.apply(this, args);
            last = now;
        }
    }
}

async function loadTags()
{
    const response = await fetch(`${API_ROUTE}tags`);
    if (!response.ok) return;
    const tags = await response.json();
    const $select = $("#tagFilter");
    tags.forEach(tag => {
        $select.append(`<option value="${tag}">${tag}</option>`);
    });
}

async function recipeSearchHandler(event)
{
    const search = $("#searchText").val();
    const tagVal = ($("#tagFilter").val() || "").trim();
    const tags   = (window.activeTagFilters && window.activeTagFilters.length > 0)
        ? window.activeTagFilters
        : (tagVal ? [tagVal] : []);

    if (search.length < 3 && tags.length === 0)
    {
        $("#recipeResults").text("");
        return;
    }

    $("#error").hide();
    $("#recipeResults").text("");
    updateAddSelectedBtn();

    let recipes;

    if (tags.length <= 1)
    {
        let url = `${API_ROUTE}search?name=${encodeURIComponent(search)}`;
        if (tags.length === 1) url += `&tag=${encodeURIComponent(tags[0])}`;
        const response = await fetch(url);

        if (response.status === 204)
        {
            showSearchError(
                "No recipes match your active dietary filters. " +
                "<a href='/UserSettings/Dietary'>Adjust your dietary restrictions</a>."
            );
            return;
        }
        if (!response.ok)
        {
            showSearchError("No recipes found, sorry!");
            return;
        }
        recipes = await response.json();
    }
    else
    {
        // Parallel fetch per tag, then union results (OR logic)
        const fetches = tags.map(tag =>
            fetch(`${API_ROUTE}search?name=${encodeURIComponent(search)}&tag=${encodeURIComponent(tag)}`)
                .then(r => (r.ok ? r.json() : []))
        );
        const results = await Promise.all(fetches);

        const seen = new Set();
        recipes = [];
        for (const set of results)
        {
            for (const r of set)
            {
                if (!seen.has(r.id))
                {
                    seen.add(r.id);
                    recipes.push(r);
                }
            }
        }

        if (recipes.length === 0)
        {
            showSearchError("No recipes found, sorry!");
            return;
        }
    }

    renderRecipeRows(recipes);
}

function showSearchError(msg)
{
    $("#error").show().html(msg);
}

function renderRecipeRows(recipes)
{
    const rowTemplate = $("#recipeResult");
    for (const recipe of recipes)
    {
        const row    = rowTemplate.contents().clone(true);
        const rating = (recipe.votePercentage * 100).toFixed(0) + "%";
        $(row).attr("externalUri", recipe.externalUri);
        if (recipe.externalUri) $(".row-end", row).text("");
        $(".recipeName", row).text(recipe.name);
        $(".recipeId", row).text(recipe.id);
        $(".recipeIdInput", row).val(recipe.id);
        $(".recipeRating", row).text(rating);
        $(".recipeRating", row).attr("style", `color: color-mix(in oklch, ${LOW_RATING_COLOR}, ${HIGH_RATING_COLOR} ${rating});`);

        const hasImage    = recipe.imageUrl && recipe.imageUrl.length > 0;
        const thumbnailSrc = hasImage ? recipe.imageUrl : "/images/placeholder/no-image.svg";
        $(".recipe-thumbnail", row)
            .attr("src", thumbnailSrc)
            .attr("alt", recipe.name)
            .addClass(hasImage ? "recipe-has-image" : "recipe-no-image");

        if (recipe.matchedRestrictionTags && recipe.matchedRestrictionTags.length > 0)
        {
            recipe.matchedRestrictionTags.forEach(tag => {
                $(".recipeTags", row).append(
                    `<span class="badge bg-success restriction-tag">${tag}</span>`
                );
            });
        }

        $("#recipeResults").append(row);
    }
}
