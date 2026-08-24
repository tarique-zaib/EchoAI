import { useState } from "react";
import "./ResumeUpload.css";

export default function ResumeUpload() {
  const [file, setFile] = useState(null);
  const [status, setStatus] = useState("");

  const upload = async () => {
    if (!file) return;

    const data = new FormData();
    data.append("file", file);

    setStatus("Uploading...");

    const res = await fetch(
      "http://localhost:5153/api/resume/upload",
      {
        method: "POST",
        body: data,
      }
    );

    const json = await res.json();

    if (json.success)
      setStatus("Resume uploaded successfully.");
    else
      setStatus("Upload failed.");
  };

  return (
    <section className="resume-card">
      <div className="resume-header">
        <h2>Resume Brain</h2>
        <p>Upload once. EchoPrep remembers everything.</p>
      </div>

      <input
        type="file"
        accept=".pdf,.doc,.docx"
        onChange={(e) => setFile(e.target.files[0])}
      />

      <button onClick={upload}>
        Upload Resume
      </button>

      {status && <p className="status">{status}</p>}
    </section>
  );
}