// ── Ingredients ──────────────────────────────────────────────────────────────

// TODO: measurements should eventually be retrieved from the DB rather than hardcoded
const UNIT_OPTIONS = [
    { value: 'Unit',     label: 'Unit' },
    { value: 'Count',    label: 'Count' },
    { value: 'Cup(s)',   label: 'Cup(s)' },
    { value: 'Ounce(s)', label: 'Ounce(s)' },
    { value: 'Pound(s)', label: 'Pounds' },
    { value: 'L',        label: 'L' },
    { value: 'KG',       label: 'KG' },
];

let _editingRow = null;

function buildEditBtn(row) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ar-ing-edit-btn';
    btn.title = 'Edit';
    btn.innerHTML = '<i class="ti ti-pencil"></i>';
    btn.addEventListener('click', () => enterEditMode(row));
    return btn;
}

function enterEditMode(row) {
    if (_editingRow && _editingRow !== row) confirmEdit(_editingRow);

    _editingRow = row;
    row.classList.add('ar-ing-row--editing');

    const qty  = row.querySelector('[name="IngredientAmounts"]').value;
    const unit = row.querySelector('[name="IngredientMeasurements"]').value;
    const name = row.querySelector('[name="Ingredients"]').value;

    // Build inline edit fields
    const fields = document.createElement('div');
    fields.className = 'ar-ing-edit-fields';

    const qtyInput = document.createElement('input');
    qtyInput.type = 'number'; qtyInput.min = '0'; qtyInput.step = 'any';
    qtyInput.className = 'ar-input ar-ing-edit-qty';
    qtyInput.value = qty; qtyInput.placeholder = '0';

    const unitSel = document.createElement('select');
    unitSel.className = 'ar-input ar-ing-edit-unit';
    UNIT_OPTIONS.forEach(({ value, label, disabled }) => {
        const o = document.createElement('option');
        o.value = value; o.textContent = label;
        if (disabled) o.disabled = true;
        if (value === unit) o.selected = true;
        unitSel.appendChild(o);
    });

    const nameInput = document.createElement('input');
    nameInput.type = 'text';
    nameInput.className = 'ar-input ar-ing-edit-name';
    nameInput.value = name; nameInput.placeholder = 'Ingredient name';

    fields.append(qtyInput, unitSel, nameInput);

    // Confirm / cancel buttons
    const confirmBtn = document.createElement('button');
    confirmBtn.type = 'button'; confirmBtn.className = 'ar-ing-confirm-btn'; confirmBtn.title = 'Confirm';
    confirmBtn.innerHTML = '<i class="ti ti-check"></i>';
    confirmBtn.addEventListener('click', () => confirmEdit(row));

    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button'; cancelBtn.className = 'ar-ing-cancel-btn'; cancelBtn.title = 'Cancel';
    cancelBtn.innerHTML = '<i class="ti ti-x"></i>';
    cancelBtn.addEventListener('click', () => cancelEdit(row));

    const rowRight = row.querySelector('.ar-ing-row-right');
    rowRight.append(confirmBtn, cancelBtn);
    row.insertBefore(fields, rowRight);

    // Keyboard: Enter confirms, Escape cancels
    [qtyInput, unitSel, nameInput].forEach(el => {
        el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter')  { e.preventDefault(); confirmEdit(row); }
            if (e.key === 'Escape') { e.preventDefault(); cancelEdit(row); }
        });
    });

    nameInput.focus();
}

function confirmEdit(row) {
    const fields = row.querySelector('.ar-ing-edit-fields');
    if (!fields) return;

    const qty  = row.querySelector('.ar-ing-edit-qty').value.trim();
    const unit = row.querySelector('.ar-ing-edit-unit').value;
    const name = row.querySelector('.ar-ing-edit-name').value.trim();

    if (!name) { cancelEdit(row); return; }

    row.querySelector('[name="IngredientAmounts"]').value    = qty || '0';
    row.querySelector('[name="IngredientMeasurements"]').value = unit;
    row.querySelector('[name="Ingredients"]').value          = name;

    const parts = [];
    if (qty)  parts.push(qty);
    if (unit) parts.push(unit);
    parts.push('—');
    parts.push(name);
    row.querySelector('.ar-ing-row-name').textContent = qty || unit ? parts.join(' ') : name;

    exitEditMode(row);
}

function cancelEdit(row) {
    exitEditMode(row);
}

function exitEditMode(row) {
    row.classList.remove('ar-ing-row--editing');
    row.querySelector('.ar-ing-edit-fields')?.remove();
    row.querySelector('.ar-ing-confirm-btn')?.remove();
    row.querySelector('.ar-ing-cancel-btn')?.remove();
    _editingRow = null;
}

document.addEventListener('DOMContentLoaded', function () {
    const addButton = document.querySelector('#buttonAppend');
    if (!addButton) return;

    addButton.addEventListener('click', function (e) {
        e.preventDefault();
        createInput();
    });

    // Prevent form submission when any ingredient row has a blank name
    const recipeForm = addButton.closest('form');
    if (recipeForm) {
        recipeForm.addEventListener('submit', function (e) {
            const ingredientInputs = recipeForm.querySelectorAll('input[name="Ingredients"]');
            for (const input of ingredientInputs) {
                if (!input.value.trim()) {
                    e.preventDefault();
                    return;
                }
            }
        });
    }

    // Delegated remove for ar-ing-row items (new-style, including pre-rendered EditRecipe rows)
    const ingredientList = document.getElementById('ingredientList');
    if (ingredientList) {
        ingredientList.addEventListener('click', function (e) {
            const removeBtn = e.target.closest('.ar-ing-remove-btn');
            if (removeBtn) removeBtn.closest('.ar-ing-row').remove();
        });

        // Inject edit buttons into server-rendered rows (EditRecipe page)
        ingredientList.querySelectorAll('.ar-ing-row').forEach(row => {
            const rowRight = row.querySelector('.ar-ing-row-right');
            if (rowRight && !rowRight.querySelector('.ar-ing-edit-btn')) {
                rowRight.insertBefore(buildEditBtn(row), rowRight.firstChild);
            }
        });
    }

    // Legacy delegated delete for old-style rows
    const container = document.querySelector('#AppendHere');
    if (container) {
        container.addEventListener('click', function (e) {
            const deleteButton = e.target.closest('.deleteButton');
            if (deleteButton) {
                deleteButton.closest('.input-wrapper').remove();
            }
        });
    }
});

function getMeasurements() {
    const container = document.querySelector('#AppendHere');
    try {
        return JSON.parse(container?.dataset?.measurements || '[]');
    } catch {
        return [];
    }
}

function buildMeasurementOptions() {
    const measurements = getMeasurements();
    const options = ['<option value="">Select</option>'];
    for (const m of measurements) {
        options.push(`<option value="${m.value}">${m.label}</option>`);
    }
    return options.join('');
}

function createInput() {
    const ingQty  = document.getElementById('ingQty');
    const ingUnit = document.getElementById('ingUnit');
    const ingName = document.getElementById('ingName');
    const ingredientList = document.getElementById('ingredientList');

    if (ingName && ingredientList) {
        // New-style staging row (AddNewRecipe)
        const qty  = ingQty  ? ingQty.value.trim()  : '';
        const unit = ingUnit ? ingUnit.value : '';
        const name = ingName.value.trim();

        const parts = [];
        if (qty)  parts.push(qty);
        if (unit) parts.push(unit);
        parts.push('—');
        parts.push(name);
        const display = qty || unit ? parts.join(' ') : name;

        const row = document.createElement('div');
        row.className = 'ar-ing-row';
        row.innerHTML = `
            <span class="ar-ing-row-name">${escapeHtml(display)}</span>
            <div class="ar-ing-row-right">
                <input type="hidden" name="IngredientAmounts" value="${escapeHtml(qty || '0')}">
                <input type="hidden" name="IngredientMeasurements" value="${escapeHtml(unit)}">
                <input type="hidden" name="Ingredients" value="${escapeHtml(name)}">
                <button type="button" class="ar-ing-remove-btn" title="Remove">
                    <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2.5"
                         stroke-linecap="round" stroke-linejoin="round">
                        <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                    </svg>
                </button>
            </div>`;

        // Wire remove and add the edit button
        row.querySelector('.ar-ing-remove-btn').addEventListener('click', () => row.remove());
        const rowRight = row.querySelector('.ar-ing-row-right');
        rowRight.insertBefore(buildEditBtn(row), rowRight.firstChild);
        ingredientList.appendChild(row);

        if (ingQty)  ingQty.value  = '';
        if (ingUnit) ingUnit.value = '';
        ingName.value = '';
        ingName.focus();
    } else {
        // Legacy: EditRecipe's old row structure
        const container = document.querySelector('#AppendHere');
        if (!container) return;

        const inputWrapper = document.createElement('div');
        inputWrapper.className = 'row input-wrapper';
        inputWrapper.innerHTML = `
            <div class="row">
                <input type="text" class="col-1 back2-textbox-partial mx-1" name="IngredientAmounts" placeholder="e.g. 1/2">
                <select class="col-2 back2-textbox-partial mx-1" name="IngredientMeasurements">
                    ${buildMeasurementOptions()}
                </select>
                <input type="text" class="col back2-textbox-partial mx-1" placeholder="Enter Ingredient" name="Ingredients" required>
            </div>`;

        const deleteButton = document.createElement('button');
        deleteButton.type = 'button';
        deleteButton.className = 'deleteButton';
        const deleteImg = document.createElement('img');
        deleteImg.src = '/images/icons/delete.png';
        deleteImg.alt = 'delete';
        deleteImg.className = 'deleteImage';
        deleteButton.appendChild(deleteImg);
        inputWrapper.appendChild(deleteButton);
        container.appendChild(inputWrapper);
    }
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// ── Step-by-step instructions ─────────────────────────────────────────────────

function parseDirections(raw) {
    if (!raw || !raw.trim()) return [];
    return raw.split('\n')
        .map(l => l.trim())
        .filter(Boolean)
        .map(l => { const m = l.match(/^\d+\.\s+(.+)$/); return m ? m[1] : l; });
}

function serializeSteps() {
    return Array.from(document.querySelectorAll('#step-list .ar-step-row'))
        .map((row, i) => {
            const span  = row.querySelector('.ar-step-text');
            const input = row.querySelector('.ar-step-inline-input');
            const text  = span ? span.textContent.trim() : (input ? input.value.trim() : '');
            return `${i + 1}. ${text}`;
        })
        .join('\n');
}

function renumberSteps() {
    document.querySelectorAll('#step-list .ar-step-row').forEach((row, i) => {
        row.querySelector('.ar-step-num').textContent = `${i + 1}.`;
    });
}

function wireStepClick(row, span) {
    span.addEventListener('click', () => enterStepEditMode(row, span));
}

function enterStepEditMode(row, span) {
    if (row.querySelector('.ar-step-inline-input')) return;

    const original  = span.textContent.trim();
    let   committed = false;

    const input = document.createElement('input');
    input.type      = 'text';
    input.className = 'ar-step-inline-input';
    input.value     = original;
    row.replaceChild(input, span);
    input.focus();
    input.select();

    function commit(text) {
        if (committed) return;
        committed = true;
        const newSpan = document.createElement('span');
        newSpan.className   = 'ar-step-text';
        newSpan.textContent = text || original;
        wireStepClick(row, newSpan);
        row.replaceChild(newSpan, input);
    }

    input.addEventListener('keydown', e => {
        if (e.key === 'Enter')  { e.preventDefault(); commit(input.value.trim()); }
        if (e.key === 'Escape') { commit(original); }
    });
    input.addEventListener('blur', () => commit(input.value.trim()));
}

function buildStepRow(text) {
    const row = document.createElement('div');
    row.className = 'ar-step-row';

    const num = document.createElement('span');
    num.className = 'ar-step-num';

    const span = document.createElement('span');
    span.className   = 'ar-step-text';
    span.textContent = text;
    wireStepClick(row, span);

    const removeBtn = document.createElement('button');
    removeBtn.type = 'button';
    removeBtn.className = 'ar-step-remove-btn';
    removeBtn.title = 'Remove step';
    removeBtn.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24"
        fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>`;
    removeBtn.addEventListener('click', () => { row.remove(); renumberSteps(); });

    row.append(num, span, removeBtn);
    return row;
}

function addStep(text) {
    const trimmed = text.trim();
    if (!trimmed) return;
    const list = document.getElementById('step-list');
    if (!list) return;
    list.appendChild(buildStepRow(trimmed));
    renumberSteps();
}

document.addEventListener('DOMContentLoaded', function () {
    const stepList      = document.getElementById('step-list');
    const stepInput     = document.getElementById('step-input');
    const addStepBtn    = document.getElementById('add-step-btn');
    const dirField      = document.getElementById('Directions');
    const validationMsg = document.getElementById('step-validation-msg');

    if (!stepList || !stepInput) return;

    // Populate from saved directions on Edit Recipe
    if (dirField && dirField.value.trim()) {
        parseDirections(dirField.value).forEach(text => addStep(text));
    }

    if (addStepBtn) {
        addStepBtn.addEventListener('click', function () {
            addStep(stepInput.value);
            stepInput.value = '';
            stepInput.focus();
            if (validationMsg) validationMsg.style.display = 'none';
        });
    }

    stepInput.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        addStep(stepInput.value);
        stepInput.value = '';
        if (validationMsg) validationMsg.style.display = 'none';
    });

    const form = stepInput.closest('form');
    if (form) {
        form.addEventListener('submit', function (e) {
            const steps = stepList.querySelectorAll('.ar-step-text');
            if (steps.length === 0) {
                e.preventDefault();
                if (validationMsg) validationMsg.style.display = '';
                return;
            }
            if (dirField) dirField.value = serializeSteps();
        });
    }
});

// ── Image upload ──────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', function () {
    const fileInput    = document.getElementById('imageFile');
    const preview      = document.getElementById('imagePreview');
    const uploadZone   = document.getElementById('uploadZone');
    const filenamePill = document.getElementById('imageFilename');
    // Legacy selector for EditRecipe
    const fileNameDisplay = document.querySelector('.file-name-display');
    const removeBtn    = document.getElementById('removeImageBtn');
    const currentImage = document.querySelector('img[alt="Current recipe image"]');
    const removeHidden = document.getElementById('removeImageHidden');

    function setFile(file) {
        if (!file) return;
        if (filenamePill) {
            filenamePill.textContent = file.name + '  ×';
            filenamePill.style.display = 'inline-flex';
        }
        if (fileNameDisplay) fileNameDisplay.textContent = file.name;
        if (preview) {
            preview.src = URL.createObjectURL(file);
            preview.style.display = 'block';
        }
        if (currentImage) currentImage.style.display = 'none';
        if (removeHidden) removeHidden.value = 'false';
        if (removeBtn) removeBtn.style.display = '';
    }

    function clearFile() {
        if (fileInput) fileInput.value = '';
        if (filenamePill) { filenamePill.textContent = ''; filenamePill.style.display = 'none'; }
        if (fileNameDisplay) fileNameDisplay.textContent = 'No file chosen';
        if (preview) { preview.style.display = 'none'; preview.src = ''; }
        if (currentImage) {
            currentImage.style.display = 'none';
            if (removeHidden) removeHidden.value = 'true';
        }
        if (removeBtn) removeBtn.style.display = 'none';
        // New-style EditRecipe: hide the current image wrapper, reveal the upload zone
        const currentImgWrap = document.getElementById('currentImgWrap');
        if (currentImgWrap) currentImgWrap.style.display = 'none';
        if (uploadZone) uploadZone.style.display = '';
    }

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            if (fileInput.files && fileInput.files[0]) {
                setFile(fileInput.files[0]);
            } else {
                clearFile();
            }
        });
    }

    // Click the filename pill to remove the chosen file
    if (filenamePill) {
        filenamePill.addEventListener('click', clearFile);
    }

    // Legacy remove button (EditRecipe)
    if (removeBtn) {
        removeBtn.addEventListener('click', clearFile);
    }

    // Drag-and-drop on upload zone
    if (uploadZone && fileInput) {
        uploadZone.addEventListener('dragover', function (e) {
            e.preventDefault();
            uploadZone.classList.add('drag-over');
        });
        uploadZone.addEventListener('dragleave', function () {
            uploadZone.classList.remove('drag-over');
        });
        uploadZone.addEventListener('drop', function (e) {
            e.preventDefault();
            uploadZone.classList.remove('drag-over');
            const files = e.dataTransfer.files;
            if (files && files[0]) {
                const dt = new DataTransfer();
                dt.items.add(files[0]);
                fileInput.files = dt.files;
                setFile(files[0]);
            }
        });
    }
});

// ── Tag management ────────────────────────────────────────────────────────────

function addTag(tagName, fromSelect) {
    const container = document.getElementById('tags-container');
    const select    = document.getElementById('tag-select');
    if (!tagName || !tagName.trim()) return;

    const trimmed = tagName.trim();

    // Prevent duplicates (case-insensitive)
    for (const inp of document.querySelectorAll('input[name="Tags"]')) {
        if (inp.value.toLowerCase() === trimmed.toLowerCase()) {
            if (fromSelect) select.value = '';
            return;
        }
    }

    const isNewStyle = !!document.getElementById('ingredientList');

    const wrapper = document.createElement('span');
    wrapper.className = 'tag-wrapper';

    const hiddenInput = document.createElement('input');
    hiddenInput.type  = 'hidden';
    hiddenInput.name  = 'Tags';
    hiddenInput.value = trimmed;

    const pill = document.createElement('button');
    pill.type = 'button';
    pill.title = `Remove ${trimmed}`;
    if (fromSelect) pill.dataset.predefined = 'true';

    if (isNewStyle) {
        pill.className = 'tag-pill ar-tag-chip';
        pill.innerHTML = `${escapeHtml(trimmed)} <span class="ar-tag-x">×</span>`;
    } else {
        pill.className = 'tag-pill badge rounded-pill recipe-tag recipe-tag-removable';
        pill.textContent = trimmed;
    }

    pill.addEventListener('click', function () {
        if (pill.dataset.predefined === 'true' && select) {
            const opt = document.createElement('option');
            opt.value = trimmed;
            opt.textContent = trimmed;
            const opts = Array.from(select.options).slice(1);
            const insertBefore = opts.find(o => o.value.toLowerCase() > trimmed.toLowerCase());
            if (insertBefore) select.insertBefore(opt, insertBefore);
            else select.appendChild(opt);
        }
        wrapper.remove();
    });

    wrapper.appendChild(pill);
    wrapper.appendChild(hiddenInput);
    container.appendChild(wrapper);

    if (fromSelect) {
        const chosen = Array.from(select.options).find(o => o.value === trimmed);
        if (chosen) chosen.remove();
        select.value = '';
    }
}

document.addEventListener('DOMContentLoaded', function () {
    const select        = document.getElementById('tag-select');
    const customTagInput = document.getElementById('custom-tag-input');
    const addTagBtn     = document.getElementById('add-custom-tag-btn');

    if (select) {
        select.addEventListener('change', function () {
            if (select.value) addTag(select.value, true);
        });
    }

    if (addTagBtn && customTagInput) {
        addTagBtn.addEventListener('click', function () {
            addTag(customTagInput.value, false);
            customTagInput.value = '';
        });
        customTagInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                addTag(customTagInput.value, false);
                customTagInput.value = '';
            }
        });
    }

    // Wire remove handler for tags pre-rendered server-side
    document.querySelectorAll('.tag-wrapper').forEach(function (wrapper) {
        const pill = wrapper.querySelector('.tag-pill');
        const hiddenInput = wrapper.querySelector('input[name="Tags"]');
        if (!pill || !hiddenInput) return;
        const tagName = hiddenInput.value;
        pill.addEventListener('click', function () {
            if (pill.dataset.predefined === 'true' && select) {
                const opt = document.createElement('option');
                opt.value = tagName;
                opt.textContent = tagName;
                const opts = Array.from(select.options).slice(1);
                const insertBefore = opts.find(o => o.value.toLowerCase() > tagName.toLowerCase());
                if (insertBefore) select.insertBefore(opt, insertBefore);
                else select.appendChild(opt);
            }
            wrapper.remove();
        });
    });
});
