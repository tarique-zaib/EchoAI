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

  let ghostMode = false;

  globalShortcut.register("CommandOrControl+Shift+O", () => {
    ghostMode = !ghostMode;

    overlay.setIgnoreMouseEvents(ghostMode, { forward: true });

    overlay.webContents.send("ghost-mode", ghostMode);
  });

  overlay.loadFile(path.join(__dirname, "overlay.html"));
}

app.whenReady().then(createOverlay);

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
