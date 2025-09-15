# Wallet Console Application

A secure console-based wallet application implemented in **C#** using **Entity Framework Core** and **MySQL**.  
This repository contains a simple but practical demonstration of secure user registration/login (with salted SHA256 password hashing), 2FA via email, and an event-driven transaction flow with concurrency handling and retry logic.

---

# Table of Contents

- [Quick Copy-Paste Ready README](#quick-copy-paste-ready-readme)  
- [Project Summary](#project-summary)  
- [Highlights / Features](#highlights--features)  
- [Tech Stack](#tech-stack)  
- [Architecture & Design](#architecture--design)  
- [Project Structure & Responsibilities](#project-structure--responsibilities)  
- [How to Run (Local Development)](#how-to-run-local-development)  
- [Configuration & Environment Variables](#configuration--environment-variables)  
- [Database / EF Core notes](#database--ef-core-notes)  
- [Security Notes & Implementation Details](#security-notes--implementation-details)  
- [Transaction Flow (step-by-step)](#transaction-flow-step-by-step)  
- [Logging & Diagnostics](#logging--diagnostics)  
- [Possible Improvements / TODOs](#possible-improvements--todos)  
- [License](#license)

---

# Project Summary

This console app simulates a wallet system where users can:

- Register (name, email, password) — password stored as `SHA256(password + salt)`.
- Login with password and email 2FA code (6-digit).
- View account balance and transaction history.
- Withdraw funds to another wallet (event-driven workflow).
- Change/reset password (2FA protected).
- Transaction processing includes balance checks, transaction record creation, 2FA verification for critical transfers, atomic updates, concurrency retry/backoff, and status marking (Pending / Resolved / Failed).

This README is intentionally **single-file** and copy-paste ready for `README.md`.

---

# Highlights / Features

- Password hashing with **per-user salt** (generated via `RandomNumberGenerator`) and **SHA256** hashing.
- **Email-based 2FA** (6-digit codes) using SMTP (Mailtrap configuration in the sample).
- **Account lockout**: after 5 failed attempts the app pauses for 90 seconds (simple counter-based lockout).
- **Event-driven transactions**: `TransactionManager` raises an event. `TransactionHandlers` subscribes and runs multiple handlers sequentially.
- **Concurrency handling** on transfers: retry with exponential-ish backoff + jitter on `DbUpdateConcurrencyException`.
- **EF Core** `DbContext` (`AppDbContext`) for MySQL (connection string located in `OnConfiguring`).
- Basic logging via `System.Diagnostics.Trace` to `Traces.txt`.

---

# Tech Stack

- C# (console app) — tested with .NET 6/8 (code uses modern async/await and EF Core patterns).
- Entity Framework Core (MySQL provider).
- MySQL 8.x (used in `AppDbContext` connection string).
- SMTP client (System.Net.Mail) — Mailtrap-compatible sample settings provided.
- Cryptography: `System.Security.Cryptography` (for `RandomNumberGenerator` and `SHA256`).

---

# Architecture & Design

The code uses a **layered architecture** with an event-driven transaction pipeline:

1. **Presentation Layer**
   - `MenuManager` — orchestrates console menus (registration, login, wallet menu).
   - Handles user prompts and invokes services and data access.

2. **Security / Services**
   - `SecurityServices` (implements `ISecurityServices`) — password hashing, salt generation, 2FA generation/storage, email sending (async), and lockout logic.
   - Maintains an in-memory thread-safe `ConcurrentDictionary` of 2FA codes while they are valid.

3. **Data Access Layer**
   - `UserDataAcess` (implements `IUserDataAceess`) — EF Core operations for users, balances, and transactions. Also generates unique wallet IDs and performs transactional writes in registration.

4. **Transaction Processing Layer**
   - `TransactionEventArgs` — carries transaction metadata.
   - `TransactionManager` — raises transaction events asynchronously to all subscribed handlers.
   - `TransactionHandlers` — subscribes to the `TransactionManager` and runs handlers in sequence:
     - `CheckBalance` — ensures sufficient funds.
     - `RecordReserver` — inserts a transaction record in pending state.
     - `VerificationProcess` — sends 2FA to the user and reads the console for code.
     - `TransferFundsAsync` — attempts to move funds using a retry loop on concurrency conflicts, then marks the transaction Resolved/Failed.

5. **Persistence**
   - `AppDbContext` — `Users`, `Balance`, `Transactions` tables with concurrency token (`RowVersion` via `[Timestamp]`).

---

# Project Structure & Responsibilities

> Files/classes referenced below are logical groupings — the code you provided places everything into a single file. These descriptions map responsibilities to the class names from your code.

- `Program`  
  - App entry point, sets up `Trace` listeners and starts `MenuManager`.
- `MenuManager`  
  - Console UI, registration, login, wallet menu, password change flows, 2FA orchestration for login, uses `UserDataAcess` & `SecurityServices`.
- `SecurityServices` (`ISecurityServices`)  
  - `HashPasswordForRegistration`, `HashPasswordForLogin` (salted SHA256), `GenerateSalt`, `EmailPreparationandsending`, `Verify2FACode`, lockout counter.
  - Holds temporary 2FA codes in `ConcurrentDictionary<string, StringBuilder>`.
- `UserDataAcess` (`IUserDataAceess`)  
  - Add user (with transactional creation of `User` and initial `Balance`), find user by email, get salt/hash, update password, fetch wallet id, fetch user info, fetch transaction history.
- `TransactionManager`  
  - Event raising: `OnTransactionAsync(TransactionEventArgs)`.
- `TransactionHandlers`  
  - Event subscribers: `CheckBalance`, `RecordReserver`, `VerificationProcess`, `TransferFundsAsync`.
  - Concurrency/retry logic lives in `TransferFundsAsync`.
  - Uses a separate `AppDbContext` to perform DB updates inside transactions and commit/rollback.
- `AppDbContext`  
  - EF Core context; `OnConfiguring` currently contains an inline MySQL connection string.
- Entities/models:
  - `User` — `Id`, `Name`, `Email`, `PassHash`, `Salt`, `WalletId`, `Date`
  - `Balance` — includes a `RowVersion` timestamp for concurrency
  - `Transaction` — `TransactionId (GUID)`, `SourceAccount`, `DestinationAccount`, `Amount`, `Status`, `Timestamp`
  - Records: `PasswordData`, `WalletInfo`, `TransactionRecord`

---

# How to Run (Local Development)

> These steps assume your project is set up as a typical .NET console application and that EF Core packages are imported. If not, install the required NuGet packages (shown below).

1. **Install .NET SDK (6 or 8 recommended)**  
   - https://dotnet.microsoft.com/download

2. **Install MySQL and create database**  
   - Create database: `wallet_db`

3. **Install required NuGet packages** (run in project folder)
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore
   dotnet add package Pomelo.EntityFrameworkCore.MySql    # or MySql.EntityFrameworkCore provider you prefer
