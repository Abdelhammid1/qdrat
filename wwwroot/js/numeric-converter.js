window.applyIndicNumbers = function (rootSelector) {

    if (!window.enableIndicNumbers) return;

    const map = {
        '0': '٠',
        '1': '١',
        '2': '٢',
        '3': '٣',
        '4': '٤',
        '5': '٥',
        '6': '٦',
        '7': '٧',
        '8': '٨',
        '9': '٩'
    };

    function walk(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            node.nodeValue = node.nodeValue.replace(/\d/g, d => map[d]);
            return;
        }
        node.childNodes.forEach(walk);
    }

    const root = document.querySelector(rootSelector);
    if (root) {
        walk(root);
    }
};
