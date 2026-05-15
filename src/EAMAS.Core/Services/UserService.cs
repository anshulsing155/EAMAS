using EAMAS.Core.Data;
using EAMAS.Core.Enums;
using EAMAS.Core.Models;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace EAMAS.Core.Services
{
    public class UserService
    {
        private readonly MongoDbContext _db;

        public UserService(MongoDbContext db)
        {
            _db = db;
        }

        // ── Password helpers ─────────────────────────────────────────────────────
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);
            var computed = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(computed, hash);
        }

        // ── Authentication ───────────────────────────────────────────────────────

        // ── Brute-force constants ────────────────────────────────────────────────
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Authenticate a user within a specific organisation.
        /// Returns null on failure; sets <see cref="AuthFailReason"/> so callers can show
        /// a helpful message without leaking which part (user / password) was wrong.
        /// For SuperAdmin pass organizationId = "SYSTEM".
        /// </summary>
        public User? Authenticate(string organizationId, string username, string password,
            out AuthFailReason failReason)
        {
            failReason = AuthFailReason.None;

            var user = _db.Users.Find(u =>
                u.OrganizationId == organizationId &&
                u.Username == username &&
                u.IsActive).FirstOrDefault();

            if (user == null)
            {
                failReason = AuthFailReason.InvalidCredentials;
                return null;
            }

            // ── Lockout check ────────────────────────────────────────────────────
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var remaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
                failReason = AuthFailReason.AccountLocked;
                return null;
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                // Increment failed counter; lock if threshold reached
                var newAttempts = user.FailedLoginAttempts + 1;
                var lockUntil   = newAttempts >= MaxFailedAttempts
                    ? (DateTime?)DateTime.UtcNow.Add(LockoutDuration)
                    : null;

                _db.Users.UpdateOne(u => u.Id == user.Id,
                    Builders<User>.Update
                        .Set(u => u.FailedLoginAttempts, newAttempts)
                        .Set(u => u.LockedUntil, lockUntil));

                failReason = lockUntil.HasValue
                    ? AuthFailReason.AccountLocked
                    : AuthFailReason.InvalidCredentials;
                return null;
            }

            // Success — clear brute-force counters
            _db.Users.UpdateOne(u => u.Id == user.Id,
                Builders<User>.Update
                    .Set(u => u.LastLogin, DateTime.UtcNow)
                    .Set(u => u.FailedLoginAttempts, 0)
                    .Set(u => u.LockedUntil, (DateTime?)null));

            user.LastLogin = DateTime.UtcNow;
            return user;
        }

        /// <summary>Overload for callers that don't need the failure reason.</summary>
        public User? Authenticate(string organizationId, string username, string password)
            => Authenticate(organizationId, username, password, out _);

        /// <summary>Returns remaining lockout minutes if the account is currently locked, else 0.</summary>
        public int GetRemainingLockoutMinutes(string organizationId, string username)
        {
            var user = _db.Users.Find(u =>
                u.OrganizationId == organizationId &&
                u.Username == username).FirstOrDefault();

            if (user?.LockedUntil == null || user.LockedUntil.Value <= DateTime.UtcNow) return 0;
            return (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        /// <summary>Returns all active/inactive users in the given organisation.</summary>
        public List<User> GetAll(string organizationId)
        {
            return _db.Users.Find(u => u.OrganizationId == organizationId)
                .SortBy(u => u.FullName)
                .ToList();
        }

        public User? GetById(string id)
        {
            return _db.Users.Find(u => u.Id == id).FirstOrDefault();
        }

        // ── Create / Update / Delete ─────────────────────────────────────────────

        public User CreateUser(string organizationId, string username, string password,
            string fullName, string email, string department, UserRole role)
        {
            var user = new User
            {
                OrganizationId = organizationId,
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Email = email,
                Department = department,
                Role = role,
                IsActive = true,
                ConsentGiven = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.InsertOne(user);
            return user;
        }

        public void UpdateUser(User updated)
        {
            var update = Builders<User>.Update
                .Set(u => u.OrganizationId, updated.OrganizationId)
                .Set(u => u.FullName, updated.FullName)
                .Set(u => u.Email, updated.Email)
                .Set(u => u.Department, updated.Department)
                .Set(u => u.Role, updated.Role)
                .Set(u => u.IsActive, updated.IsActive);
            _db.Users.UpdateOne(u => u.Id == updated.Id, update);
        }

        public void ChangePassword(string userId, string newPassword)
        {
            var update = Builders<User>.Update
                .Set(u => u.PasswordHash, HashPassword(newPassword));
            _db.Users.UpdateOne(u => u.Id == userId, update);
        }

        public void SetConsent(string userId, bool consent)
        {
            var update = Builders<User>.Update.Set(u => u.ConsentGiven, consent);
            _db.Users.UpdateOne(u => u.Id == userId, update);
        }

        /// <summary>Soft-delete: sets IsActive = false.</summary>
        public void DeleteUser(string userId)
        {
            var update = Builders<User>.Update.Set(u => u.IsActive, false);
            _db.Users.UpdateOne(u => u.Id == userId, update);
        }

        public bool UsernameExists(string organizationId, string username)
        {
            return _db.Users.CountDocuments(u =>
                u.OrganizationId == organizationId &&
                u.Username == username) > 0;
        }

        // ── Active-session management ────────────────────────────────────────────

        /// <summary>
        /// Writes a session token to the user document. Returns the new token.
        /// </summary>
        public string OpenSession(string userId)
        {
            var token = Guid.NewGuid().ToString("N");
            var update = Builders<User>.Update
                .Set(u => u.ActiveSessionToken, token)
                .Set(u => u.SessionStartedAt, DateTime.UtcNow)
                .Set(u => u.SessionMachine, Environment.MachineName);
            _db.Users.UpdateOne(u => u.Id == userId, update);
            return token;
        }

        /// <summary>
        /// Clears the session token so the account is available for login elsewhere.
        /// Only clears if the supplied token matches (prevents a stale close from
        /// evicting a newer session).
        /// </summary>
        public void CloseSession(string userId, string token)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                Builders<User>.Filter.Eq(u => u.ActiveSessionToken, token));

            var update = Builders<User>.Update
                .Set(u => u.ActiveSessionToken, (string?)null)
                .Set(u => u.SessionStartedAt, (DateTime?)null)
                .Set(u => u.SessionMachine, (string?)null);

            _db.Users.UpdateOne(filter, update);
        }

        /// <summary>Force-clears any active session regardless of token (admin override).</summary>
        public void ForceCloseSession(string userId)
        {
            var update = Builders<User>.Update
                .Set(u => u.ActiveSessionToken, (string?)null)
                .Set(u => u.SessionStartedAt, (DateTime?)null)
                .Set(u => u.SessionMachine, (string?)null);
            _db.Users.UpdateOne(u => u.Id == userId, update);
        }
    }
}
