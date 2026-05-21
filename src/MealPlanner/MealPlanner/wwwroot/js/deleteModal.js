// showDeleteModal(title, onConfirm)
// Presents the global #deleteConfirmModal and calls onConfirm() if the user clicks Delete.
function showDeleteModal(title, onConfirm) {
    const modal    = document.getElementById('deleteConfirmModal');
    const titleEl  = document.getElementById('dcmTitle');
    const confirmBtn = document.getElementById('dcmConfirm');
    const cancelBtn  = document.getElementById('dcmCancel');
    if (!modal) return;

    if (titleEl) titleEl.textContent = title;
    modal.style.display = 'flex';

    function close() {
        modal.style.display = 'none';
        confirmBtn.removeEventListener('click', onYes);
        cancelBtn.removeEventListener('click', close);
        modal.removeEventListener('click', onBackdrop);
    }

    function onYes() {
        close();
        onConfirm();
    }

    function onBackdrop(e) {
        if (e.target === modal) close();
    }

    confirmBtn.addEventListener('click', onYes);
    cancelBtn.addEventListener('click', close);
    // Defer by one tick so the mouseup from the triggering click doesn't
    // immediately fire onBackdrop and close the modal before the user sees it.
    setTimeout(function() {
        modal.addEventListener('click', onBackdrop);
    }, 0);
}
