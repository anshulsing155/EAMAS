using EAMAS.Core.Data;
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

        /// <summary>
        /// Authenticate a user within a specific organisation.
        /// For SuperAdmin pass organizationId = "SYSTEM".
        /// </summary>
        public User? Authenticate(string organizationId, string username, string password)
        {
            var user = _db.Users.Find(u =>
                u.OrganizationId == organizationId &&
                u.Username == username &&
                u.IsActive).FirstOrDefault();

            if (user == null) return null;
            if (!VerifyPassword(password, user.PasswordHash)) return null;

            var update = Builders<User>.Update.Set(u => u.LastLogin, DateTime.UtcNow);
            _db.Users.UpdateOne(u => u.Id == user.Id, update);
            user.LastLogin = DateTime.UtcNow;
            return user;
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
    }
}
