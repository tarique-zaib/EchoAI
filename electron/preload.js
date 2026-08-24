const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("electron", {
  resizeWindow: (w, h) => ipcRenderer.send("resize-window", { w, h }),

  onGhostMode: (callback) =>
    ipcRenderer.on("ghost-mode", (_, enabled) => callback(enabled)),

  onCollapse: (callback) =>
    ipcRenderer.on("collapse", (_, collapsed) => callback(collapsed)),

  onShareSafe: (callback) =>
    ipcRenderer.on("share-safe", (_, enabled) => callback(enabled)),
});