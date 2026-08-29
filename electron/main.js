const {
  app,
  BrowserWindow,
  globalShortcut,
  ipcMain,
  dialog,
} = require("electron");
const path = require("path");
const { captureRegion } = require("./capture");
const fs = require("fs");
let overlay;

const NORMAL_SIZE = { width: 560, height: 620 };
const MINI_SIZE = { width: 90, height: 90 };
let cameraMode = false;
let previousBounds = null;

const { screen } = require("electron");

ipcMain.on("camera-mode", (_, enabled) => {
  if (!overlay || overlay.isDestroyed()) return;

  const display = screen.getPrimaryDisplay();
  const { width } = display.workAreaSize;

  if (enabled && !cameraMode) {
    previousBounds = overlay.getBounds();
    cameraMode = true;

    const CAMERA_WIDTH = 760;
    const CAMERA_HEIGHT = 320;

    overlay.setBounds(
      {
        x: Math.round((width - CAMERA_WIDTH) / 2),
        y: 16,
        width: CAMERA_WIDTH,
        height: CAMERA_HEIGHT,
      },
      true,
    );
  } else if (!enabled && cameraMode && previousBounds) {
    cameraMode = false;
    overlay.setBounds(previousBounds, true);
  }
});

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

ipcMain.handle("pick-resume", async () => {
  if (!overlay || overlay.isDestroyed()) return null;

  const result = await dialog.showOpenDialog(overlay, {
    title: "Select Resume",
    properties: ["openFile"],
    filters: [{ name: "Resume", extensions: ["pdf", "docx"] }],
  });

  if (result.canceled) return null;

  return result.filePaths[0];
});

ipcMain.handle("upload-resume", async (_, filePath) => {
  try {
    const buffer = fs.readFileSync(filePath);

    const form = new FormData();
    form.append(
      "file",
      new Blob([buffer]),
      path.basename(filePath)
    );

    const res = await fetch("http://localhost:5153/api/resume/upload", {
      method: "POST",
      body: form
    });

    const data = await res.json();

    if (!res.ok)
      throw new Error(data.error || "Upload failed");

    return data;
  } catch (err) {
    return {
      success: false,
      error: err.message
    };
  }
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
    height: 620,
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

    console.log(shareSafe ? "🛡 Share Safe Enabled" : "🛡 Share Safe Disabled");
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
