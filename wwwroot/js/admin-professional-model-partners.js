// PPM-S1: قسم «الإتاحة للشركاء». بدون JS تبقى كل الأقسام ظاهرة والخادم يتجاهل ما لا يلزم.
(function () {
    "use strict";

    function init(section) {
        var forPartners = section.querySelector("[data-ppm-for-partners]");
        var body = section.querySelector("[data-ppm-audience-body]");
        var panel = section.querySelector("[data-ppm-partners-panel]");
        var radios = section.querySelectorAll("[data-ppm-audience-radio]");
        var tools = section.querySelector("[data-ppm-tools]");
        var search = section.querySelector("[data-ppm-search]");
        var count = section.querySelector("[data-ppm-count]");
        var noResults = section.querySelector("[data-ppm-no-results]");
        var partners = Array.prototype.slice.call(section.querySelectorAll("[data-ppm-partner]"));

        function isSelectedAudience() {
            var selected = section.querySelector("[data-ppm-audience-radio]:checked");
            return !!selected && selected.id === "PartnerAudienceSelected";
        }

        function checkbox(partner) {
            return partner.querySelector("input[type=checkbox]");
        }

        function refreshVisibility() {
            body.hidden = !forPartners.checked;
            panel.hidden = !isSelectedAudience();
        }

        function refreshCount() {
            if (count) {
                count.textContent = partners.filter(function (p) { return checkbox(p).checked; }).length;
            }
        }

        function applySearch() {
            var term = (search.value || "").trim().toLowerCase();
            var shown = 0;
            partners.forEach(function (p) {
                var match = !term || (p.getAttribute("data-ppm-search-text") || "").toLowerCase().indexOf(term) !== -1;
                p.hidden = !match;
                if (match) { shown++; }
            });
            noResults.hidden = shown > 0;
        }

        function setVisibleChecked(value) {
            partners.forEach(function (p) {
                if (!p.hidden) { checkbox(p).checked = value; }
            });
            refreshCount();
        }

        forPartners.addEventListener("change", refreshVisibility);
        Array.prototype.forEach.call(radios, function (r) {
            r.addEventListener("change", refreshVisibility);
        });

        if (tools) {
            tools.hidden = false;
            search.addEventListener("input", applySearch);
            section.querySelector("[data-ppm-select-all]").addEventListener("click", function () { setVisibleChecked(true); });
            section.querySelector("[data-ppm-clear-all]").addEventListener("click", function () { setVisibleChecked(false); });
            partners.forEach(function (p) {
                checkbox(p).addEventListener("change", refreshCount);
            });
        }

        refreshVisibility();
    }

    document.addEventListener("DOMContentLoaded", function () {
        Array.prototype.forEach.call(document.querySelectorAll("[data-ppm-audience]"), init);
    });
})();
