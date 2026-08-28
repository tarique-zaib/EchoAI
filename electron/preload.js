const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("electron", {
  // Window
  resizeWindow: (w, h) => ipcRenderer.send("resize-window", { w, h }),
  dragWindow: (x, y) => ipcRenderer.send("overlay-drag", { x, y }),
  getWindowPosition: () => ipcRenderer.invoke("get-window-position"),

  // Screen Capture
  captureScreen: () => ipcRenderer.invoke("capture-screen"),

  cameraMode: (enabled) => ipcRenderer.send("camera-mode", enabled),

  // Events
  onGhostMode: (callback) =>
    ipcRenderer.on("ghost-mode", (_, enabled) => callback(enabled)),

  onCollapse: (callback) =>
    ipcRenderer.on("collapse", (_, collapsed) => callback(collapsed)),

  onShareSafe: (callback) =>
    ipcRenderer.on("share-safe", (_, enabled) => callback(enabled)),
});
