window.activeTagFilters = [];

document.addEventListener('DOMContentLoaded', function () {
    initRepeatToggle();
    initDayChips();
    initTagFilterChips();
});

// ── Repeat weekly toggle ──────────────────────────────────────

function initRepeatToggle() {
    const checkbox = document.getElementById('repeatWeeklyToggle');
    const visual   = document.getElementById('emRepeatToggle');
    const panel    = document.getElementById('repeatDaysPanel');
    if (!checkbox || !visual || !panel) return;

    function sync() {
        visual.classList.toggle('on', checkbox.checked);
        panel.classList.toggle('open', checkbox.checked);
    }

    visual.addEventListener('click', function () {
        checkbox.checked = !checkbox.checked;
        sync();
    });

    checkbox.addEventListener('change', sync);
}

// ── Repeat day chips ──────────────────────────────────────────

function initDayChips() {
    document.querySelectorAll('.em-day-chip').forEach(function (chip) {
        chip.addEventListener('click', function (e) {
            const cb = this.querySelector('input[type="checkbox"]');
            if (!cb) return;
            if (e.target === cb) {
                // Selenium/direct checkbox click: checkbox already toggled, sync class
                this.classList.toggle('on', cb.checked);
            } else {
                // Click on label area: toggle manually
                e.preventDefault();
                cb.checked = !cb.checked;
                this.classList.toggle('on', cb.checked);
            }
        });
    });
}

// ── Tag filter chips ──────────────────────────────────────────
// Builds toggleable pill chips in #tagFilterChips.
// recipeSearch.js populates #tagFilter with <option>s via loadTags();
// we watch those mutations to add matching chips.

function initTagFilterChips() {
    const container = document.getElementById('tagFilterChips');
    const tagFilter  = document.getElementById('tagFilter');
    if (!container || !tagFilter) return;

    let selectedTags = [];

    const allChip = document.createElement('button');
    allChip.type = 'button';
    allChip.className = 'filter-chip active';
    allChip.dataset.tag = '';
    allChip.textContent = 'All tags';
    container.appendChild(allChip);

    function commit() {
        window.activeTagFilters = [...selectedTags];
        tagFilter.dispatchEvent(new Event('change'));
    }

    function updateUI() {
        allChip.classList.toggle('active', selectedTags.length === 0);
        container.querySelectorAll('.filter-chip[data-tag]').forEach(function (chip) {
            if (!chip.dataset.tag) return;
            chip.classList.toggle('active', selectedTags.includes(chip.dataset.tag));
        });
        commit();
    }

    allChip.addEventListener('click', function () {
        selectedTags = [];
        updateUI();
    });

    const observer = new MutationObserver(function () {
        Array.from(tagFilter.options).forEach(function (opt) {
            if (!opt.value) return;
            if (container.querySelector(`.filter-chip[data-tag="${CSS.escape(opt.value)}"]`)) return;
            const chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'filter-chip';
            chip.dataset.tag = opt.value;
            chip.textContent = opt.textContent.trim();
            chip.addEventListener('click', function () {
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
