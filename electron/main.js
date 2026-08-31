const {
  app,
  BrowserWindow,
  globalShortcut,
  ipcMain,
  dialog,
  screen,
} = require("electron");

const path = require("path");
const fs = require("fs");
const { captureRegion } = require("./capture");

let overlay;

// Shared bounds
let previousBounds = null;
let previousNormalBounds = null;

const NORMAL_SIZE = { width: 560, height: 460 };
const MINI_SIZE = { width: 90, height: 90 };

let cameraMode = false;

// -------------------------------------
// Camera Mode
// -------------------------------------

ipcMain.on("camera-mode", (_, enabled) => {
  if (!overlay || overlay.isDestroyed()) return;

  const display = screen.getPrimaryDisplay();
  const { width } = display.workAreaSize;

  if (enabled && !cameraMode) {
    previousBounds = overlay.getBounds();
    cameraMode = true;

    overlay.setBounds(
      {
        x: Math.round((width - 760) / 2),
        y: 16,
        width: 760,
        height: 320,
      },
      true,
    );
  } else if (!enabled && cameraMode) {
    cameraMode = false;

    overlay.setBounds(previousNormalBounds ?? previousBounds, true);
  }
});

// -------------------------------------
// Auto Resize
// -------------------------------------

ipcMain.on("resize-window", (_, { w, h }) => {
  if (!overlay || overlay.isDestroyed() || cameraMode) return;

  const display = screen.getDisplayMatching(overlay.getBounds());
  const bounds = overlay.getBounds();

  const maxHeight = display.workArea.height - 20;
  const newHeight = Math.min(Math.round(h), maxHeight);

  const maxY = display.workArea.y + display.workArea.height - newHeight;

  overlay.setBounds({
    x: bounds.x,
    y: Math.min(Math.max(bounds.y, display.workArea.y), maxY),
    width: Math.round(w),
    height: newHeight,
  });
});

// -------------------------------------
// Drag
// -------------------------------------

ipcMain.handle("get-window-position", () => {
  if (!overlay || overlay.isDestroyed()) return { x: 0, y: 0 };

  const [x, y] = overlay.getPosition();
  return { x, y };
});

ipcMain.on("overlay-drag", (_, { x, y }) => {
  if (!overlay || overlay.isDestroyed()) return;

  overlay.setPosition(Math.round(x), Math.round(y), true);
});

// -------------------------------------
// Resume
// -------------------------------------

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
    form.append("file", new Blob([buffer]), path.basename(filePath));

    const res = await fetch("http://localhost:5153/api/resume/upload", {
      method: "POST",
      body: form,
    });

    const data = await res.json();

    if (!res.ok) throw new Error(data.error || "Upload failed");

    return data;
  } catch (err) {
    return {
      success: false,
      error: err.message,
    };
  }
});

// -------------------------------------
// Screen Capture
// -------------------------------------

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

// -------------------------------------
// Overlay Window
// -------------------------------------

function createOverlay() {
  overlay = new BrowserWindow({
    width: NORMAL_SIZE.width,
    height: NORMAL_SIZE.height,
    frame: false,
    transparent: true,
    resizable: true,
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

  // ---------------- Ghost Mode ----------------

  globalShortcut.register("CommandOrControl+Shift+O", () => {
    if (!overlay || overlay.isDestroyed()) return;

    ghostMode = !ghostMode;

    overlay.setIgnoreMouseEvents(ghostMode, { forward: true });
    overlay.webContents.send("ghost-mode", ghostMode);
  });

  // ---------------- Mini Mode ----------------

  globalShortcut.register("Escape", () => {
    if (!overlay || overlay.isDestroyed()) return;

    collapsed = !collapsed;

    if (collapsed) {
      previousNormalBounds = overlay.getBounds();

      overlay.setSize(MINI_SIZE.width, MINI_SIZE.height, true);
      overlay.webContents.send("collapse", true);
    } else {
      if (previousNormalBounds) {
        overlay.setBounds(previousNormalBounds, true);
      }

      overlay.webContents.send("collapse", false);
    }
  });

  // ---------------- Share Safe ----------------

  globalShortcut.register("CommandOrControl+Shift+S", () => {
    if (!overlay || overlay.isDestroyed()) return;

    shareSafe = !shareSafe;

    overlay.setContentProtection(shareSafe);
    overlay.webContents.send("share-safe", shareSafe);

    console.log(
      shareSafe ? "🛡 Share Safe Enabled" : "🛡 Share Safe Disabled",
    );
  });

  overlay.loadFile(path.join(__dirname, "overlay.html"));
}

// -------------------------------------
// App Lifecycle
// -------------------------------------

app.whenReady().then(createOverlay);

app.on("will-quit", () => {
  globalShortcut.unregisterAll();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});