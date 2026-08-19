namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// Password hashing, abstracted so the algorithm can be replaced without
/// touching business logic.
/// </summary>
/// <remarks>
/// Application declares it; Infrastructure implements it with BCrypt. That seam
/// matters in practice: migrating to Argon2 later means one new implementation
/// class, not edits across every service that authenticates.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password, generating a fresh random salt.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext attempt against a stored hash.
    /// </summary>
    /// <remarks>
    /// Returns false rather than throwing on a malformed hash, so a corrupted
    /// row fails the login instead of returning a 500 that tells an attacker the
    /// account exists.
    /// </remarks>
    bool Verify(string password, string passwordHash);
}
