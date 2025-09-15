# C# Secure Wallet Console Application

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=.net&logoColor=white)
![MySQL](https://img.shields.io/badge/MySQL-4479A1?style=for-the-badge&logo=mysql&logoColor=white)

---

## Description
A **C# console application** simulating a secure wallet system with user registration, login with 2FA, balance management, and transaction processing.  
Built using **OOP principles**, async programming, and **Entity Framework Core** with MySQL integration.

---

## Features

<details>
<summary>Click to expand features</summary>

- **User Registration & Login**
  - Secure password hashing with salt (SHA256)
  - Two-Factor Authentication (2FA) via email

- **Wallet Management**
  - View account balance
  - Withdraw funds
  - Transaction history

- **Transaction Processing**
  - Event-based transaction handlers
  - Verification and transfer of funds
  - Automatic retry on concurrency conflicts
  - Logging and transaction status tracking

- **Security & Lockout**
  - Lockout after multiple failed login attempts
  - Reset lockout after successful login

- **Design & Architecture**
  - OOP principles: encapsulation, interfaces, records
  - Async/await for responsive operations
  - `IDisposable` and finalizers for proper resource management

</details>

---

## Installation

<details>
<summary>Click to expand installation instructions</summary>

1. Clone the repository:  
   ```bash
   git clone https://github.com/<your-username>/<repository-name>.git
