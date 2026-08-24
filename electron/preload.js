const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("electron", {
  onGhostMode: cb =>
    ipcRenderer.on("ghost-mode", (_, enabled) => cb(enabled)),
  resizeWindow: (w, h) =>
    ipcRenderer.send("resize-window", { w, h }),
  onCollapse: cb =>
    ipcRenderer.on("collapse", (_, value) => cb(value))
});
