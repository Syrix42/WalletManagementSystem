C# Secure Wallet Console Application
Description

A C# console application simulating a secure wallet system.
Features include user registration, login with 2FA, balance management, and transaction processing.
Built using OOP principles, async programming, and Entity Framework Core with MySQL integration.

Features

User Registration & Login

Secure password hashing with salt (SHA256)

Two-Factor Authentication (2FA) via email

Wallet Management

View account balance

Withdraw funds

Transaction history

Transaction Processing

Transaction events with multiple handlers

Verification and transfer of funds

Automatic retry on concurrency conflicts

Logging and transaction status tracking

Security & Lockout

Lockout after multiple failed login attempts

Reset lockout after successful login

Design

OOP principles: encapsulation, interfaces, records

Async/await for responsive operations

Finalizer and IDisposable patterns for proper resource management

Installation

Clone the repository:

git clone https://github.com/<your-username>/<repository-name>.git


Ensure .NET 7+ or latest is installed.

Set up MySQL and update the connection string in AppDbContext.

Install required NuGet packages:

dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Pomelo.EntityFrameworkCore.MySql

Usage

Run the application:

dotnet run


Main menu options:

1 Register

2 Login

3 Exit

After login, wallet menu options:

1 View Balance

2 Withdraw Funds

3 Transaction History

4 Change Password

5 Logout

Database Schema
Users
Column	Type	Description
Id	int	Primary Key
Name	string	Full name
Email	string	Email address
PassHash	string	Password hash
Salt	string	Salt for password
WalletId	string	Unique wallet ID
Date	DateTime	Registration date
Balance
Column	Type	Description
Id	int	Primary Key
WalletId	string	User’s wallet ID
balance	decimal	Wallet balance
LastUpdated	DateTime	Last balance update
RowVersion	byte[]	Concurrency token
Transactions
Column	Type	Description
Id	int	Primary Key
TransactionId	Guid	Unique transaction ID
SourceAccount	string	Wallet sending the funds
DestinationAccount	string	Wallet receiving the funds
Amount	decimal	Transaction amount
Status	int	Pending, Resolved, Failed
Timestamp	DateTime	Transaction timestamp
Notes

Designed as a console app for learning and practice purposes.

Some methods (like sending emails) are fire-and-forget for responsiveness.

Resource cleanup is handled via IDisposable and finalizers.

License

MIT License © [Your Name]
