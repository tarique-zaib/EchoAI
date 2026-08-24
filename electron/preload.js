const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("electron", {
    onGhostMode: (cb) => ipcRenderer.on("ghost-mode", (_, enabled) => cb(enabled))
});