// PPL-S4: سلوك مشترك لصفحات اختبارات تحديد المستوى في منطقة الشريك
(function () {
    function ask(message, confirmText) {
        if (window.Swal) {
            return Swal.fire({
                text: message,
                icon: "warning",
                showCancelButton: true,
                confirmButtonText: confirmText || "تأكيد",
                cancelButtonText: "إلغاء",
                confirmButtonColor: "#9a4a26"
            }).then(function (result) { return result.isConfirmed; });
        }

        return Promise.resolve(window.confirm(message));
    }

    function setBusy(button) {
        if (!button) {
            return;
        }

        button.disabled = true;
        button.innerHTML = '<i class="fa fa-spinner fa-spin" aria-hidden="true"></i> جارٍ التنفيذ...';
    }

    // نموذج بتأكيد: data-confirm على الـ form
    document.querySelectorAll("form[data-confirm]").forEach(function (form) {
        form.addEventListener("submit", function (event) {
            if (form.dataset.confirmed === "1") {
                return;
            }

            event.preventDefault();
            var button = form.querySelector('button[type="submit"]');

            ask(form.dataset.confirm, form.dataset.confirmText).then(function (confirmed) {
                if (!confirmed) {
                    return;
                }

                form.dataset.confirmed = "1";
                setBusy(button);
                form.submit();
            });
        });
    });

    // نموذج بحالة تحميل عند الإرسال: data-busy على الـ form
    document.querySelectorAll("form[data-busy]").forEach(function (form) {
        form.addEventListener("submit", function () {
            setBusy(form.querySelector('button[type="submit"]'));
        });
    });

    // بحث داخل الصفحة: data-search-target = محدد العناصر، وكل عنصر يحمل data-search
    document.querySelectorAll("input[data-search-target]").forEach(function (input) {
        var items = document.querySelectorAll(input.dataset.searchTarget);

        input.addEventListener("input", function () {
            var value = input.value.trim().toLowerCase();

            items.forEach(function (item) {
                var text = (item.dataset.search || "").toLowerCase();
                item.style.display = text.indexOf(value) !== -1 ? "" : "none";
            });
        });
    });

    // معاينة السؤال: زر يحمل data-preview-url داخل صفحة بها #questionPreviewModal
    var modalElement = document.getElementById("questionPreviewModal");
    if (modalElement) {
        var body = modalElement.querySelector(".modal-body");

        document.querySelectorAll("[data-preview-url]").forEach(function (button) {
            button.addEventListener("click", function () {
                body.innerHTML = '<div class="pp-state"><i class="fa fa-spinner fa-spin" aria-hidden="true"></i> جارٍ تحميل السؤال...</div>';
                bootstrap.Modal.getOrCreateInstance(modalElement).show();

                fetch(button.dataset.previewUrl, { headers: { "Accept": "text/html" } })
                    .then(function (response) {
                        if (!response.ok) {
                            throw new Error("preview failed");
                        }

                        return response.text();
                    })
                    .then(function (html) { body.innerHTML = html; })
                    .catch(function () {
                        body.innerHTML = '<div class="pp-state pp-state-error">تعذّر تحميل السؤال، حاول مرة أخرى.</div>';
                    });
            });
        });
    }
})();
