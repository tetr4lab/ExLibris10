
let modifierKeys = {
    ctrl: false,
    alt: false,
    shift: false
};

document.addEventListener("keydown", (event) => {
    modifierKeys.ctrl = event.ctrlKey;
    modifierKeys.alt = event.altKey;
    modifierKeys.shift = event.shiftKey;
});

document.addEventListener("keyup", (event) => {
    modifierKeys.ctrl = event.ctrlKey;
    modifierKeys.alt = event.altKey;
    modifierKeys.shift = event.shiftKey;
});

function getModifierKeys() {
    return modifierKeys;
}

function openInNewTab(url) {
    window.open(url, '_blank');
}
