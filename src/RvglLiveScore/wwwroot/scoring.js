const key = lobbyId => `rvgl.scoring.v3.${lobbyId}`;

export function loadScoring(lobbyId) {
    return localStorage.getItem(key(lobbyId));
}

export function saveScoring(lobbyId, json) {
    try {
        localStorage.setItem(key(lobbyId), json);
        return true;
    } catch {
        return false;
    }
}

export function downloadScoring(json) {
    const url = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'rvgl-scoring.json';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
