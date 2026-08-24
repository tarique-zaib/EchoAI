const status = document.querySelector(".status");
const question = document.querySelector(".question");
const answer = document.querySelector(".answer");

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5153/interviewHub")
  .withAutomaticReconnect()
  .build();

window.electron.onGhostMode((enabled) => {
  document.querySelector(".status-text").textContent = enabled
    ? "Ghost Mode"
    : "Listening";
});

connection.on("ReceiveStatus", (s) => {
  status.textContent = "🎙 " + s;
});

connection.on("ReceiveTranscript", (q) => {
  q = q.replace(/^Explained\b/i, "Explain");
  question.textContent = q;
});

connection.on("ClearAnswer", () => {
  answer.textContent = "";
});

const answerEl = document.querySelector(".answer");
let fullAnswer = "";

connection.on("ClearAnswer", () => {
  fullAnswer = "";
  answerEl.innerHTML = "";
});

connection.on("ReceiveAnswerChunk", (chunk) => {
  fullAnswer += chunk;
  answerEl.innerHTML = marked.parse(fullAnswer);

  answerEl.scrollTop = answerEl.scrollHeight;
});

connection
  .start()
  .then(() => console.log("Overlay connected"))
  .catch(console.error);
