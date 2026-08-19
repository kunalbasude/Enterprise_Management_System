using EnterpriseManagement.Infrastructure.Identity;

namespace EnterpriseManagement.Tests.Auth;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_never_returns_the_plaintext()
    {
        const string password = "Analytical1";

        var hash = _hasher.Hash(password);

        Assert.DoesNotContain(password, hash);
    }

    [Fact]
    public void Hash_embeds_algorithm_and_work_factor()
    {
        // The prefix is what allows the work factor to be raised later without
        // invalidating existing hashes: each one carries the factor it was
        // created with.
        var hash = _hasher.Hash("Analytical1");

        Assert.StartsWith("$2a$11$", hash);
    }

    [Fact]
    public void Same_password_hashed_twice_produces_different_hashes()
    {
        // Because the salt is random per call. This is what makes precomputed
        // rainbow tables useless and stops one crack revealing every account
        // that shares a password.
        var first = _hasher.Hash("Analytical1");
        var second = _hasher.Hash("Analytical1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("Analytical1", first));
        Assert.True(_hasher.Verify("Analytical1", second));
    }

    [Fact]
    public void Verify_accepts_the_correct_password()
    {
        var hash = _hasher.Hash("Analytical1");

        Assert.True(_hasher.Verify("Analytical1", hash));
    }

    [Theory]
    [InlineData("analytical1")]   // wrong case
    [InlineData("Analytical2")]   // one character different
    [InlineData("")]
    [InlineData("Analytical1 ")]  // trailing space
    public void Verify_rejects_anything_else(string attempt)
    {
        var hash = _hasher.Hash("Analytical1");

        Assert.False(_hasher.Verify(attempt, hash));
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("")]
    [InlineData("$2a$corrupted")]
    public void Verify_returns_false_for_a_malformed_hash_rather_than_throwing(string malformed)
    {
        // A corrupted row must fail the login. Throwing would surface a 500,
        // which tells an attacker the account exists and is in an odd state.
        Assert.False(_hasher.Verify("Analytical1", malformed));
    }
}
