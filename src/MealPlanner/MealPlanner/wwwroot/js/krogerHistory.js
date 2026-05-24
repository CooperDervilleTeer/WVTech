const modal = document.getElementById("exportHistoryModal");
const body = document.getElementById("exportHistoryBody");

function cloneTemplate(id) {
  return document.getElementById(id).content.cloneNode(true);
}

function formatTime(isoString) {
  if (!isoString.endsWith("Z") && !isoString.includes("+")) isoString += "Z";
  const d = new Date(isoString);
  return (
    d.toLocaleDateString(undefined, { month: "short", day: "numeric" }) +
    " · " +
    d.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" })
  );
}

async function loadHistory() {
  body.innerHTML =
    '<p class="text-center text-muted fst-italic">Loading...</p>';

  let exports = [];
  try {
    const res = await fetch("/Kroger/ExportHistory");
    if (!res.ok) throw new Error(res.status);
    exports = await res.json();
  } catch {
    body.innerHTML =
      '<p class="text-center text-danger">Could not load export history.</p>';
    return;
  }

  body.innerHTML = "";

  if (exports.length === 0) {
    body.innerHTML =
      '<p class="text-center text-muted fst-italic">No previous exports</p>';
    return;
  }

  exports.forEach((exp) => {
    const fragment = cloneTemplate("tmpl-export-entry");
    const entry = fragment.querySelector(".export-history-entry");

    entry.querySelector(".js-entry-label").textContent =
      exp.itemCount === 1 ? "1 item" : `${exp.itemCount} items`;
    entry.querySelector(".js-entry-time").textContent = formatTime(
      exp.exportedAt,
    );
    entry.querySelector(".export-item-list").style.display = "none";

    entry.addEventListener("click", () => toggleEntry(entry, exp.id));
    body.appendChild(fragment);
  });
}

async function toggleEntry(entry, exportId) {
  const list = entry.querySelector(".export-item-list");
  const chevron = entry.querySelector(".export-history-chevron");
  const isOpen = list.style.display === "block";

  if (isOpen) {
    list.style.display = "none";
    chevron.textContent = "▼";
    return;
  }

  chevron.textContent = "▲";

  if (list.dataset.loaded) {
    list.style.display = "block";
    return;
  }

  list.innerHTML =
    '<span class="fst-italic export-history-time">Loading items...</span>';
  list.style.display = "block";

  let detail;
  try {
    const res = await fetch(`/Kroger/ExportDetail?id=${exportId}`);
    if (!res.ok) throw new Error(res.status);
    detail = await res.json();
  } catch {
    list.innerHTML =
      '<span class="text-danger export-history-time">Could not load items.</span>';
    return;
  }

  list.dataset.loaded = "true";
  list.innerHTML = "";

  list.appendChild(cloneTemplate("tmpl-export-controls"));

  detail.items.forEach((item) => {
    const fragment = cloneTemplate("tmpl-export-item");
    const checkbox = fragment.querySelector(".re-export-checkbox");
    const label = fragment.querySelector(".js-item-label");

    checkbox.dataset.name = item.name;
    checkbox.dataset.amount = item.amount;
    checkbox.dataset.measurement = item.measurement;
    label.textContent =
      item.amount > 0
        ? `${item.amount} ${item.measurement} · ${item.name}`
        : item.name;

    checkbox.addEventListener("click", (e) => e.stopPropagation());
    list.appendChild(fragment);
  });

  list.appendChild(cloneTemplate("tmpl-export-footer"));

  list.querySelector(".select-all-btn").addEventListener("click", (e) => {
    e.stopPropagation();
    list
      .querySelectorAll(".re-export-checkbox")
      .forEach((cb) => (cb.checked = true));
  });
  list.querySelector(".deselect-all-btn").addEventListener("click", (e) => {
    e.stopPropagation();
    list
      .querySelectorAll(".re-export-checkbox")
      .forEach((cb) => (cb.checked = false));
  });

  list
    .querySelector(".add-to-list-btn")
    .addEventListener("click", async (e) => {
      e.stopPropagation();
      const btn = e.currentTarget;
      const status = list.querySelector(".add-to-list-status");

      const selected = Array.from(
        list.querySelectorAll(".re-export-checkbox:checked"),
      ).map((cb) => ({
        name: cb.dataset.name,
        amount: parseFloat(cb.dataset.amount) || 0,
        measurement: cb.dataset.measurement,
      }));

      if (selected.length === 0) {
        status.textContent = "Select at least one item.";
        return;
      }

      btn.disabled = true;
      btn.textContent = "Adding...";

      try {
        const res = await fetch("/Shopping/AddItemsBatch", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(selected.map(i => ({
            name: i.name,
            amount: i.amount,
            measurement: i.measurement,
          }))),
        });
        if (!res.ok) throw new Error(res.status);
        modal.style.display = "none";
        window.location.reload();
      } catch {
        status.textContent = "Something went wrong. Please try again.";
        btn.textContent = "Add to Shopping List";
        btn.disabled = false;
      }
    });
}

document.getElementById("previousExportsBtn").addEventListener("click", () => {
  modal.style.display = "flex";
  loadHistory();
});

document.getElementById("closeExportHistory").addEventListener("click", () => {
  modal.style.display = "none";
});

modal.addEventListener("click", (e) => {
  if (e.target === modal) modal.style.display = "none";
});
