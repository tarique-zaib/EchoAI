const profileEl = document.querySelector(".profile");
const statusText = document.querySelector(".status-text");
const question = document.querySelector(".question");
const answerEl = document.querySelector(".answer");
const answerContainer = document.querySelector(".answer-container");
const profileCard = document.getElementById("profileCard");
const profileContent = document.getElementById("profileContent");
const resumeInput = document.getElementById("resumeInput");
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
let hasFinalQuestion = false;

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
const card = document.querySelector(".card");

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

  question.textContent = "Analyzing screenshot...";
  document.querySelector(".answer-panel").style.display = "none";

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
      statusText.textContent = "Listening";

      profileContent.innerHTML = `
        <div class="profile-icon">📄</div>
        <div class="profile-info">
          <strong>No Resume</strong>
          <span>Click or drag a PDF/DOCX here</span>
        </div>
      `;
      return;
    }

    statusText.textContent = "Resume Loaded";

    profileContent.innerHTML = `
      <div class="profile-icon">✅</div>
      <div class="profile-info">
        <strong>${data.name}</strong>
        <span>${data.years}+ Years • Resume Active</span>
      </div>
    `;
  } catch {
    statusText.textContent = "Offline";

    profileContent.innerHTML = `
      <div class="profile-icon">⚠️</div>
      <div class="profile-info">
        <strong>Backend Offline</strong>
        <span>Start the backend to load a resume.</span>
      </div>
    `;
  }
}

loadResumeStatus();

async function uploadResume(filePath) {
  if (!filePath) return;

  showToast("Uploading resume...");

  const result = await window.electron.uploadResume(filePath);

  if (!result.success) {
    showToast("Upload failed");
    console.error(result.error);
    return;
  }

  showToast("Resume uploaded");

  loadResumeStatus();
}

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

profileCard?.addEventListener("click", async () => {
  const filePath = await window.electron.pickResume();
  await uploadResume(filePath);
});

["dragenter", "dragover"].forEach((event) => {
  profileCard?.addEventListener(event, (e) => {
    e.preventDefault();
    profileCard.classList.add("drag-over");
  });
});

["dragleave", "drop"].forEach((event) => {
  profileCard?.addEventListener(event, (e) => {
    e.preventDefault();
    profileCard.classList.remove("drag-over");
  });
});

profileCard?.addEventListener("drop", async (e) => {
  e.preventDefault();
  profileCard.classList.remove("drag-over");

  const file = e.dataTransfer.files[0];
  if (!file) return;

  await uploadResume(file.path);
});

// ---------- Resize ----------
let lastHeight = 0;

function resizeOverlay() {
  if (cameraMode) return;

  requestAnimationFrame(() => {
    const answer = document.querySelector(".answer");
    const panel = document.querySelector(".answer-panel");
    if (!answer || !panel) return;

    const displayMax = window.screen.availHeight - 24;
    const desiredHeight = panel.offsetTop + answer.scrollHeight + 140;

    // Grow window until screen limit
    const finalHeight = Math.min(desiredHeight, displayMax);

    if (finalHeight !== lastHeight) {
      lastHeight = finalHeight;
      window.electron.resizeWindow(OVERLAY_WIDTH, finalHeight);
    }

    // After reaching max height, make only the answer scroll
    const available = finalHeight - panel.offsetTop - 40;
    answerContainer.style.maxHeight = `${available}px`;
    answerContainer.style.overflowY = "auto";
  });
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
  document.body.classList.toggle("stealth", enabled);
  showToast(enabled ? "🛡 Share Safe ON" : "Share Safe OFF");
});

// ---------- SignalR ----------

connection.on("ReceiveStatus", (s) => {
  statusText.textContent = "🎙 " + s;
  card.classList.remove("ai-active");

  if (s.toLowerCase().includes("listening")) {
    hasFinalQuestion = false;
  }
});

connection.on("ResumeUpdated", (data) => {
  statusText.textContent = "Resume Loaded";

  profileContent.innerHTML = `
    <div class="profile-icon">✅</div>
    <div class="profile-info">
      <strong>${data.name}</strong>
      <span>${data.years}+ Years • Resume Active</span>
    </div>
  `;

  showToast("Resume Loaded");
});

connection.on("ReceivePartialTranscript", (text) => {
  if (hasFinalQuestion) return;

  question.textContent = text.replace(/^Explained\b/i, "Explain");
  card.classList.add("ai-active");
  resizeOverlay();
});

connection.on("ReceiveTranscript", (q) => {
  question.textContent = q.replace(/^Explained\b/i, "Explain");
  hasFinalQuestion = true;
  teleIndex = 0;
  teleWords = [];

  if (teleTimer) {
    clearInterval(teleTimer);
    teleTimer = null;
  }
  fullAnswer = "";
  answerEl.innerHTML = "";
  card.classList.add("ai-active");
  thinkingIndicator.classList.remove("hidden");

  resizeOverlay();
});

connection.on("ClearAnswer", () => {
  fullAnswer = "";
  hasFinalQuestion = false;
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

  question.textContent = "Screenshot Explanation";

  document.querySelector(".answer-panel").style.display = "block";

  showToast("Explanation Ready");

  resizeOverlay();
});

connection.on("ReceiveAnswerChunk", (chunk) => {
  thinkingIndicator.classList.add("hidden");
  document.querySelector(".answer-panel").style.display = "block";
  card.classList.remove("ai-active");

  fullAnswer += chunk;
  answerEl.innerHTML = marked.parse(fullAnswer);

  if (!cameraMode) {
    requestAnimationFrame(() => {
      requestAnimationFrame(resizeOverlay);
    });
  } else {
    answerContainer.scrollTop = answerContainer.scrollHeight;
  }
});

// ---------- Connect ----------
connection
  .start()
  .then(async () => {
    console.log("Overlay connected");
    document.querySelector(".status").classList.remove("offline");
    document.querySelector(".status").classList.add("online");
    document.querySelector(".card").classList.add("ai-active");

    const res = await fetch(`${API}/settings/answer-mode`);
    const data = await res.json();

    answerMode = data.mode;

    modeQuick?.classList.toggle("active", answerMode === "quick");
    modeDetailed?.classList.toggle("active", answerMode === "detailed");
    modeInterview?.classList.toggle("active", answerMode === "interview");
  })
  .catch(console.error);
