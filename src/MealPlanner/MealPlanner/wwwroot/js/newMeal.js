// ── Segmented control ─────────────────────────────────────
(function () {
    const segSingle  = document.getElementById('segSingle');
    const showDayPlanWizard = document.getElementById('showDayPlanWizard');
    const modal      = document.getElementById('dayPlanModal');
    const aiBanner   = document.querySelector('.ai-banner');

    function activate(btn) {
        segSingle.classList.remove('active');
        showDayPlanWizard.classList.remove('active');
        btn.classList.add('active');
        if (aiBanner) aiBanner.style.display = btn === segSingle ? '' : 'none';
    }

    segSingle.addEventListener('click', function () { activate(segSingle); });
    showDayPlanWizard.addEventListener('click', function () { activate(showDayPlanWizard); });

    // Revert to single-meal when modal closes
    if (modal) {
        modal.addEventListener('hidden.bs.modal', function () { activate(segSingle); });
    }
})();

// ── Repeat weekly toggle ──────────────────────────────────
(function () {
    const checkbox = document.getElementById('repeatWeeklyToggle');
    const visual   = document.getElementById('repeatToggleVisual');
    const panel    = document.getElementById('repeatDaysPanel');

    function sync() {
        visual.classList.toggle('on', checkbox.checked);
        panel.classList.toggle('open', checkbox.checked);
    }

    visual.addEventListener('click', function () {
        sync();
    });

    checkbox.addEventListener('change', sync);

    visual.addEventListener('keydown', function (e) {
        if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); checkbox.checked = !checkbox.checked; sync(); }
    });

    sync();
})();

// ── Repeat day chips ──────────────────────────────────────
(function () {
    document.querySelectorAll('.repeat-day-chip').forEach(function (chip) {
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
})();

// ── Update "Add meal to …" label when date selects change ─
(function () {
    const monthSel = document.getElementById('SelectedMonth');
    const daySel   = document.getElementById('SelectedDay');
    const label    = document.getElementById('addMealBtnLabel');

    const monthNames = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

    function updateLabel() {
        const m = parseInt(monthSel.value, 10);
        const d = parseInt(daySel.value, 10);
        if (m && d) {
            label.textContent = 'Add meal to ' + monthNames[m - 1] + ' ' + d;
        }
    }

    if (monthSel) monthSel.addEventListener('change', updateLabel);
    if (daySel)   daySel.addEventListener('change', updateLabel);
})();

// ── Recommend meal tag pills ──────────────────────────────
(function () {
    const form        = document.getElementById('recommendMealForm');
    if (!form) return;
    const availableTags = JSON.parse(form.dataset.tags);
    const pills         = document.getElementById('recommendMealTagPills');
    const select        = document.getElementById('recommendMealTagSelect');
    const customInput   = document.getElementById('recommendMealCustomTag');
    const addBtn        = document.getElementById('recommendMealAddTag');

    function addTag(id, name, isCustom) {
        if (id && form.querySelector(`input[name="TagIds"][value="${id}"]`)) { select.value = ''; return; }

        const pill   = document.createElement('button');
        pill.type    = 'button';
        pill.className = 'tag-pill badge rounded-pill recipe-tag recipe-tag-removable';
        pill.title   = `Remove ${name}`;

        const hidden = document.createElement('input');
        hidden.type  = 'hidden';
        hidden.value = isCustom ? name : id;
        hidden.name  = isCustom ? 'CustomTagName' : 'TagIds';

        pill.textContent = name;
        pill.appendChild(hidden);
        pill.addEventListener('click', function () {
            if (!isCustom) {
                const opt = document.createElement('option');
                opt.value = id; opt.textContent = name;
                const opts = Array.from(select.options).slice(1);
                const before = opts.find(o => o.textContent.toLowerCase() > name.toLowerCase());
                if (before) select.insertBefore(opt, before); else select.appendChild(opt);
            }
            pill.remove();
        });

        if (isCustom) {
            const prev = pills.querySelector('.custom-pill');
            if (prev) prev.remove();
            pill.classList.add('custom-pill');
        } else {
            const opt = Array.from(select.options).find(o => o.value == id);
            if (opt) opt.remove();
        }

        pills.appendChild(pill);
        select.value = '';
    }

    select.addEventListener('change', function () {
        if (!select.value) return;
        addTag(select.value, select.options[select.selectedIndex].textContent.trim(), false);
    });

    addBtn.addEventListener('click', function () {
        const name = customInput.value.trim();
        if (!name) return;
        const match = availableTags.find(t => t.name.toLowerCase() === name.toLowerCase());
        if (match) addTag(match.id, match.name, false);
        else addTag(null, name, true);
        customInput.value = '';
    });

    customInput.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        addBtn.click();
    });
})();
