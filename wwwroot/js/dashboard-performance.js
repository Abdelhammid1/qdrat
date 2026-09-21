document.addEventListener("DOMContentLoaded", () => {
    const modal = new bootstrap.Modal(document.getElementById("batchModal"));
    const modalBody = document.getElementById("batchModalBody");

    document.querySelectorAll(".view-batch").forEach(btn => {
        btn.addEventListener("click", async () => {
            const batchId = btn.dataset.batch;
            modalBody.innerHTML = "<div class='text-center p-5 text-muted'>⏳ جاري تحميل التفاصيل...</div>";

            const res = await fetch(`/Admin/PerformanceDashboard/BatchDetails?batchId=${batchId}`);
            const html = await res.text();
            modalBody.innerHTML = html;
            modal.show();
        });
    });
});
