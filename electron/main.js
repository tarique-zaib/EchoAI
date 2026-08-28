const {
  app,
  BrowserWindow,
  globalShortcut,
  ipcMain,
} = require("electron");
const path = require("path");
const { captureRegion } = require("./capture");

let overlay;

const NORMAL_SIZE = { width: 560, height: 260 };
const MINI_SIZE = { width: 90, height: 90 };

// ---------------- Resize ----------------

ipcMain.on("resize-window", (_, { w, h }) => {
  if (overlay && !overlay.isDestroyed()) {
    overlay.setSize(w, h, true);
  }
});

// ---------------- Drag ----------------

ipcMain.handle("get-window-position", () => {
  if (!overlay || overlay.isDestroyed()) return { x: 0, y: 0 };

  const [x, y] = overlay.getPosition();

  return { x, y };
});

ipcMain.on("overlay-drag", (_, { x, y }) => {
  if (!overlay || overlay.isDestroyed()) return;

  overlay.setPosition(Math.round(x), Math.round(y), true);
});


// ---------------- Screen Capture ----------------

ipcMain.handle("capture-screen", async () => {
  if (!overlay || overlay.isDestroyed()) return null;

  overlay.hide();

  try {
    const file = await captureRegion();

    overlay.showInactive();

    return file;
  } catch (err) {
    overlay.showInactive();
    throw err;
  }
});

// ---------------- Overlay ----------------

function createOverlay() {
  overlay = new BrowserWindow({
    width: 560,
    height: 360,
    frame: false,
    transparent: true,
    resizable: false,
    movable: true,
    alwaysOnTop: true,
    skipTaskbar: true,
    hasShadow: true,
    backgroundColor: "#00000000",
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  let ghostMode = false;
  let collapsed = false;
  let shareSafe = false;

  // Ghost Mode
  globalShortcut.register("CommandOrControl+Shift+O", () => {
    if (!overlay || overlay.isDestroyed()) return;

    ghostMode = !ghostMode;

    overlay.setIgnoreMouseEvents(ghostMode, { forward: true });
    overlay.webContents.send("ghost-mode", ghostMode);
  });

  // Mini Mode
  globalShortcut.register("Escape", () => {
    if (!overlay || overlay.isDestroyed()) return;

    collapsed = !collapsed;

    if (collapsed) {
      overlay.setSize(MINI_SIZE.width, MINI_SIZE.height, true);
      overlay.webContents.send("collapse", true);
    } else {
      overlay.setSize(NORMAL_SIZE.width, NORMAL_SIZE.height, true);
      overlay.webContents.send("collapse", false);
    }
  });

  // Share Safe
  globalShortcut.register("CommandOrControl+Shift+S", () => {
    if (!overlay || overlay.isDestroyed()) return;

    shareSafe = !shareSafe;

    overlay.setContentProtection(shareSafe);
    overlay.webContents.send("share-safe", shareSafe);

    console.log(
      shareSafe
        ? "🛡 Share Safe Enabled"
        : "🛡 Share Safe Disabled"
    );
  });

  overlay.loadFile(path.join(__dirname, "overlay.html"));
}

app.whenReady().then(createOverlay);

app.on("will-quit", () => {
  globalShortcut.unregisterAll();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});