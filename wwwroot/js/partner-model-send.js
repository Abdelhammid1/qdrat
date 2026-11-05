// PPM-S4.1 وPPM-S5.2: صفحة إرسال واجب أو اختبار من نموذج في منطقة الشريك. الخادم يعيد التحقق من كل شيء عند الإرسال.
(function () {
    var form = document.getElementById("modelSendForm");
    if (!form) {
        return;
    }

    var modalElement = document.getElementById("replacementModal");
    var modalBody = modalElement.querySelector(".modal-body");
    var sendButton = document.getElementById("sendButton");
    var confirmBox = document.getElementById("confirmWarnings");
    var stale = document.getElementById("stalePreview");
    var batchBoxes = Array.prototype.slice.call(form.querySelectorAll('input[name="BatchIds"]:not([disabled])'));
    var toggleBatches = document.getElementById("toggleBatches");
    var isStale = false;
    var activeQuestion = null;
    var candidateRequest = 0;

    function toIndic(value) {
        return String(value).replace(/\d/g, function (digit) { return "٠١٢٣٤٥٦٧٨٩"[digit]; });
    }

    function updateSendButton() {
        if (sendButton) {
            sendButton.disabled = isStale || (confirmBox !== null && !confirmBox.checked);
        }
    }

    function markStale() {
        if (!sendButton || isStale) {
            return;
        }

        isStale = true;
        if (stale) {
            stale.classList.add("is-visible");
        }
        updateSendButton();
    }

    function updateCounts() {
        var selected = batchBoxes.filter(function (box) { return box.checked; }).length;
        document.getElementById("selectedBatchCount").textContent = toIndic(selected);
        toggleBatches.textContent = batchBoxes.length > 0 && selected === batchBoxes.length ? "إلغاء التحديد" : "تحديد الكل";

        var kept = form.querySelectorAll('.pms-question:not(.is-excluded)').length;
        document.getElementById("finalQuestionCount").textContent = toIndic(kept);
    }

    form.addEventListener("change", function (event) {
        var target = event.target;

        if (target === confirmBox) {
            updateSendButton();
            return;
        }

        if (target.name === "ExcludedQuestionIds") {
            target.closest(".pms-question").classList.toggle("is-excluded", target.checked);
        }

        updateCounts();
        markStale();
    });

    form.addEventListener("input", function (event) {
        if (event.target.classList.contains("form-control")) {
            markStale();
        }
    });

    toggleBatches.addEventListener("click", function () {
        var selectAll = batchBoxes.some(function (box) { return !box.checked; });
        batchBoxes.forEach(function (box) { box.checked = selectAll; });
        updateCounts();
        markStale();
    });

    // التعطيل بعد بناء بيانات النموذج، وإلا سقطت قيمة Intent من زر معطل
    form.addEventListener("submit", function (event) {
        var button = event.submitter;
        window.setTimeout(function () {
            form.querySelectorAll('button[type="submit"]').forEach(function (item) { item.disabled = true; });
            if (button) {
                button.innerHTML = '<i class="fa fa-spinner fa-spin" aria-hidden="true"></i> جارٍ التنفيذ...';
            }
        }, 0);
    });

    // ---------- الاستبدال ----------
    function setModalState(cssClass, message) {
        modalBody.innerHTML = "";
        var box = document.createElement("div");
        box.className = "pp-state " + cssClass;
        box.textContent = message;
        modalBody.appendChild(box);
    }

    function chosenIds() {
        return Array.prototype.map.call(form.querySelectorAll('[data-role="substitute-id"]'), function (input) {
            return input.value.toLowerCase();
        });
    }

    function openCandidates(question) {
        activeQuestion = question;
        var current = ++candidateRequest;

        setModalState("", "جارٍ تحميل البدائل...");
        bootstrap.Modal.getOrCreateInstance(modalElement).show();

        var url = form.dataset.candidatesUrl + "?questionId=" + encodeURIComponent(question.dataset.question);

        fetch(url, { headers: { "Accept": "text/html" } })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("candidates failed");
                }

                return response.text();
            })
            .then(function (html) {
                if (current !== candidateRequest) {
                    return;
                }

                modalBody.innerHTML = html;

                // السؤال المختار بديلًا لسؤال آخر لا يمكن اختياره مرة ثانية
                var taken = chosenIds();
                modalBody.querySelectorAll("[data-candidate]").forEach(function (item) {
                    if (taken.indexOf(item.dataset.candidate.toLowerCase()) !== -1) {
                        item.remove();
                    }
                });

                if (!modalBody.children.length) {
                    setModalState("", "كل البدائل المتاحة مختارة لأسئلة أخرى.");
                }
            })
            .catch(function () {
                if (current === candidateRequest) {
                    setModalState("pp-state-error", "تعذّر تحميل البدائل، حاول مرة أخرى.");
                }
            });
    }

    function applyReplacement(question, candidate) {
        var holder = question.querySelector('[data-role="substitute"]');
        var input = holder.querySelector('[data-role="substitute-id"]');

        if (!input) {
            input = document.createElement("input");
            input.type = "hidden";
            input.name = "Replacements[" + question.dataset.question + "]";
            input.setAttribute("data-role", "substitute-id");
            holder.appendChild(input);
        }

        input.value = candidate.dataset.candidate;
        holder.querySelector('[data-role="substitute-body"]').innerHTML =
            candidate.querySelector('[data-role="candidate-body"]').innerHTML;
        question.classList.add("is-replaced");
        markStale();
    }

    function cancelReplacement(question) {
        var holder = question.querySelector('[data-role="substitute"]');
        var input = holder.querySelector('[data-role="substitute-id"]');

        if (input) {
            input.remove();
        }

        holder.querySelector('[data-role="substitute-body"]').innerHTML = "";
        question.classList.remove("is-replaced");
        markStale();
    }

    form.addEventListener("click", function (event) {
        var replace = event.target.closest("[data-replace]");
        if (replace) {
            openCandidates(replace.closest(".pms-question"));
            return;
        }

        var cancel = event.target.closest("[data-cancel-replace]");
        if (cancel) {
            cancelReplacement(cancel.closest(".pms-question"));
        }
    });

    modalBody.addEventListener("click", function (event) {
        var choose = event.target.closest("[data-choose-candidate]");
        if (!choose || !activeQuestion) {
            return;
        }

        applyReplacement(activeQuestion, choose.closest("[data-candidate]"));
        bootstrap.Modal.getOrCreateInstance(modalElement).hide();
    });

    updateCounts();
    updateSendButton();

    var review = document.getElementById("sendReview");
    if (review) {
        review.scrollIntoView({ block: "start" });
    }
})();
