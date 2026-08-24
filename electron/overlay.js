const profileEl = document.querySelector(".profile");
const statusText = document.querySelector(".status-text");
const status = document.querySelector(".status");
const question = document.querySelector(".question");
const answerEl = document.querySelector(".answer");

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5153/interviewHub")
  .withAutomaticReconnect()
  .build();

let fullAnswer = "";

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

    profileEl.innerHTML =
      `<strong>${data.name}</strong><br>${data.years}+ Years`;
  } catch {
    statusText.textContent = "Offline";
    profileEl.innerHTML = "Backend unavailable";
  }
}

loadResumeStatus();

// ---------- Resize ----------

function resizeOverlay() {
  const card = document.querySelector(".card");

  const height = Math.min(Math.max(card.scrollHeight + 40, 170), 420);

  window.electron.resizeWindow(560, height);
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
  status.textContent = "🎙 " + s;
});

connection.on("ResumeUpdated", (data) => {
  statusText.textContent = "Resume Loaded";

  profileEl.innerHTML =
    `<strong>${data.name}</strong><br>${data.years}+ Years • Resume Active`;
});

connection.on("ReceiveTranscript", (q) => {
  q = q.replace(/^Explained\b/i, "Explain");

  question.textContent = q;

  resizeOverlay();
});

connection.on("ClearAnswer", () => {
  fullAnswer = "";

  answerEl.innerHTML = "";

  resizeOverlay();
});

connection.on("ReceiveAnswerChunk", (chunk) => {
  fullAnswer += chunk;

  answerEl.innerHTML = marked.parse(fullAnswer);

  answerEl.scrollTop = answerEl.scrollHeight;

  resizeOverlay();
});

connection
  .start()
  .then(() => console.log("Overlay connected"))
  .catch(console.error);