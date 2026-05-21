// window.nutritionData must be set before this script runs:
//   { allDays: DailyNutritionDto[], dailyTargets: MacroTargets, initialTab: string }

const { allDays, dailyTargets, initialTab } = window.nutritionData;

let activeTab = initialTab || 'weekly';
let barChart = null;

const DAY_NAMES   = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const MACRO_COLORS = (function () {
    const s = getComputedStyle(document.documentElement);
    return {
        calories: s.getPropertyValue('--macro-calories').trim() || '#3B82F6',
        protein:  s.getPropertyValue('--macro-protein').trim()  || '#92400E',
        carbs:    s.getPropertyValue('--macro-carbs').trim()    || '#8B5CF6',
        fat:      s.getPropertyValue('--macro-fats').trim()     || '#F97316',
    };
})();

// ── Helpers ──────────────────────────────────────────────────────────────────

function parseLocalDate(isoString) {
    const [y, m, d] = isoString.split('-').map(Number);
    return new Date(y, m - 1, d);
}

function todayMidnight() {
    const t = new Date();
    t.setHours(0, 0, 0, 0);
    return t;
}

function sum(days) {
    return {
        calories: days.reduce((s, d) => s + d.calories, 0),
        protein:  days.reduce((s, d) => s + d.protein,  0),
        carbs:    days.reduce((s, d) => s + d.carbs,    0),
        fat:      days.reduce((s, d) => s + d.fat,      0),
    };
}

function capitalize(s) { return s.charAt(0).toUpperCase() + s.slice(1); }

function weeklyDays() { return allDays.slice(-7); }

function monthlyWeeks() {
    const splits = [[0, 7], [7, 14], [14, 21], [21, 30]];
    return splits.map(([start, end], i) => {
        const chunk  = allDays.slice(start, end);
        const totals = sum(chunk);
        return { label: `W${i + 1}`, ...totals, goal: dailyTargets.calories * chunk.length, days: chunk.length };
    });
}

// ── Date range label ─────────────────────────────────────────────────────────

function updateDateRange(days) {
    if (!days.length) return;
    const start = parseLocalDate(days[0].day);
    const end   = parseLocalDate(days[days.length - 1].day);
    const opts  = { month: 'short', day: 'numeric' };
    const label = `${start.toLocaleDateString('en-US', opts)} – ${end.toLocaleDateString('en-US', opts)}, ${end.getFullYear()}`;
    document.getElementById('report-date-range').textContent = label;
}

// ── Macro cards ──────────────────────────────────────────────────────────────

function updateCard(macro, actual, goal, unit) {
    const pct  = goal > 0 ? Math.min((actual / goal) * 100, 100) : 0;
    const over = actual > goal;
    document.getElementById(`card-${macro}-actual`).textContent = actual.toLocaleString();
    document.getElementById(`card-${macro}-goal`).textContent   = `/ ${goal.toLocaleString()} ${unit}`;
    const bar = document.getElementById(`card-${macro}-bar`);
    bar.style.width           = `${pct}%`;
    bar.style.backgroundColor = over ? '#f87171' : MACRO_COLORS[macro];
}

// ── Bar chart ────────────────────────────────────────────────────────────────

function barColor(checkedCount, calories, dayStr) {
    const dt     = parseLocalDate(dayStr);
    const future = dt > todayMidnight();
    if (future) return 'rgba(160,160,160,0.15)';
    if (checkedCount === 0) return 'rgba(160,160,160,0.2)';
    if (calories > dailyTargets.calories) return '#E24B4A';
    return MACRO_COLORS.calories;
}

function updateBarChart(isWeekly, days) {
    const textColor = getComputedStyle(document.documentElement).getPropertyValue('--text-primary').trim() || '#ccc';
    const gridColor = getComputedStyle(document.documentElement).getPropertyValue('--card-border').trim() || 'rgba(128,128,128,0.2)';

    const labels = days.map(d => {
        const dt = parseLocalDate(d.day);
        return isWeekly
            ? DAY_NAMES[dt.getDay()]
            : `${MONTH_NAMES[dt.getMonth()]} ${dt.getDate()}`;
    });

    const values   = days.map(d => d.calories);
    const bgColors = days.map(d => barColor(d.checkedMealsCount, d.calories, d.day));
    const goals    = days.map(() => dailyTargets.calories);

    if (barChart) barChart.destroy();

    barChart = new Chart(document.getElementById('barChart').getContext('2d'), {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    type: 'bar',
                    label: 'Calories',
                    data: values,
                    backgroundColor: bgColors,
                    borderRadius: 4,
                    order: 2,
                },
                {
                    type: 'line',
                    label: 'Goal',
                    data: goals,
                    borderColor: '#f87171',
                    borderDash: [6, 4],
                    borderWidth: 1.5,
                    pointRadius: 0,
                    fill: false,
                    order: 1,
                },
            ],
        },
        options: {
            responsive: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: ctx => ctx.dataset.type === 'line'
                            ? `Goal: ${ctx.parsed.y} kcal`
                            : `${ctx.parsed.y} kcal`,
                    },
                },
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: { color: gridColor },
                    ticks: { color: textColor, font: { size: 10 } },
                },
                x: {
                    grid: { display: false },
                    ticks: {
                        maxTicksLimit: isWeekly ? 7 : 8,
                        autoSkip: true,
                        maxRotation: 0,
                        color: textColor,
                        font: { size: 10 },
                    },
                },
            },
        },
    });
}

// ── Day chips ────────────────────────────────────────────────────────────────

function dayIndicator(checkedCount, calories, goal) {
    if (checkedCount === 0) {
        return {
            state:   'grey',
            icon:    null,
            color:   '#a0a0a0',
            tooltip: 'No meals logged',
        };
    }
    if (calories > goal) {
        return {
            state:   'red',
            icon:    'ti ti-x',
            color:   '#E24B4A',
            bg:      'rgba(226,75,74,0.1)',
            border:  'rgba(226,75,74,0.3)',
            tooltip: `${Math.round(calories)} / ${goal} kcal — over goal`,
        };
    }
    if (calories < goal * 0.70) {
        return {
            state:   'yellow',
            icon:    'ti ti-x',
            color:   '#D97706',
            bg:      'rgba(217,119,6,0.1)',
            border:  'rgba(217,119,6,0.3)',
            tooltip: `${Math.round(calories)} / ${goal} kcal — below 70% goal`,
        };
    }
    return {
        state:   'green',
        icon:    'ti ti-check',
        color:   '#2EC99A',
        bg:      'rgba(46,201,154,0.1)',
        border:  'rgba(46,201,154,0.3)',
        tooltip: `${Math.round(calories)} / ${goal} kcal — goal met`,
    };
}

function updateDayChips(days) {
    const row = document.getElementById('day-chips-row');
    row.innerHTML = '';
    row.style.display = '';

    days.forEach(d => {
        const dt  = parseLocalDate(d.day);
        const ind = dayIndicator(d.checkedMealsCount, d.calories, dailyTargets.calories);

        const chip = document.createElement('div');
        chip.className = 'day-stat-chip';
        chip.dataset.state = ind.state;
        if (ind.bg)     chip.style.backgroundColor = ind.bg;
        if (ind.border) chip.style.borderColor      = ind.border;

        const lbl = document.createElement('span');
        lbl.className   = 'day-stat-chip-label';
        lbl.textContent = DAY_NAMES[dt.getDay()];
        lbl.style.color = ind.color;
        chip.append(lbl);

        if (ind.icon) {
            const icon = document.createElement('i');
            icon.className   = `day-stat-chip-icon ${ind.icon}`;
            icon.style.color = ind.color;
            chip.append(icon);
        }

        const tip = document.createElement('div');
        tip.className   = 'day-chip-tooltip';
        tip.textContent = ind.tooltip;
        chip.append(tip);

        row.appendChild(chip);
    });
}

// ── Insights ─────────────────────────────────────────────────────────────────

function generateInsights(isWeekly, days, totals) {
    const period = isWeekly ? 'week' : 'month';

    // Insight 1 — goal hit rate
    let i1;
    if (isWeekly) {
        const hits = days.filter(d => {
            const r = dailyTargets.calories > 0 ? d.calories / dailyTargets.calories : 0;
            return r >= 0.9 && r <= 1.1;
        }).length;
        i1 = `You hit your daily calorie goal on <strong>${hits}</strong> / <strong>${days.length}</strong> days this ${period}.`;
    } else {
        const weeks = monthlyWeeks();
        const hits  = weeks.filter(w => {
            const r = w.goal > 0 ? w.calories / w.goal : 0;
            return r >= 0.9 && r <= 1.1;
        }).length;
        i1 = `You hit your weekly calorie goal on <strong>${hits}</strong> / <strong>4</strong> weeks this ${period}.`;
    }

    // Insight 2 — most / least consistent macro
    const macros = ['protein', 'carbs', 'fat'];
    const consistency = macros.map(m => {
        const goal = dailyTargets[m] * days.length;
        return { macro: m, pct: goal > 0 ? (totals[m] / goal) * 100 : 0 };
    }).sort((a, b) => Math.abs(100 - a.pct) - Math.abs(100 - b.pct));

    const best  = consistency[0];
    const worst = consistency[2];
    const i2 = `Most consistent macro: <strong>${capitalize(best.macro)}</strong> (<strong>${best.pct.toFixed(0)}%</strong> of goal). ` +
               `Least consistent: <strong>${capitalize(worst.macro)}</strong> (<strong>${worst.pct.toFixed(0)}%</strong> of goal).`;

    document.getElementById('insight-1').innerHTML = i1;
    document.getElementById('insight-2').innerHTML = i2;
}

// ── Today's Macros sidebar ────────────────────────────────────────────────────

function updateTodaysMacros() {
    const t = todayMidnight();
    const todayStr = `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}-${String(t.getDate()).padStart(2, '0')}`;
    const d = allDays.find(e => e.day === todayStr) || { calories: 0, protein: 0, carbs: 0, fat: 0 };

    const rows = [
        { key: 'calories', actual: d.calories, goal: dailyTargets.calories, unit: ''   },
        { key: 'protein',  actual: d.protein,  goal: dailyTargets.protein,  unit: ' g' },
        { key: 'fat',      actual: d.fat,       goal: dailyTargets.fat,      unit: ' g' },
        { key: 'carbs',    actual: d.carbs,    goal: dailyTargets.carbs,    unit: ' g' },
    ];

    rows.forEach(({ key, actual, goal, unit }) => {
        const pct   = goal > 0 ? Math.min((actual / goal) * 100, 100) : 0;
        const valEl = document.getElementById(`${key}Fraction`);
        const barEl = document.getElementById(`tm-${key}-bar`);
        if (valEl) valEl.textContent = `${Math.round(actual)} / ${goal}`;
        if (barEl) barEl.style.width = `${pct}%`;
    });
}

// ── Render ────────────────────────────────────────────────────────────────────

function render() {
    const isWeekly = activeTab === 'weekly';
    const days     = isWeekly ? weeklyDays() : allDays;
    const totals   = sum(days);
    const numDays  = days.length;
    const goal     = {
        calories: dailyTargets.calories * numDays,
        protein:  dailyTargets.protein  * numDays,
        carbs:    dailyTargets.carbs    * numDays,
        fat:      dailyTargets.fat      * numDays,
    };

    // Segmented control
    ['weekly', 'monthly'].forEach(t => {
        document.getElementById(`tab-${t}`).classList.toggle('active', t === activeTab);
    });

    // Date range
    updateDateRange(days);

    // Macro cards
    updateCard('calories', totals.calories, goal.calories, 'kcal');
    updateCard('protein',  totals.protein,  goal.protein,  'g');
    updateCard('carbs',    totals.carbs,    goal.carbs,    'g');
    updateCard('fat',      totals.fat,      goal.fat,      'g');

    // Bar chart
    updateBarChart(isWeekly, days);

    // Day chips (weekly only)
    const chipsRow = document.getElementById('day-chips-row');
    if (isWeekly) {
        updateDayChips(days);
    } else {
        chipsRow.innerHTML = '';
        chipsRow.style.display = 'none';
    }

    // Today's macros sidebar (always shows today regardless of tab)
    updateTodaysMacros();

    // Insights
    generateInsights(isWeekly, days, totals);
}

// ── Boot ──────────────────────────────────────────────────────────────────────

document.getElementById('tab-weekly').addEventListener('click',  () => { activeTab = 'weekly';  render(); });
document.getElementById('tab-monthly').addEventListener('click', () => { activeTab = 'monthly'; render(); });

render();
