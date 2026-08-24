const profileEl = document.querySelector(".profile");
const statusText = document.querySelector(".status-text");
const status = document.querySelector(".status");
const question = document.querySelector(".question");
const answer = document.querySelector(".answer");

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5153/interviewHub")
  .withAutomaticReconnect()
  .build();

async function loadResumeStatus() {
  try {
    const res = await fetch("http://localhost:5153/api/resume/status");
    const data = await res.json();

    if (!data.loaded) {
      profileEl.innerHTML = "No Resume Loaded";
      return;
    }

    profileEl.innerHTML = `
      <strong>${data.name}</strong><br>
      ${data.headline.split(" with ")[0]}<br>
      ${data.years}+ Years • Resume Active
    `;
  } catch {
    profileEl.innerHTML = "Backend unavailable";
  }
}

loadResumeStatus();

function resizeOverlay() {
  const card = document.querySelector(".card");

  const height = Math.min(Math.max(card.scrollHeight + 40, 170), 420);

  window.electron.resizeWindow(560, height);
}

window.electron.onGhostMode((enabled) => {
  document.querySelector(".status-text").textContent = enabled
    ? "Ghost Mode"
    : "Listening";
});

window.electron.onCollapse((collapsed) => {
  document.body.classList.toggle("mini-mode", collapsed);
});

connection.on("ReceiveStatus", (s) => {
  status.textContent = s;
});

connection.on("ResumeUpdated", (data) => {
  profileEl.innerHTML = `
    <strong>${data.name}</strong><br>
    ${data.headline.split(" with ")[0]}<br>
    ${data.years}+ Years • Resume Active
  `;
});

connection.on("ReceiveTranscript", (q) => {
  q = q.replace(/^Explained\b/i, "Explain");
  question.textContent = q;
  resizeOverlay();
});

const answerEl = document.querySelector(".answer");
let fullAnswer = "";

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
