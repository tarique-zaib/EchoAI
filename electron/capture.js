const { clipboard, shell } = require("electron");
const fs = require("fs");
const path = require("path");

async function captureRegion() {
  clipboard.clear();

  await shell.openExternal("ms-screenclip:");

  for (let i = 0; i < 200; i++) {
    await new Promise((r) => setTimeout(r, 100));

    const image = clipboard.readImage();

    if (!image.isEmpty()) {
      const dir = path.join(process.cwd(), "temp");

      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }

      const file = path.join(dir, "interview-current.png");

      fs.writeFileSync(file, image.toPNG());

      return file;
    }
  }

  return null;
}

module.exports = {
  captureRegion,
};