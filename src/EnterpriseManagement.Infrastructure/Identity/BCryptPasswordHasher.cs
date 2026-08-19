using EnterpriseManagement.Application.Common.Interfaces;

namespace EnterpriseManagement.Infrastructure.Identity;

/// <summary>
/// BCrypt password hashing.
/// </summary>
/// <remarks>
/// <para>
/// BCrypt is chosen over a raw SHA-family hash because it is deliberately slow
/// and salted. SHA-256 is designed to be fast, which is exactly wrong here: a
/// commodity GPU computes billions per second, so a leaked table of SHA-256
/// password hashes is cracked in hours.
/// </para>
/// <para>
/// The salt is generated per password and stored inside the returned hash
/// string, so no separate salt column exists. Two accounts sharing a password
/// still produce different hashes, which defeats precomputed rainbow tables and
/// stops an attacker cracking many accounts in one pass.
/// </para>
/// <para>
/// Alternative: Argon2id, which is the current PHC recommendation and resists
/// GPU attack better still. BCrypt is used here because it ships as a small,
/// well-audited .NET package with no native dependency. The
/// <see cref="IPasswordHasher"/> seam means switching later is one class.
/// </para>
/// </remarks>
public class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Cost factor: the hash runs 2^11 iterations, roughly 100ms on current
    /// hardware. Chosen as the usual balance between resisting offline cracking
    /// and not turning every login into a visible delay. It is stored inside
    /// each hash, so raising it later leaves existing hashes verifiable.
    /// </summary>
    private const int WorkFactor = 11;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            // A malformed, truncated or empty hash in the database must fail the
            // login, not throw. A 500 here would tell an attacker that the
            // account exists and that something unusual is stored against it.
            //
            // The exception types differ by defect: SaltParseException for an
            // unparseable salt, ArgumentException for an empty one, and
            // FormatException for corrupt base64. All three mean the same thing
            // operationally - this credential cannot be verified.
            return false;
        }
    }
}
