const API_ROUTE = "/api/recipe/";

window.activeTagFilters = [];

const pendingRecipeIds = new Set();

$(document).ready(() => {
    $(document).on("click", ".recipeSearchRow", toggleRecipe);
    $(document).on("click", ".nm-recipe-remove-btn", removeSelectedRecipe);
    $(document).on("click", "#addSelectedRecipesBtn", commitSelectedRecipes);
    initTagFilterChips();
});

function updateSelectedCard() {
    const $card = $("#selectedRecipesCard");
    const hasItems = $("#createMealList .nm-recipe-row").length > 0;
    $card.toggleClass("open", hasItems);
}

async function toggleRecipe() {
    const $row = $(this);

    let recipeId = Number($(".recipeId", this).text());
    const recipeName = $(".recipeName", this).text();

    // Resolve external URI to a local recipe ID
    const externalUri = encodeURIComponent($row.attr("externaluri") || "");
    if (!recipeId && externalUri && externalUri !== "undefined") {
        const url = API_ROUTE + `external?recipeName=${encodeURIComponent(recipeName)}&externalUri=${externalUri}`;
        const response = await fetch(url);
        if (!response.ok) return;
        recipeId = await response.json();
        // Store resolved ID so repeated clicks don't re-fetch
        $(".recipeId", this).text(recipeId);
        $(".recipeIdInput", this).val(recipeId);
    }

    if (!recipeId) return;

    // Deselect if already pending
    if ($row.hasClass("selected")) {
        $row.removeClass("selected");
        pendingRecipeIds.delete(recipeId);
        updateAddSelectedBtn();
        return;
    }

    // Alert if already committed or pending
    if ($(`#createMealList .nm-recipe-row[data-id="${recipeId}"]`).length > 0 || pendingRecipeIds.has(recipeId)) {
        alert("This recipe is already in the meal");
        return;
    }

    pendingRecipeIds.add(recipeId);
    $row.addClass("selected");
    updateAddSelectedBtn();
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

function commitSelectedRecipes() {
    $(".recipeSearchRow.selected").each(function () {
        const $row = $(this);
        const recipeId = Number($(".recipeId", $row).text());
        const recipeName = $(".recipeName", $row).text();
        if (!recipeId) return;
        if ($(`#createMealList .nm-recipe-row[data-id="${recipeId}"]`).length > 0) return;
        pendingRecipeIds.delete(recipeId);
        $("#createMealForm").append(`<input type="hidden" name="RecipeIds" value="${recipeId}" />`);
        addToSelectedCard(recipeId, recipeName);
    });
    $(".recipeSearchRow").removeClass("selected");
    pendingRecipeIds.clear();
    updateAddSelectedBtn();
    updateSelectedCard();
}

function addToSelectedCard(recipeId, recipeName) {
    const $row = $(`<div class="nm-recipe-row mealRecipeItem" data-id="${recipeId}">`)
        .append(`<h4 class="nm-recipe-name">${$("<span>").text(recipeName).html()}</h4>`)
        .append(`<button type="button" class="nm-recipe-remove-btn delete-recipe-btn" data-id="${recipeId}" title="Remove recipe"><i class="ti ti-x"></i></button>`);
    $("#createMealList").append($row);
}

function removeSelectedRecipe() {
    const recipeId = $(this).data("id");
    const $btn = $(this);

    function doRemove() {
        $btn.closest(".nm-recipe-row").remove();
        $(`#createMealForm input[name="RecipeIds"][value="${recipeId}"]`).remove();
        pendingRecipeIds.delete(recipeId);
        $(`.recipeSearchRow`).each(function () {
            if (Number($(".recipeId", this).text()) === recipeId) {
                $(this).removeClass("selected");
            }
        });
        updateSelectedCard();
    }

    if (typeof showDeleteModal === "function") {
        showDeleteModal("Remove this recipe?", doRemove);
    } else {
        doRemove();
    }
}

// ── Tag filter chips ──────────────────────────────────────────
// Builds toggleable pill chips in #tagFilterChips.
// recipeSearch.js populates #tagFilter with <option>s via loadTags();
// we watch those mutations to add matching chips.
// Selected tags are stored in window.activeTagFilters and a change event
// on #tagFilter triggers recipeSearchHandler in recipeSearch.js.

function initTagFilterChips() {
    const container = document.getElementById("tagFilterChips");
    const tagFilter  = document.getElementById("tagFilter");
    if (!container || !tagFilter) return;

    let selectedTags = [];

    const allChip = document.createElement("button");
    allChip.type = "button";
    allChip.className = "filter-chip active";
    allChip.dataset.tag = "";
    allChip.textContent = "All tags";
    container.appendChild(allChip);

    function commit() {
        window.activeTagFilters = [...selectedTags];
        tagFilter.dispatchEvent(new Event("change"));
    }

    function updateUI() {
        allChip.classList.toggle("active", selectedTags.length === 0);
        container.querySelectorAll(".filter-chip[data-tag]").forEach(chip => {
            if (!chip.dataset.tag) return;
            chip.classList.toggle("active", selectedTags.includes(chip.dataset.tag));
        });
        commit();
    }

    allChip.addEventListener("click", () => {
        selectedTags = [];
        updateUI();
    });

    const observer = new MutationObserver(() => {
        Array.from(tagFilter.options).forEach(opt => {
            if (!opt.value) return;
            if (container.querySelector(`.filter-chip[data-tag="${CSS.escape(opt.value)}"]`)) return;
            const chip = document.createElement("button");
            chip.type = "button";
            chip.className = "filter-chip";
            chip.dataset.tag = opt.value;
            chip.textContent = opt.textContent.trim();
            chip.addEventListener("click", () => {
                const idx = selectedTags.indexOf(opt.value);
                if (idx === -1) selectedTags.push(opt.value);
                else selectedTags.splice(idx, 1);
                updateUI();
            });
            container.appendChild(chip);
        });
    });
    observer.observe(tagFilter, { childList: true });
}
