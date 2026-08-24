const { app, BrowserWindow, globalShortcut, ipcMain } = require("electron");

const path = require("path");

let overlay;

function createOverlay() {
  overlay = new BrowserWindow({
    width: 540,
    height: 360,
    frame: false,
    transparent: true,
    resizable: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    movable: true,
    hasShadow: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
    },
  });

  ipcMain.on("resize-window", (_, { w, h }) => {
    overlay.setSize(w, h, true);
  });

  let ghostMode = false;

  globalShortcut.register("CommandOrControl+Shift+O", () => {
    ghostMode = !ghostMode;

    overlay.setIgnoreMouseEvents(ghostMode, { forward: true });

    overlay.webContents.send("ghost-mode", ghostMode);
  });

  let collapsed = false;

  globalShortcut.register("Escape", () => {
    collapsed = !collapsed;

    if (collapsed) {
      overlay.setSize(90, 90, true);
      overlay.webContents.send("collapse", true);
    } else {
      overlay.setSize(560, 260, true);
      overlay.webContents.send("collapse", false);
    }
  });

  overlay.loadFile(path.join(__dirname, "overlay.html"));
}

app.whenReady().then(createOverlay);

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
