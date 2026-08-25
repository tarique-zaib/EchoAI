\# EchoPrepAI Architecture



\*\*Version:\*\* v0.5 Stable



\*\*Status:\*\* Core Audio Engine Frozen



\---



\## Overview



EchoPrepAI is a desktop AI interview assistant built with three independent layers.



\* \*\*Electron\*\* → Overlay UI and native desktop integration.

\* \*\*.NET Backend\*\* → Orchestrator, SignalR, Gemini Vision, Resume APIs.

\* \*\*Python\*\* → Stable transcription engine using Whisper + Silero VAD.



Each layer has a single responsibility and communicates through APIs or SignalR.



\---



\## Architecture



Electron Overlay

↓

.NET Backend (SignalR + Gemini)

↓

Python Transcriber (Frozen Core)



\---



\## Folder Structure



EchoPrepAI/

├── electron/

│   ├── main.js

│   ├── preload.js

│   ├── overlay.html

│   ├── overlay.css

│   ├── overlay.js

│   └── capture.js

│

├── backend/

│   ├── Controllers/

│   ├── Services/

│   ├── Hubs/

│   ├── Models/

│   ├── appsettings.json

│   └── Program.cs

│

├── python/

│   └── transcriber.py

│

├── temp/

│   └── interview-current.png

│

└── ARCHITECTURE.md



\---



\## Layer Responsibilities



\### Electron (Presentation Layer)



Responsible for everything visible to the user.



Features:



\* Glass overlay

\* Draggable window

\* Mini Mode

\* Ghost Mode

\* Share Safe Mode

\* Native screenshot capture

\* Capture preview

\* Retake

\* Explain button

\* Window resizing



Never performs AI processing.



\---



\### .NET Backend (Orchestrator)



Acts as the brain.



Responsibilities:



\* SignalR Hub

\* Resume APIs

\* Gemini Vision

\* Screenshot analysis

\* Streaming AI responses

\* Process coordination



Never handles UI rendering.



\---



\### Python (Frozen Core)



\*\*Status: LOCKED\*\*



File:



python/transcriber.py



This is the production transcription engine.



Contains:



\* Faster Whisper

\* Silero VAD

\* Duplicate suppression

\* Merge logic

\* Technical word cleanup

\* System audio capture

\* Microphone capture



Rule:



> Do not modify unless fixing a critical bug.



New audio features should be implemented around this file instead of inside it.



\---



\## SignalR Flow



Interviewer speaks

↓

Python transcriber

↓

.NET Backend

↓

SignalR

↓

Electron Overlay



Streaming events:



\* ReceiveTranscript

\* ReceiveAnswerChunk

\* ClearAnswer

\* ResumeUpdated



\---



\## Vision Flow



Click Camera

↓

Electron hides overlay

↓

Windows Snipping Tool

↓

Image saved to temp/interview-current.png

↓

.NET VisionService

↓

Gemini 3.6 Flash

↓

SignalR streaming

↓

Overlay displays explanation



\---



\## Window Modes



\### Normal Mode



\* Full glass card

\* Transcript

\* AI answer

\* Camera button



\### Mini Mode



\* Green pulse only

\* Hidden content

\* ESC toggles mode



\### Ghost Mode



\* Click-through overlay

\* Toggle with Ctrl+Shift+O



\### Share Safe



\* Content protection enabled

\* Designed for screen-sharing scenarios



\---



\## Audio Strategy



Current:



\* Python selects microphone or system audio at startup.



Future:



\* Keep transcriber.py unchanged.

\* Build an external Audio Controller.

\* Electron buttons switch modes through .NET.



This preserves the stable transcription engine.



\---



\## Current Release



\### v0.5



Completed:



\* Glass HUD

\* SignalR streaming

\* Resume detection

\* Native region capture

\* Capture preview

\* Retake

\* Gemini Vision

\* Structured explanations

\* Stable transcription core

\* GitHub release



\---



\## Roadmap



\### v0.6



\* Audio Controller

\* Dual Audio Mode

\* Auto Interview Mode

\* Faster streaming polish



\### v0.7



\* Memory Drawer

\* Resume Quick Recall

\* STAR Story Library

\* OneTrade knowledge base



\---



\## Golden Rules



1\. Freeze working code.

2\. Add features through new files whenever possible.

3\. Keep Electron as presentation only.

4\. Keep .NET as orchestration only.

5\. Keep Python as the transcription engine.

6\. Every feature follows:

&#x20;  Design → Build → Test → Check-in → Tag → Release.



