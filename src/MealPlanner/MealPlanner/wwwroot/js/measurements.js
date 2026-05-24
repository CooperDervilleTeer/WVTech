document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.measurement-edit-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const row = document.getElementById('row-' + btn.dataset.id);
            row.querySelector('.measurement-display-name').style.display = 'none';
            row.querySelector('.measurement-display-abbrev').style.display = 'none';
            row.querySelector('.measurement-edit-name').style.display = '';
            row.querySelector('.measurement-edit-abbrev').style.display = '';
            row.querySelector('.measurement-view-actions').style.display = 'none';
            row.querySelector('.measurement-edit-actions').style.display = 'flex';
        });
    });

    document.querySelectorAll('.measurement-cancel-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const row = document.getElementById('row-' + btn.dataset.id);
            row.querySelector('.measurement-display-name').style.display = '';
            row.querySelector('.measurement-display-abbrev').style.display = '';
            row.querySelector('.measurement-edit-name').style.display = 'none';
            row.querySelector('.measurement-edit-abbrev').style.display = 'none';
            row.querySelector('.measurement-view-actions').style.display = '';
            row.querySelector('.measurement-edit-actions').style.display = 'none';
        });
    });

    document.querySelectorAll('.measurement-save-form').forEach(function (form) {
        form.addEventListener('submit', function () {
            const row = form.closest('tr');
            form.querySelector('.measurement-save-name').value =
                row.querySelector('.measurement-edit-name').value;
            form.querySelector('.measurement-save-abbrev').value =
                row.querySelector('.measurement-edit-abbrev').value;
        });
    });
});
