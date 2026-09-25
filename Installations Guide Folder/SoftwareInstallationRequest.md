# Software Installation Request — ClarityClaim (Hackathon)

One request block per software item, matching the fields in the internal IT
request tool. Copy each block into a separate request (or one request per
line item, depending on how the tool groups them). All six items are needed
to build and run the ClarityClaim hackathon project locally.

**Why there's no React, ASP.NET Core, Tailwind, Dapper, etc. on this list:**
those are not standalone software with their own installer — they're code
libraries the project depends on (npm packages for the frontend, NuGet
packages for the backend). They get downloaded automatically into the
project folder the first time someone runs `npm install` / `dotnet build`,
using the Node.js and .NET SDK requested below. There's nothing separate for
IT to install for any of them. The one thing this **does** depend on: the
machine needs network access to the public package registries
(`registry.npmjs.org`, `api.nuget.org`) for that automatic download to
succeed. If the office network proxies or blocks those, that's a firewall/
proxy-allowlist question for IT, not a software install — flagging it here
in case it comes up.

---

## 1. .NET 10 SDK

- **Software Name:** .NET 10 SDK
- **Priority:** High
- **Version Name:** 10.0.x (SDK, not just the runtime)
- **Vendor of the Software:** Microsoft
- **License Type:** Free / Open Source (MIT License)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://dotnet.microsoft.com/download/dotnet/10.0
- **Description:** Required to build and run the backend of a hackathon project (ClarityClaim), an ASP.NET Core Web API written in C# targeting .NET 10. Without the SDK, the project cannot be compiled or run locally. Free, Microsoft-published, no cost or licensing approval needed beyond installation. Used only for local development and testing during the hackathon; not deployed to any production system.

---

## 2. Node.js (LTS)

- **Software Name:** Node.js
- **Priority:** High
- **Version Name:** 22.x LTS or later
- **Vendor of the Software:** OpenJS Foundation
- **License Type:** Free / Open Source (MIT License)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://nodejs.org/
- **Description:** Required to build and run the React-based frontend (Vite + Tailwind) of the ClarityClaim hackathon project. Includes `npm`, used to install JavaScript dependencies and run the local development server. Free and open source, maintained by the OpenJS Foundation. Used only for local development during the hackathon; the frontend calls a locally-run backend API and is not exposed externally.

---

## 3. Git

- **Software Name:** Git
- **Priority:** High
- **Version Name:** 2.55.x or later
- **Vendor of the Software:** Software Freedom Conservancy / Git open-source project
- **License Type:** Free / Open Source (GNU GPL v2)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://git-scm.com/downloads
- **Description:** Version control tool required to clone, manage, and collaborate on the ClarityClaim hackathon project's source code repository with the rest of the team. Free and open source, one of the most widely used development tools in the industry. No license cost or approval concerns. Needed for day-to-day development workflow only (cloning, committing, pushing changes).

---

## 4. Microsoft SQL Server (Developer Edition)

- **Software Name:** Microsoft SQL Server — Developer Edition
- **Priority:** High
- **Version Name:** 2019 or later, Developer Edition
- **Vendor of the Software:** Microsoft
- **License Type:** Free (Microsoft Developer Edition license — full-featured, licensed for development/test use only, not production)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://www.microsoft.com/sql-server/sql-server-downloads
- **Description:** Required to host the local `ClarityClaimDb` database used by the ClarityClaim hackathon project (patient records, visits, SOAP notes, validation results, audit log). Developer Edition is free and fully featured, intended specifically for non-production development use, which matches this hackathon's scope. Runs locally with Windows Authentication — no server exposure or shared credentials involved.

---

## 5. SQL Server Management Studio (SSMS)

- **Software Name:** SQL Server Management Studio (SSMS)
- **Priority:** High
- **Version Name:** 18.x or later (20.x recommended)
- **Vendor of the Software:** Microsoft
- **License Type:** Free (Microsoft, no-cost tool)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms
- **Description:** Administrative tool needed to run the ClarityClaim hackathon project's database setup scripts (schema, stored procedures, seed data) against the local SQL Server instance requested above. Free Microsoft tool, standard for any SQL Server-based development work. Used only to execute local setup scripts and inspect data during development; not used against any production database.

---

## 6. Visual Studio Code

- **Software Name:** Visual Studio Code
- **Priority:** Medium
- **Version Name:** Latest stable release
- **Vendor of the Software:** Microsoft
- **License Type:** Free (MIT-licensed core; Microsoft-branded build distributed under a free-to-use product license)
- **Uninstall Date:** December 28, 2026
- **Purpose:** Other — Hackathon
- **Attachment:** N/A — official download: https://code.visualstudio.com/
- **Description:** Code editor used to write and debug the ClarityClaim hackathon project (C# backend and React frontend). Free, widely used, lightweight alternative to a full IDE. If Visual Studio 2022 is already installed and approved, this request can be skipped — either works for this project.

---

## Note on network access (not a software install)

The project also calls an internal LLM inference server on the office
network (private IP address, port 11434) for AI-generated content. This is
existing internal infrastructure, not new software to install — flagging it
here only in case network/firewall access to that address needs separate
approval. The application works without it (falls back to pre-generated
sample data automatically), so this is not a blocker for setup.
