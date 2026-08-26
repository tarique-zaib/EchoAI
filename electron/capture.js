const { clipboard, shell } = require("electron");
const fs = require("fs");
const path = require("path");

async function captureRegion() {
  clipboard.clear();

  const dir = path.join(process.cwd(), "temp");

  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  // Clean old captures (keep the folder tidy)
  for (const file of fs.readdirSync(dir)) {
    if (file.startsWith("interview-") && file.endsWith(".png")) {
      try {
        fs.unlinkSync(path.join(dir, file));
      } catch {}
    }
  }

  await shell.openExternal("ms-screenclip:");

  for (let i = 0; i < 200; i++) {
    await new Promise((r) => setTimeout(r, 100));

    const image = clipboard.readImage();

    if (!image.isEmpty()) {
      // New file every capture
      const file = path.join(dir, `interview-${Date.now()}.png`);

      fs.writeFileSync(file, image.toPNG());

      return file;
    }
  }

  return null;
}

module.exports = {
  captureRegion,
};
