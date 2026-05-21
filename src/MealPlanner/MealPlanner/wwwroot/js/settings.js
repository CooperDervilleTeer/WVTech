document.addEventListener('DOMContentLoaded', function () {

    // ── Section switching ─────────────────────────────────────
    let currentSection = document.body.dataset.activeSection || 'profile';

    function activateSection(section) {
        document.querySelectorAll('.settings-panel').forEach(p => {
            p.style.display = p.id === 'panel-' + section ? '' : 'none';
        });
        document.querySelectorAll('.settings-nav-item[data-section]').forEach(item => {
            item.classList.toggle('active', item.dataset.section === section);
        });
        currentSection = section;
    }

    activateSection(currentSection);

    document.querySelectorAll('.settings-nav-item[data-section]').forEach(item => {
        item.addEventListener('click', () => activateSection(item.dataset.section));
    });

    // ── Dietary restriction chips (auto-save) ────────────────
    const selectedRestrictions = new Set(); // stores integer DietaryRestrictionId values
    let dietarySaveTimer = null;
    let bannerFadeTimer  = null;
    let bannerRemoveTimer = null;

    document.querySelectorAll('.restriction-chip').forEach(chip => {
        const idInput = chip.querySelector('input[name*="DietaryRestrictionId"]');
        const cbInput = chip.querySelector('input[type="checkbox"]');
        const id = idInput ? parseInt(idInput.value, 10) : null;
        if (!id) return;

        if (cbInput && cbInput.checked) selectedRestrictions.add(id);

        chip.addEventListener('click', function (e) {
            e.preventDefault();
            if (selectedRestrictions.has(id)) {
                selectedRestrictions.delete(id);
                this.classList.remove('active');
                if (cbInput) cbInput.checked = false;
            } else {
                selectedRestrictions.add(id);
                this.classList.add('active');
                if (cbInput) cbInput.checked = true;
            }
            clearTimeout(dietarySaveTimer);
            dietarySaveTimer = setTimeout(saveDietary, 400);
        });
    });

    async function saveDietary() {
        const token = document.querySelector('#form-dietary input[name="__RequestVerificationToken"]')?.value ?? '';
        try {
            const res = await fetch('/UserSettings/DietaryAutoSave', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify([...selectedRestrictions])
            });
            if (!res.ok) throw new Error();
            const data = await res.json();
            if (!data.success) throw new Error();
            showDietaryBanner(true);
        } catch {
            showDietaryBanner(false);
        }
    }

    function showDietaryBanner(success) {
        clearTimeout(bannerFadeTimer);
        clearTimeout(bannerRemoveTimer);

        const panel = document.getElementById('panel-dietary');
        panel?.querySelector('.dietary-auto-banner')?.remove();

        const banner = document.createElement('div');
        banner.className = (success ? 'settings-success' : 'settings-error') + ' dietary-auto-banner';
        banner.textContent = success
            ? 'Dietary Restrictions Saved.'
            : 'Failed to save. Please try again.';
        panel?.prepend(banner);

        bannerFadeTimer = setTimeout(() => banner.classList.add('fade-out'), 2100);
        bannerRemoveTimer = setTimeout(() => banner.remove(), 2500);
    }

    // ── Dark mode toggle ──────────────────────────────────────
    const html = document.documentElement;
    const isDark = () => html.getAttribute('data-theme') !== 'light';

    function applyThemeToggle(toggle) {
        if (!toggle) return;
        toggle.classList.toggle('on', isDark());
        toggle.addEventListener('click', function () {
            const willBeDark = !this.classList.contains('on');
            document.querySelectorAll('#themeToggle-panel').forEach(t => {
                t.classList.toggle('on', willBeDark);
            });
            if (willBeDark) {
                html.removeAttribute('data-theme');
            } else {
                html.setAttribute('data-theme', 'light');
            }
            fetch('/UserSettings/ToggleTheme', { method: 'POST' });
        });
    }

    applyThemeToggle(document.getElementById('themeToggle-panel'));

    // ── Food preferences ─────────────────────────────────────
    const pendingTags = new Set();
    const foodForm = document.getElementById('form-food');
    const pendingContainer = document.getElementById('food-pref-pending-container');
    const foodSelect = document.getElementById('food-pref-select');
    const foodInput = document.getElementById('food-pref-custom-input');
    const foodAddBtn = document.getElementById('food-pref-add-btn');

    function addPendingTag(name) {
        name = name.trim();
        if (!name || pendingTags.has(name.toLowerCase())) return;
        pendingTags.add(name.toLowerCase());

        const chip = document.createElement('span');
        chip.className = 'settings-chip active food-pref-pending-pill';
        chip.innerHTML = `
            <svg class="chip-check-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12"/>
            </svg>
            <span>${name}</span>
            <svg class="chip-remove-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>`;

        const hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = 'NewPreferences';
        hidden.value = name;
        hidden.dataset.pendingTag = name.toLowerCase();
        if (foodForm) foodForm.appendChild(hidden);

        chip.addEventListener('click', function () {
            pendingTags.delete(name.toLowerCase());
            const h = foodForm && foodForm.querySelector(`input[data-pending-tag="${CSS.escape(name.toLowerCase())}"]`);
            if (h) h.remove();
            this.remove();
        });

        if (pendingContainer) pendingContainer.appendChild(chip);
        if (foodSelect) foodSelect.value = '';
        if (foodInput) foodInput.value = '';
    }

    if (foodSelect) {
        foodSelect.addEventListener('change', function () {
            if (this.value) addPendingTag(this.value);
        });
    }

    if (foodAddBtn) {
        foodAddBtn.addEventListener('click', function () {
            if (foodInput) addPendingTag(foodInput.value);
        });
    }

    if (foodInput) {
        foodInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); addPendingTag(this.value); }
        });
    }

    // ── Edit Profile (inline) ────────────────────────────────
    const epInline        = document.getElementById('epInline');
    const epOpenBtn       = document.getElementById('openEditProfileInline');
    const epCancelBtn     = document.getElementById('epCancelInline');
    const epSaveBtn       = document.getElementById('epSaveInline');
    const epFileInput     = document.getElementById('epFileInput');
    const epChangePhoto   = document.getElementById('epChangePhoto');
    const epRemovePhoto   = document.getElementById('epRemovePhoto');
    const epFullName      = document.getElementById('epFullName');
    const epUsername      = document.getElementById('epUsername');
    const epAvatarCircle  = document.getElementById('epAvatarCircle');
    const epAvatarInitials = document.getElementById('epAvatarInitials');
    const epAvatarPhoto   = document.getElementById('epAvatarPhoto');

    let epPendingPhoto = null;
    let epRemoveFlag   = false;

    function epComputeInitials(name) {
        if (!name) return '';
        const parts = name.trim().split(/\s+/).filter(Boolean);
        if (!parts.length) return '';
        if (parts.length === 1) return parts[0][0].toUpperCase();
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }

    function epSetAvatarPhoto(src) {
        if (src) {
            epAvatarPhoto.src = src;
            epAvatarPhoto.style.display = '';
            epAvatarCircle.classList.add('has-photo');
            if (epRemovePhoto) epRemovePhoto.style.display = '';
        } else {
            epAvatarPhoto.src = '';
            epAvatarPhoto.style.display = 'none';
            epAvatarCircle.classList.remove('has-photo');
            if (epRemovePhoto) epRemovePhoto.style.display = 'none';
        }
    }

    function epOpen() {
        if (!epInline) return;
        const fullName = epInline.dataset.fullName || '';
        const handle   = epInline.dataset.handle   || '';
        // Read photo from the already-rendered avatar so we never rely on a data attribute
        // that breaks when a base64 URL contains double quotes matching the HTML delimiter.
        const sAvatar  = document.getElementById('settingsAvatar');
        const sPhotoEl = document.getElementById('settingsAvatarPhoto');
        const photoUrl = (sAvatar && sAvatar.classList.contains('has-photo') && sPhotoEl)
            ? sPhotoEl.src : '';

        epFullName.value = fullName;
        epUsername.value = handle;
        epPendingPhoto   = null;
        epRemoveFlag     = false;
        if (epFileInput) epFileInput.value = '';

        epAvatarInitials.textContent = epComputeInitials(fullName) || epInline.dataset.initials || '?';
        epSetAvatarPhoto(photoUrl);

        epInline.classList.add('open');
        if (epOpenBtn) {
            epOpenBtn.textContent = 'Cancel';
            epOpenBtn.classList.add('is-cancel');
        }
        epFullName.focus();
    }

    function epClose() {
        if (!epInline) return;
        epInline.classList.remove('open');
        if (epOpenBtn) {
            epOpenBtn.textContent = 'Edit profile';
            epOpenBtn.classList.remove('is-cancel');
        }
    }

    if (epOpenBtn) {
        epOpenBtn.addEventListener('click', function () {
            epInline && epInline.classList.contains('open') ? epClose() : epOpen();
        });
    }

    if (epCancelBtn) epCancelBtn.addEventListener('click', epClose);

    if (epFullName) {
        epFullName.addEventListener('input', function () {
            if (!epAvatarCircle.classList.contains('has-photo')) {
                epAvatarInitials.textContent = epComputeInitials(this.value) || '?';
            }
        });
    }

    if (epChangePhoto) {
        epChangePhoto.addEventListener('click', function () {
            if (epFileInput) epFileInput.click();
        });
    }

    if (epFileInput) {
        epFileInput.addEventListener('change', function () {
            const file = this.files[0];
            if (!file) return;
            const reader = new FileReader();
            reader.onload = function (e) {
                epPendingPhoto = e.target.result;
                epRemoveFlag   = false;
                epSetAvatarPhoto(epPendingPhoto);
                // Live preview: also update the profile-row avatar
                const sPhoto  = document.getElementById('settingsAvatarPhoto');
                const sAvatar = document.getElementById('settingsAvatar');
                if (sPhoto)  { sPhoto.src = epPendingPhoto; sPhoto.style.display = ''; }
                if (sAvatar) sAvatar.classList.add('has-photo');
            };
            reader.readAsDataURL(file);
        });
    }

    if (epRemovePhoto) {
        epRemovePhoto.addEventListener('click', function () {
            epPendingPhoto = null;
            epRemoveFlag   = true;
            epSetAvatarPhoto(null);
            epAvatarInitials.textContent = epComputeInitials(epFullName.value) || epInline.dataset.initials || '?';
            // Live preview: also revert the profile-row avatar
            const sPhoto    = document.getElementById('settingsAvatarPhoto');
            const sAvatar   = document.getElementById('settingsAvatar');
            const sInitials = document.getElementById('settingsAvatarInitials');
            if (sPhoto)    { sPhoto.src = ''; sPhoto.style.display = 'none'; }
            if (sAvatar)   sAvatar.classList.remove('has-photo');
            if (sInitials) sInitials.textContent = epComputeInitials(epFullName.value) || '?';
        });
    }

    if (epSaveBtn) {
        epSaveBtn.addEventListener('click', async function () {
            epSaveBtn.disabled = true;
            const token = epInline
                ? (epInline.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '')
                : '';
            try {
                const res = await fetch('/UserSettings/UpdateProfile', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        FullName:      epFullName.value,
                        DisplayHandle: epUsername.value,
                        PhotoData:     epPendingPhoto,
                        RemovePhoto:   epRemoveFlag
                    })
                });

                if (!res.ok) throw new Error('Server error');
                const data = await res.json();
                if (!data.success) throw new Error('Update failed');

                epInline.dataset.fullName = data.fullName ?? '';
                epInline.dataset.handle   = data.handle ?? '';
                epInline.dataset.initials = data.initials;

                const sAvatar   = document.getElementById('settingsAvatar');
                const sInitials = document.getElementById('settingsAvatarInitials');
                const sPhoto    = document.getElementById('settingsAvatarPhoto');
                const sName     = document.getElementById('settingsProfileName');

                if (sInitials) sInitials.textContent = data.initials;
                if (sName)     sName.textContent     = data.displayName;
                if (data.photoUrl) {
                    if (sPhoto)  { sPhoto.src = data.photoUrl; sPhoto.style.display = ''; }
                    if (sAvatar) sAvatar.classList.add('has-photo');
                } else {
                    if (sPhoto)  { sPhoto.src = ''; sPhoto.style.display = 'none'; }
                    if (sAvatar) sAvatar.classList.remove('has-photo');
                }

                const navAvatar   = document.getElementById('navAvatar');
                const navInitials = document.getElementById('navAvatarInitials');
                const navPhoto    = document.getElementById('navAvatarPhoto');

                if (navInitials) navInitials.textContent = data.initials;
                if (data.photoUrl) {
                    if (navPhoto)  { navPhoto.src = data.photoUrl; navPhoto.style.display = ''; }
                    if (navAvatar) navAvatar.classList.add('has-photo');
                } else {
                    if (navPhoto)  { navPhoto.src = ''; navPhoto.style.display = 'none'; }
                    if (navAvatar) navAvatar.classList.remove('has-photo');
                }

                epClose();
            } catch (err) {
                console.error('Profile update failed', err);
            } finally {
                epSaveBtn.disabled = false;
            }
        });
    }
});
