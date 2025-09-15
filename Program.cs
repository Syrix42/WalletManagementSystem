using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// ========================== MENU MANAGER ==========================
class MenuManager
{
    private SecurityServices _security;
    private UserDataAcess _userDataAccess;

    public MenuManager(SecurityServices security, UserDataAcess User)
    {
        _security = security;
        _userDataAccess = User;
    }

    public void ShowMainMenu()
    {
        bool Exiting = false;

        while (true)
        {
            Console.WriteLine("1[Register]\n2[Login]\n3[Exit]");
            string i = Console.ReadLine();

            switch (i)
            {
                case "1":
                    ShowRegisteration().GetAwaiter().GetResult();
                    break;
                case "2":
                    _security.CheckAndApplyLockout().GetAwaiter().GetResult();
                    ShowLogInPanel().GetAwaiter().GetResult();
                    break;
                case "3":
                    Console.WriteLine("Exiting");
                    Exiting = true;
                    break;
                default:
                    Console.WriteLine("Invalid");
                    break;
            }

            if (Exiting)
                break;
        }
    }

    public async Task WalletMenu(int id)
    {
        bool Exiting = false;

        while (true)
        {
            Console.WriteLine($"1[Account Balance]\n2[Withdraw]\n3[Transaction History]\n4[Change Password]\n5[Logout]");
            string i = Console.ReadLine();
            switch (i)
            {
                case "1":
                    await AccountInfo(id);
                    break;
                case "2":
                    await WithdrawFlow(id);
                    break;
                case "3":
                    await TransactionInfo(id);
                    break;
                case "4":
                    await ChangingPassword(id);
                    break;
                case "5":
                    Exiting = true;
                    break;
                default:
                    Console.WriteLine("Invalid");
                    break;
            }
            if (Exiting)
            {
                break;
            }
        }
    }

    private async Task ShowRegisteration()
    {
        Console.Write("1[Name]: ");
        string FullName = Console.ReadLine();
        Console.Write("2[Email Adress]: ");
        string EmailAdress = Console.ReadLine();
        Console.Write("3[Password]: ");
        string Password = Console.ReadLine();

        try
        {
            var _result = _security.HashPasswordForRegistration(Password);
            await _userDataAccess.AddUser(FullName, EmailAdress.Trim(), _result);

            Console.WriteLine("Registration successful.");
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("A user with this email already exists");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine("A database error occurred: " + ex.Message);
        }
        finally
        {
            Console.WriteLine("Returning to the Main Menu");
        }
    }


    private async Task ShowLogInPanel()
    {
        Console.Write("[Email Adress]: ");
        string Email = Console.ReadLine();
        int id = await _userDataAccess.FindUserIdByEmail(Email);

        if (id == -1)
        {
            Console.WriteLine("Could not find such an email");
            return;
        }

        Console.Write("[Password]: ");
        string Password = Console.ReadLine();
        string UserSalt = await _userDataAccess.GetSaltDataByIdAsync(id);
        PasswordData Data = _security.HashPasswordForLogin(Password, UserSalt);

        bool FirstDoor = await _userDataAccess.AuthenticateUserAsync(id, Data.Hash);

        if (FirstDoor)
        {
            try
            {
                Console.WriteLine("2FA code has been sent to your email adress insert it you have 90 seconds  ");
                Console.Write("Insert the Code :");


                _security.EmailPreparationandsending(Email);
                string code = await PerformTwoFactorAsync();
                bool TwoFaResult = _security.Verify2FACode(Email, code);


                if (TwoFaResult)
                {
                    Console.WriteLine("Authentication successful");
                    _security.RestLockout();
                    await WalletMenu(id);
                }
                else
                {
                    Console.WriteLine("Invalid Code");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }



        }
        else
        {
            Console.WriteLine("Invalid Password");
            Console.WriteLine("Forgotten password ?[1 if yes]");
            string choice = Console.ReadLine();
            if (choice == "1")
            {
                try
                {
                    Console.WriteLine("2FA code has been sent to your email adress insert it you have 90 seconds  ");
                    Console.Write("Insert the Code : ");
                    _security.EmailPreparationandsending(Email);
                    string code = await PerformTwoFactorAsync();

                    await ResetPasswordFlowAsync(id, Email, code);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }



            }
        }
    }



    private async Task<string> PerformTwoFactorAsync()
    {
        var inputtask = Task.Run(() => Console.ReadLine());
        var complieted = await Task.WhenAny(inputtask, Task.Delay(90000));
        if (complieted == inputtask)
        {
            string userInput = inputtask.Result;
            return userInput;

        }
        else throw new Exception("you Ran out of time ");

    }

    private async Task ResetPasswordFlowAsync(int id, string email, string code)
    {



        if (_security.Verify2FACode(email, code))
        {
            Console.WriteLine("Insert your new Password:");
            Console.Write("Password: ");
            string newPassword = Console.ReadLine();

            PasswordData newData = _security.HashPasswordForRegistration(newPassword);
            await _userDataAccess.UpdateHashPassAsync(id, newData.Hash, newData.Salt);

            _security.RestLockout();
            Console.WriteLine("Password updated successfully.");
        }
        else
        {
            Console.WriteLine("2FA verification failed or timed out. Password not updated.");
        }
    }

    private async Task WithdrawFlow(int id)
    {
        (string wallet, decimal amount) = WithdrawInfo();
        if (wallet != null)
        {
            string from = await _userDataAccess.GetWalletId(id);
            TransactionEventArgs info = new TransactionEventArgs(
                Guid.NewGuid()
                , from
                , wallet
                , amount
                , DateTime.UtcNow
                );
            using TransactionManager manager = new TransactionManager();
            using TransactionHandlers handler = new TransactionHandlers(manager);
            try
            {
                await manager.OnTransactionAsync(info);
                Console.WriteLine("Transaction completed successfully!");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transaction failed: {ex.Message}");
            }



        }

    }

    private (string Wallet, decimal Amount) WithdrawInfo()
    {
        Console.Write("Please insert the Wallet id you want to Transfer Found to :");
        string Wallet = Console.ReadLine();
        Console.Write("Amount :");
        decimal amount = decimal.Parse(Console.ReadLine());
        return (Wallet, amount);
    }


    private async Task AccountInfo(int id)
    {
        var info = await _userDataAccess.UserInfo(id);
        Console.WriteLine($"Name : {info.Name}\nWalletId :{info.WalletId}\nBalance : {info.Balance.ToString("C")}");

    }

    private async Task TransactionInfo(int id)
    {
        var info = await _userDataAccess.TransactionHistory(id);
        foreach (var record in info)
        {
            string sourceToShow;
            if (record.From.Length >= 4)
            {
                string first4 = record.From.Substring(0, 4);
                string masked = new string('*', 16 - 4); // fill rest with stars
                sourceToShow = first4 + masked;
            }
            else
            {
                // If it's shorter than 4, just mask the rest up to 16
                string masked = new string('*', 16 - record.From.Length);
                sourceToShow = record.From + masked;
            }
            Console.WriteLine(
        $"TransactionId : {record.TransactionId}\n" +
        $"From          : {sourceToShow}\n" +
        $"To            : {record.To}\n" +
        $"Amount        : {record.Amount:C}\n" +
        $"Status        : {record.status}\n" +
        $"Date          : {record.Date}\n"
    );
        }
    }
    private async Task ChangingPassword(int id)
    {
        string Email = await _userDataAccess.GetEmailByIdAsync(id);
        try
        {
            Console.WriteLine("2FA code has been sent to your email adress insert it you have 90 seconds  ");
            Console.Write("Insert the Code : ");
            string code = await PerformTwoFactorAsync();

            await ResetPasswordFlowAsync(id, Email, code);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

    // Records 
    public record PasswordData(string Hash, string Salt);
    public record WalletInfo(string Name, string WalletId, decimal Balance);
    public record TransactionRecord(Guid TransactionId, string From, string To, decimal Amount, TransactionStatus status, DateTime Date);

    // ========================== INTERFACES ==========================


    interface IUserDataAceess
    {
        Task AddUser(string UserName, string EmailAdress, PasswordData Encryption);
        Task<bool> AuthenticateUserAsync(int Id, string passwordhash);
        Task<int> FindUserIdByEmail(string email);
        Task<string> GetPasswordDataByIdAsync(int id);
        Task<string> GetSaltDataByIdAsync(int id);
        Task<bool> UpdateHashPassAsync(int id, string Hash, string Salt);

        Task<WalletInfo> UserInfo(int id);
        Task<string> GetWalletId(int id);
        Task<List<TransactionRecord>> TransactionHistory(int id);

        Task<string> GetEmailByIdAsync(int id);
    }

    interface ISecurityServices
    {
        PasswordData HashPasswordForRegistration(string Password);
        PasswordData HashPasswordForLogin(string Password, string existingsalt);

        Task CheckAndApplyLockout();
        void RestLockout();

        public bool Verify2FACode(string Email, string Code);
        public void EmailPreparationandsending(string Email);
    }
    //Task Delegate 
    public delegate Task TransactionEventHandler(object sender, TransactionEventArgs e);

    public enum TransactionStatus
    {
        Pending = 0,
        Resolved = 1,
        Failed = 2
    }


    public sealed class TransactionEventArgs : EventArgs
    {
        public Guid TransactionId { get; }
        public string FromWalletId { get; }
        public string ToWalletId { get; }
        public decimal Amount { get; }
        public DateTime timeStamp { get; }
        public string? MetaData { get; }


        public TransactionEventArgs(Guid txId, string from, string to, decimal amount, DateTime timestamp, string? metadata = null)
        {
            TransactionId = txId;
            FromWalletId = from;
            ToWalletId = to;
            Amount = amount;
            timeStamp = timestamp;
            MetaData = metadata;
        }

    }


public class SecurityServices : ISecurityServices, IDisposable
{
    private AppDbContext _dbcontext;
    private int _counter;
    private ConcurrentDictionary<string, StringBuilder> _twoFaCodes;
    private bool _disposed = false;


    public SecurityServices()
    {
        _dbcontext = new AppDbContext();
        _twoFaCodes = new ConcurrentDictionary<string, StringBuilder>();
    }

    public PasswordData HashPasswordForRegistration(string password)
    {
        string salt = GenerateSalt();
        return ComputeHash(password, salt);
    }

    public PasswordData HashPasswordForLogin(string Password, string existingsalt)
    {
        return ComputeHash(Password, existingsalt);
    }

    private PasswordData ComputeHash(string password, string salt)
    {
        string PasswordWithSalt = password + salt;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashbytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(PasswordWithSalt));
            string hash = Convert.ToBase64String(hashbytes);
            return new PasswordData(hash, salt);
        }
    }

    private string GenerateSalt(int length = 16)
    {
        byte[] bytesalt = new byte[length];
        RandomNumberGenerator.Fill(bytesalt);
        return Convert.ToBase64String(bytesalt);
    }



    private async void SendEmailAsync(MailMessage msg)
    {
        bool result = false;
        StringBuilder excetionHappend = new StringBuilder();
        try
        {
            var smtpClient = new SmtpClient("live.smtp.mailtrap.io")
            {
                Port = 587,
                Credentials = new NetworkCredential("api", "a554208fb34450bc317e04146b362d03"),
                EnableSsl = true
            };
            await smtpClient.SendMailAsync(msg);
            result = true;
        }
        catch (Exception ex)
        {
            result = false;
            excetionHappend.Append(ex.Message);
        }
        finally
        {
            string messege = $"[LOG Type : Emailing] ,[Time : {DateTime.Now} ] , [Destination : {msg.To}] , [Result : {result}] ,[Exception : {excetionHappend}]";
            lock (Program.TraceLock)
            {
                Trace.WriteLine(messege);
            }
        }
    }

    public void EmailPreparationandsending(string Email)
    {
        StringBuilder TwoFaCode = new StringBuilder();
        for (int i = 0; i < 6; i++)
            TwoFaCode.Append(RandomNumberGenerator.GetInt32(0, 10));

        _twoFaCodes[Email] = TwoFaCode;
        MailMessage mailMessage = new MailMessage();
        mailMessage.From = new MailAddress("hello@demomailtrap.co");
        mailMessage.To.Add(Email.Trim());
        mailMessage.Subject = "2FA Registration Code";
        mailMessage.Body = $"Here is your login code: {TwoFaCode}";
        SendEmailAsync(mailMessage);

    }
    public bool Verify2FACode(string Email, string Code)
    {

        if (_twoFaCodes.TryGetValue(Email, out StringBuilder expectedCode))
        {
            bool result = Code == expectedCode.ToString();

            _twoFaCodes.TryRemove(Email, out _);

            return result;
        }
        return false;
    }

    public async Task CheckAndApplyLockout()
    {
        _counter += 1;
        if (_counter >= 5)
        {
            Console.WriteLine("App is locked, wait for 90 seconds ");
            await Task.Delay(90000);
            _counter = 0;
        }
    }

    public void RestLockout() => _counter = 0;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {

        }
        _dbcontext.Dispose();
        _disposed = true;



    }
    ~SecurityServices()
    {
        Dispose(false);
    }
}


    public class UserDataAcess : IUserDataAceess, IDisposable
    {
        private AppDbContext _dbcontext;
        private bool _disposed = false;


        public UserDataAcess() => _dbcontext = new AppDbContext();

        public async Task AddUser(string UserName, string EmailAdress, PasswordData Encryption)
        {
            if (await CheckEmail(EmailAdress))
                throw new InvalidOperationException("A user with this email already exists");
            string walletid = await WalletIdGenerator();
            var NewUser = new User
            {
                Name = UserName,
                Email = EmailAdress,
                PassHash = Encryption.Hash,
                Date = DateTime.Today,
                Salt = Encryption.Salt,
                WalletId = walletid
            };

            var newBalance = new Balance
            {
                WalletId = walletid,
                balance = 20,
                LastUpdated = DateTime.UtcNow
            };
            using var transaction = await _dbcontext.Database.BeginTransactionAsync();

            try
            {
                _dbcontext.Users.Add(NewUser);
                _dbcontext.Balance.Add(newBalance);

                await _dbcontext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<bool> CheckEmail(string EmailAdress)
            => await _dbcontext.Users.AnyAsync(u => u.Email == EmailAdress);

        public async Task<bool> AuthenticateUserAsync(int Id, string passwordhash)
        {
            if (Id == -1) return false;
            string UserPassHash = await GetPasswordDataByIdAsync(Id);
            return passwordhash == UserPassHash;
        }

        public async Task<int> FindUserIdByEmail(string email)
        {
            int? Id = await (from n in _dbcontext.Users
                             where n.Email == email
                             select (int?)n.Id).FirstOrDefaultAsync();
            return Id ?? -1;
        }

        public async Task<string> GetPasswordDataByIdAsync(int id)
            => await (from n in _dbcontext.Users
                      where n.Id == id
                      select n.PassHash).FirstOrDefaultAsync();

        public async Task<string> GetSaltDataByIdAsync(int id)
            => await (from n in _dbcontext.Users
                      where n.Id == id
                      select n.Salt).FirstOrDefaultAsync();

        public async Task<bool> UpdateHashPassAsync(int id, string Hash, string Salt)
        {
            var user = await _dbcontext.Users.FindAsync(id);
            if (user == null) return false;

            user.PassHash = Hash;
            user.Salt = Salt;
            await _dbcontext.SaveChangesAsync();
            return true;
        }


        private async Task<string> WalletIdGenerator()
        {
            var RandomId = new Random();
            while (true)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 16; i++)
                    sb.Append(RandomId.Next(0, 10));

                bool Isthere = await _dbcontext.Users.AnyAsync(u => u.WalletId == sb.ToString());
                if (!Isthere)
                    return sb.ToString();
            }
        }
        public async Task<string> GetWalletId(int id)
        {
            var Walletid = await (from n in _dbcontext.Users
                                  where n.Id == id
                                  select n.WalletId).FirstOrDefaultAsync();
            return Walletid;
        }
        public async Task<WalletInfo> UserInfo(int id)
        {
            var result = await (
                from n in _dbcontext.Users
                where n.Id == id
                join b in _dbcontext.Balance on n.WalletId equals b.WalletId
                select new WalletInfo(n.Name, b.WalletId, b.balance)).FirstOrDefaultAsync();

            return result;

        }

        public async Task<List<TransactionRecord>> TransactionHistory(int id)
        {
            var Walletid = await (from n in _dbcontext.Users
                                  where n.Id == id
                                  select n.WalletId).SingleOrDefaultAsync();
            var records = await (
        from n in _dbcontext.Transactions
        where n.SourceAccount == Walletid
        select new TransactionRecord(
            n.TransactionId,
            n.SourceAccount,
            n.DestinationAccount,
            n.Amount,
            n.Status,
            n.Timestamp
        )
    ).ToListAsync();
            return records;
        }

        public async Task<string> GetEmailByIdAsync(int id)
        {
            var Email = await (from n in _dbcontext.Users
                               where n.Id == id
                               select n.Email).FirstOrDefaultAsync();
            return Email;
        }




        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {

            }
            _dbcontext.Dispose();
            _disposed = true;



        }
        ~UserDataAcess()
        {
            Dispose(false);
        }

    }

    public class TransactionManager : IDisposable
    {

        public event TransactionEventHandler? CriticalTransaction;



        public async Task OnTransactionAsync(TransactionEventArgs e)
        {
            if (CriticalTransaction != null)
            {
                foreach (var d in CriticalTransaction.GetInvocationList().Cast<TransactionEventHandler>())
                {
                    try
                    {
                        await d(this, e);

                    }
                    catch (Exception ex)
                    {

                        LogCriticalFailure(e.TransactionId, d.Method.Name, ex);
                        throw;
                    }

                }
            }

        }


        private void LogCriticalFailure(Guid txId, string handlerName, Exception ex)
        {
            lock (Program.TraceLock)
            {
                Trace.WriteLine($"[TX {txId}] Critical handler {handlerName} failed: {ex}");
            }

        }

        public void Dispose()
        {

        }
    }

    public class TransactionHandlers : IDisposable
    {
        AppDbContext _dbcontext;

        public TransactionHandlers(TransactionManager manager)
        {

            _dbcontext = new AppDbContext();

            manager.CriticalTransaction += CheckBalance;
            manager.CriticalTransaction += RecordReserver;
            manager.CriticalTransaction += VerificationProcess;
            manager.CriticalTransaction += TransferFundsAsync;

        }

        private async Task CheckBalance(object sender, TransactionEventArgs e)
        {
            var balance = await (from n in _dbcontext.Balance
                                 where e.FromWalletId == n.WalletId
                                 select n.balance).FirstOrDefaultAsync();
            if (balance < e.Amount)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds in wallet {e.FromWalletId}. Balance: {balance}, Required: {e.Amount}");
            }

        }

        private async Task RecordReserver(object sender, TransactionEventArgs e)
        {
            var Transaction = new Transaction()
            {
                TransactionId = e.TransactionId,
                SourceAccount = e.FromWalletId,
                DestinationAccount = e.ToWalletId,
                Amount = e.Amount,
                Status = TransactionStatus.Pending,
                Timestamp = DateTime.UtcNow,
            };
            _dbcontext.Transactions.Add(Transaction);
            await _dbcontext.SaveChangesAsync();
        }
        private (string, MailMessage) CodeGeneration(string Email)
        {

            StringBuilder TwoFaCode = new StringBuilder();
            for (int i = 0; i < 6; i++)
                TwoFaCode.Append(RandomNumberGenerator.GetInt32(0, 10));


            MailMessage mailMessage = new MailMessage();

            mailMessage.From = new MailAddress("hello@demomailtrap.co");
            mailMessage.To.Add(Email.Trim());
            mailMessage.Subject = "2FA Registration Code";
            mailMessage.Body = $"Here is your login code: {TwoFaCode}";
            return (TwoFaCode.ToString(), mailMessage);



        }
        private async Task SendEmailAsync(MailMessage msg)
        {
            bool result = false;
            StringBuilder excetionHappend = new StringBuilder();
            try
            {
                using var smtpClient = new SmtpClient("live.smtp.mailtrap.io", 587)
                {
                    Port = 587,
                    Credentials = new NetworkCredential("api", "a554208fb34450bc317e04146b362d03"),
                    EnableSsl = true
                };
                await smtpClient.SendMailAsync(msg);
                result = true;
            }
            catch (Exception ex)
            {
                result = false;
                excetionHappend.Append(ex.Message);
            }
            finally
            {
                string messege = $"[LOG Type : Emailing] ,[Time : {DateTime.UtcNow} ] , [Destination : {msg.To}] , [Result : {result}] ,[Exception : {excetionHappend}]";
                lock (Program.TraceLock)
                {
                    Trace.WriteLine(messege);

                }
                try { msg.Dispose(); } catch { }
            }
        }
        private bool Verify2FACode(string userCode, string orgCode)
        {
            if (userCode == orgCode) return true;
            else return false;

        }
        private string ConsoleIntractions()
        {
            Console.Write("Insert the  2fa Code :");
            string code = Console.ReadLine();
            return code;
        }
        private async Task VerificationProcess(object sender, TransactionEventArgs e)
        {

            var email = await _dbcontext.Users
                .Where(u => u.WalletId == e.FromWalletId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException("User email not found.");


            (string code, MailMessage mailMessage) = CodeGeneration(email);


            _ = SendEmailAsync(mailMessage);


            string userCode = ConsoleIntractions();


            bool success = Verify2FACode(userCode, code);
            if (!success)
            {
                await MarkTransactionFailedAsync(e.TransactionId, "2FA verification failed.");
                throw new UnauthorizedAccessException("2FA verification failed.");
            }
        }
        private async Task TransferFundsAsync(object sender, TransactionEventArgs e)
        {
            const int maxAttempts = 3;
            int attempt = 0;
            int backoffBaseMs = 150;

            while (++attempt <= maxAttempts)
            {

                using var ctx = new AppDbContext();


                using var dbTx = await ctx.Database.BeginTransactionAsync();

                try
                {
                    // load by WalletId (not FindAsync)
                    var fromAcc = await ctx.Balance
                        .Where(b => b.WalletId == e.FromWalletId)
                        .SingleOrDefaultAsync();

                    var toAcc = await ctx.Balance
                        .Where(b => b.WalletId == e.ToWalletId)
                        .SingleOrDefaultAsync();

                    if (fromAcc == null)
                        throw new InvalidOperationException($"Source wallet not found: {e.FromWalletId}");
                    if (toAcc == null)
                        throw new InvalidOperationException($"Destination wallet not found: {e.ToWalletId}");

                    if (fromAcc.balance < e.Amount)
                    {
                        await MarkTransactionFailedAsync(e.TransactionId, "Insufficient funds");
                        throw new InvalidOperationException("Insufficient funds.");
                    }


                    fromAcc.balance -= e.Amount;
                    fromAcc.LastUpdated = DateTime.UtcNow;

                    toAcc.balance += e.Amount;
                    toAcc.LastUpdated = DateTime.UtcNow;


                    await ctx.SaveChangesAsync();

                    // commit DB transaction for this attempt
                    await dbTx.CommitAsync();

                    // mark success in your transactions table (use a separate context or same ctx)
                    await MarkTransactionSuccessAsync(e.TransactionId);

                    // success — exit
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    // concurrency conflict: rollback and retry (unless attempts exhausted)
                    await dbTx.RollbackAsync();

                    if (attempt == maxAttempts)
                    {
                        // final failure after retries
                        await MarkTransactionFailedAsync(e.TransactionId, "Concurrency conflict - retries exhausted");
                        throw new InvalidOperationException("Transfer failed due to concurrent updates. Please retry.");
                    }


                    int backoff = backoffBaseMs * attempt;
                    var jitter = RandomNumberGenerator.GetInt32(0, 100);
                    await Task.Delay(backoff + jitter);
                    // loop will retry
                }
                catch (Exception ex)
                {

                    try { await dbTx.RollbackAsync(); } catch { }

                    await MarkTransactionFailedAsync(e.TransactionId, ex.Message);
                    throw; // preserve original exception for caller/logging
                }
            }
        }

        private async Task MarkTransactionSuccessAsync(Guid txId)
        {
            var tx = await _dbcontext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == txId);

            if (tx == null)
            {
                lock (Program.TraceLock)
                    Trace.WriteLine($"[TX {txId}] MarkSuccess: transaction record not found.");
                return;
            }

            tx.Status = TransactionStatus.Resolved;
            tx.Timestamp = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync();

            lock (Program.TraceLock)
                Trace.WriteLine($"[TX {txId}] Marked Resolved at {DateTime.UtcNow}.");
        }

        private async Task MarkTransactionFailedAsync(Guid txId, string reason)
        {
            var tx = await _dbcontext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == txId);

            if (tx == null)
            {
                lock (Program.TraceLock)
                    Trace.WriteLine($"[TX {txId}] MarkFailed: transaction record not found. Reason: {reason}");
                return;
            }

            tx.Status = TransactionStatus.Failed;
            tx.Timestamp = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync();

            lock (Program.TraceLock)
                Trace.WriteLine($"[TX {txId}] Marked Failed at {DateTime.UtcNow}. Reason: {reason}");
        }

        public void Dispose()
        {
            _dbcontext.Dispose();
        }
    }






    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<Balance> Balance { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = "server=localhost;database=wallet_db;user=root;password=12345678;";
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 41));
            optionsBuilder.UseMySql(connectionString, serverVersion);
        }

    }


    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PassHash { get; set; }
        public string Salt { get; set; }
        public string WalletId { get; set; }
        public DateTime Date { get; set; }
    }
    public class Transaction
    {
        public int Id { get; set; }
        public Guid TransactionId { get; set; }
        public string SourceAccount { get; set; }
        public string DestinationAccount { get; set; }
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Balance
    {
        public int Id { get; set; }

        public string WalletId { get; set; }

        public decimal balance { get; set; }

        public DateTime LastUpdated { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

    }




    class Program
    {
        public static object TraceLock = new object();
        static void Main(string[] args)
        {
            TextWriterTraceListener textListener = new TextWriterTraceListener("Traces.txt");
            Trace.Listeners.Add(textListener);
            Trace.AutoFlush = true;
            UserDataAcess user = new UserDataAcess();
            SecurityServices securty = new SecurityServices();
            MenuManager menu = new MenuManager(securty , user);
            menu.ShowMainMenu();
        }
    }





    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<Balance> Balance { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = "server=localhost;database=wallet_db;user=root;password=12345678;";
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 41));
            optionsBuilder.UseMySql(connectionString, serverVersion);
        }

    }


    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PassHash { get; set; }
        public string Salt { get; set; }
        public string WalletId { get; set; }
        public DateTime Date { get; set; }
    }
    public class Transaction
    {
        public int Id { get; set; }
        public Guid TransactionId { get; set; }
        public string SourceAccount { get; set; }
        public string DestinationAccount { get; set; }
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Balance
    {
        public int Id { get; set; }

        public string WalletId { get; set; }

        public decimal balance { get; set; }

        public DateTime LastUpdated { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

    }




    class Program
    {
        public static object TraceLock = new object();
        static void Main(string[] args)
        {
            TextWriterTraceListener textListener = new TextWriterTraceListener("Traces.txt");
            Trace.Listeners.Add(textListener);
            Trace.AutoFlush = true;

            MenuManager menu = new MenuManager();
            menu.ShowMainMenu();
        }
    }



        Console.WriteLine("A database error occurred: " + ex.Message);
        }
        finally
        {
            Console.WriteLine("Returning to the Main Menu");
        }
    }
