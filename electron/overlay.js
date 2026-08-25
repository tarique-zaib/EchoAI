const profileEl = document.querySelector(".profile");
const statusText = document.querySelector(".status-text");
const status = document.querySelector(".status");
const question = document.querySelector(".question");
const answerEl = document.querySelector(".answer");
const API = "http://localhost:5153/api";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5153/interviewHub")
  .withAutomaticReconnect()
  .build();

let fullAnswer = "";
let latestCapturePath = null;

const captureBtn = document.getElementById("capture");
const capturePreview = document.getElementById("capturePreview");
const captureImage = document.getElementById("captureImage");
const retakeCapture = document.getElementById("retakeCapture");
const explainCapture = document.getElementById("explainCapture");
const headphoneBtn = document.getElementById("headphoneMode");
const systemBtn = document.getElementById("systemMode");
const thinkingIndicator = document.getElementById("thinkingIndicator");

async function setAudioMode(mode) {
  const endpoint =
    mode === "system"
      ? `${API}/audio/mode/system`
      : `${API}/audio/mode/headphone`;

  const res = await fetch(endpoint, { method: "POST" });

  if (!res.ok) throw new Error("Failed to switch audio mode");

  return res.json();
}

function updateAudioMode(mode) {
  headphoneBtn?.classList.toggle("active", mode === "headphone");
  systemBtn?.classList.toggle("active", mode === "system");

  statusText.textContent =
    mode === "headphone" ? "You Speaking" : "Interview Listening";
}

async function startCapture() {
  showToast("Select area to capture");

  try {
    const file = await window.electron.captureScreen();

    if (!file) {
      showToast("Capture cancelled");
      return;
    }

    captureBtn.classList.add("active");

    captureImage.onload = () => {
      resizeOverlay(); // Resize AFTER image is rendered
    };

    captureImage.src = `${file}?t=${Date.now()}`;

    capturePreview.classList.remove("hidden");

    showToast("📷 Screenshot Captured");
    latestCapturePath = file;
  } catch {
    showToast("Capture failed");
  }
}

captureBtn?.addEventListener("click", startCapture);

retakeCapture?.addEventListener("click", startCapture);

explainCapture?.addEventListener("click", async () => {
  if (!latestCapturePath) {
    showToast("Capture a screenshot first.");
    return;
  }

  captureBtn.classList.remove("active");
  captureBtn.classList.add("processing");

  showToast("Analyzing image...");

  fullAnswer = "";
  answerEl.innerHTML = "";

  try {
    await fetch("http://localhost:5153/api/vision/explain", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        imagePath: latestCapturePath,
      }),
    });
  } catch (err) {
    captureBtn.classList.remove("processing");

    showToast("Vision failed");
  }
});

// ---------- Resume ----------

async function loadResumeStatus() {
  try {
    const res = await fetch("http://localhost:5153/api/resume/status");
    const data = await res.json();

    if (!data.loaded) {
      statusText.textContent = "No Resume";
      profileEl.innerHTML = "Upload Resume";
      return;
    }

    statusText.textContent = "Resume Loaded";

    profileEl.innerHTML = `<strong>${data.name}</strong><br>${data.years}+ Years`;
  } catch {
    statusText.textContent = "Offline";
    profileEl.innerHTML = "Backend unavailable";
  }
}

loadResumeStatus();

async function loadAudioMode() {
  try {
    const res = await fetch(`${API}/audio/mode`);
    const data = await res.json();

    updateAudioMode(data.mode);
  } catch {
    console.log("Audio mode unavailable");
  }
}

loadAudioMode();

headphoneBtn?.addEventListener("click", async () => {
  try {
    await setAudioMode("headphone");
    updateAudioMode("headphone");
    showToast("🎧 You Mode");
  } catch {
    showToast("Failed to switch");
  }
});

systemBtn?.addEventListener("click", async () => {
  try {
    await setAudioMode("system");
    updateAudioMode("system");
    showToast("🖥 System Mode");
  } catch {
    showToast("Failed to switch");
  }
});

// ---------- Resize ----------

function resizeOverlay() {
  const card = document.querySelector(".card");

  const height = Math.min(Math.max(card.scrollHeight + 40, 260), 620);

  window.electron.resizeWindow(620, height);
}

// ---------- Toast ----------

function showToast(message) {
  const existing = document.querySelector(".toast");

  if (existing) existing.remove();

  const toast = document.createElement("div");

  toast.className = "toast";
  toast.textContent = message;

  document.body.appendChild(toast);

  requestAnimationFrame(() => toast.classList.add("show"));

  setTimeout(() => {
    toast.classList.remove("show");

    setTimeout(() => toast.remove(), 300);
  }, 2000);
}

// ---------- Electron Events ----------

window.electron.onGhostMode((enabled) => {
  statusText.textContent = enabled ? "Ghost Mode" : "Listening";
});

window.electron.onCollapse((collapsed) => {
  document.body.classList.toggle("mini-mode", collapsed);
});

window.electron.onShareSafe((enabled) => {
  showToast(enabled ? "🛡 Share Safe ON" : "Share Safe OFF");
});

// ---------- SignalR ----------

connection.on("ReceiveStatus", (s) => {
  statusText.textContent = "🎙 " + s;
});

connection.on("ResumeUpdated", (data) => {
  statusText.textContent = "Resume Loaded";

  profileEl.innerHTML = `<strong>${data.name}</strong><br>${data.years}+ Years • Resume Active`;
});

connection.on("ReceiveTranscript", (q) => {
  q = q.replace(/^Explained\b/i, "Explain");

  question.textContent = q;

  resizeOverlay();
});

connection.on("ClearAnswer", () => {
  fullAnswer = "";
  answerEl.innerHTML = "";

  thinkingIndicator.classList.remove("hidden");

  resizeOverlay();
});

connection.on("VisionCompleted", () => {
  captureBtn.classList.remove("processing");
  captureBtn.classList.add("active");

  showToast("Explanation Ready");
});

connection.on("ReceiveAnswerChunk", (chunk) => {
  thinkingIndicator.classList.add("hidden");

  fullAnswer += chunk;

  answerEl.innerHTML = marked.parse(fullAnswer);

  answerEl.scrollTop = answerEl.scrollHeight;

  resizeOverlay();
});

connection
  .start()
  .then(() => console.log("Overlay connected"))
  .catch(console.error);
