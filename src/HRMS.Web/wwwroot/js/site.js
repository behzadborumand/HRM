// Bulk selection on the employee list: the header checkbox mirrors the rows,
// and the bulk button stays inert until something is checked (only when JS runs).
document.addEventListener("change", (event) => {
    const all = event.target.closest("[data-select-all]");
    if (all) {
        document.querySelectorAll("[data-select-item]").forEach((box) => {
            box.checked = all.checked;
        });
    }

    const rowBoxes = [...document.querySelectorAll("[data-select-item]")];
    if (rowBoxes.length === 0) {
        return;
    }

    const bulkButton = document.querySelector("[data-bulk-submit]");
    if (bulkButton) {
        bulkButton.disabled = !rowBoxes.some((box) => box.checked);
    }

    const header = document.querySelector("[data-select-all]");
    if (header) {
        header.checked = rowBoxes.every((box) => box.checked);
    }
});

document.querySelectorAll("[data-bulk-submit]").forEach((button) => {
    button.disabled = true;
});
