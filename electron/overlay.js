const profileEl = document.querySelector(".profile");
const statusText = document.querySelector(".status-text");
const question = document.querySelector(".question");
const answerEl = document.querySelector(".answer");
const answerContainer = document.querySelector(".answer-container");
const API = "http://localhost:5153/api";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5153/interviewHub")
  .withAutomaticReconnect()
  .build();

let fullAnswer = "";
let latestCapturePath = null;
const OVERLAY_WIDTH = 560;
let teleWords = [];
let teleIndex = 0;
let teleTimer = null;

// ---------- UI Elements ----------

const captureBtn = document.getElementById("capture");
const capturePreview = document.getElementById("capturePreview");
const captureImage = document.getElementById("captureImage");
const retakeCapture = document.getElementById("retakeCapture");
const explainCapture = document.getElementById("explainCapture");

const headphoneBtn = document.getElementById("headphoneMode");
const systemBtn = document.getElementById("systemMode");

const thinkingIndicator = document.getElementById("thinkingIndicator");

const modeQuick = document.getElementById("modeQuick");
const modeDetailed = document.getElementById("modeDetailed");
const modeInterview = document.getElementById("modeInterview");
const cameraModeBtn = document.getElementById("cameraMode");

let cameraMode = false;

// ---------- Answer Modes ----------

let answerMode = "quick";

async function setMode(mode) {
  answerMode = mode;

  try {
    await fetch(`${API}/settings/answer-mode`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ mode }),
    });

    modeQuick?.classList.toggle("active", mode === "quick");
    modeDetailed?.classList.toggle("active", mode === "detailed");
    modeInterview?.classList.toggle("active", mode === "interview");

    const labels = {
      quick: "⚡ 30 Sec",
      detailed: "📘 Detailed",
      interview: "🎯 Interview",
    };

    showToast(`Mode: ${labels[mode]}`);

    const currentQuestion = question.textContent.trim();

    if (
      currentQuestion &&
      currentQuestion !== "Listening for interview question..."
    ) {
      fullAnswer = "";
      answerEl.innerHTML = "";
      thinkingIndicator.classList.remove("hidden");

      await fetch(`${API}/interview/regenerate`, {
        method: "POST",
      });
    }
  } catch {
    showToast("Failed to change mode");
  }
}

modeQuick?.addEventListener("click", () => setMode("quick"));
modeDetailed?.addEventListener("click", () => setMode("detailed"));
modeInterview?.addEventListener("click", () => setMode("interview"));

// ---------- Audio Mode ----------

async function setAudioMode(mode) {
  const endpoint =
    mode === "system"
      ? `${API}/audio/mode/system`
      : `${API}/audio/mode/headphone`;

  const res = await fetch(endpoint, { method: "POST" });

  if (!res.ok) throw new Error();

  return res.json();
}

function updateAudioMode(mode) {
  headphoneBtn?.classList.toggle("active", mode === "headphone");
  systemBtn?.classList.toggle("active", mode === "system");

  statusText.textContent =
    mode === "headphone" ? "You Speaking" : "Interview Listening";
}

// ---------- Screen Capture ----------

async function startCapture() {
  latestCapturePath = null;
  fullAnswer = "";
  answerEl.innerHTML = "";

  capturePreview.classList.add("hidden");
  captureImage.removeAttribute("src");

  showToast("Select area to capture");

  try {
    const file = await window.electron.captureScreen();

    if (!file) {
      showToast("Capture cancelled");
      return;
    }

    latestCapturePath = file;

    captureBtn.classList.add("active");

    captureImage.onload = resizeOverlay;
    captureImage.src = `${file}?t=${Date.now()}`;

    capturePreview.classList.remove("hidden");

    showToast("📷 Screenshot Captured");
  } catch {
    showToast("Capture failed");
  }
}

cameraModeBtn?.addEventListener("click", () => {
  cameraMode = !cameraMode;

  document.body.classList.toggle("camera-mode", cameraMode);

  window.electron.cameraMode(cameraMode);

  showToast(cameraMode ? "📷 Camera Mode" : "Normal Mode");
});

captureBtn?.addEventListener("click", startCapture);
retakeCapture?.addEventListener("click", startCapture);

explainCapture?.addEventListener("click", async () => {
  if (!latestCapturePath) {
    showToast("Capture a screenshot first.");
    return;
  }

  captureBtn.classList.remove("active");
  captureBtn.classList.add("processing");

  fullAnswer = "";
  answerEl.innerHTML = "";

  thinkingIndicator.classList.remove("hidden");

  showToast(`Analyzing (${answerMode})...`);

  try {
    await fetch(`${API}/vision/explain`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        imagePath: latestCapturePath,
        mode: answerMode,
      }),
    });
  } catch {
    captureBtn.classList.remove("processing");
    thinkingIndicator.classList.add("hidden");
    showToast("Vision failed");
  }
});

// ---------- Resume ----------

async function loadResumeStatus() {
  try {
    const res = await fetch(`${API}/resume/status`);
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

// ---------- Load Audio Mode ----------

async function loadAudioMode() {
  try {
    const res = await fetch(`${API}/audio/mode`);
    const data = await res.json();

    updateAudioMode(data.mode);
  } catch {}
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
  if (cameraMode) return;

  const card = document.querySelector(".card");
  if (!card) return;

  const height = Math.min(Math.max(card.scrollHeight + 40, 260), 620);

  window.electron.resizeWindow(OVERLAY_WIDTH, height);
}

// ---------- Toast ----------

function showToast(message) {
  document.querySelector(".toast")?.remove();

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

connection.on("ReceivePartialTranscript", (text) => {
  question.textContent = text.replace(/^Explained\b/i, "Explain");
  resizeOverlay();
});

connection.on("ReceiveTranscript", (q) => {
  question.textContent = q.replace(/^Explained\b/i, "Explain");
  teleIndex = 0;
  teleWords = [];

  if (teleTimer) {
    clearInterval(teleTimer);
    teleTimer = null;
  }
  fullAnswer = "";
  answerEl.innerHTML = "";
  thinkingIndicator.classList.remove("hidden");

  resizeOverlay();
});

connection.on("ClearAnswer", () => {
  fullAnswer = "";
  teleIndex = 0;
  teleWords = [];

  if (teleTimer) {
    clearInterval(teleTimer);
    teleTimer = null;
  }
  answerEl.innerHTML = "";
  document.querySelector(".answer-panel").style.display = "none";
  thinkingIndicator.classList.remove("hidden");
  resizeOverlay();
});

connection.on("VisionCompleted", () => {
  captureBtn.classList.remove("processing");
  captureBtn.classList.add("active");
  thinkingIndicator.classList.add("hidden");
  showToast("Explanation Ready");
});

connection.on("ReceiveAnswerChunk", (chunk) => {
  thinkingIndicator.classList.add("hidden");
  document.querySelector(".answer-panel").style.display = "block";

  fullAnswer += chunk;

  // Always render the FULL answer
  answerEl.innerHTML = marked.parse(fullAnswer);

  // Auto-scroll only in Normal Mode
  if (!cameraMode) {
    answerContainer.scrollTop = answerContainer.scrollHeight;
    resizeOverlay();
  }
});

// ---------- Connect ----------

connection
  .start()
  .then(async () => {
    console.log("Overlay connected");

    const res = await fetch(`${API}/settings/answer-mode`);
    const data = await res.json();

    answerMode = data.mode;

    modeQuick?.classList.toggle("active", answerMode === "quick");
    modeDetailed?.classList.toggle("active", answerMode === "detailed");
    modeInterview?.classList.toggle("active", answerMode === "interview");
  })
  .catch(console.error);
